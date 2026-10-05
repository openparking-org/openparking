using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenParking.Core.Interfaces;

namespace OpenParking.Infrastructure.Services;

/// <summary>
/// Runs in the background every N minutes (configurable via SystemSettings).
/// Calls IEnforcementService.DetectAndTriggerOverstaysAsync() which finds sessions
/// that have exceeded their booked EndTime and fires LangGraph OVERSTAY workflows.
///
/// Design reference: design.md §8.3 step 1 — "Session expires in PostgreSQL
/// (ASP.NET Core BackgroundService)".
/// </summary>
public class OverstayDetectionService(
    IServiceScopeFactory scopeFactory,
    ILogger<OverstayDetectionService> logger) : BackgroundService
{
    // Default: check every 2 minutes. Overridable via SystemSettings key "overstay_check_interval_minutes".
    private static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("OverstayDetectionService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var interval = DefaultInterval;

            try
            {
                using var scope = scopeFactory.CreateScope();

                // Read configurable interval from SystemSettings
                var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
                var intervalMinutes = await settings.GetIntAsync("overstay_check_interval_minutes", 2);
                interval = TimeSpan.FromMinutes(intervalMinutes);

                var enforcement = scope.ServiceProvider.GetRequiredService<IEnforcementService>();
                var triggered = await enforcement.DetectAndTriggerOverstaysAsync();

                if (triggered.Count > 0)
                    logger.LogInformation("Overstay check: triggered {Count} workflow(s). SessionIds: {Ids}",
                        triggered.Count, string.Join(", ", triggered));
            }
            catch (Exception ex)
            {
                // Log and continue — don't crash the background service on transient errors
                logger.LogError(ex, "OverstayDetectionService encountered an error during detection cycle.");
            }

            await Task.Delay(interval, stoppingToken);
        }

        logger.LogInformation("OverstayDetectionService stopped.");
    }
}
