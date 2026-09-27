using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using OpenParking.Core.Interfaces;
using OpenParking.Core.Models;

namespace OpenParking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SessionsController(IBookingService bookingService, ISettingsService settingsService) : ControllerBase
{
    [HttpPost("check-in")]
    public async Task<ActionResult<ApiResponse<SessionDto>>> CheckIn([FromBody] CheckInRequest request)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        
        if (request.UserId == null && request.BookingId == null && string.IsNullOrWhiteSpace(request.BookingCode) && request.SlotId.HasValue)
        {
             request.UserId = GetCurrentUserId();
        }

        var session = await bookingService.CheckInAsync(request, ip);
        var currency = await settingsService.GetStringAsync("pricing.default_currency", "USD");
        return Ok(ApiResponse<SessionDto>.Ok(SessionDto.FromEntity(session, session.Booking, session.Slot, currency), HttpContext.TraceIdentifier));
    }

    [HttpPost("check-out")]
    public async Task<ActionResult<ApiResponse<SessionDto>>> CheckOut([FromBody] CheckOutRequest request)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var result = await bookingService.CheckOutAsync(request, ip);
        var currency = await settingsService.GetStringAsync("pricing.default_currency", "USD");
        
        return Ok(ApiResponse<SessionDto>.Ok(
            SessionDto.FromEntity(result.Session!, result.Session!.Booking, result.Session!.Slot, currency), 
            HttpContext.TraceIdentifier));
    }

    [HttpGet("active")]
    public async Task<ActionResult<ApiResponse<SessionDto>>> GetActiveSession([FromQuery] Guid? userId = null, [FromQuery] Guid? bookingId = null)
    {
        var targetUserId = userId ?? GetCurrentUserId();
        var session = await bookingService.GetActiveSessionAsync(targetUserId, bookingId);
        
        if (session == null)
            throw new AppException(ErrorCodes.NotFound, "No active parking session found.", 404);

        var currency = await settingsService.GetStringAsync("pricing.default_currency", "USD");
        return Ok(ApiResponse<SessionDto>.Ok(SessionDto.FromEntity(session, session.Booking, session.Slot, currency), HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<SessionDto>>> GetSessionById(Guid id)
    {
        var session = await bookingService.GetSessionAsync(id);
        var currency = await settingsService.GetStringAsync("pricing.default_currency", "USD");
        return Ok(ApiResponse<SessionDto>.Ok(SessionDto.FromEntity(session, session.Booking, session.Slot, currency), HttpContext.TraceIdentifier));
    }

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (Guid.TryParse(sub, out var guid)) return guid;
        return null;
    }
}

public class SessionDto
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public Guid UserId { get; set; }
    public Guid SlotId { get; set; }
    public string SlotNumber { get; set; } = string.Empty;
    public string ZoneName { get; set; } = string.Empty;
    public Guid? ZoneId { get; set; }
    public DateTime CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public int OverstayMinutes { get; set; }
    public decimal TotalFee { get; set; }
    public decimal PenaltyFee { get; set; }
    public string? ReceiptPdfUrl { get; set; }
    public decimal HourlyRate { get; set; }
    public string Currency { get; set; } = "USD";

    public static SessionDto FromEntity(OpenParking.Core.Entities.ParkingSession s, OpenParking.Core.Entities.Booking? b, OpenParking.Core.Entities.Slot? slot, string currency)
    {
        return new SessionDto
        {
            Id = s.Id,
            BookingId = s.BookingId,
            UserId = s.UserId,
            SlotId = s.SlotId,
            SlotNumber = slot?.SlotNumber ?? b?.Slot?.SlotNumber ?? "Unassigned",
            ZoneName = slot?.Zone?.Name ?? b?.Slot?.Zone?.Name ?? "Main Campus Lot",
            ZoneId = slot?.ZoneId ?? b?.Slot?.ZoneId,
            CheckInTime = s.CheckInTime,
            CheckOutTime = s.CheckOutTime,
            Status = s.Status.ToString(),
            OverstayMinutes = s.OverstayMinutes,
            TotalFee = s.TotalFee,
            PenaltyFee = s.PenaltyFee,
            ReceiptPdfUrl = s.ReceiptPdfUrl,
            HourlyRate = slot?.Zone?.BaseHourlyRate ?? 5.00m,
            Currency = currency
        };
    }
}
