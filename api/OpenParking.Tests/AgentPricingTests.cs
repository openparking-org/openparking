using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OpenParking.Api.Controllers;
using OpenParking.Core.Entities;
using OpenParking.Core.Interfaces;
using OpenParking.Core.Models;
using OpenParking.Infrastructure.Data;
using OpenParking.Infrastructure.Services;
using Xunit;

namespace OpenParking.Tests;

public class AgentPricingTests
{
    private static (AppDbContext db, AgentPricingController controller, EnforcementService service) Create()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["INTERNAL_API_TOKEN"] = "test-service-token", ["AI_SERVICE_URL"] = "http://ai.test" }).Build();
        var controller = new AgentPricingController(db, config)
            { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        controller.Request.Headers["X-Internal-Token"] = "test-service-token";
        var settings = new SettingsService(db, new MemoryCache(new MemoryCacheOptions()));
        var service = new EnforcementService(db, new HttpClient(), Mock.Of<IEmailService>(), Mock.Of<IRealtimeNotifier>(),
            settings, config, NullLogger<EnforcementService>.Instance);
        return (db, controller, service);
    }

    private static JsonElement State(Guid id, Guid zone, string status = "PENDING_APPROVAL", string action = "INCREASE_PRICE",
                                      decimal current = 5, decimal proposed = 7.5m, decimal multiplier = 1.5m) =>
        JsonSerializer.SerializeToElement(new { workflow_id = id.ToString(), workflow_type = "DYNAMIC_PRICING",
            zone_id = zone.ToString(), status, goal = "Evaluate demo zone", reason = "Measured demand",
            action_proposal = new { action, current_rate = current, proposed_rate = proposed, calculated_rate = proposed, multiplier },
            validation = new { valid = true }, final_decision = status });

    [Fact]
    public async Task InternalToolsRejectMissingAndIncorrectCredentials()
    {
        var (_, controller, _) = Create();
        controller.Request.Headers.Remove("X-Internal-Token");
        Assert.IsType<UnauthorizedResult>(await controller.Demand(Guid.NewGuid()));
        controller.Request.Headers["X-Internal-Token"] = "incorrect";
        Assert.IsType<UnauthorizedResult>(await controller.PriceChanges(Guid.NewGuid()));
    }

    [Fact]
    public async Task DemandContainsAggregateCountsWithoutPersonalInformation()
    {
        var (db, controller, _) = Create();
        var zone = new Zone();
        var slot = new Slot { ZoneId = zone.Id, Zone = zone };
        db.AddRange(zone, slot,
            new ParkingSession { Slot = slot, SlotId = slot.Id, CheckInTime = DateTime.UtcNow.AddMinutes(-3) },
            new ParkingSession { Slot = slot, SlotId = slot.Id, CheckInTime = DateTime.UtcNow.AddHours(-2), CheckOutTime = DateTime.UtcNow.AddMinutes(-2) },
            new Booking { Slot = slot, SlotId = slot.Id, StartTime = DateTime.UtcNow.AddMinutes(20), Status = BookingStatus.Confirmed, VehiclePlate = "PRIVATE" });
        await db.SaveChangesAsync();
        var result = Assert.IsType<OkObjectResult>(await controller.Demand(zone.Id));
        var json = JsonSerializer.SerializeToElement(result.Value);
        Assert.Equal(1, json.GetProperty("recent_arrivals").GetInt32());
        Assert.Equal(1, json.GetProperty("recent_departures").GetInt32());
        Assert.Equal(1, json.GetProperty("reservation_demand").GetInt32());
        Assert.DoesNotContain("PRIVATE", json.GetRawText());
    }

    [Fact]
    public async Task ServiceProposalCannotClaimAdministratorApproval()
    {
        var (db, controller, _) = Create();
        var zone = new Zone(); db.Add(zone); await db.SaveChangesAsync();
        Assert.IsType<BadRequestResult>(await controller.SaveProposal(Guid.NewGuid(),
            State(Guid.NewGuid(), zone.Id, "APPROVED")));
        Assert.Empty(db.AgentWorkflowRuns);
    }

    [Fact]
    public async Task PendingProposalPersistsAndCannotBeOverwritten()
    {
        var (db, controller, _) = Create();
        var zone = new Zone(); db.Add(zone); await db.SaveChangesAsync();
        var id = Guid.NewGuid(); var state = State(id, zone.Id);
        Assert.IsType<OkObjectResult>(await controller.SaveProposal(id, state));
        var run = await db.AgentWorkflowRuns.FindAsync(id);
        Assert.Equal(WorkflowStatus.AwaitingApproval, run!.Status);
        Assert.Null(run.ApprovedBy);
        Assert.Contains("validation", run.PlanJson);
        Assert.IsType<ConflictResult>(await controller.SaveProposal(id, state));
        var result = Assert.IsType<OkObjectResult>(await controller.Workflow(id));
        var saved = JsonSerializer.SerializeToElement(result.Value);
        Assert.Equal("AwaitingApproval", saved.GetProperty("backend_status").GetString());
    }

    [Fact]
    public async Task NoChangeEvaluationDoesNotPretendAdminApproved()
    {
        var (db, controller, _) = Create();
        var zone = new Zone(); db.Add(zone); await db.SaveChangesAsync();
        var id = Guid.NewGuid();
        Assert.IsType<OkObjectResult>(await controller.SaveProposal(id,
            State(id, zone.Id, "AUTO_APPROVED", "KEEP_PRICE", proposed: 5, multiplier: 1)));
        var run = await db.AgentWorkflowRuns.FindAsync(id);
        Assert.Equal(WorkflowStatus.Completed, run!.Status);
        Assert.Null(run.ApprovedAt);
        Assert.Null(run.ApprovedBy);
    }

    [Theory]
    [InlineData("cap")]
    [InlineData("stale")]
    [InlineData("math")]
    [InlineData("cooldown")]
    [InlineData("disabled")]
    [InlineData("validator")]
    public async Task AdminApprovalRechecksDeterministicPolicy(string failure)
    {
        var (db, _, service) = Create();
        var zone = new Zone(); db.Add(zone);
        var id = Guid.NewGuid(); var state = State(id, zone.Id, proposed: failure == "math" ? 8 : 7.5m);
        var run = new AgentWorkflowRun { Id = id, ZoneId = zone.Id, WorkflowType = "SURGE_PRICING", Status = WorkflowStatus.AwaitingApproval,
            PlanJson = failure == "validator" ? "{\"validation\":{\"valid\":false}}" : state.GetRawText(),
            StepResultsJson = state.GetProperty("action_proposal").GetRawText() };
        db.Add(run);
        if (failure == "cap") db.Add(new SystemSetting { Key = "pricing.max_surge_multiplier", Value = "1.2" });
        if (failure == "stale") zone.BaseHourlyRate = 6;
        if (failure == "disabled") db.Add(new SystemSetting { Key = "pricing.is_enabled", Value = "false" });
        if (failure == "cooldown") db.Add(new AgentWorkflowRun { ZoneId = zone.Id, WorkflowType = "SURGE_PRICING", Status = WorkflowStatus.Approved,
            ApprovedAt = DateTime.UtcNow.AddMinutes(-1), ApprovedBy = Guid.NewGuid() });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<AppException>(() => service.FinalizeApprovalAsync(id, Guid.NewGuid()));
        Assert.Equal(failure == "stale" ? 6 : 5, zone.BaseHourlyRate);
        Assert.Equal(WorkflowStatus.AwaitingApproval, run.Status);
        Assert.Null(run.ApprovedBy);
    }

    [Fact]
    public async Task ActualAdminApprovalAppliesRateAndIsVisibleToResume()
    {
        var (db, controller, service) = Create();
        var zone = new Zone(); db.Add(zone); await db.SaveChangesAsync();
        var id = Guid.NewGuid(); var actor = Guid.NewGuid();
        await controller.SaveProposal(id, State(id, zone.Id));
        await service.FinalizeApprovalAsync(id, actor);
        Assert.Equal(7.5m, zone.BaseHourlyRate);
        var result = Assert.IsType<OkObjectResult>(await controller.Workflow(id));
        var state = JsonSerializer.SerializeToElement(result.Value);
        Assert.Equal("APPROVED", state.GetProperty("final_decision").GetString());
        Assert.Equal(actor.ToString(), state.GetProperty("approved_by").GetString());
        Assert.True(state.GetProperty("applied").GetBoolean());
        await Assert.ThrowsAsync<AppException>(() => service.FinalizeApprovalAsync(id, actor));
    }

    [Fact]
    public async Task ManualZoneRateChangeAppearsInCooldownHistory()
    {
        var (db, controller, _) = Create();
        var zone = new Zone(); db.Add(zone);
        db.Add(new AuditLog { EntityType = "Zone", EntityId = zone.Id, Action = "PriceChanged" });
        await db.SaveChangesAsync();
        var result = Assert.IsType<OkObjectResult>(await controller.PriceChanges(zone.Id));
        Assert.NotEqual(JsonValueKind.Null, JsonSerializer.SerializeToElement(result.Value).GetProperty("last_price_change_at").ValueKind);
    }
}
