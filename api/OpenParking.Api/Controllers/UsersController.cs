using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenParking.Core.Entities;
using OpenParking.Core.Interfaces;
using OpenParking.Core.Models;

namespace OpenParking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController(IUserService userService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<AuthResult>>> Register([FromBody] RegisterRequest req)
    {
        var result = await userService.RegisterAsync(req);
        return Ok(ApiResponse<AuthResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<AuthResult>>> Login([FromBody] LoginRequest req)
    {
        var result = await userService.LoginAsync(req.Email, req.Password);
        return Ok(ApiResponse<AuthResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<UserProfile>>> GetMe()
    {
        var userId = GetCurrentUserId() ?? throw new AppException(ErrorCodes.Unauthorized, "User context not found", 401);
        var user = await userService.GetByIdAsync(userId);
        if (user == null) return NotFound();
        return Ok(ApiResponse<UserProfile>.Ok(MapProfile(user), HttpContext.TraceIdentifier));
    }

    [Authorize(Roles = "SystemAdmin")]
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<UserProfile>>>> ListUsers([FromQuery] PaginatedQuery query)
    {
        var result = await userService.ListAsync(query);
        var pagedProfiles = new PagedResult<UserProfile>
        {
            Items = result.Items.Select(MapProfile).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        };
        return Ok(ApiResponse<PagedResult<UserProfile>>.Ok(pagedProfiles, HttpContext.TraceIdentifier));
    }

    [Authorize(Roles = "SystemAdmin")]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<UserProfile>>> GetUser(Guid id)
    {
        var user = await userService.GetByIdAsync(id);
        if (user == null) return NotFound();
        return Ok(ApiResponse<UserProfile>.Ok(MapProfile(user), HttpContext.TraceIdentifier));
    }

    [Authorize(Roles = "SystemAdmin")]
    [HttpPatch("{id:guid}/role")]
    public async Task<ActionResult<ApiResponse<UserProfile>>> UpdateRole(Guid id, [FromBody] UpdateRoleRequest req)
    {
        var actorId = GetCurrentUserId() ?? throw new AppException(ErrorCodes.Unauthorized, "User context not found", 401);
        var user = await userService.UpdateRoleAsync(id, req.Role, actorId);
        return Ok(ApiResponse<UserProfile>.Ok(MapProfile(user), HttpContext.TraceIdentifier));
    }

    [Authorize]
    [HttpPost("{id:guid}/permits")]
    public async Task<ActionResult<ApiResponse<DisabilityPermit>>> SubmitPermit(Guid id, [FromBody] SubmitPermitRequest req)
    {
        var actorId = GetCurrentUserId() ?? throw new AppException(ErrorCodes.Unauthorized, "User context not found", 401);
        if (id != actorId && !User.IsInRole("SystemAdmin"))
            throw new AppException(ErrorCodes.Forbidden, "You can only submit permits for yourself.", 403);
            
        var permit = await userService.SubmitPermitAsync(id, req);
        return Ok(ApiResponse<DisabilityPermit>.Ok(permit, HttpContext.TraceIdentifier));
    }

    [Authorize(Roles = "ParkingAdmin,SystemAdmin")]
    [HttpGet("permits/pending")]
    public async Task<ActionResult<ApiResponse<List<DisabilityPermit>>>> GetPendingPermits()
    {
        var permits = await userService.GetPendingPermitsAsync();
        return Ok(ApiResponse<List<DisabilityPermit>>.Ok(permits, HttpContext.TraceIdentifier));
    }

    [Authorize(Roles = "ParkingAdmin,SystemAdmin")]
    [HttpPatch("permits/{permitId:guid}/review")]
    public async Task<ActionResult<ApiResponse<DisabilityPermit>>> ReviewPermit(Guid permitId, [FromBody] ReviewPermitRequest req)
    {
        var actorId = GetCurrentUserId() ?? throw new AppException(ErrorCodes.Unauthorized, "User context not found", 401);
        var permit = await userService.ReviewPermitAsync(permitId, req.Decision, req.Notes, actorId);
        return Ok(ApiResponse<DisabilityPermit>.Ok(permit, HttpContext.TraceIdentifier));
    }

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (Guid.TryParse(sub, out var guid)) return guid;
        return null;
    }

    private static UserProfile MapProfile(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        FullName = user.FullName,
        Role = user.Role.ToString(),
        HasDisabilityPermit = user.HasDisabilityPermit
    };
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class UpdateRoleRequest
{
    public UserRole Role { get; set; }
}

public class ReviewPermitRequest
{
    public PermitStatus Decision { get; set; }
    public string? Notes { get; set; }
}
