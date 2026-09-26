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

        // Status must start as PENDING_APPROVAL so it appears in the approval queue
        Assert.Equal("PENDING_APPROVAL", run.Status);

        // All JSONB columns must default to "{}" — not null — to avoid DB constraint errors
        Assert.Equal("{}", run.PlanJson);
        Assert.Equal("{}", run.StepResultsJson);
        Assert.Equal("{}", run.InputPayloadJson);
        Assert.Equal("{}", run.ExecutionSummaryJson);
        Assert.Equal("{}", run.ErrorLogJson);

        // Optional FK fields must be null until explicitly set
        Assert.Null(run.ApprovedBy);
        Assert.Null(run.ResolvedAt);
        Assert.Null(run.ZoneId);
        Assert.Null(run.SessionId);
    }

    [Fact]
    public void ZonePricingRule_Multiplier_Defaults_To_One()
    {
        var rule = new ZonePricingRule
        {
            ZoneId = Guid.NewGuid(),
            Reason = "Peak hour AI surge"
        };

        // Default multiplier is 1.00 (no surge) — must never be 0 or negative
        Assert.Equal(1.00m, rule.Multiplier);

        // FK links must start null until approval assigns them
        Assert.Null(rule.ApprovedBy);
        Assert.Null(rule.WorkflowRunId);
        Assert.Null(rule.ActiveUntil);
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
