using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using OpenParking.Core.Entities;
using OpenParking.Core.Interfaces;
using OpenParking.Core.Models;
using OpenParking.Infrastructure.Data;
using OpenParking.Infrastructure.Services;
using Xunit;

namespace OpenParking.Tests;

public class EnforcementServiceTests
{
    private static (AppDbContext db, Mock<IEmailService> emailMock, Mock<IRealtimeNotifier> notifierMock, Mock<ISettingsService> settingsMock, EnforcementService service) CreateService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options);

        var emailMock = new Mock<IEmailService>();
        var notifierMock = new Mock<IRealtimeNotifier>();
        var settingsMock = new Mock<ISettingsService>();
        var loggerMock = new Mock<ILogger<EnforcementService>>();

        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["AI_SERVICE_URL"]).Returns("http://localhost:8000");

        var httpClient = new HttpClient();

        var service = new EnforcementService(
            db,
            httpClient,
            emailMock.Object,
            notifierMock.Object,
            settingsMock.Object,
            configMock.Object,
            loggerMock.Object);

        return (db, emailMock, notifierMock, settingsMock, service);
    }

    [Fact]
    public async Task FinalizeApprovalAsync_CreatesPenalty_AndUpdatesWorkflow()
    {
        var (db, emailMock, notifierMock, settingsMock, service) = CreateService();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "driver@openparking.test",
            FullName = "Driver Dave",
            PasswordHash = "hash"
        };
        db.Users.Add(user);

        var session = new ParkingSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Status = SessionStatus.OverstayDetected
        };
        db.ParkingSessions.Add(session);

        var workflow = new AgentWorkflowRun
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            WorkflowType = "OVERSTAY",
            Status = WorkflowStatus.AwaitingApproval,
            StepResultsJson = "{\"amount\": 350.0, \"reason\": \"30 min overstay\"}"
        };
        db.AgentWorkflowRuns.Add(workflow);
        await db.SaveChangesAsync();

        settingsMock.Setup(s => s.GetDecimalAsync("max_penalty_amount", 5000m))
            .ReturnsAsync(5000m);

        var adminId = Guid.NewGuid();
        await service.FinalizeApprovalAsync(workflow.Id, adminId);

        var updatedWorkflow = await db.AgentWorkflowRuns.FindAsync(workflow.Id);
        Assert.NotNull(updatedWorkflow);
        Assert.Equal(WorkflowStatus.Approved, updatedWorkflow.Status);
        Assert.Equal(adminId, updatedWorkflow.ApprovedBy);

        var penalty = await db.Penalties.FirstOrDefaultAsync(p => p.WorkflowRunId == workflow.Id);
        Assert.NotNull(penalty);
        Assert.Equal(350.0m, penalty.Amount);
        Assert.Equal("30 min overstay", penalty.Reason);
        Assert.Equal(PenaltyStatus.Approved, penalty.Status);

        emailMock.Verify(e => e.SendPenaltyNoticeAsync(user.Email, user.FullName, It.IsAny<Penalty>()), Times.Once);
    }

    [Fact]
    public async Task FinalizeApprovalAsync_ThrowsException_WhenWorkflowNotAwaitingApproval()
    {
        var (db, _, _, _, service) = CreateService();

        var workflow = new AgentWorkflowRun
        {
            Id = Guid.NewGuid(),
            Status = WorkflowStatus.Running
        };
        db.AgentWorkflowRuns.Add(workflow);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<AppException>(() => service.FinalizeApprovalAsync(workflow.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task FinalizeRejectionAsync_UpdatesWorkflowToRejected()
    {
        var (db, _, _, _, service) = CreateService();

        var workflow = new AgentWorkflowRun
        {
            Id = Guid.NewGuid(),
            Status = WorkflowStatus.AwaitingApproval
        };
        db.AgentWorkflowRuns.Add(workflow);
        await db.SaveChangesAsync();

        var adminId = Guid.NewGuid();
        await service.FinalizeRejectionAsync(workflow.Id, "Grace period granted by operator", adminId);

        var updatedWorkflow = await db.AgentWorkflowRuns.FindAsync(workflow.Id);
        Assert.NotNull(updatedWorkflow);
        Assert.Equal(WorkflowStatus.Rejected, updatedWorkflow.Status);
        Assert.Contains("Grace period granted", updatedWorkflow.ErrorLog ?? "");
    }

    [Fact]
    public async Task DisputePenaltyAsync_SetsStatusToDisputed_WhenWithinWindow()
    {
        var (db, _, _, _, service) = CreateService();

        var userId = Guid.NewGuid();
        var penalty = new Penalty
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Amount = 250m,
            Status = PenaltyStatus.Approved,
            IssuedAt = DateTime.UtcNow.AddDays(-2)
        };
        db.Penalties.Add(penalty);
        await db.SaveChangesAsync();

        var result = await service.DisputePenaltyAsync(penalty.Id, "Machine malfunction during exit", userId);

        Assert.Equal(PenaltyStatus.Disputed, result.Status);
        var inDb = await db.Penalties.FindAsync(penalty.Id);
        Assert.NotNull(inDb);
        Assert.Equal(PenaltyStatus.Disputed, inDb.Status);
    }

    [Fact]
    public async Task DisputePenaltyAsync_ThrowsForbidden_WhenDisputedByDifferentUser()
    {
        var (db, _, _, _, service) = CreateService();

        var penalty = new Penalty
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Amount = 250m,
            Status = PenaltyStatus.Approved,
            IssuedAt = DateTime.UtcNow.AddDays(-1)
        };
        db.Penalties.Add(penalty);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<AppException>(() =>
            service.DisputePenaltyAsync(penalty.Id, "Not my vehicle", Guid.NewGuid()));
    }

    [Fact]
    public async Task DisputePenaltyAsync_ThrowsDisputeWindowExpired_WhenOlderThan7Days()
    {
        var (db, _, _, _, service) = CreateService();

        var userId = Guid.NewGuid();
        var penalty = new Penalty
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Amount = 250m,
            Status = PenaltyStatus.Approved,
            IssuedAt = DateTime.UtcNow.AddDays(-8)
        };
        db.Penalties.Add(penalty);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<AppException>(() =>
            service.DisputePenaltyAsync(penalty.Id, "Late dispute", userId));
    }

    [Fact]
    public async Task ResolvePenaltyAsync_UpdatesPenaltyStatus()
    {
        var (db, _, _, _, service) = CreateService();

        var penalty = new Penalty
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Amount = 200m,
            Status = PenaltyStatus.Disputed
        };
        db.Penalties.Add(penalty);
        await db.SaveChangesAsync();

        var adminId = Guid.NewGuid();
        var resolved = await service.ResolvePenaltyAsync(penalty.Id, PenaltyStatus.Waived, adminId);

        Assert.Equal(PenaltyStatus.Waived, resolved.Status);
        var inDb = await db.Penalties.FindAsync(penalty.Id);
        Assert.NotNull(inDb);
        Assert.Equal(PenaltyStatus.Waived, inDb.Status);
    }

    [Fact]
    public async Task WriteAuditLogAsync_AppendsAuditRecord()
    {
        var (db, _, _, _, service) = CreateService();

        var entityId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        await service.WriteAuditLogAsync("Penalty", entityId, "Waived", new { reason = "VIP customer" }, actorId, "admin@openparking.test", "127.0.0.1");

        var auditLog = await db.AuditLogs.FirstOrDefaultAsync(a => a.EntityId == entityId);
        Assert.NotNull(auditLog);
        Assert.Equal("Penalty", auditLog.EntityType);
        Assert.Equal("Waived", auditLog.Action);
        Assert.Equal(actorId, auditLog.ActorUserId);
        Assert.Contains("VIP customer", auditLog.PayloadJson ?? "");
    }
}
