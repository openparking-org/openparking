using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenParking.Core.Entities;
using OpenParking.Core.Interfaces;
using OpenParking.Core.Models;
using OpenParking.Infrastructure.Data;

namespace OpenParking.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/parking/recommendations")]
public class ParkingRecommendationsController(AppDbContext db, ISettingsService settings,
    IHttpClientFactory clients, IConfiguration config) : ControllerBase
{
    public sealed class RecommendationRequest : IValidatableObject
    {
        [System.Text.Json.Serialization.JsonRequired, Range(-90, 90)] public double Latitude { get; set; }
        [System.Text.Json.Serialization.JsonRequired, Range(-180, 180)] public double Longitude { get; set; }
        [Range(1, 100)] public double RadiusKm { get; set; } = 25;
        public string Preference { get; set; } = "nearest";
        public string SlotType { get; set; } = "Standard";
        public IEnumerable<ValidationResult> Validate(ValidationContext context)
        {
            if (!double.IsFinite(Latitude) || !double.IsFinite(Longitude) || !double.IsFinite(RadiusKm))
                yield return new ValidationResult("Location and radius must be finite.");
            if (Preference is not ("nearest" or "cheapest" or "balanced"))
                yield return new ValidationResult("Choose nearest, cheapest, or balanced.");
            if (SlotType is not ("Standard" or "EV" or "Accessible"))
                yield return new ValidationResult("Choose Standard, EV, or Accessible parking.");
        }
    }

    [HttpPost]
    public async Task<IActionResult> Recommend(RecommendationRequest request, CancellationToken cancellationToken)
    {
        var type = Enum.Parse<SlotType>(request.SlotType);
        if (type == SlotType.Accessible)
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (!await db.DisabilityPermits.AnyAsync(p => p.UserId == userId &&
                p.Status == PermitStatus.Verified && p.ExpiryDate > DateTime.UtcNow, cancellationToken))
                return StatusCode(403, ApiResponse<object>.Fail("PERMIT_REQUIRED", "A verified, unexpired disability permit is required.", HttpContext.TraceIdentifier));
        }
        var currency = await settings.GetStringAsync("pricing.default_currency", "USD");
        var zones = await db.Zones.AsNoTracking().Select(z => new {
            z.Id, z.Name, z.Latitude, z.Longitude, z.BaseHourlyRate,
            AvailableCount = z.Slots.Count(s => s.Status == SlotStatus.Available && s.Type == type)
        }).Where(z => z.AvailableCount > 0).ToListAsync(cancellationToken);
        var eligible = zones.Where(z => double.IsFinite(z.Latitude) && double.IsFinite(z.Longitude) &&
            Math.Abs(z.Latitude) <= 90 && Math.Abs(z.Longitude) <= 180 &&
            !(z.Latitude == 0 && z.Longitude == 0)) // Legacy unset coordinate placeholder.
            .Select(z => new { zoneId = z.Id, name = z.Name, availableCount = z.AvailableCount,
                distanceMeters = DistanceMeters(request.Latitude, request.Longitude, z.Latitude, z.Longitude),
                hourlyRate = z.BaseHourlyRate, currency })
            .Where(z => z.distanceMeters <= request.RadiusKm * 1000).ToList();
        var maxRate = Math.Max(1, eligible.Select(z => (double)z.hourlyRate).DefaultIfEmpty(1).Max());
        var candidates = eligible.OrderBy(z => request.Preference switch {
                "cheapest" => (double)z.hourlyRate,
                "balanced" => .7 * z.distanceMeters / (request.RadiusKm * 1000) + .3 * (double)z.hourlyRate / maxRate,
                _ => z.distanceMeters })
            .ThenBy(z => z.distanceMeters).Take(50).ToList();
        var token = config["INTERNAL_API_TOKEN"];
        if (string.IsNullOrEmpty(token)) return Unavailable();
        using var client = clients.CreateClient("ParkingFinder");
        client.Timeout = TimeSpan.FromSeconds(10);
        client.DefaultRequestHeaders.Add("X-Internal-Token", token);
        try
        {
            var url = (config["AI_SERVICE_URL"] ?? "http://localhost:8000").TrimEnd('/');
            using var response = await client.PostAsJsonAsync(url + "/ai/parking/recommend", new {
                request.Preference, radiusMeters = request.RadiusKm * 1000, candidates
            }, cancellationToken);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            if (result.ValueKind != JsonValueKind.Object ||
                !result.TryGetProperty("requestId", out var runId) || runId.ValueKind != JsonValueKind.String || !runId.TryGetGuid(out _) ||
                !result.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.String ||
                !result.TryGetProperty("recommendations", out var rows) || rows.ValueKind != JsonValueKind.Array)
                return Unavailable();
            // Only observations supplied by this request may be returned; the agent cannot invent a zone.
            if (rows.GetArrayLength() > 3 || rows.EnumerateArray().Any(r =>
                r.ValueKind != JsonValueKind.Object || !r.TryGetProperty("zoneId", out var id) ||
                id.ValueKind != JsonValueKind.String || !id.TryGetGuid(out var value) ||
                !candidates.Any(c => c.zoneId == value))) return Unavailable();
            return Ok(ApiResponse<JsonElement>.Ok(result, HttpContext.TraceIdentifier));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Unavailable();
        }
    }

    private ObjectResult Unavailable() => StatusCode(503, ApiResponse<object>.Fail(
        "RECOMMENDATIONS_UNAVAILABLE", "Parking recommendations are unavailable. Retry or browse the parking zones below.", HttpContext.TraceIdentifier));

    public static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double radians = Math.PI / 180;
        var a = Math.Pow(Math.Sin((lat2 - lat1) * radians / 2), 2) +
            Math.Cos(lat1 * radians) * Math.Cos(lat2 * radians) * Math.Pow(Math.Sin((lon2 - lon1) * radians / 2), 2);
        return 6371000 * 2 * Math.Asin(Math.Sqrt(Math.Clamp(a, 0, 1)));
    }
}
