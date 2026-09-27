using OpenParking.Core.Models;

namespace OpenParking.Core.Interfaces;

public interface IAnalyticsService
{
    Task<AnalyticsSummaryDto> GetSummaryAsync(int days = 30, DateTime? from = null, DateTime? to = null);
    Task<List<DailyRevenueDto>> GetDailyRevenueAsync(int days = 30, DateTime? from = null, DateTime? to = null);
    Task<List<ZoneOccupancyDto>> GetOccupancyAsync();
    Task<List<WeeklyViolationDto>> GetWeeklyViolationsAsync(int weeks = 8, DateTime? from = null, DateTime? to = null);
    Task<WorkflowAnalyticsSummaryDto> GetWorkflowsSummaryAsync(DateTime? from = null, DateTime? to = null);
}

public class AnalyticsSummaryDto
{
    public int TotalSessions { get; set; }
    public int ActiveSessions { get; set; }
    public int CompletedSessions { get; set; }
    public int OverstaySessions { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalPenalties { get; set; }
    public double AvgDurationMinutes { get; set; }
    public int PendingWorkflowsCount { get; set; }
    public int TotalWorkflowsCount { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
}

public class DailyRevenueDto
{
    public string Date { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
}

public class ZoneOccupancyDto
{
    public Guid ZoneId { get; set; }
    public string ZoneName { get; set; } = string.Empty;
    public string ZoneCode { get; set; } = string.Empty;
    public int TotalCapacity { get; set; }
    public int OccupiedSlots { get; set; }
    public int AvailableSlots { get; set; }
    public int ReservedSlots { get; set; }
    public double OccupancyPercentage { get; set; }
}

public class WeeklyViolationDto
{
    public string Week { get; set; } = string.Empty;
    public string WeekLabel { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class WorkflowAnalyticsSummaryDto
{
    public int Pending { get; set; }
    public int Approved { get; set; }
    public int AutoApproved { get; set; }
    public int Rejected { get; set; }
    public int Failed { get; set; }
    public int Running { get; set; }
    public int Total { get; set; }
}
