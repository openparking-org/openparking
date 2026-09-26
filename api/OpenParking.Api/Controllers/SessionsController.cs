using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OpenParking.Api.Hubs;
using OpenParking.Core.Entities;
using OpenParking.Core.Models;
using OpenParking.Infrastructure.Data;

namespace OpenParking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SessionsController(AppDbContext db, IHubContext<SlotHub>? hubContext = null) : ControllerBase
{
    [HttpPost("check-in")]
    public async Task<ActionResult<ApiResponse<SessionDto>>> CheckIn([FromBody] CheckInRequest request)
    {
        Booking? booking = null;

        if (request.BookingId.HasValue && request.BookingId.Value != Guid.Empty)
        {
            booking = await db.Bookings
                .Include(b => b.Slot)
                .ThenInclude(s => s!.Zone)
                .FirstOrDefaultAsync(b => b.Id == request.BookingId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(request.BookingCode))
        {
            booking = await db.Bookings
                .Include(b => b.Slot)
                .ThenInclude(s => s!.Zone)
                .FirstOrDefaultAsync(b => b.QrCodeContent.Contains(request.BookingCode) || b.Id.ToString() == request.BookingCode);
        }

        if (booking == null)
        {
            if (request.SlotId.HasValue && request.SlotId.Value != Guid.Empty)
            {
                var slot = await db.Slots.Include(s => s.Zone).FirstOrDefaultAsync(s => s.Id == request.SlotId.Value);
                if (slot == null)
                    throw new AppException(ErrorCodes.NotFound, "Booking or slot not found for provided check-in QR.", 404);

                var userId = GetCurrentUserId() ?? request.UserId ?? Guid.NewGuid();
                booking = new Booking
                {
                    Id = request.BookingId ?? Guid.NewGuid(),
                    UserId = userId,
                    SlotId = slot.Id,
                    StartTime = DateTime.UtcNow,
                    EndTime = DateTime.UtcNow.AddHours(2),
                    Status = BookingStatus.Active,
                    QrCodeContent = $"openparking://session/start?bookingId={request.BookingId}&slotId={slot.Id}",
                    EstimatedFee = slot.Zone?.BaseHourlyRate * 2 ?? 10m
                };
                db.Bookings.Add(booking);
            }
            else
            {
                throw new AppException(ErrorCodes.NotFound, "Booking not found for the scanned QR code.", 404);
            }
        }

        var existingActiveSession = await db.ParkingSessions
            .FirstOrDefaultAsync(s => s.BookingId == booking.Id && s.Status == SessionStatus.Active);

        if (existingActiveSession != null)
            throw new AppException(ErrorCodes.SessionActive, "An active parking session is already running for this booking.", 409);

        if (booking.Status == BookingStatus.Completed || booking.Status == BookingStatus.Cancelled)
            throw new AppException(ErrorCodes.ValidationFailed, $"Booking cannot be checked in because its status is {booking.Status}.");

        var session = new ParkingSession
        {
            Id = Guid.NewGuid(),
            BookingId = booking.Id,
            UserId = booking.UserId,
            SlotId = booking.SlotId,
            CheckInTime = DateTime.UtcNow,
            Status = SessionStatus.Active,
            OverstayMinutes = 0,
            TotalFee = 0m,
            PenaltyFee = 0m
        };

        booking.Status = BookingStatus.Active;
        db.ParkingSessions.Add(session);

        var targetSlot = booking.Slot ?? await db.Slots.Include(s => s.Zone).FirstOrDefaultAsync(s => s.Id == session.SlotId);
        if (targetSlot != null)
        {
            targetSlot.Status = SlotStatus.Occupied;
            targetSlot.UpdatedAt = DateTime.UtcNow;

            if (hubContext != null)
            {
                try
                {
                    await hubContext.Clients.Group($"zone:{targetSlot.ZoneId}").SendAsync("SlotStatusChanged", new
                    {
                        slotId = targetSlot.Id.ToString(),
                        status = "Occupied",
                        updatedAt = DateTime.UtcNow.ToString("o")
                    });
                }
                catch { }
            }
        }

        await db.SaveChangesAsync();

        return Ok(ApiResponse<SessionDto>.Ok(SessionDto.FromEntity(session, booking, targetSlot), HttpContext.TraceIdentifier));
    }

    [HttpPost("check-out")]
    public async Task<ActionResult<ApiResponse<SessionDto>>> CheckOut([FromBody] CheckOutRequest request)
    {
        ParkingSession? session = null;

        if (request.SessionId.HasValue && request.SessionId.Value != Guid.Empty)
        {
            session = await db.ParkingSessions
                .Include(s => s.Booking)
                .FirstOrDefaultAsync(s => s.Id == request.SessionId.Value);
        }
        else if (request.BookingId.HasValue && request.BookingId.Value != Guid.Empty)
        {
            session = await db.ParkingSessions
                .Include(s => s.Booking)
                .Where(s => s.BookingId == request.BookingId.Value && s.Status == SessionStatus.Active)
                .OrderByDescending(s => s.CheckInTime)
                .FirstOrDefaultAsync();
        }

        if (session == null)
            throw new AppException(ErrorCodes.NotFound, "No active parking session found.", 404);

        if (session.Status != SessionStatus.Active && session.Status != SessionStatus.OverstayDetected)
            throw new AppException(ErrorCodes.ValidationFailed, $"Session cannot be checked out because it is already {session.Status}.");

        var now = DateTime.UtcNow;
        session.CheckOutTime = now;
        session.UpdatedAt = now;
        session.Status = SessionStatus.Completed;

        var slot = await db.Slots.Include(s => s.Zone).FirstOrDefaultAsync(s => s.Id == session.SlotId);
        var hourlyRate = slot?.Zone?.BaseHourlyRate ?? 5.00m;

        var durationMinutes = Math.Max(1, (now - session.CheckInTime).TotalMinutes);
        var billableBlocks = (decimal)Math.Ceiling(durationMinutes / 15.0);
        var billableHours = (billableBlocks * 15.0m) / 60.0m;
        var subtotal = Math.Round(billableHours * hourlyRate, 2);

        if (session.Booking != null && now > session.Booking.EndTime)
        {
            var overstaySpan = now - session.Booking.EndTime;
            session.OverstayMinutes = (int)Math.Max(0, overstaySpan.TotalMinutes);

            var penaltySetting = await db.SystemSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Key == "overstay.penalty_per_hour" || s.Key == "overstay.penalty_per_extra_hour");

            var penaltyPerHour = 25.00m;
            if (penaltySetting != null && decimal.TryParse(penaltySetting.Value, out var parsedRate))
                penaltyPerHour = parsedRate;

            var extraHours = (decimal)Math.Ceiling(session.OverstayMinutes / 60.0);
            session.PenaltyFee = Math.Round(extraHours * penaltyPerHour, 2);
        }

        session.TotalFee = subtotal + session.PenaltyFee;
        session.ReceiptPdfUrl = $"/receipts/{session.Id}.pdf";

        if (session.Booking != null)
            session.Booking.Status = BookingStatus.Completed;

        if (slot != null)
        {
            slot.Status = SlotStatus.Available;
            slot.UpdatedAt = now;

            if (hubContext != null)
            {
                try
                {
                    await hubContext.Clients.Group($"zone:{slot.ZoneId}").SendAsync("SlotStatusChanged", new
                    {
                        slotId = slot.Id.ToString(),
                        status = "Available",
                        updatedAt = now.ToString("o")
                    });
                }
                catch { }
            }
        }

        await db.SaveChangesAsync();

        return Ok(ApiResponse<SessionDto>.Ok(SessionDto.FromEntity(session, session.Booking, slot), HttpContext.TraceIdentifier));
    }

