using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenParking.Core.Entities;
using OpenParking.Core.Models;
using OpenParking.Infrastructure.Data;

namespace OpenParking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "ParkingAdmin,SystemAdmin")]
public class AnalyticsController(AppDbContext db) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<ApiResponse<AnalyticsSummaryDto>>> GetSummary([FromQuery] int days = 30, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        var (startDate, endDate) = ResolveDateRange(days, from, to);

        var sessionsInRange = db.ParkingSessions
            .AsNoTracking()
            .Where(s => s.CheckInTime >= startDate && s.CheckInTime <= endDate);

        var totalSessions = await sessionsInRange.CountAsync();
        var activeSessions = await db.ParkingSessions.AsNoTracking().CountAsync(s => s.Status == SessionStatus.Active);
        var completedSessions = await sessionsInRange.CountAsync(s => s.Status == SessionStatus.Completed);
        var overstaySessions = await sessionsInRange.CountAsync(s => s.Status == SessionStatus.OverstayDetected || s.OverstayMinutes > 0);

        var totalRevenue = await sessionsInRange.SumAsync(s => (decimal?)s.TotalFee) ?? 0m;
        var totalPenalties = await sessionsInRange.SumAsync(s => (decimal?)s.PenaltyFee) ?? 0m;

        var completedDurations = await sessionsInRange
            .Where(s => s.CheckOutTime != null && s.CheckOutTime > s.CheckInTime)
            .Select(s => new { s.CheckInTime, CheckOutTime = s.CheckOutTime!.Value })
            .ToListAsync();

        double avgDurationMinutes = 0;
        if (completedDurations.Count > 0)
        {
            avgDurationMinutes = Math.Round(completedDurations.Average(d => (d.CheckOutTime - d.CheckInTime).TotalMinutes), 1);
        }

        var pendingWorkflows = await db.AgentWorkflowRuns
            .AsNoTracking()
            .CountAsync(w => w.Status == WorkflowStatus.AwaitingApproval);

        var totalWorkflows = await db.AgentWorkflowRuns
            .AsNoTracking()
            .CountAsync(w => w.CreatedAt >= startDate && w.CreatedAt <= endDate);

        // I need to use CreatedAt since TriggeredAt was renamed to CreatedAt in Entities.cs.
        var summary = new AnalyticsSummaryDto
        {
            TotalSessions = totalSessions,
            ActiveSessions = activeSessions,
            CompletedSessions = completedSessions,
            OverstaySessions = overstaySessions,
            TotalRevenue = totalRevenue,
            TotalPenalties = totalPenalties,
            AvgDurationMinutes = avgDurationMinutes,
            PendingWorkflowsCount = pendingWorkflows,
            TotalWorkflowsCount = totalWorkflows,
            From = startDate,
            To = endDate
        };

        return Ok(ApiResponse<AnalyticsSummaryDto>.Ok(summary, HttpContext.TraceIdentifier));
    }

    [HttpGet("revenue/daily")]
    public async Task<ActionResult<ApiResponse<List<DailyRevenueDto>>>> GetDailyRevenue([FromQuery] int days = 30, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        var (startDate, endDate) = ResolveDateRange(days, from, to);

        var rawSessions = await db.ParkingSessions
            .AsNoTracking()
            .Where(s => s.CheckInTime >= startDate && s.CheckInTime <= endDate)
            .Select(s => new { Date = s.CheckInTime.Date, s.TotalFee })
            .ToListAsync();

        var revenueByDate = rawSessions
            .GroupBy(s => s.Date)
            .ToDictionary(g => g.Key, g => g.Sum(s => s.TotalFee));

        var dailyList = new List<DailyRevenueDto>();
        var current = startDate.Date;
        var end = endDate.Date;

        while (current <= end)
        {
            dailyList.Add(new DailyRevenueDto
            {
                Date = current.ToString("yyyy-MM-dd"),
                Revenue = revenueByDate.TryGetValue(current, out var rev) ? rev : 0m
            });
            current = current.AddDays(1);
        }

        return Ok(ApiResponse<List<DailyRevenueDto>>.Ok(dailyList, HttpContext.TraceIdentifier));
    }

    [HttpGet("occupancy")]
    public async Task<ActionResult<ApiResponse<List<ZoneOccupancyDto>>>> GetOccupancy()
    {
        var zones = await db.Zones
            .Include(z => z.Slots)
            .AsNoTracking()
            .ToListAsync();

        var list = zones.Select(z =>
        {
            var capacity = z.TotalCapacity > 0 ? z.TotalCapacity : Math.Max(1, z.Slots.Count);
            var occupied = z.Slots.Count(s => s.Status == SlotStatus.Occupied);
            var available = z.Slots.Count(s => s.Status == SlotStatus.Available);
            var reserved = z.Slots.Count(s => s.Status == SlotStatus.Reserved);
            var percentage = Math.Round((double)occupied / capacity * 100, 1);

            return new ZoneOccupancyDto
            {
                ZoneId = z.Id,
                ZoneName = z.Name,
                ZoneCode = z.Code,
                TotalCapacity = capacity,
                OccupiedSlots = occupied,
                AvailableSlots = available,
                ReservedSlots = reserved,
                OccupancyPercentage = percentage
            };
        }).ToList();

        return Ok(ApiResponse<List<ZoneOccupancyDto>>.Ok(list, HttpContext.TraceIdentifier));
    }

    [HttpGet("violations/weekly")]
    public async Task<ActionResult<ApiResponse<List<WeeklyViolationDto>>>> GetWeeklyViolations([FromQuery] int weeks = 8, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        var endDate = to ?? DateTime.UtcNow;
        var startDate = from ?? endDate.AddDays(-(weeks * 7));

        var overstaySessions = await db.ParkingSessions
            .AsNoTracking()
            .Where(s => (s.Status == SessionStatus.OverstayDetected || s.OverstayMinutes > 0) &&
                        s.CheckInTime >= startDate && s.CheckInTime <= endDate)
            .Select(s => s.CheckInTime)
            .ToListAsync();

        var weeklyList = new List<WeeklyViolationDto>();
        var current = startDate.Date;

        for (int i = 0; i < weeks; i++)
        {
            var weekStart = current;
            var weekEnd = current.AddDays(7);
            var count = overstaySessions.Count(t => t >= weekStart && t < weekEnd);

            weeklyList.Add(new WeeklyViolationDto
            {
                Week = $"W{i + 1}",
                WeekLabel = $"{weekStart:MMM dd} - {weekEnd.AddDays(-1):MMM dd}",
                Count = count
            });

            current = weekEnd;
        }

        return Ok(ApiResponse<List<WeeklyViolationDto>>.Ok(weeklyList, HttpContext.TraceIdentifier));
    }

    [HttpGet("workflows/summary")]
    public async Task<ActionResult<ApiResponse<WorkflowAnalyticsSummaryDto>>> GetWorkflowsSummary([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        var query = db.AgentWorkflowRuns.AsNoTracking().AsQueryable();

        if (from.HasValue) query = query.Where(w => w.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(w => w.CreatedAt <= to.Value);

        var runs = await query.Select(w => w.Status).ToListAsync();

        var summary = new WorkflowAnalyticsSummaryDto
        {
            Pending = runs.Count(s => s == WorkflowStatus.AwaitingApproval),
            Approved = runs.Count(s => s == WorkflowStatus.Approved),
            AutoApproved = 0, // AutoApproved is not in our enum currently, can map to Approved or skip
            Rejected = runs.Count(s => s == WorkflowStatus.Rejected),
            Failed = runs.Count(s => s == WorkflowStatus.Failed),
            Running = runs.Count(s => s == WorkflowStatus.Running),
            Total = runs.Count
        };

        return Ok(ApiResponse<WorkflowAnalyticsSummaryDto>.Ok(summary, HttpContext.TraceIdentifier));
    }

    private static (DateTime start, DateTime end) ResolveDateRange(int days, DateTime? from, DateTime? to)
    {
        var end = to ?? DateTime.UtcNow;
        var start = from ?? end.AddDays(-Math.Max(1, days));
        return (start, end);
    }
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
