using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OpenParking.Core.Entities;
using OpenParking.Core.Interfaces;
using OpenParking.Infrastructure.Data;

namespace OpenParking.Infrastructure.Services;

public class SettingsService(AppDbContext db, IMemoryCache cache) : ISettingsService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private static string CacheKey(string key) => $"setting:{key}";

    public async Task<string> GetStringAsync(string key, string defaultValue = "")
    {
        if (cache.TryGetValue(CacheKey(key), out string? cached) && cached != null)
            return cached;

        var setting = await db.SystemSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == key);
        var val = setting?.Value ?? defaultValue;

        cache.Set(CacheKey(key), val, CacheDuration);
        return val;
    }

    public async Task<decimal> GetDecimalAsync(string key, decimal defaultValue = 0m)
    {
        var val = await GetStringAsync(key, defaultValue.ToString(CultureInfo.InvariantCulture));
        return decimal.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : defaultValue;
    }

    public async Task<int> GetIntAsync(string key, int defaultValue = 0)
    {
        var val = await GetStringAsync(key, defaultValue.ToString());
        return int.TryParse(val, out var result) ? result : defaultValue;
    }

    public async Task<bool> GetBoolAsync(string key, bool defaultValue = false)
    {
        var val = await GetStringAsync(key, defaultValue.ToString());
        return bool.TryParse(val, out var result) ? result : defaultValue;
    }

    public async Task SetAsync(string key, string value, string updatedBy = "System")
    {
        var setting = await db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (setting == null)
        {
            setting = new SystemSetting
            {
                Key = key,
                Value = value,
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = updatedBy
            };
            db.SystemSettings.Add(setting);
        }
        else
        {
            setting.Value = value;
            setting.UpdatedAt = DateTime.UtcNow;
            setting.UpdatedBy = updatedBy;
        }

        var aliases = key switch
        {
            "overstay.grace_period_mins" or "overstay.grace_period_minutes" => new[] { "overstay.grace_period_mins", "overstay.grace_period_minutes" },
            "overstay.penalty_per_hour" or "overstay.penalty_per_extra_hour" => new[] { "overstay.penalty_per_hour", "overstay.penalty_per_extra_hour" },
            _ => new[] { key }
        };
        var aliasKeys = aliases.ToList();
        foreach (var alias in await db.SystemSettings.Where(s => aliasKeys.Contains(s.Key)).ToListAsync())
        {
            alias.Value = value; alias.UpdatedAt = DateTime.UtcNow; alias.UpdatedBy = updatedBy;
        }
        await db.SaveChangesAsync();
        foreach (var alias in aliases) await InvalidateCacheAsync(alias);
    }

    public Task InvalidateCacheAsync(string key)
    {
        cache.Remove(CacheKey(key));
        return Task.CompletedTask;
    }

    public async Task<Dictionary<string, List<SystemSetting>>> GetAllGroupedAsync()
    {
        var settings = await db.SystemSettings.AsNoTracking().ToListAsync();
        return settings.GroupBy(s => s.Category).ToDictionary(g => g.Key, g => g.ToList());
    }
}
