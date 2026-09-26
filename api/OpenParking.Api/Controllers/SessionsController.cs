using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OpenParking.Api.Hubs;
using OpenParking.Core.Entities;
using OpenParking.Infrastructure.Data;

namespace OpenParking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SessionsController(AppDbContext db, IHubContext<SlotHub>? hubContext = null) : ControllerBase
{
    /// <summary>
    /// QR scan entry / check-in: validates booking, starts parking session, sets slot occupied, notifies SignalR.
    /// (design.md §5.1, §19.3)
    /// </summary>
    [HttpPost("check-in")]
    public async Task<IActionResult> CheckIn([FromBody] CheckInRequest request)
    {
        if (request == null)
        {
            return BadRequest(new { message = "Invalid check-in request payload." });
        }

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
            // Support booking code search or parsing
            booking = await db.Bookings
                .Include(b => b.Slot)
                .ThenInclude(s => s!.Zone)
                .FirstOrDefaultAsync(b => b.QrCodeContent.Contains(request.BookingCode) || b.Id.ToString() == request.BookingCode);
        }

        // Check if booking was found
        if (booking == null)
        {
            // If booking was not pre-created (e.g. ad-hoc / on-site entry), create an ad-hoc session if slot or zone is valid
            if (request.SlotId.HasValue && request.SlotId.Value != Guid.Empty)
            {
                var slot = await db.Slots.Include(s => s.Zone).FirstOrDefaultAsync(s => s.Id == request.SlotId.Value);
                if (slot == null)
                {
                    return NotFound(new { message = "Booking or slot not found for provided check-in QR." });
                }

                // Create fallback booking for the ad-hoc arrival
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
                return NotFound(new { message = "Booking not found for the scanned QR code." });
            }
        }

        // Prevent duplicate check-in if session already active
        var existingActiveSession = await db.ParkingSessions
            .FirstOrDefaultAsync(s => s.BookingId == booking.Id && s.Status == SessionStatus.Active);

        if (existingActiveSession != null)
        {
            return Conflict(new
            {
                message = "An active parking session is already running for this booking.",
                sessionId = existingActiveSession.Id,
                checkInTime = existingActiveSession.CheckInTime,
                slotId = existingActiveSession.SlotId
            });
        }

        if (booking.Status == BookingStatus.Completed || booking.Status == BookingStatus.Cancelled)
        {
            return BadRequest(new { message = $"Booking cannot be checked in because its status is {booking.Status}." });
        }

        // Create new active parking session
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

