using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OpenParking.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenParking.Core.Entities;
using OpenParking.Core.Interfaces;
using OpenParking.Core.Models;

namespace OpenParking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PenaltiesController(IEnforcementService enforcementService, AppDbContext db, ISettingsService settings) : ControllerBase
{
    [HttpGet("my")]
    public async Task<ActionResult<ApiResponse<PagedResult<CustomerPenalty>>>> GetMyPenalties([FromQuery] PaginatedQuery query)
    {
        var userId = GetCurrentUserId() ?? throw new AppException(ErrorCodes.Unauthorized, "User context not found", 401);
        var source = db.Penalties.AsNoTracking().Where(p => p.UserId == userId);
        var currency = await settings.GetStringAsync("pricing.default_currency", "USD");
        var rows = await source.OrderByDescending(p => p.IssuedAt).Skip(Math.Max(0, query.Skip)).Take(query.Take).ToListAsync();
        var result = new PagedResult<CustomerPenalty>
        {
            Items = rows.Select(p => new CustomerPenalty(p.Id, p.UserId, p.SessionId, p.Amount, p.Reason,
                p.Status.ToString(), p.DisputeNotes, p.IssuedAt, currency)).ToList(),
            TotalCount = await source.CountAsync(), Page = query.Page, PageSize = query.PageSize
        };

        return Ok(ApiResponse<PagedResult<CustomerPenalty>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:guid}/dispute")]
    public async Task<ActionResult<ApiResponse<Penalty>>> DisputePenalty(Guid id, [FromBody] DisputeRequest req)
    {
        var userId = GetCurrentUserId() ?? throw new AppException(ErrorCodes.Unauthorized, "User context not found", 401);
        var penalty = await enforcementService.DisputePenaltyAsync(id, req.Notes ?? "", userId);
        return Ok(ApiResponse<Penalty>.Ok(penalty, HttpContext.TraceIdentifier));
    }

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (Guid.TryParse(sub, out var guid)) return guid;
        return null;
    }
}

public class DisputeRequest
{
    public string? Notes { get; set; }
}

public record CustomerPenalty(Guid Id, Guid UserId, Guid SessionId, decimal Amount, string Reason,
    string Status, string? DisputeNotes, DateTime IssuedAt, string Currency);
