using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OpenParking.Infrastructure.Data;
using OpenParking.Infrastructure.Services;
using Xunit;

namespace OpenParking.Tests;

public class SettingsServiceTests
{
    private static (AppDbContext db, IMemoryCache cache, SettingsService service) CreateService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options);
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var service = new SettingsService(db, memoryCache);
        return (db, memoryCache, service);
    }

    [Fact]
    public async Task GetStringAsync_ReturnsDefaultValue_WhenNotSet()
    {
        var (_, _, service) = CreateService();

        var result = await service.GetStringAsync("pricing.default_currency", "USD");

        Assert.Equal("USD", result);
    }

    [Fact]
    public async Task SetAsync_And_GetStringAsync_PersistsAndRetrieves()
    {
        var (db, _, service) = CreateService();

        await service.SetAsync("pricing.default_currency", "EUR", "Admin");

        var result = await service.GetStringAsync("pricing.default_currency", "USD");
        Assert.Equal("EUR", result);

        var inDb = await db.SystemSettings.FirstOrDefaultAsync(s => s.Key == "pricing.default_currency");
        Assert.NotNull(inDb);
        Assert.Equal("EUR", inDb.Value);
        Assert.Equal("Admin", inDb.UpdatedBy);
    }

    [Fact]
    public async Task GetDecimalAsync_ParsesCorrectly()
    {
        var (_, _, service) = CreateService();

        await service.SetAsync("pricing.base_rate", "250.75", "Admin");

        var rate = await service.GetDecimalAsync("pricing.base_rate", 100m);
        Assert.Equal(250.75m, rate);
    }

    [Fact]
    public async Task GetIntAsync_And_GetBoolAsync_ParseCorrectly()
    {
        var (_, _, service) = CreateService();

        await service.SetAsync("system.max_retries", "5");
        await service.SetAsync("system.feature_flag", "true");

        var maxRetries = await service.GetIntAsync("system.max_retries", 1);
        var flag = await service.GetBoolAsync("system.feature_flag", false);

        Assert.Equal(5, maxRetries);
        Assert.True(flag);
    }
}
