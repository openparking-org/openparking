using OpenParking.Core.Interfaces;
using OpenParking.Core.Models;

namespace OpenParking.Infrastructure.Services;

/// <summary>
/// Reads fee policy through ISettingsService, so it shares that service's cache
/// and an admin's change in the settings console applies to the next booking or
/// check-out without a redeploy. Defaults mirror SystemSettingsSeeder and only
/// apply when a key has been deleted.
/// </summary>
public class FeePolicyProvider(ISettingsService settings) : IFeePolicyProvider
{
    public async Task<FeePolicy> GetAsync()
    {
        var defaults = new FeePolicy();

        return new FeePolicy
        {
            PricingEnabled     = await settings.GetBoolAsync("pricing.is_enabled", defaults.PricingEnabled),
            BaseHourlyRate     = await settings.GetDecimalAsync("pricing.base_hourly_rate", defaults.BaseHourlyRate),
            MaxSurgeMultiplier = await settings.GetDecimalAsync("pricing.max_surge_multiplier", defaults.MaxSurgeMultiplier),
            GracePeriodMinutes = await settings.GetIntAsync("overstay.grace_period_mins", defaults.GracePeriodMinutes),
            PenaltyPerHour     = await settings.GetDecimalAsync("overstay.penalty_per_hour", defaults.PenaltyPerHour),
            PenaltyCap         = await settings.GetDecimalAsync("overstay.max_penalty_cap", defaults.PenaltyCap),
            DisabilityDiscount = await settings.GetDecimalAsync("pricing.disability_discount", defaults.DisabilityDiscount)
        };
    }
}
