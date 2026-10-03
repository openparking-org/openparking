using OpenParking.Core.Interfaces;
using OpenParking.Core.Models;

namespace OpenParking.Infrastructure.Services;

/// <summary>
/// Booking &amp; Payment slice. Every rate, grace period and cap is read from
/// SystemSettings through ISettingsService on each call, so changing a policy in
/// the admin console takes effect on the next request with no redeploy.
/// </summary>
public class FeeCalculationService(ISettingsService settings) : IFeeCalculationService
{
    private const decimal MinimumSurgeMultiplier = 1.0m;

    public async Task<FeeQuote> EstimateAsync(
        DateTime startTime,
        DateTime endTime,
        decimal? baseHourlyRate = null,
        decimal surgeMultiplier = 1.0m)
    {
        if (endTime <= startTime)
            throw new ArgumentException("End time must be after start time.", nameof(endTime));

        var rate = baseHourlyRate ?? await settings.GetDecimalAsync(SettingsKeys.BaseHourlyRate, 5.00m);
        var maxSurge = await settings.GetDecimalAsync(SettingsKeys.MaxSurgeMultiplier, 2.50m);

        var appliedSurge = ClampSurge(surgeMultiplier, maxSurge);
        var effectiveRate = Round(rate * appliedSurge);
        var billableHours = BillableHours(endTime - startTime);

        return new FeeQuote
        {
            BillableHours = billableHours,
            BaseHourlyRate = rate,
            RequestedSurgeMultiplier = surgeMultiplier,
            AppliedSurgeMultiplier = appliedSurge,
            EffectiveHourlyRate = effectiveRate,
            Amount = Round(effectiveRate * billableHours)
        };
    }

    public async Task<SessionCharge> CalculateSessionChargeAsync(
        DateTime bookedStart,
        DateTime bookedEnd,
        DateTime checkOutTime,
        decimal? baseHourlyRate = null,
        decimal surgeMultiplier = 1.0m)
    {
        var quote = await EstimateAsync(bookedStart, bookedEnd, baseHourlyRate, surgeMultiplier);

        var graceMinutes = await settings.GetIntAsync(SettingsKeys.OverstayGracePeriodMins, 15);
        var penaltyPerHour = await settings.GetDecimalAsync(SettingsKeys.OverstayPenaltyPerHour, 25.00m);
        var penaltyCap = await settings.GetDecimalAsync(SettingsKeys.OverstayMaxPenaltyCap, 150.00m);

        // Time past the booked end time, rounded up to the minute. A driver who
        // leaves early owes nothing extra, so this floors at zero.
        var overstayMinutes = checkOutTime > bookedEnd
            ? (int)CeilingMinutes(checkOutTime - bookedEnd)
            : 0;

        // The grace period is forgiven outright rather than merely delaying the
        // charge, so a driver 5 minutes late under a 15 minute grace pays nothing.
        var chargeableMinutes = Math.Max(0, overstayMinutes - graceMinutes);
        var penaltyHours = chargeableMinutes == 0 ? 0 : (int)CeilingHours(chargeableMinutes);

        var uncappedPenalty = Round(penaltyPerHour * penaltyHours);
        var penalty = Math.Min(uncappedPenalty, penaltyCap);

        return new SessionCharge
        {
            BillableHours = quote.BillableHours,
            ParkingFee = quote.Amount,
            OverstayMinutes = overstayMinutes,
            ChargeableOverstayMinutes = chargeableMinutes,
            PenaltyHours = penaltyHours,
            UncappedPenaltyFee = uncappedPenalty,
            PenaltyFee = penalty,
            TotalFee = Round(quote.Amount + penalty)
        };
    }

    /// <summary>
    /// Surge never discounts below the base rate and never exceeds the
    /// administrator's ceiling, which is the guard rail on the AI Action Agent's
    /// pricing proposals.
    /// </summary>
    private static decimal ClampSurge(decimal requested, decimal maxSurge)
    {
        var ceiling = Math.Max(MinimumSurgeMultiplier, maxSurge);
        return Math.Clamp(requested, MinimumSurgeMultiplier, ceiling);
    }

    /// <summary>Parking is billed per started hour, with a one hour minimum.</summary>
    private static int BillableHours(TimeSpan duration) =>
        Math.Max(1, (int)CeilingHours(CeilingMinutes(duration)));

    // Integer ceilings computed from ticks so that an exact two hour booking
    // bills as two hours rather than three on a floating point rounding error.
    private static long CeilingMinutes(TimeSpan duration) =>
        (duration.Ticks + TimeSpan.TicksPerMinute - 1) / TimeSpan.TicksPerMinute;

    private static long CeilingHours(long minutes) => (minutes + 59) / 60;

    private static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
