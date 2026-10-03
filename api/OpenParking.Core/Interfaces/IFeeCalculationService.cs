using OpenParking.Core.Models;

namespace OpenParking.Core.Interfaces;

/// <summary>
/// Booking &amp; Payment slice. Owns every monetary figure the system produces, so
/// that pricing lives in one auditable place rather than being recomputed by each
/// caller. All rates and limits are resolved from SystemSettings at call time.
/// </summary>
public interface IFeeCalculationService
{
    /// <summary>
    /// Prices a reservation window ahead of time. Parking is billed per started
    /// hour. When <paramref name="baseHourlyRate"/> is null the zone-independent
    /// default from pricing.base_hourly_rate is used.
    /// </summary>
    Task<FeeQuote> EstimateAsync(
        DateTime startTime,
        DateTime endTime,
        decimal? baseHourlyRate = null,
        decimal surgeMultiplier = 1.0m);

    /// <summary>
    /// Settles a finished session. The driver is billed for the window they
    /// reserved, because the slot was held for them whether or not they used all
    /// of it; time past the booked end time is charged separately as an overstay
    /// penalty once the grace period is deducted, capped by
    /// overstay.max_penalty_cap.
    /// </summary>
    Task<SessionCharge> CalculateSessionChargeAsync(
        DateTime bookedStart,
        DateTime bookedEnd,
        DateTime checkOutTime,
        decimal? baseHourlyRate = null,
        decimal surgeMultiplier = 1.0m);
}
