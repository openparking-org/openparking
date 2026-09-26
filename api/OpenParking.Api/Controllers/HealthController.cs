using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenParking.Core.Models;
using OpenParking.Infrastructure.Data;

namespace OpenParking.Api.Controllers;

/// <summary>
/// /health — used by Docker health checks, Cloudflare Tunnel, and the admin dashboard.
/// Returns database connectivity status so infra issues are immediately visible.
/// </summary>
[ApiController]
[Route("[controller]")]
public class HealthController(AppDbContext db, ILogger<HealthController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<HealthDto>>> Get()
    {
        bool dbOk;
        try
        {
            dbOk = await db.Database.CanConnectAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Health check: database connectivity test failed.");
            dbOk = false;
        }

        var result = new HealthDto
        {
            Status    = dbOk ? "Healthy" : "Degraded",
            Timestamp = DateTime.UtcNow,
            Version   = "1.0.0",
            Database  = dbOk ? "Connected" : "Unreachable",
            Modules   =
            [
                new("User & Access",              dbOk ? "Healthy" : "Degraded"),
                new("Space & Availability",        dbOk ? "Healthy" : "Degraded"),
                new("Booking & Payment",           dbOk ? "Healthy" : "Degraded"),
                new("Enforcement & AI Orchestration", dbOk ? "Healthy" : "Degraded")
            ]
        };

        // Return 503 if degraded — allows load balancers to stop routing here
        var statusCode = dbOk ? 200 : 503;
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
