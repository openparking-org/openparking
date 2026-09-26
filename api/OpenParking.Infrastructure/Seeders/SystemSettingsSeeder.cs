using Microsoft.EntityFrameworkCore;
using OpenParking.Core.Entities;
using OpenParking.Infrastructure.Data;

namespace OpenParking.Infrastructure.Seeders;

public static class SystemSettingsSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        var defaultSettings = new List<SystemSetting>
        {
            // --- Existing settings (keys preserved to avoid breaking existing callers) ---
            new() { Key = "pricing.base_hourly_rate", Value = "5.00", Description = "Default hourly parking rate in USD", Category = "Pricing" },
            new() { Key = "pricing.peak_multiplier", Value = "1.50", Description = "Peak hours surge multiplier", Category = "Pricing" },
            new() { Key = "pricing.max_surge_multiplier", Value = "2.50", Description = "Maximum allowed dynamic surge multiplier", Category = "Pricing" },
            // grace_period_mins / penalty_per_hour / max_penalty_cap already use these keys in python config_tools.py
            new() { Key = "overstay.grace_period_mins", Value = "15", Description = "Minutes after booking end before overstay triggers", Category = "Overstay" },
            new() { Key = "overstay.penalty_per_hour", Value = "25.00", Description = "Hourly penalty fee for overstaying", Category = "Overstay" },
            new() { Key = "overstay.max_penalty_cap", Value = "150.00", Description = "Maximum total overstay penalty fee", Category = "Overstay" },
            // --- Issue #24: additional overstay settings required by design.md §24 ---
            new() { Key = "overstay.penalty_fixed_amount", Value = "50.00", Description = "Fixed base fine applied the moment an overstay is detected", Category = "Overstay" },
            new() { Key = "overstay.warning_lead_minutes", Value = "10", Description = "Minutes before booking end to send expiry push notification", Category = "Overstay" },
            new() { Key = "overstay.escalation_threshold_hours", Value = "2", Description = "Hours of overstay after which the penalty doubles", Category = "Overstay" },
            // --- Compatibility aliases for documented design.md keys ---
            new() { Key = "overstay.grace_period_minutes", Value = "15", Description = "Minutes after booking end before overstay triggers (alias)", Category = "Overstay" },
            new() { Key = "overstay.penalty_per_extra_hour", Value = "25.00", Description = "Hourly penalty fee for overstaying (alias)", Category = "Overstay" },
            new() { Key = "permits.auto_approve_confidence", Value = "0.90", Description = "AI confidence threshold for auto-approving permits", Category = "Permits" }

        };

        foreach (var setting in defaultSettings)
        {
            if (!await db.SystemSettings.AnyAsync(s => s.Key == setting.Key))
            {
                db.SystemSettings.Add(setting);
            }
        }

        await db.SaveChangesAsync();
    }
}
