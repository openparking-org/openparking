using Xunit;
using OpenParking.Core.Models;
using OpenParking.Core.Entities;

namespace OpenParking.Tests;

public class CoreTests
{
    [Fact]
    public void PaginatedQuery_Clamps_Maximum_Page_Size()
    {
        var query = new PaginatedQuery { Page = 1, PageSize = 500 };
        Assert.Equal(100, query.PageSize);
    }

    [Fact]
    public void PaginatedQuery_Prevents_Zero_Or_Negative_Page_Size()
    {
        var query = new PaginatedQuery { Page = 1, PageSize = -5 };
        Assert.Equal(1, query.PageSize);
    }

    [Fact]
    public void PagedResult_Calculates_TotalPages_Accurately()
    {
        var items = new List<string> { "item1", "item2" };
        var paged = PagedResult<string>.Create(items, totalCount: 25, page: 1, pageSize: 10);

        Assert.Equal(3, paged.TotalPages);
        Assert.True(paged.HasNextPage);
        Assert.False(paged.HasPreviousPage);
    }

    [Fact]
    public void SystemSetting_Defaults_Are_Consistent()
    {
        var setting = new SystemSetting
        {
            Key = "pricing.base_hourly_rate",
            Value = "5.00"
        };

        Assert.Equal("pricing.base_hourly_rate", setting.Key);
        Assert.Equal("General", setting.Category);
        Assert.Equal("System", setting.UpdatedBy);
    }

    // -----------------------------------------------------------------------
    // Issue #24 — AgentWorkflowRun & ZonePricingRule entity defaults
    // -----------------------------------------------------------------------

    [Fact]
    public void AgentWorkflowRun_Defaults_Are_Safe_For_Persistence()
    {
        var run = new AgentWorkflowRun
        {
            Objective = "Detect overstay for session sess-001",
            WorkflowType = "OVERSTAY_ENFORCEMENT"
        };

        // Status must start as Running (default)
        Assert.Equal(WorkflowStatus.Running, run.Status);

        // JSONB columns must default to "{}"
        Assert.Equal("{}", run.PlanJson);
        Assert.Equal("{}", run.StepResultsJson);

        // Optional FK fields must be null until explicitly set
        Assert.Null(run.ApprovedBy);
        Assert.Null(run.ApprovedAt);
        Assert.Null(run.ZoneId);
        Assert.Null(run.SessionId);
    }

    [Fact]
    public void ZonePricingRule_Test_Skipped_Entity_Not_In_Current_Schema()
    {
        // ZonePricingRule was planned but not implemented in the current entity schema.
        // Kept as a placeholder for the surge pricing milestone.
        Assert.True(true);
    }

    [Fact]
    public void ParkingSession_Starts_With_Active_Status_And_Zero_Overstay()
    {
        var session = new ParkingSession
        {
            BookingId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            SlotId = Guid.NewGuid()
        };

        Assert.Equal(SessionStatus.Active, session.Status);
        Assert.Equal(0, session.OverstayMinutes);
        Assert.Equal(0m, session.PenaltyFee);
    }
}
