using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenParking.Core.Interfaces;
using OpenParking.Core.Models;

namespace OpenParking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SimulateController(IBookingService bookingService, IConfiguration config) : ControllerBase
{
    private bool IsAuthorized()
    {
        var apiKey = config["SIMULATION_API_KEY"];
        if (string.IsNullOrEmpty(apiKey)) return false; // If not configured, reject all

        if (!Request.Headers.TryGetValue("X-Api-Key", out var extractedApiKey))
            return false;

        return apiKey == extractedApiKey.ToString();
    }

    [HttpPost("entry")]
    public async Task<IActionResult> SimulateEntry([FromBody] SimulateEntryRequest request)
    {
        if (!IsAuthorized()) return Unauthorized(new { Message = "Invalid or missing API Key." });

        if (string.IsNullOrWhiteSpace(request.LicensePlate) || string.IsNullOrWhiteSpace(request.ZoneCode))
            return BadRequest(new { Message = "LicensePlate and ZoneCode are required." });

        var session = await bookingService.SimulateEntryAsync(request.LicensePlate, request.ZoneCode);
        return Ok(new { Message = "Entry simulated successfully.", SessionId = session.Id });
    }

    [HttpPost("exit")]
    public async Task<IActionResult> SimulateExit([FromBody] SimulateExitRequest request)
    {
        if (!IsAuthorized()) return Unauthorized(new { Message = "Invalid or missing API Key." });

        if (string.IsNullOrWhiteSpace(request.LicensePlate))
            return BadRequest(new { Message = "LicensePlate is required." });

        var session = await bookingService.SimulateExitAsync(request.LicensePlate);
        return Ok(new { Message = "Exit simulated successfully.", SessionId = session.SessionId });
    }
}

public class SimulateEntryRequest
{
    public string LicensePlate { get; set; } = string.Empty;
    public string ZoneCode { get; set; } = string.Empty;
}

public class SimulateExitRequest
{
    public string LicensePlate { get; set; } = string.Empty;
}
