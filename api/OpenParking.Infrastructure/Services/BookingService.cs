using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenParking.Core.Entities;
using OpenParking.Core.Interfaces;
using OpenParking.Core.Models;
using OpenParking.Infrastructure.Data;
using OpenParking.Infrastructure.Extensions;

namespace OpenParking.Infrastructure.Services;

/// <summary>
/// Booking &amp; Payment module service — Student 3 (Dev).
/// Handles reservation logic, QR generation, session lifecycle, and ANPR simulator.
/// Design reference: design.md §3, §19, §20
/// </summary>
public class BookingService(
    AppDbContext db,
    IEmailService emailService,
    ILogger<BookingService> logger) : IBookingService
{
    // ── IParkingModule ─────────────────────────────────────────────────────
    public string ModuleName => "Booking & Payment";

    public async Task<HealthStatus> HealthCheckAsync()
    {
        try
        {
            await db.Bookings.CountAsync();
            return HealthStatus.Healthy;
        }
        catch
        {
            return HealthStatus.Unhealthy;
        }
    }

    public Task<ModuleMetrics> GetMetricsAsync() =>
        Task.FromResult(new ModuleMetrics { ModuleName = ModuleName });

    // ── Constants ──────────────────────────────────────────────────────────

    /// <summary>QR content prefix. Flutter scanner validates this prefix before calling check-in.</summary>
    private const string QrPrefix = "openparking://session/start?bookingId=";

    /// <summary>Bill in 15-minute blocks, minimum 1 block (design.md §19.2).</summary>
    private const decimal BillBlockMinutes = 15m;

    private const decimal DefaultHourlyRate  = 5.00m;
    private const decimal DisabilityDiscount = 0.15m;

    // ── Bookings ───────────────────────────────────────────────────────────

    public async Task<Booking> CreateBookingAsync(CreateBookingRequest req, Guid userId)
    {
        // 1. Load slot (with zone for rate)
        var slot = await db.Slots
            .Include(s => s.Zone)
            .FirstOrDefaultAsync(s => s.Id == req.SlotId)
            ?? throw new AppException(ErrorCodes.NotFound, "Slot not found.", 404);

        // 2. Availability check
        if (slot.Status != SlotStatus.Available)
            throw new AppException(ErrorCodes.SlotUnavailable,
                $"Slot '{slot.SlotNumber}' is not available (current status: {slot.Status}).", 409);

        // 3. Time range validation
        if (req.StartTime >= req.EndTime)
            throw new AppException(ErrorCodes.InvalidBookingTime,
                "Booking end time must be after start time.");

        if (req.StartTime < DateTime.UtcNow.AddMinutes(-1))
            throw new AppException(ErrorCodes.InvalidBookingTime,
                "Booking start time cannot be in the past.");

        // 4. Overlap check — another booking on the same slot in the same window
        var hasOverlap = await db.Bookings.AnyAsync(b =>
            b.SlotId == req.SlotId &&
            b.Status != BookingStatus.Cancelled &&
            b.StartTime < req.EndTime &&
            b.EndTime > req.StartTime);

        if (hasOverlap)
            throw new AppException(ErrorCodes.BookingOverlap,
                "This slot already has a booking in the requested time window.", 409);

        // 5. Load user (for disability discount)
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        var hourlyRate   = slot.Zone?.BaseHourlyRate ?? DefaultHourlyRate;
        var durationHrs  = (decimal)(req.EndTime - req.StartTime).TotalHours;
        var estimatedFee = Math.Round(hourlyRate * durationHrs, 2);

        if (user?.HasDisabilityPermit == true)
            estimatedFee = Math.Round(estimatedFee * (1m - DisabilityDiscount), 2);

        // 6. Transactional booking + slot reservation
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var bookingId = Guid.NewGuid();
            var booking   = new Booking
            {
                Id            = bookingId,
                UserId        = userId,
                SlotId        = req.SlotId,
                StartTime     = req.StartTime,
                EndTime       = req.EndTime,
                VehiclePlate  = req.VehiclePlate?.Trim().ToUpperInvariant(),
                Status        = BookingStatus.Pending,
                QrCodeContent = $"{QrPrefix}{bookingId}&slotId={req.SlotId}",
                EstimatedFee  = estimatedFee,
                CreatedAt     = DateTime.UtcNow,
                UpdatedAt     = DateTime.UtcNow,
                CreatedBy     = userId.ToString()
            };

            slot.Status    = SlotStatus.Reserved;
            slot.UpdatedAt = DateTime.UtcNow;

            db.Bookings.Add(booking);
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            logger.LogInformation("Booking created: {BookingId} slot={SlotId} user={UserId}",
                bookingId, req.SlotId, userId);

            // Send confirmation email (non-fatal)
            if (user is not null)
                await emailService.SendBookingConfirmationAsync(user.Email, user.FullName, booking);

            return booking;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<Booking> GetBookingAsync(Guid bookingId)
    {
        return await db.Bookings
            .Include(b => b.Slot).ThenInclude(s => s!.Zone)
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == bookingId)
            ?? throw new AppException(ErrorCodes.NotFound, "Booking not found.", 404);
    }

    public async Task<PagedResult<Booking>> GetUserBookingsAsync(Guid userId, PaginatedQuery query)
    {
        var q = db.Bookings
            .Include(b => b.Slot).ThenInclude(s => s!.Zone)
            .Where(b => b.UserId == userId)
            .AsNoTracking();

        q = query.SortDir == "asc"
            ? q.OrderBy(b => b.StartTime)
            : q.OrderByDescending(b => b.StartTime);

        return await q.ToPagedResultAsync(query);
    }

    public async Task<Booking> CancelBookingAsync(Guid bookingId, Guid userId)
    {
        var booking = await db.Bookings
            .Include(b => b.Slot)
            .FirstOrDefaultAsync(b => b.Id == bookingId)
            ?? throw new AppException(ErrorCodes.NotFound, "Booking not found.", 404);

        // Only the owner can cancel (or admin — checked at controller level with role)
        if (booking.UserId != userId)
            throw new AppException(ErrorCodes.Forbidden, "You can only cancel your own bookings.", 403);

        if (booking.Status == BookingStatus.Active || booking.Status == BookingStatus.Completed)
            throw new AppException(ErrorCodes.CannotCancelBooking,
                $"A booking with status '{booking.Status}' cannot be cancelled.", 409);

        booking.Status    = BookingStatus.Cancelled;
        booking.UpdatedAt = DateTime.UtcNow;

        // Release slot back to available
        if (booking.Slot is not null)
        {
            booking.Slot.Status    = SlotStatus.Available;
            booking.Slot.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Booking cancelled: {BookingId} by {UserId}", bookingId, userId);
        return booking;
    }

    // ── QR Session Lifecycle ───────────────────────────────────────────────

    public async Task<ParkingSession> CheckInAsync(string qrContent, string ipAddress)
    {
        // Parse bookingId from QR: "openparking://session/start?bookingId=<GUID>&slotId=<GUID>"
        if (!TryParseQr(qrContent, out var bookingId))
            throw new AppException(ErrorCodes.ValidationFailed, "Invalid QR code format.");

        var booking = await db.Bookings
            .Include(b => b.Slot)
            .FirstOrDefaultAsync(b => b.Id == bookingId)
            ?? throw new AppException(ErrorCodes.NotFound, "Booking not found for this QR code.", 404);

        if (booking.Status == BookingStatus.Completed || booking.Status == BookingStatus.Cancelled)
            throw new AppException(ErrorCodes.ValidationFailed,
                $"Booking is already '{booking.Status}'.");

        var alreadyActive = await db.ParkingSessions
            .AnyAsync(s => s.BookingId == bookingId && s.Status == SessionStatus.Active);

        if (alreadyActive)
            throw new AppException(ErrorCodes.SessionActive,
                "A session for this booking is already active.", 409);

        var session = new ParkingSession
        {
            Id          = Guid.NewGuid(),
            BookingId   = booking.Id,
            UserId      = booking.UserId,
            SlotId      = booking.SlotId,
            CheckInTime = DateTime.UtcNow,
            Status      = SessionStatus.Active,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow
        };

        booking.Status = BookingStatus.Active;

        if (booking.Slot is not null)
        {
            booking.Slot.Status    = SlotStatus.Occupied;
            booking.Slot.UpdatedAt = DateTime.UtcNow;
        }

        db.ParkingSessions.Add(session);
        await db.SaveChangesAsync();

        logger.LogInformation("Check-in: session={SessionId} booking={BookingId} ip={Ip}",
            session.Id, bookingId, ipAddress);
        return session;
    }

    public async Task<SessionCheckOutResult> CheckOutAsync(Guid sessionId, string ipAddress)
    {
        var session = await db.ParkingSessions
            .Include(s => s.Booking)
            .FirstOrDefaultAsync(s => s.Id == sessionId)
            ?? throw new AppException(ErrorCodes.NotFound, "Session not found.", 404);

        if (session.Status != SessionStatus.Active && session.Status != SessionStatus.OverstayDetected)
            throw new AppException(ErrorCodes.ValidationFailed,
                $"Session cannot be checked out — status is '{session.Status}'.");

        var slot = await db.Slots.Include(s => s.Zone)
            .FirstOrDefaultAsync(s => s.Id == session.SlotId);

        var now        = DateTime.UtcNow;
        var hourlyRate = slot?.Zone?.BaseHourlyRate ?? DefaultHourlyRate;

        // Fee calculation — 15-min billing blocks (design.md §19.2)
        var rawMinutes     = (now - session.CheckInTime).TotalMinutes;
        var billableBlocks = (decimal)Math.Ceiling(Math.Max(rawMinutes, (double)BillBlockMinutes) / (double)BillBlockMinutes);
        var subtotal       = Math.Round(billableBlocks * BillBlockMinutes / 60m * hourlyRate, 2);

        // Overstay penalty
        decimal penaltyFee    = 0m;
        int     overstayMins  = 0;

        if (session.Booking is not null && now > session.Booking.EndTime)
        {
            overstayMins = (int)(now - session.Booking.EndTime).TotalMinutes;
            var penaltyRate = await GetPenaltyRateAsync();
            var extraHours  = (decimal)Math.Ceiling(overstayMins / 60.0);
            penaltyFee      = Math.Round(extraHours * penaltyRate, 2);
        }

        session.CheckOutTime    = now;
        session.UpdatedAt       = now;
        session.Status          = SessionStatus.Completed;
        session.TotalFee        = subtotal + penaltyFee;
        session.PenaltyFee      = penaltyFee;
        session.OverstayMinutes = overstayMins;
        session.ReceiptPdfUrl   = $"/receipts/{session.Id}.pdf";

        if (session.Booking is not null)
            session.Booking.Status = BookingStatus.Completed;

        if (slot is not null)
        {
            slot.Status    = SlotStatus.Available;
            slot.UpdatedAt = now;
        }

        await db.SaveChangesAsync();

        logger.LogInformation("Check-out: session={SessionId} fee={Fee} penalty={Penalty} ip={Ip}",
            sessionId, session.TotalFee, penaltyFee, ipAddress);

        return new SessionCheckOutResult
        {
            SessionId       = sessionId,
            TotalFee        = session.TotalFee,
            PenaltyFee      = penaltyFee,
            OverstayMinutes = overstayMins,
            ReceiptPdfUrl   = session.ReceiptPdfUrl,
            CheckOutTime    = now
        };
    }

    public async Task<ParkingSession?> GetActiveSessionAsync(Guid userId)
    {
        return await db.ParkingSessions
            .Include(s => s.Booking)
            .AsNoTracking()
            .Where(s => s.UserId == userId &&
                        (s.Status == SessionStatus.Active || s.Status == SessionStatus.OverstayDetected))
            .OrderByDescending(s => s.CheckInTime)
            .FirstOrDefaultAsync();
    }

    // ── ANPR Simulator (design.md §8.4) ───────────────────────────────────

    public async Task<ParkingSession> SimulateEntryAsync(string licensePlate, string zoneCode)
    {
        var zone = await db.Zones.FirstOrDefaultAsync(z => z.Code == zoneCode.ToUpperInvariant())
            ?? throw new AppException(ErrorCodes.NotFound, $"Zone '{zoneCode}' not found.", 404);

        var slot = await db.Slots
            .Where(s => s.ZoneId == zone.Id && s.Status == SlotStatus.Available)
            .OrderBy(s => s.SlotNumber)
            .FirstOrDefaultAsync()
            ?? throw new AppException(ErrorCodes.SlotUnavailable,
                $"No available slots in zone '{zoneCode}'.", 409);

        // Create an ad-hoc booking for ANPR entry
        var bookingId = Guid.NewGuid();
        var booking = new Booking
        {
            Id           = bookingId,
            UserId       = Guid.Empty, // anonymous for ANPR
            SlotId       = slot.Id,
            StartTime    = DateTime.UtcNow,
            EndTime      = DateTime.UtcNow.AddHours(2),
            VehiclePlate = licensePlate.ToUpperInvariant(),
            Status       = BookingStatus.Active,
            QrCodeContent = $"{QrPrefix}{bookingId}",
            EstimatedFee = zone.BaseHourlyRate * 2,
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow,
            CreatedBy    = "ANPR_SIMULATOR"
        };

        var session = new ParkingSession
        {
            Id          = Guid.NewGuid(),
            BookingId   = bookingId,
            UserId      = Guid.Empty,
            SlotId      = slot.Id,
            CheckInTime = DateTime.UtcNow,
            Status      = SessionStatus.Active,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow
        };

        slot.Status = SlotStatus.Occupied;
        slot.UpdatedAt = DateTime.UtcNow;

        db.Bookings.Add(booking);
        db.ParkingSessions.Add(session);
        await db.SaveChangesAsync();

        logger.LogInformation("ANPR entry: plate={Plate} zone={Zone} slot={SlotNum}",
            licensePlate, zoneCode, slot.SlotNumber);
        return session;
    }

    public async Task<SessionCheckOutResult> SimulateExitAsync(string licensePlate)
    {
        var session = await db.ParkingSessions
            .Include(s => s.Booking)
            .Where(s => s.Booking!.VehiclePlate == licensePlate.ToUpperInvariant() &&
                        s.Status == SessionStatus.Active)
            .OrderByDescending(s => s.CheckInTime)
            .FirstOrDefaultAsync()
            ?? throw new AppException(ErrorCodes.NotFound,
                $"No active session for plate '{licensePlate}'.", 404);

        return await CheckOutAsync(session.Id, "ANPR_SIMULATOR");
    }

    // ── Private Helpers ────────────────────────────────────────────────────

    private static bool TryParseQr(string qrContent, out Guid bookingId)
    {
        bookingId = Guid.Empty;
        if (!qrContent.StartsWith(QrPrefix)) return false;

        var afterPrefix = qrContent[QrPrefix.Length..];
        var guidPart    = afterPrefix.Split('&')[0];
        return Guid.TryParse(guidPart, out bookingId);
    }

    private async Task<decimal> GetPenaltyRateAsync()
    {
        var setting = await db.SystemSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == "overstay.penalty_per_hour" ||
                                      s.Key == "overstay_penalty_per_hour");

        if (setting is not null && decimal.TryParse(setting.Value, out var rate))
            return rate;

        return 25.00m; // design.md §19.2 default
    }
}