    [HttpGet("active")]
    public async Task<ActionResult<ApiResponse<SessionDto>>> GetActiveSession([FromQuery] Guid? userId = null, [FromQuery] Guid? bookingId = null)
    {
        var targetUserId = userId ?? GetCurrentUserId();

        var query = db.ParkingSessions
            .Include(s => s.Booking)
            .Where(s => s.Status == SessionStatus.Active || s.Status == SessionStatus.OverstayDetected)
            .AsNoTracking();

        if (bookingId.HasValue && bookingId.Value != Guid.Empty)
            query = query.Where(s => s.BookingId == bookingId.Value);
        else if (targetUserId.HasValue)
            query = query.Where(s => s.UserId == targetUserId.Value);

        var session = await query.OrderByDescending(s => s.CheckInTime).FirstOrDefaultAsync();
        if (session == null)
            throw new AppException(ErrorCodes.NotFound, "No active parking session found.", 404);

        var slot = await db.Slots.Include(s => s.Zone).FirstOrDefaultAsync(s => s.Id == session.SlotId);
        return Ok(ApiResponse<SessionDto>.Ok(SessionDto.FromEntity(session, session.Booking, slot), HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<SessionDto>>> GetSessionById(Guid id)
    {
        var session = await db.ParkingSessions
            .Include(s => s.Booking)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id);

        if (session == null)
            throw new AppException(ErrorCodes.NotFound, "Session not found.", 404);

        var slot = await db.Slots.Include(s => s.Zone).FirstOrDefaultAsync(s => s.Id == session.SlotId);
        return Ok(ApiResponse<SessionDto>.Ok(SessionDto.FromEntity(session, session.Booking, slot), HttpContext.TraceIdentifier));
    }

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (Guid.TryParse(sub, out var guid)) return guid;
        return null;
    }
}

public class CheckInRequest
{
    public Guid? BookingId { get; set; }
    public string? BookingCode { get; set; }
    public Guid? SlotId { get; set; }
    public Guid? UserId { get; set; }
}

public class CheckOutRequest
{
    public Guid? SessionId { get; set; }
    public Guid? BookingId { get; set; }
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

    public static SessionDto FromEntity(ParkingSession s, Booking? b, Slot? slot)
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
            HourlyRate = slot?.Zone?.BaseHourlyRate ?? 5.00m
        };
    }
}
