using Microsoft.AspNetCore.Mvc;
using OpenParking.Core.Interfaces;
using OpenParking.Core.Models;

namespace OpenParking.Api.Controllers;

/// <summary>
/// /health — used by Docker health checks, Cloudflare Tunnel, and the admin dashboard.
/// Returns database connectivity status so infra issues are immediately visible.
/// </summary>
[ApiController]
[Route("[controller]")]
public class HealthController(IEnumerable<IParkingModule> modules, ILogger<HealthController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<HealthDto>>> Get()
    {
        var moduleHealths = new List<ModuleHealth>();
        bool overallOk = true;

        foreach (var module in modules)
        {
            var status = HealthStatus.Unhealthy;
            try
            {
                status = await module.HealthCheckAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Health check failed for module {Module}", module.ModuleName);
            }
            
            if (status != HealthStatus.Healthy) overallOk = false;
            moduleHealths.Add(new ModuleHealth(module.ModuleName, status.ToString()));
        }

        var result = new HealthDto
        {
            Status    = overallOk ? "Healthy" : "Degraded",
            Timestamp = DateTime.UtcNow,
            Version   = "1.0.0",
            Database  = overallOk ? "Connected" : "Unreachable",
            Modules   = moduleHealths
        };

        // Return 503 if degraded — allows load balancers to stop routing here
        var statusCode = overallOk ? 200 : 503;
        return StatusCode(statusCode, ApiResponse<HealthDto>.Ok(result, HttpContext.TraceIdentifier));
    }
}

public class HealthDto
{
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Version { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public List<ModuleHealth> Modules { get; set; } = [];
}

public record ModuleHealth(string Name, string Status);
