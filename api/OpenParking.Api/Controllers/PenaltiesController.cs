using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenParking.Core.Entities;
using OpenParking.Core.Interfaces;
using OpenParking.Core.Models;

namespace OpenParking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PenaltiesController(IEnforcementService enforcementService) : ControllerBase
{
    [HttpGet("my")]
    public async Task<ActionResult<ApiResponse<PagedResult<Penalty>>>> GetMyPenalties([FromQuery] PaginatedQuery query)
    {
        var userId = GetCurrentUserId() ?? throw new AppException(ErrorCodes.Unauthorized, "User context not found", 401);
        // Note: The mobile app requests user's penalties. 
        // We will fetch all penalties and filter by userId as a fallback, since the interface doesn't have a GetUserPenaltiesAsync method yet.
        var allPenalties = await enforcementService.ListPenaltiesAsync(new PaginatedQuery { Page = 1, PageSize = 1000 });
        
        var userPenalties = allPenalties.Items.Where(p => p.UserId == userId).ToList();
        
        var result = new PagedResult<Penalty>
        {
            Items = userPenalties.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList(),
            TotalCount = userPenalties.Count,
            Page = query.Page,
            PageSize = query.PageSize
        };
        
        return Ok(ApiResponse<PagedResult<Penalty>>.Ok(result, HttpContext.TraceIdentifier));
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
