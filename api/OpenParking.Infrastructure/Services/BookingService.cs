using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenParking.Core.Entities;
using OpenParking.Core.Interfaces;
using OpenParking.Core.Models;
using OpenParking.Infrastructure.Data;
using OpenParking.Infrastructure.Extensions;

namespace OpenParking.Infrastructure.Services;

public class SurgePricingResult {
    public decimal Multiplier { get; set; }
    public decimal CalculatedRate { get; set; }
    public decimal BaseRate { get; set; }
    public string Rationale { get; set; } = string.Empty;
}

/// <summary>
/// Booking &amp; Payment module service — Student 3 (Dev).
/// Handles reservation logic, QR generation, session lifecycle, and ANPR simulator.
/// Design reference: design.md §3, §19, §20
/// </summary>
public class BookingService(
    AppDbContext db,
    IEmailService emailService,
    IRealtimeNotifier realtimeNotifier,
    ILogger<BookingService> logger,
    IConfiguration config,
    IHttpClientFactory httpClientFactory,
    IFeePolicyProvider feePolicies) : IBookingService
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

    // Rates, discount and penalty policy live in FeePolicy / FeeRules.

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

        var policy = await feePolicies.GetAsync();
        decimal estimatedFee = 0m;

        if (policy.PricingEnabled)
        {
            var hourlyRate   = slot.Zone?.BaseHourlyRate ?? policy.BaseHourlyRate;

            // Fetch dynamic surge multipliers from DB settings
            var criticalSetting = await db.SystemSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "pricing.surge_critical_multiplier");
            var highSetting = await db.SystemSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "pricing.surge_high_multiplier");
            var modSetting = await db.SystemSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "pricing.surge_moderate_multiplier");

            decimal criticalMult = criticalSetting != null && decimal.TryParse(criticalSetting.Value, out var c) ? c : 2.0m;
            decimal highMult = highSetting != null && decimal.TryParse(highSetting.Value, out var h) ? h : 1.5m;
            decimal modMult = modSetting != null && decimal.TryParse(modSetting.Value, out var m) ? m : 1.2m;

            // Calculate dynamic surge multiplier
            decimal multiplier = 1.0m;
            try 
            {
                var totalSlots = await db.Slots.CountAsync(s => s.ZoneId == slot.ZoneId);
                var occupiedSlots = await db.Slots.CountAsync(s => s.ZoneId == slot.ZoneId && s.Status != SlotStatus.Available);
                var congestionScore = totalSlots > 0 ? (double)occupiedSlots / totalSlots : 0;
                string congestionLevel = congestionScore >= 0.9 ? "CRITICAL" : (congestionScore >= 0.7 ? "HIGH" : "MODERATE");

                var aiUrl = config["AI_SERVICE_URL"] ?? "http://localhost:8000";
                var client = httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                
                var payload = new {
                    base_rate = hourlyRate,
                    congestion_level = congestionLevel,
                    velocity_score = 0.5,
                    critical_mult = criticalMult,
                    high_mult = highMult,
                    mod_mult = modMult
                };

                var response = await client.PostAsJsonAsync($"{aiUrl}/ai/pricing/surge", payload);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<SurgePricingResult>();
                    if (result != null) multiplier = result.Multiplier;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fetch dynamic pricing from AI service. Falling back to base rate.");
            }

            // Same 15-minute block rule as check-out, so leaving on time costs
            // what was quoted. The AI's multiplier is clamped to policy here.
            var quote = FeeRules.Quote(
                policy, req.StartTime, req.EndTime, hourlyRate, multiplier,
                hasDisabilityPermit: user?.HasDisabilityPermit == true);

            if (quote.SurgeWasCapped)
                logger.LogWarning(
                    "Surge {Requested}x from the pricing agent exceeds the {Max}x policy ceiling; applied {Applied}x",
                    quote.RequestedSurge, policy.MaxSurgeMultiplier, quote.AppliedSurge);

            estimatedFee = quote.Amount;
        }

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

            // Broadcast real-time update so other users see this slot as Reserved
            await realtimeNotifier.NotifySlotUpdatedAsync(
                slot.ZoneId.ToString(), slot.Id.ToString(), SlotStatus.Reserved.ToString());

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

    public async Task<ParkingSession> CheckInAsync(CheckInRequest request, string ipAddress)
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

                var userId = request.UserId ?? Guid.NewGuid();
                booking = new Booking
                {
                    Id = request.BookingId ?? Guid.NewGuid(),
                    UserId = userId,
                    SlotId = slot.Id,
                    StartTime = DateTime.UtcNow,
                    EndTime = DateTime.UtcNow.AddHours(2),
                    Status = BookingStatus.Active,
                    QrCodeContent = $"{QrPrefix}{request.BookingId}&slotId={slot.Id}",
                    EstimatedFee = slot.Zone?.BaseHourlyRate * 2 ?? 10m,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedBy = userId.ToString()
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
            PenaltyFee = 0m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        booking.Status = BookingStatus.Active;
        db.ParkingSessions.Add(session);

        var targetSlot = booking.Slot ?? await db.Slots.Include(s => s.Zone).FirstOrDefaultAsync(s => s.Id == session.SlotId);
        if (targetSlot != null)
        {
            targetSlot.Status = SlotStatus.Occupied;
            targetSlot.UpdatedAt = DateTime.UtcNow;

            await realtimeNotifier.NotifySlotUpdatedAsync(
                targetSlot.ZoneId.ToString(), 
                targetSlot.Id.ToString(), 
                SlotStatus.Occupied.ToString());
        }

        await db.SaveChangesAsync();

        session.Booking = booking;
        session.Slot = targetSlot;

        logger.LogInformation("Check-in: session={SessionId} booking={BookingId} ip={Ip}",
            session.Id, booking.Id, ipAddress);
            
        return session;
    }

    public async Task<SessionCheckOutResult> CheckOutAsync(CheckOutRequest request, string ipAddress)
    {
        ParkingSession? session = null;

        if (request.SessionId.HasValue && request.SessionId.Value != Guid.Empty)
        {
            session = await db.ParkingSessions
                .Include(s => s.Booking).ThenInclude(b => b!.User)
                .FirstOrDefaultAsync(s => s.Id == request.SessionId.Value);
        }
        else if (request.BookingId.HasValue && request.BookingId.Value != Guid.Empty)
        {
            session = await db.ParkingSessions
                .Include(s => s.Booking).ThenInclude(b => b!.User)
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

        var policy = await feePolicies.GetAsync();

        // Grace period and penalty cap now come from overstay.grace_period_mins
        // and overstay.max_penalty_cap; previously both were ignored, so one
        // minute late cost a full penalty hour and long overstays were uncapped.
        var charge = FeeRules.Settle(
            policy,
            checkIn: session.CheckInTime,
            checkOut: now,
            bookedEnd: session.Booking?.EndTime,
            zoneHourlyRate: slot?.Zone?.BaseHourlyRate,
            hasDisabilityPermit: session.Booking?.User?.HasDisabilityPermit == true);

        var penaltyFee = charge.PenaltyFee;
        var overstayMins = charge.OverstayMinutes;

        if (charge.PenaltyWasCapped)
            logger.LogInformation(
                "Overstay penalty for session {SessionId} capped at {Cap} (uncapped {Uncapped})",
                session.Id, policy.PenaltyCap, charge.UncappedPenalty);

        session.TotalFee = charge.TotalFee;
        session.PenaltyFee = penaltyFee;
        session.OverstayMinutes = overstayMins;
        session.ReceiptPdfUrl = $"/receipts/{session.Id}.pdf";

        if (session.Booking != null)
            session.Booking.Status = BookingStatus.Completed;

        if (slot != null)
        {
            slot.Status = SlotStatus.Available;
            slot.UpdatedAt = now;
            
            await realtimeNotifier.NotifySlotUpdatedAsync(
                slot.ZoneId.ToString(), 
                slot.Id.ToString(), 
                SlotStatus.Available.ToString());
        }

        await db.SaveChangesAsync();
        session.Slot = slot;

        // Send receipt email
        if (session.Booking?.User != null)
        {
            await emailService.SendReceiptAsync(session.Booking.User.Email, session.Booking.User.FullName, session);
        }

        logger.LogInformation("Check-out: session={SessionId} fee={Fee} penalty={Penalty} ip={Ip}",
            session.Id, session.TotalFee, penaltyFee, ipAddress);

        return new SessionCheckOutResult
        {
            SessionId = session.Id,
            TotalFee = session.TotalFee,
            PenaltyFee = penaltyFee,
            OverstayMinutes = overstayMins,
            ReceiptPdfUrl = session.ReceiptPdfUrl,
            CheckOutTime = now,
            Session = session
        };
    }

    public async Task<ParkingSession?> GetActiveSessionAsync(Guid? userId, Guid? bookingId = null)
    {
        var query = db.ParkingSessions
            .Include(s => s.Booking)
            .Include(s => s.Slot).ThenInclude(s => s!.Zone)
            .AsNoTracking()
            .Where(s => s.Status == SessionStatus.Active || s.Status == SessionStatus.OverstayDetected);

        if (bookingId.HasValue && bookingId.Value != Guid.Empty)
            query = query.Where(s => s.BookingId == bookingId.Value);
        else if (userId.HasValue)
            query = query.Where(s => s.UserId == userId.Value);

        return await query.OrderByDescending(s => s.CheckInTime).FirstOrDefaultAsync();
    }

    public async Task<ParkingSession> GetSessionAsync(Guid sessionId)
    {
        return await db.ParkingSessions
            .Include(s => s.Booking)
            .Include(s => s.Slot).ThenInclude(s => s!.Zone)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId)
            ?? throw new AppException(ErrorCodes.NotFound, "Session not found.", 404);
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

        return await CheckOutAsync(new CheckOutRequest { SessionId = session.Id }, "ANPR_SIMULATOR");
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
}
