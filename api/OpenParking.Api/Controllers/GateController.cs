using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenParking.Core.Interfaces;
using OpenParking.Core.Models;
using OpenParking.Infrastructure.Services;

namespace OpenParking.Api.Controllers;

[ApiController]
[Route("api/gate")]
[Authorize(Roles = "ParkingAdmin,SystemAdmin")]
public class GateController(GateService gate, ISettingsService settings) : ControllerBase
{
    [HttpPost("entry")]
    public async Task<ActionResult<ApiResponse<SessionDto>>> Entry(GateRequest request) => await Run(request, true);

    [HttpPost("exit")]
    public async Task<ActionResult<ApiResponse<SessionDto>>> Exit(GateRequest request) => await Run(request, false);

    private async Task<ActionResult<ApiResponse<SessionDto>>> Run(GateRequest request, bool entry)
    {
        if (request.ZoneId == Guid.Empty)
            throw new AppException(ErrorCodes.ValidationFailed, "Select a parking zone.");
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var actor))
            throw new AppException(ErrorCodes.Unauthorized, "Sign in again.", 401);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var session = entry ? await gate.EnterAsync(request.VehiclePlate, request.ZoneId, actor, ip)
            : await gate.ExitAsync(request.VehiclePlate, request.ZoneId, actor, ip);
        var currency = await settings.GetStringAsync("pricing.default_currency", "USD");
        return Ok(ApiResponse<SessionDto>.Ok(SessionDto.FromEntity(session, session.Booking, session.Slot, currency), HttpContext.TraceIdentifier));
    }
}

public class GateRequest
{
    [Required, StringLength(30)] public string VehiclePlate { get; set; } = string.Empty;
    public Guid ZoneId { get; set; }
}
