using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenParking.Core.Interfaces;
using OpenParking.Core.Models;

namespace OpenParking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "ParkingAdmin,SystemAdmin")]
public class AnalyticsController(IAnalyticsService analyticsService) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<ApiResponse<AnalyticsSummaryDto>>> GetSummary([FromQuery] int days = 30, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        var summary = await analyticsService.GetSummaryAsync(days, from, to);
        return Ok(ApiResponse<AnalyticsSummaryDto>.Ok(summary, HttpContext.TraceIdentifier));
    }

    [HttpGet("revenue/daily")]
    public async Task<ActionResult<ApiResponse<List<DailyRevenueDto>>>> GetDailyRevenue([FromQuery] int days = 30, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        var dailyList = await analyticsService.GetDailyRevenueAsync(days, from, to);
        return Ok(ApiResponse<List<DailyRevenueDto>>.Ok(dailyList, HttpContext.TraceIdentifier));
    }

    [HttpGet("occupancy")]
    public async Task<ActionResult<ApiResponse<List<ZoneOccupancyDto>>>> GetOccupancy()
    {
        var list = await analyticsService.GetOccupancyAsync();
        return Ok(ApiResponse<List<ZoneOccupancyDto>>.Ok(list, HttpContext.TraceIdentifier));
    }

    [HttpGet("violations/weekly")]
    public async Task<ActionResult<ApiResponse<List<WeeklyViolationDto>>>> GetWeeklyViolations([FromQuery] int weeks = 8, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        var weeklyList = await analyticsService.GetWeeklyViolationsAsync(weeks, from, to);
        return Ok(ApiResponse<List<WeeklyViolationDto>>.Ok(weeklyList, HttpContext.TraceIdentifier));
    }

    [HttpGet("workflows/summary")]
    public async Task<ActionResult<ApiResponse<WorkflowAnalyticsSummaryDto>>> GetWorkflowsSummary([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        var summary = await analyticsService.GetWorkflowsSummaryAsync(from, to);
        return Ok(ApiResponse<WorkflowAnalyticsSummaryDto>.Ok(summary, HttpContext.TraceIdentifier));
    }
}
