using Microsoft.EntityFrameworkCore;
using OpenParking.Core.Entities;
using OpenParking.Core.Interfaces;
using OpenParking.Core.Models;
using OpenParking.Infrastructure.Data;

namespace OpenParking.Infrastructure.Services;

public class AnalyticsService(AppDbContext db) : IAnalyticsService
{
    public async Task<AnalyticsSummaryDto> GetSummaryAsync(int days = 30, DateTime? from = null, DateTime? to = null)
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

        return new AnalyticsSummaryDto
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
    }

    public async Task<List<DailyRevenueDto>> GetDailyRevenueAsync(int days = 30, DateTime? from = null, DateTime? to = null)
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

        return dailyList;
    }

    public async Task<List<ZoneOccupancyDto>> GetOccupancyAsync()
    {
        var zones = await db.Zones
            .Include(z => z.Slots)
            .AsNoTracking()
            .ToListAsync();

        return zones.Select(z =>
        {
            var capacity = z.TotalCapacity > 0 ? z.TotalCapacity : Math.Max(1, z.Slots.Count);
            var occupied = z.Slots.Count(s => s.Status == SlotStatus.Occupied);
            var available = z.Slots.Count(s => s.Status == SlotStatus.Available);
            var reserved = z.Slots.Count(s => s.Status == SlotStatus.Reserved);
            var percentage = capacity > 0 ? Math.Round((double)occupied / capacity * 100, 1) : 0;

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
    }

    public async Task<List<WeeklyViolationDto>> GetWeeklyViolationsAsync(int weeks = 8, DateTime? from = null, DateTime? to = null)
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

        return weeklyList;
    }

    public async Task<WorkflowAnalyticsSummaryDto> GetWorkflowsSummaryAsync(DateTime? from = null, DateTime? to = null)
    {
        var query = db.AgentWorkflowRuns.AsNoTracking().AsQueryable();

        if (from.HasValue) query = query.Where(w => w.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(w => w.CreatedAt <= to.Value);

        var runs = await query.Select(w => w.Status).ToListAsync();

        return new WorkflowAnalyticsSummaryDto
        {
            Pending = runs.Count(s => s == WorkflowStatus.AwaitingApproval),
            Approved = runs.Count(s => s == WorkflowStatus.Approved),
            AutoApproved = 0,
            Rejected = runs.Count(s => s == WorkflowStatus.Rejected),
            Failed = runs.Count(s => s == WorkflowStatus.Failed),
            Running = runs.Count(s => s == WorkflowStatus.Running),
            Total = runs.Count
        };
    }

    private static (DateTime start, DateTime end) ResolveDateRange(int days, DateTime? from, DateTime? to)
    {
        var end = to ?? DateTime.UtcNow;
        var start = from ?? end.AddDays(-Math.Max(1, days));
        return (start, end);
    }
}
