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
            new() { Key = "pricing.base_hourly_rate", Value = "5.00", Description = "Default hourly parking rate in USD", Category = "Pricing" },
            new() { Key = "pricing.peak_multiplier", Value = "1.50", Description = "Peak hours surge multiplier", Category = "Pricing" },
            new() { Key = "pricing.max_surge_multiplier", Value = "2.50", Description = "Maximum allowed dynamic surge multiplier", Category = "Pricing" },
            new() { Key = "overstay.grace_period_mins", Value = "15", Description = "Minutes after booking end before overstay triggers", Category = "Overstay" },
            new() { Key = "overstay.penalty_per_hour", Value = "25.00", Description = "Hourly penalty fee for overstaying", Category = "Overstay" },
            new() { Key = "overstay.max_penalty_cap", Value = "150.00", Description = "Maximum total overstay penalty fee", Category = "Overstay" },
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
