using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenParking.Api.Dtos;
using OpenParking.Core.Entities;
using OpenParking.Core.Interfaces;
using OpenParking.Core.Models;
using OpenParking.Infrastructure.Data;

namespace OpenParking.Api.Controllers;

/// <summary>
/// Booking &amp; Payment slice. Owns what happens after the driver arrives:
/// scanning in, occupying the slot, and settling the bill on the way out.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SessionsController(
    AppDbContext db,
    IFeeCalculationService fees,
    ISettingsService settings,
    ILogger<SessionsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetSessions([FromQuery] PaginatedQuery query, [FromQuery] SessionStatus? status = null)
    {
        var sessions = db.ParkingSessions
            .Include(s => s.Booking)!.ThenInclude(b => b!.Slot)
            .AsNoTracking();

        if (status.HasValue)
            sessions = sessions.Where(s => s.Status == status.Value);

        var totalCount = await sessions.CountAsync();

        var page = await sessions
            .OrderByDescending(s => s.CheckInTime)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return Ok(PagedResult<SessionResponse>.Create(
            page.Where(s => s.Booking != null)
                .Select(s => SessionResponse.From(s, s.Booking!))
                .ToList(),
            totalCount,
            query.Page,
            query.PageSize));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetSession(Guid id)
    {
        var session = await db.ParkingSessions
            .Include(s => s.Booking)!.ThenInclude(b => b!.Slot)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id);

        if (session?.Booking == null)
            return NotFound(new { error = $"Session '{id}' not found" });

        return Ok(SessionResponse.From(session, session.Booking));
    }

    /// <summary>
    /// Exchanges a scanned pass code for an active session. The code is an
    /// opaque reference, so it is resolved by lookup rather than decoded.
    /// </summary>
    [HttpPost("check-in")]
    public async Task<IActionResult> CheckIn([FromBody] CheckInRequest request)
    {
        if (!DigitalPass.IsWellFormed(request.QrCodeContent))
            return BadRequest(new { error = "That does not look like an OpenParking pass code" });

        var booking = await db.Bookings
            .Include(b => b.Slot)
            .FirstOrDefaultAsync(b => b.QrCodeContent == request.QrCodeContent);

        if (booking == null)
            return NotFound(new { error = "No booking matches that pass" });

        if (booking.Status == BookingStatus.Active)
            return Conflict(new { error = "That pass has already been used to check in" });

        if (booking.Status != BookingStatus.Confirmed)
            return Conflict(new { error = $"Cannot check in against a booking that is {booking.Status}" });

        var now = DateTime.UtcNow;

        if (now > booking.EndTime)
        {
            // The window elapsed without the driver arriving, so retire the
            // reservation rather than leaving it to block the slot forever.
            booking.Status = BookingStatus.Expired;
            await db.SaveChangesAsync();
            return Conflict(new { error = "That booking has expired" });
        }

        var earlyAllowanceMins = await settings.GetIntAsync(SettingsKeys.EarlyCheckInMins, 15);
        if (now < booking.StartTime.AddMinutes(-earlyAllowanceMins))
            return Conflict(new
            {
                error = $"Too early — check-in opens {earlyAllowanceMins} minutes before the booked start",
                opensAt = booking.StartTime.AddMinutes(-earlyAllowanceMins)
            });

        var session = new ParkingSession
        {
            BookingId = booking.Id,
            UserId = booking.UserId,
            SlotId = booking.SlotId,
            CheckInTime = now,
            Status = SessionStatus.Active
        };

        booking.Status = BookingStatus.Active;

        if (booking.Slot != null)
        {
            booking.Slot.Status = SlotStatus.Occupied;
            booking.Slot.UpdatedAt = now;
        }

        db.ParkingSessions.Add(session);
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Session {SessionId} opened on slot {SlotNumber} against booking {BookingId}",
            session.Id, booking.Slot?.SlotNumber, booking.Id);

        return CreatedAtAction(nameof(GetSession), new { id = session.Id },
            SessionResponse.From(session, booking));
    }

    /// <summary>
    /// Closes a session and settles the bill, returning an itemised receipt so
    /// the driver can see exactly how the figure was reached.
    /// </summary>
    [HttpPost("{id:guid}/check-out")]
    public async Task<IActionResult> CheckOut(Guid id)
    {
        var session = await db.ParkingSessions
            .Include(s => s.Booking)!.ThenInclude(b => b!.Slot)!.ThenInclude(s => s!.Zone)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (session?.Booking == null)
            return NotFound(new { error = $"Session '{id}' not found" });

        if (session.Status != SessionStatus.Active)
            return Conflict(new { error = $"Session is already {session.Status}" });

        var booking = session.Booking;
        var slot = booking.Slot;
        var now = DateTime.UtcNow;

        var charge = await fees.CalculateSessionChargeAsync(
            booking.StartTime,
            booking.EndTime,
            checkOutTime: now,
            baseHourlyRate: slot?.Zone?.BaseHourlyRate);

        session.CheckOutTime = now;
        session.OverstayMinutes = charge.OverstayMinutes;
        session.TotalFee = charge.TotalFee;
        session.PenaltyFee = charge.PenaltyFee;

        // An overstay is recorded on the session so the Enforcement slice can
        // pick it up for its agentic review, separately from the money owed.
        session.Status = charge.IsOverstay ? SessionStatus.OverstayDetected : SessionStatus.Completed;

        booking.Status = BookingStatus.Completed;

        if (slot != null)
        {
            slot.Status = SlotStatus.Available;
            slot.UpdatedAt = now;
        }

        await db.SaveChangesAsync();

        logger.LogInformation(
            "Session {SessionId} settled: parking {ParkingFee}, penalty {PenaltyFee}, total {TotalFee}",
            session.Id, charge.ParkingFee, charge.PenaltyFee, charge.TotalFee);

        return Ok(ReceiptResponse.From(session, booking, charge, slot));
    }
}
