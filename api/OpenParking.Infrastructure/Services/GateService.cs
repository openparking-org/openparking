using System.Data;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OpenParking.Core.Entities;
using OpenParking.Core.Interfaces;
using OpenParking.Core.Models;
using OpenParking.Infrastructure.Data;

namespace OpenParking.Infrastructure.Services;

/// <summary>Attendant operations against real customer reservations.</summary>
public class GateService(AppDbContext db, IBookingService bookings)
{
    public static string NormalizePlate(string plate)
    {
        var normalized = Regex.Replace(plate.Trim().ToUpperInvariant(), @"[\s-]", "");
        if (!Regex.IsMatch(normalized, "^[A-Z0-9]{3,20}$"))
            throw new AppException(ErrorCodes.ValidationFailed, "Enter a valid vehicle number (3–20 letters or digits).");
        return normalized;
    }

    public async Task<ParkingSession> EnterAsync(string plate, Guid zoneId, Guid actorId, string ip)
    {
        plate = NormalizePlate(plate);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var active = await db.ParkingSessions.Include(s => s.Booking)
            .Where(s => s.Status == SessionStatus.Active || s.Status == SessionStatus.OverstayDetected)
            .ToListAsync();
        if (active.Any(s => SamePlate(s.Booking?.VehiclePlate, plate)))
            throw new AppException(ErrorCodes.SessionActive, "This vehicle is already checked in.", 409);

        var now = DateTime.UtcNow;
        var candidates = await db.Bookings.Include(b => b.Slot).ThenInclude(s => s!.Zone)
            .Where(b => b.Slot!.ZoneId == zoneId && b.StartTime <= now && b.EndTime > now &&
                (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed))
            .ToListAsync();
        var matches = candidates.Where(b => SamePlate(b.VehiclePlate, plate)).ToList();
        if (matches.Count == 0)
            throw new AppException(ErrorCodes.NotFound, "No reservation valid now for this vehicle in the selected zone. Check the vehicle number and booking time.", 404);
        if (matches.Count > 1)
            throw new AppException(ErrorCodes.ValidationFailed, "Multiple matching reservations. Resolve them in Reservations before checking in.", 409);
        var booking = matches[0];
        if (booking.Slot!.Status is SlotStatus.Occupied or SlotStatus.Maintenance)
            throw new AppException(ErrorCodes.SlotUnavailable, "The reserved space cannot accept this vehicle.", 409);
        if (active.Any(s => s.SlotId == booking.SlotId))
            throw new AppException(ErrorCodes.SlotUnavailable, "Another session is using this space.", 409);

        var session = await bookings.CheckInAsync(new CheckInRequest { BookingId = booking.Id }, ip);
        Audit(session, actorId, "GATE_CHECK_IN", plate);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return session;
    }

    public async Task<ParkingSession> ExitAsync(string plate, Guid zoneId, Guid actorId, string ip)
    {
        plate = NormalizePlate(plate);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var candidates = await db.ParkingSessions.Include(s => s.Booking).Include(s => s.Slot)
            .Where(s => s.Slot!.ZoneId == zoneId &&
                (s.Status == SessionStatus.Active || s.Status == SessionStatus.OverstayDetected))
            .ToListAsync();
        var matches = candidates.Where(s => SamePlate(s.Booking?.VehiclePlate, plate)).ToList();
        if (matches.Count == 0)
            throw new AppException(ErrorCodes.NotFound, "No active session for this vehicle in the selected zone.", 404);
        if (matches.Count > 1)
            throw new AppException(ErrorCodes.ValidationFailed, "Multiple active sessions. Resolve them in Reservations.", 409);
        var result = await bookings.CheckOutAsync(new CheckOutRequest { SessionId = matches[0].Id }, ip);
        Audit(result.Session!, actorId, "GATE_CHECK_OUT", plate);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return result.Session!;
    }

    private static bool SamePlate(string? stored, string normalized) => stored != null &&
        Regex.Replace(stored.Trim().ToUpperInvariant(), @"[\s-]", "") == normalized;

    private void Audit(ParkingSession session, Guid actor, string action, string plate) => db.AuditLogs.Add(new AuditLog
    {
        EntityType = "ParkingSession", EntityId = session.Id, ActorUserId = actor,
        Action = action, PayloadJson = System.Text.Json.JsonSerializer.Serialize(new { plate, session.BookingId }),
        ActorEmail = "Gate attendant", IpAddress = "gate"
    });
}
