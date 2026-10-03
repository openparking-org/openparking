using System.ComponentModel.DataAnnotations;
using OpenParking.Core.Entities;
using OpenParking.Core.Models;

namespace OpenParking.Api.Dtos;

public class CreateBookingRequest
{
    // Deliberately no UserId: the owner is taken from the bearer token, so a
    // caller cannot create or price a reservation in someone else's name.

    [Required]
    public Guid SlotId { get; set; }

    [Required]
    public DateTime StartTime { get; set; }

    [Required]
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Surge proposed by the AI Action Agent. Always re-clamped server side
    /// against pricing.max_surge_multiplier, so a caller cannot price above the
    /// administrator's ceiling.
    /// </summary>
    public decimal SurgeMultiplier { get; set; } = 1.0m;
}

public class BookingResponse
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public Guid SlotId { get; init; }
    public string SlotNumber { get; init; } = string.Empty;
    public string ZoneName { get; init; } = string.Empty;
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public string Status { get; init; } = string.Empty;
    public string QrCodeContent { get; init; } = string.Empty;
    public decimal EstimatedFee { get; init; }
    public DateTime CreatedAt { get; init; }

    public static BookingResponse From(Booking booking, Slot? slot = null) => new()
    {
        Id = booking.Id,
        UserId = booking.UserId,
        SlotId = booking.SlotId,
        SlotNumber = (slot ?? booking.Slot)?.SlotNumber ?? string.Empty,
        ZoneName = (slot ?? booking.Slot)?.Zone?.Name ?? string.Empty,
        StartTime = booking.StartTime,
        EndTime = booking.EndTime,
        Status = booking.Status.ToString(),
        QrCodeContent = booking.QrCodeContent,
        EstimatedFee = booking.EstimatedFee,
        CreatedAt = booking.CreatedAt
    };
}

/// <summary>Quote returned alongside a booking so the price can be explained.</summary>
public class FeeBreakdownResponse
{
    public int BillableHours { get; init; }
    public decimal BaseHourlyRate { get; init; }
    public decimal AppliedSurgeMultiplier { get; init; }
    public decimal EffectiveHourlyRate { get; init; }
    public bool SurgeWasCapped { get; init; }
    public decimal Amount { get; init; }

    public static FeeBreakdownResponse From(FeeQuote quote) => new()
    {
        BillableHours = quote.BillableHours,
        BaseHourlyRate = quote.BaseHourlyRate,
        AppliedSurgeMultiplier = quote.AppliedSurgeMultiplier,
        EffectiveHourlyRate = quote.EffectiveHourlyRate,
        SurgeWasCapped = quote.SurgeWasCapped,
        Amount = quote.Amount
    };
}

public class CheckInRequest
{
    /// <summary>The code carried by the driver's digital pass, as scanned.</summary>
    [Required]
    public string QrCodeContent { get; set; } = string.Empty;
}

public class SessionResponse
{
    public Guid Id { get; init; }
    public Guid BookingId { get; init; }
    public Guid UserId { get; init; }
    public Guid SlotId { get; init; }
    public string SlotNumber { get; init; } = string.Empty;
    public DateTime BookedStart { get; init; }
    public DateTime BookedEnd { get; init; }
    public DateTime CheckInTime { get; init; }
    public DateTime? CheckOutTime { get; init; }
    public string Status { get; init; } = string.Empty;
    public int OverstayMinutes { get; init; }
    public decimal TotalFee { get; init; }
    public decimal PenaltyFee { get; init; }

    public static SessionResponse From(ParkingSession session, Booking booking, Slot? slot = null) => new()
    {
        Id = session.Id,
        BookingId = session.BookingId,
        UserId = session.UserId,
        SlotId = session.SlotId,
        SlotNumber = (slot ?? booking.Slot)?.SlotNumber ?? string.Empty,
        BookedStart = booking.StartTime,
        BookedEnd = booking.EndTime,
        CheckInTime = session.CheckInTime,
        CheckOutTime = session.CheckOutTime,
        Status = session.Status.ToString(),
        OverstayMinutes = session.OverstayMinutes,
        TotalFee = session.TotalFee,
        PenaltyFee = session.PenaltyFee
    };
}

/// <summary>Itemised receipt returned at check-out.</summary>
public class ReceiptResponse
{
    public Guid SessionId { get; init; }
    public Guid BookingId { get; init; }
    public string SlotNumber { get; init; } = string.Empty;
    public DateTime CheckInTime { get; init; }
    public DateTime CheckOutTime { get; init; }
    public int BillableHours { get; init; }
    public decimal ParkingFee { get; init; }
    public int OverstayMinutes { get; init; }
    public int ChargeableOverstayMinutes { get; init; }
    public decimal PenaltyFee { get; init; }
    public bool PenaltyWasCapped { get; init; }
    public decimal TotalFee { get; init; }

    public static ReceiptResponse From(ParkingSession session, Booking booking, SessionCharge charge, Slot? slot) => new()
    {
        SessionId = session.Id,
        BookingId = booking.Id,
        SlotNumber = slot?.SlotNumber ?? string.Empty,
        CheckInTime = session.CheckInTime,
        CheckOutTime = session.CheckOutTime ?? DateTime.UtcNow,
        BillableHours = charge.BillableHours,
        ParkingFee = charge.ParkingFee,
        OverstayMinutes = charge.OverstayMinutes,
        ChargeableOverstayMinutes = charge.ChargeableOverstayMinutes,
        PenaltyFee = charge.PenaltyFee,
        PenaltyWasCapped = charge.PenaltyWasCapped,
        TotalFee = charge.TotalFee
    };
}
