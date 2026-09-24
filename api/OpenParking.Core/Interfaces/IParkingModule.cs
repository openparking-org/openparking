namespace OpenParking.Core.Interfaces;

public enum HealthStatus
{
    Healthy,
    Degraded,
    Unhealthy
}

public class ModuleMetrics
{
    public string ModuleName { get; set; } = string.Empty;
    public long ProcessedOperationsCount { get; set; }
    public double AverageResponseTimeMs { get; set; }
    public DateTime LastActivityUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Shared Module Interface (OpenMRS-Inspired)
/// Enforces bounded ownership across the 4 member vertical slices.
/// </summary>
public interface IParkingModule
{
    string ModuleName { get; }
    Task<HealthStatus> HealthCheckAsync();
    Task<ModuleMetrics> GetMetricsAsync();
}
