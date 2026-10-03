namespace OpenParking.Core.Models;

/// <summary>
/// Booking &amp; Payment pricing rules (Student 3). Pure functions: no database,
/// no clock, no configuration. Policy and times are passed in, so the same
/// inputs always give the same figure and every rule is directly testable.
///
/// The quote and the final bill use the same block rule, so a driver who
/// leaves exactly when booked is charged what they were quoted.
/// </summary>
public static class FeeRules
{
    private const decimal MinimumSurge = 1.0m;

    /// <summary>
    /// Prices a reservation window. Surge is clamped to [1.0, MaxSurgeMultiplier]
    /// here, at the point money is computed, rather than trusting whatever the AI
    /// pricing agent returned.
    /// </summary>
    public static FeeQuote Quote(
        FeePolicy policy,
        DateTime start,
        DateTime end,
        decimal? zoneHourlyRate,
        decimal requestedSurge,
        bool hasDisabilityPermit)
    {
        if (end <= start)
            throw new ArgumentException("End time must be after start time.", nameof(end));

        var rate = zoneHourlyRate ?? policy.BaseHourlyRate;
        var surge = ClampSurge(requestedSurge, policy.MaxSurgeMultiplier);
        var discount = hasDisabilityPermit ? policy.DisabilityDiscount : 0m;
        var blocks = BillableBlocks(end - start);

        if (!policy.PricingEnabled)
            return new FeeQuote(blocks, rate, requestedSurge, surge, discount, 0m);

        var amount = Money(BlocksToHours(blocks) * rate * surge * (1m - discount));
        return new FeeQuote(blocks, rate, requestedSurge, surge, discount, amount);
    }

    /// <summary>
    /// Settles a session: time parked from check-in to check-out in 15-minute
    /// blocks, plus an overstay penalty for time past the booked end that
    /// survives the grace period, capped by policy. The disability discount
    /// applies to parking but not to the penalty, which is a fine.
    /// </summary>
    public static SessionCharge Settle(
        FeePolicy policy,
        DateTime checkIn,
        DateTime checkOut,
        DateTime? bookedEnd,
        decimal? zoneHourlyRate,
        bool hasDisabilityPermit)
    {
        var parked = checkOut > checkIn ? checkOut - checkIn : TimeSpan.Zero;
        var blocks = BillableBlocks(parked);

        var overstayMinutes = bookedEnd is { } end && checkOut > end
            ? (int)CeilingMinutes(checkOut - end)
            : 0;

        var chargeableMinutes = Math.Max(0, overstayMinutes - policy.GracePeriodMinutes);
        var penaltyHours = (chargeableMinutes + 59) / 60;

        if (!policy.PricingEnabled)
            return new SessionCharge(blocks, 0m, overstayMinutes, chargeableMinutes, penaltyHours, 0m, 0m);

        var rate = zoneHourlyRate ?? policy.BaseHourlyRate;
        var discount = hasDisabilityPermit ? policy.DisabilityDiscount : 0m;
        var parkingFee = Money(BlocksToHours(blocks) * rate * (1m - discount));

        var uncappedPenalty = Money(penaltyHours * policy.PenaltyPerHour);
        var penalty = Math.Min(uncappedPenalty, policy.PenaltyCap);

        return new SessionCharge(
            blocks, parkingFee, overstayMinutes, chargeableMinutes, penaltyHours, uncappedPenalty, penalty);
    }

    public static decimal ClampSurge(decimal requested, decimal maxSurge) =>
        Math.Clamp(requested, MinimumSurge, Math.Max(MinimumSurge, maxSurge));

    /// <summary>Started 15-minute blocks, with a one-block minimum.</summary>
    public static int BillableBlocks(TimeSpan duration)
    {
        var minutes = CeilingMinutes(duration);
        var blocks = (minutes + FeePolicy.BillingBlockMinutes - 1) / FeePolicy.BillingBlockMinutes;
        return (int)Math.Max(1, blocks);
    }

    private static decimal BlocksToHours(int blocks) => blocks * FeePolicy.BillingBlockMinutes / 60m;

    /// <summary>
    /// Started minutes, at one-second resolution. Sub-second remainders are
    /// dropped first: .NET keeps 100ns ticks but Postgres stores microseconds, and
    /// two clock reads a few microseconds apart would otherwise turn an exact
    /// two-hour window into 121 minutes and a ninth billing block. One whole
    /// second over a boundary still starts the next minute. Integer arithmetic
    /// throughout, so there is no floating point rounding either.
    /// </summary>
    private static long CeilingMinutes(TimeSpan duration)
    {
        var seconds = duration.Ticks / TimeSpan.TicksPerSecond;
        return seconds <= 0 ? 0 : (seconds + 59) / 60;
    }

    private static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
