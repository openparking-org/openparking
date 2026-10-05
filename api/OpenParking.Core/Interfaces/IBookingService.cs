using OpenParking.Core.Entities;
using OpenParking.Core.Models;

namespace OpenParking.Core.Interfaces;

/// <summary>
/// Booking &amp; Payment module — owned by Dev (Student 3).
/// Covers: Reservation logic, attendant session lifecycle,
/// Mapbox navigation data, and Resend email confirmations.
/// </summary>
public interface IBookingService : IParkingModule
{
    // ── Bookings ──────────────────────────────────────────────────────────
    Task<Booking> CreateBookingAsync(CreateBookingRequest req, Guid userId);
    Task<Booking> GetBookingAsync(Guid bookingId);
    Task<PagedResult<Booking>> GetUserBookingsAsync(Guid userId, PaginatedQuery query);
    Task<Booking> CancelBookingAsync(Guid bookingId, Guid userId);

    // Attendant session lifecycle
    /// <summary>Opens a ParkingSession for a reservation and marks its space occupied.</summary>
    Task<ParkingSession> CheckInAsync(CheckInRequest request, string ipAddress);

    /// <summary>Closes the session, calculates fee, marks slot Available, triggers email receipt.</summary>
    Task<SessionCheckOutResult> CheckOutAsync(CheckOutRequest request, string ipAddress);


    /// <summary>Returns the current active session (for Flutter live status screen).</summary>
    Task<ParkingSession?> GetActiveSessionAsync(Guid? userId, Guid? bookingId = null);
    
    /// <summary>Returns a session by ID.</summary>
    Task<ParkingSession> GetSessionAsync(Guid sessionId);

    // ── ANPR Simulator hook (design.md §8.4) ─────────────────────────────
    Task<ParkingSession> SimulateEntryAsync(string licensePlate, string zoneCode);
    Task<SessionCheckOutResult> SimulateExitAsync(string licensePlate);
}

// ── Request / Result DTOs ─────────────────────────────────────────────────

public class CreateBookingRequest
{
    public Guid SlotId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    [System.ComponentModel.DataAnnotations.Required]
    public string? VehiclePlate { get; set; }
}

public class CheckInRequest
{
    public Guid? BookingId { get; set; }
    public Guid? SlotId { get; set; }
    public Guid? UserId { get; set; }
}

public class CheckOutRequest
{
    public Guid? SessionId { get; set; }
    public Guid? BookingId { get; set; }
}

public class SessionCheckOutResult
{
    public Guid SessionId { get; set; }
    public decimal TotalFee { get; set; }
    public decimal PenaltyFee { get; set; }
    public int OverstayMinutes { get; set; }
    public string? ReceiptPdfUrl { get; set; }
    public DateTime CheckOutTime { get; set; }
    public ParkingSession? Session { get; set; }
}
