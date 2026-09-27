using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenParking.Api.Controllers;
using OpenParking.Core.Entities;
using OpenParking.Infrastructure.Data;
using Xunit;

namespace OpenParking.Tests;

public class AnalyticsControllerTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public void AnalyticsController_IsProtectedBy_AdminRoleAuthorization()
    {
        var authAttr = typeof(AnalyticsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(authAttr);
        Assert.Equal("ParkingAdmin,SystemAdmin", authAttr.Roles);
    }

    [Fact]
    public async Task GetSummary_ReturnsSafeDefaults_WhenDatabaseIsEmpty()
    {
        using var db = CreateInMemoryDbContext();
        var controller = new AnalyticsController(db);

        var result = await controller.GetSummary(days: 30);
        var okResult = Assert.IsType<OkObjectResult>(result);
        var summary = Assert.IsType<AnalyticsSummaryDto>(okResult.Value);

        Assert.Equal(0, summary.TotalSessions);
        Assert.Equal(0, summary.ActiveSessions);
        Assert.Equal(0, summary.CompletedSessions);
        Assert.Equal(0, summary.OverstaySessions);
        Assert.Equal(0m, summary.TotalRevenue);
        Assert.Equal(0m, summary.TotalPenalties);
        Assert.Equal(0.0, summary.AvgDurationMinutes);
        Assert.Equal(0, summary.PendingWorkflowsCount);
        Assert.Equal(0, summary.TotalWorkflowsCount);
    }

    [Fact]
    public async Task GetSummary_Aggregates_SessionAndWorkflowMetrics()
    {
        using var db = CreateInMemoryDbContext();
        var now = DateTime.UtcNow;

        db.ParkingSessions.AddRange(
            new ParkingSession
            {
                BookingId = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                SlotId = Guid.NewGuid(),
                Status = SessionStatus.Active,
                CheckInTime = now.AddHours(-1),
                TotalFee = 15m,
                PenaltyFee = 0m,
                OverstayMinutes = 0
            },
            new ParkingSession
            {
                BookingId = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                SlotId = Guid.NewGuid(),
                Status = SessionStatus.Completed,
                CheckInTime = now.AddHours(-3),
                CheckOutTime = now.AddHours(-1),
                TotalFee = 25m,
                PenaltyFee = 10m,
                OverstayMinutes = 30
            }
        );

        db.AgentWorkflowRuns.AddRange(
            new AgentWorkflowRun
            {
                WorkflowType = "OVERSTAY_ENFORCEMENT",
                Objective = "Overstay check",
                Status = WorkflowStatus.AwaitingApproval,
                CreatedAt = now.AddMinutes(-30)
            },
            new AgentWorkflowRun
            {
                WorkflowType = "DYNAMIC_PRICING",
                Objective = "Surge pricing",
                Status = WorkflowStatus.Approved,
                CreatedAt = now.AddMinutes(-10)
            }
        );

        await db.SaveChangesAsync();

        var controller = new AnalyticsController(db);
        var result = await controller.GetSummary(days: 7);
        var okResult = Assert.IsType<OkObjectResult>(result);
        var summary = Assert.IsType<AnalyticsSummaryDto>(okResult.Value);

        Assert.Equal(2, summary.TotalSessions);
        Assert.Equal(1, summary.ActiveSessions);
        Assert.Equal(1, summary.CompletedSessions);
        Assert.Equal(1, summary.OverstaySessions);
        Assert.Equal(40m, summary.TotalRevenue);
        Assert.Equal(10m, summary.TotalPenalties);
        Assert.Equal(120.0, summary.AvgDurationMinutes);
        Assert.Equal(1, summary.PendingWorkflowsCount);
        Assert.Equal(2, summary.TotalWorkflowsCount);
    }

    [Fact]
    public async Task GetDailyRevenue_ProvidesContinuousPoints()
    {
        using var db = CreateInMemoryDbContext();
        var pastTime = DateTime.UtcNow.AddHours(-2);
        var expectedDate = pastTime.Date.ToString("yyyy-MM-dd");

        db.ParkingSessions.Add(new ParkingSession
        {
            BookingId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            SlotId = Guid.NewGuid(),
            Status = SessionStatus.Completed,
            CheckInTime = pastTime,
            CheckOutTime = pastTime.AddHours(1),
            TotalFee = 50.00m
        });
        await db.SaveChangesAsync();

        var controller = new AnalyticsController(db);
        var result = await controller.GetDailyRevenue(days: 7);
        var okResult = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<List<DailyRevenueDto>>(okResult.Value);

        Assert.NotEmpty(list);
        Assert.True(list.Count >= 7);
        var todayItem = list.FirstOrDefault(x => x.Date == expectedDate);
        Assert.NotNull(todayItem);
        Assert.Equal(50.00m, todayItem.Revenue);
    }

    [Fact]
    public async Task GetOccupancy_ComputesPercentagesPerZone()
    {
        using var db = CreateInMemoryDbContext();
        var zone = new Zone
        {
            Id = Guid.NewGuid(),
            Name = "Zone Alpha",
            Code = "ZA",
            TotalCapacity = 10
        };

        zone.Slots.Add(new Slot { Id = Guid.NewGuid(), ZoneId = zone.Id, SlotNumber = "A1", Status = SlotStatus.Occupied });
        zone.Slots.Add(new Slot { Id = Guid.NewGuid(), ZoneId = zone.Id, SlotNumber = "A2", Status = SlotStatus.Occupied });
        zone.Slots.Add(new Slot { Id = Guid.NewGuid(), ZoneId = zone.Id, SlotNumber = "A3", Status = SlotStatus.Available });
        zone.Slots.Add(new Slot { Id = Guid.NewGuid(), ZoneId = zone.Id, SlotNumber = "A4", Status = SlotStatus.Reserved });

        db.Zones.Add(zone);
        await db.SaveChangesAsync();

        var controller = new AnalyticsController(db);
        var result = await controller.GetOccupancy();
        var okResult = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<List<ZoneOccupancyDto>>(okResult.Value);

        Assert.Single(list);
        var item = list[0];
        Assert.Equal("Zone Alpha", item.ZoneName);
        Assert.Equal(10, item.TotalCapacity);
        Assert.Equal(2, item.OccupiedSlots);
        Assert.Equal(1, item.AvailableSlots);
        Assert.Equal(1, item.ReservedSlots);
        Assert.Equal(20.0, item.OccupancyPercentage);
    }

    [Fact]
    public async Task GetWorkflowsSummary_GroupsStatusesAccurately()
    {
        using var db = CreateInMemoryDbContext();
        db.AgentWorkflowRuns.AddRange(
            new AgentWorkflowRun { Status = WorkflowStatus.AwaitingApproval, Objective = "Obj 1", WorkflowType = "T1" },
            new AgentWorkflowRun { Status = WorkflowStatus.AwaitingApproval, Objective = "Obj 2", WorkflowType = "T1" },
            new AgentWorkflowRun { Status = WorkflowStatus.Approved, Objective = "Obj 3", WorkflowType = "T1" },
            new AgentWorkflowRun { Status = WorkflowStatus.Approved, Objective = "Obj 4", WorkflowType = "T1" },
            new AgentWorkflowRun { Status = WorkflowStatus.Rejected, Objective = "Obj 5", WorkflowType = "T1" },
            new AgentWorkflowRun { Status = WorkflowStatus.Failed, Objective = "Obj 6", WorkflowType = "T1" },
            new AgentWorkflowRun { Status = WorkflowStatus.Running, Objective = "Obj 7", WorkflowType = "T1" }
        );
        await db.SaveChangesAsync();

        var controller = new AnalyticsController(db);
        var result = await controller.GetWorkflowsSummary();
        var okResult = Assert.IsType<OkObjectResult>(result);
        var summary = Assert.IsType<WorkflowAnalyticsSummaryDto>(okResult.Value);

        Assert.Equal(2, summary.Pending);
        Assert.Equal(1, summary.Approved);
        Assert.Equal(1, summary.AutoApproved);
        Assert.Equal(1, summary.Rejected);
        Assert.Equal(1, summary.Failed);
        Assert.Equal(1, summary.Running);
        Assert.Equal(7, summary.Total);
    }
}
