using Microsoft.EntityFrameworkCore;
using Moq;
using OpenParking.Core.Entities;
using OpenParking.Core.Interfaces;
using OpenParking.Infrastructure.Data;
using OpenParking.Infrastructure.Services;
using Xunit;

namespace OpenParking.Tests;

public class AnalyticsServiceTests
{
    private static (AppDbContext db, Mock<ISettingsService> settingsMock, AnalyticsService service) CreateService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options);
        var settingsMock = new Mock<ISettingsService>();
        settingsMock.Setup(s => s.GetStringAsync("pricing.default_currency", "USD"))
            .ReturnsAsync("LKR");

        var service = new AnalyticsService(db, settingsMock.Object);
        return (db, settingsMock, service);
    }

    [Fact]
    public async Task GetSummaryAsync_ComputesTotalsAndAveragesCorrectly()
    {
        var (db, _, service) = CreateService();

        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Session 1: completed, 60 minutes, fee 300
        db.ParkingSessions.Add(new ParkingSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Status = SessionStatus.Completed,
            CheckInTime = now.AddMinutes(-90),
            CheckOutTime = now.AddMinutes(-30),
            TotalFee = 300m,
            PenaltyFee = 0m
        });

        // Session 2: completed, 120 minutes, fee 600, penalty 100, overstay
        db.ParkingSessions.Add(new ParkingSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Status = SessionStatus.Completed,
            CheckInTime = now.AddMinutes(-150),
            CheckOutTime = now.AddMinutes(-30),
            TotalFee = 600m,
            PenaltyFee = 100m,
            OverstayMinutes = 30
        });

        // Session 3: active
        db.ParkingSessions.Add(new ParkingSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Status = SessionStatus.Active,
            CheckInTime = now.AddMinutes(-10)
        });

        // Workflow runs
        db.AgentWorkflowRuns.Add(new AgentWorkflowRun
        {
            Id = Guid.NewGuid(),
            Status = WorkflowStatus.AwaitingApproval,
            CreatedAt = now.AddHours(-1)
        });

        await db.SaveChangesAsync();

        var summary = await service.GetSummaryAsync(days: 7);

        Assert.Equal(3, summary.TotalSessions);
        Assert.Equal(1, summary.ActiveSessions);
        Assert.Equal(2, summary.CompletedSessions);
        Assert.Equal(1, summary.OverstaySessions);
        Assert.Equal(900m, summary.TotalRevenue);
        Assert.Equal(100m, summary.TotalPenalties);
        Assert.Equal(90.0, summary.AvgDurationMinutes); // (60 + 120) / 2
        Assert.Equal(1, summary.PendingWorkflowsCount);
        Assert.Equal("LKR", summary.Currency);
    }
}
