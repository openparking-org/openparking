namespace OpenParking.Core.Interfaces;

public interface ISettingsService
{
    Task<string> GetStringAsync(string key, string defaultValue = "");
    Task<decimal> GetDecimalAsync(string key, decimal defaultValue = 0m);
    Task<int> GetIntAsync(string key, int defaultValue = 0);
    Task<bool> GetBoolAsync(string key, bool defaultValue = false);
    Task SetAsync(string key, string value, string updatedBy = "System");
    Task InvalidateCacheAsync(string key);
    Task<Dictionary<string, List<OpenParking.Core.Entities.SystemSetting>>> GetAllGroupedAsync();
}
