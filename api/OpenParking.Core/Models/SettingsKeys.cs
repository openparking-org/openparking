namespace OpenParking.Core.Models;

/// <summary>
/// Canonical keys for the runtime-configurable policies held in SystemSettings.
/// Business rules are read through these at request time rather than compiled in,
/// so an admin can retune pricing and enforcement without a redeploy.
/// </summary>
public static class SettingsKeys
{
    public const string BaseHourlyRate = "pricing.base_hourly_rate";
    public const string PeakMultiplier = "pricing.peak_multiplier";
    public const string MaxSurgeMultiplier = "pricing.max_surge_multiplier";

    /// <summary>How early a driver may check in ahead of their start time.</summary>
    public const string EarlyCheckInMins = "booking.early_checkin_mins";

    public const string OverstayGracePeriodMins = "overstay.grace_period_mins";
    public const string OverstayPenaltyPerHour = "overstay.penalty_per_hour";
    public const string OverstayMaxPenaltyCap = "overstay.max_penalty_cap";
}