        // Mark slot occupied
        var targetSlot = booking.Slot ?? await db.Slots.Include(s => s.Zone).FirstOrDefaultAsync(s => s.Id == session.SlotId);
        if (targetSlot != null)
        {
            targetSlot.Status = SlotStatus.Occupied;
            targetSlot.UpdatedAt = DateTime.UtcNow;

            // Notify SignalR subscribers in this zone
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
                catch
                {
                    // SignalR broadcast failure is non-fatal
                }
            }
        }

        await db.SaveChangesAsync();

        return Ok(SessionDto.FromEntity(session, booking, targetSlot));
    }

    /// <summary>
    /// QR scan exit / check-out: closes parking session, calculates duration & fees, marks slot available.
    /// (design.md §5.1, §19.2, §19.3)
    /// </summary>
    [HttpPost("check-out")]
    public async Task<IActionResult> CheckOut([FromBody] CheckOutRequest request)
    {
        if (request == null)
        {
            return BadRequest(new { message = "Invalid check-out request payload." });
        }

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
        {
            return NotFound(new { message = "No active parking session found for the provided identifier." });
        }

        if (session.Status != SessionStatus.Active && session.Status != SessionStatus.OverstayDetected)
        {
            return BadRequest(new { message = $"Session cannot be checked out because it is already {session.Status}." });
        }

        // Close session
        var now = DateTime.UtcNow;
        session.CheckOutTime = now;
        session.UpdatedAt = now;
        session.Status = SessionStatus.Completed;

        // Slot and Zone lookup for hourly rate
        var slot = await db.Slots.Include(s => s.Zone).FirstOrDefaultAsync(s => s.Id == session.SlotId);
        var hourlyRate = slot?.Zone?.BaseHourlyRate ?? 5.00m;

        // Calculate billable time (in 15-minute increments, minimum 1 block = 15m) (design.md §19.2)
        var durationMinutes = Math.Max(1, (now - session.CheckInTime).TotalMinutes);
        var billableBlocks = (decimal)Math.Ceiling(durationMinutes / 15.0);
        var billableHours = (billableBlocks * 15.0m) / 60.0m;
        var subtotal = Math.Round(billableHours * hourlyRate, 2);

        // Check for overstay against booking end time
        if (session.Booking != null && now > session.Booking.EndTime)
        {
            var overstaySpan = now - session.Booking.EndTime;
            session.OverstayMinutes = (int)Math.Max(0, overstaySpan.TotalMinutes);

            // Fetch penalty rate setting or fallback $25.00/hr
            var penaltySetting = await db.SystemSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Key == "overstay.penalty_per_hour" || s.Key == "overstay.penalty_per_extra_hour");

            var penaltyPerHour = 25.00m;
            if (penaltySetting != null && decimal.TryParse(penaltySetting.Value, out var parsedRate))
            {
                penaltyPerHour = parsedRate;
            }

            var extraHours = (decimal)Math.Ceiling(session.OverstayMinutes / 60.0);
            session.PenaltyFee = Math.Round(extraHours * penaltyPerHour, 2);
        }

        session.TotalFee = subtotal + session.PenaltyFee;
        session.ReceiptPdfUrl = $"/receipts/{session.Id}.pdf";

        if (session.Booking != null)
        {
            session.Booking.Status = BookingStatus.Completed;
        }

        // Free up slot
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
                catch
                {
                    // Non-fatal
                }
            }
        }

        await db.SaveChangesAsync();

        return Ok(SessionDto.FromEntity(session, session.Booking, slot));
    }

    /// <summary>
    /// Gets the current active session for the authenticated user or requested booking/user ID.
    /// (design.md §19.3, §19.4)
    /// </summary>
    [HttpGet("active")]
    public async Task<IActionResult> GetActiveSession([FromQuery] Guid? userId = null, [FromQuery] Guid? bookingId = null)
    {
        var targetUserId = userId ?? GetCurrentUserId();

        var query = db.ParkingSessions
            .Include(s => s.Booking)
            .Where(s => s.Status == SessionStatus.Active || s.Status == SessionStatus.OverstayDetected)
            .AsNoTracking();

        if (bookingId.HasValue && bookingId.Value != Guid.Empty)
        {
            query = query.Where(s => s.BookingId == bookingId.Value);
        }
        else if (targetUserId.HasValue)
        {
            query = query.Where(s => s.UserId == targetUserId.Value);
        }

        var session = await query.OrderByDescending(s => s.CheckInTime).FirstOrDefaultAsync();
        if (session == null)
        {
            return NotFound(new { message = "No active parking session found." });
        }

        var slot = await db.Slots.Include(s => s.Zone).FirstOrDefaultAsync(s => s.Id == session.SlotId);
        return Ok(SessionDto.FromEntity(session, session.Booking, slot));
    }

    /// <summary>
    /// Gets a specific session by ID.
    /// (design.md §19.3)
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetSessionById(Guid id)
    {
        var session = await db.ParkingSessions
            .Include(s => s.Booking)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id);

        if (session == null)
        {
            return NotFound(new { message = "Session not found." });
        }

        var slot = await db.Slots.Include(s => s.Zone).FirstOrDefaultAsync(s => s.Id == session.SlotId);
        return Ok(SessionDto.FromEntity(session, session.Booking, slot));
    }

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (Guid.TryParse(sub, out var guid))
        {
            return guid;
        }
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
