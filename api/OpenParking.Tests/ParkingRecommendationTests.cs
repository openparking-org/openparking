using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OpenParking.Api.Controllers;
using OpenParking.Infrastructure.Data;
using Xunit;
using Moq;

namespace OpenParking.Tests;

public class ParkingRecommendationTests
{
    [Fact]
    public void Distance_UsesGeographicCoordinates()
    {
        Assert.Equal(0, ParkingRecommendationsController.DistanceMeters(6.9, 79.8, 6.9, 79.8));
        Assert.InRange(ParkingRecommendationsController.DistanceMeters(0, 0, 0, 1), 111000, 112000);
        Assert.True(double.IsFinite(ParkingRecommendationsController.DistanceMeters(90, 180, -90, -180)));
    }

    [Fact]
    public async Task Request_RequiresAuthenticationAndValidLocation()
    {
        await using var app = await CustomerFlowTestHost.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(CustomerFlowTestHost.Address(app)) };
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/parking/recommendations", new { latitude = 6.9, longitude = 79.8 })).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "verification-driver");
        foreach (var body in new object[] { new { latitude = 91, longitude = 80 }, new { longitude = 80 },
            new { latitude = 6.9, longitude = 79.8, preference = "fake" }, new { latitude = 6.9, longitude = 79.8, radiusKm = 101 } })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/parking/recommendations", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/parking/recommendations", new { latitude = 6.9, longitude = 79.8, slotType = "Accessible" })).StatusCode);
    }

    private sealed class AgentHandler(bool invent = false) : HttpMessageHandler
    {
        public JsonElement Evidence { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.EndsWith("/ai/parking/recommend", request.RequestUri!.AbsolutePath);
            Assert.Equal("parking-test", request.Headers.GetValues("X-Internal-Token").Single());
            Evidence = JsonSerializer.Deserialize<JsonElement>(await request.Content!.ReadAsStringAsync(ct));
            var rows = Evidence.GetProperty("candidates").EnumerateArray().Select(c => new {
                zoneId = invent ? Guid.NewGuid() : c.GetProperty("zoneId").GetGuid(),
                name = c.GetProperty("name").GetString(), distanceMeters = c.GetProperty("distanceMeters").GetDouble(),
                hourlyRate = c.GetProperty("hourlyRate").GetDecimal(), availableCount = c.GetProperty("availableCount").GetInt32(),
                currency = c.GetProperty("currency").GetString(), reason = "Closest available parking"
            });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new {
                requestId = Guid.NewGuid(), agent = "ParkingFinderAgent", status = "completed", recommendations = rows,
                message = "Select a space", distanceType = "straight_line" }) };
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Endpoint_PassesLiveEvidenceAndRejectsInventedZones(bool invent)
    {
        var agent = new AgentHandler(invent);
        await using var app = await CustomerFlowTestHost.StartAsync(configure: builder => {
            builder.Configuration["INTERNAL_API_TOKEN"] = "parking-test";
            var clients = new Mock<IHttpClientFactory>();
            clients.Setup(c => c.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(agent, disposeHandler: false));
            builder.Services.AddSingleton(clients.Object);
        });
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var zone = (await db.Zones.FindAsync(CustomerFlowTestHost.ZoneId))!;
            zone.Latitude = 6.901; zone.Longitude = 79.8; await db.SaveChangesAsync();
        }
        using var client = new HttpClient { BaseAddress = new Uri(CustomerFlowTestHost.Address(app)) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "verification-driver");
        var response = await client.PostAsJsonAsync("/api/parking/recommendations", new { latitude = 6.9, longitude = 79.8 });
        Assert.Equal(invent ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK, response.StatusCode);
        var candidates = agent.Evidence.GetProperty("candidates");
        Assert.Single(candidates.EnumerateArray());
        Assert.Equal("LKR", candidates[0].GetProperty("currency").GetString());
        Assert.InRange(candidates[0].GetProperty("distanceMeters").GetDouble(), 110, 112);
        Assert.False(agent.Evidence.TryGetProperty("latitude", out _));
    }

    [Fact]
    public async Task MissingAgentConfiguration_ReturnsActionableUnavailableResponse()
    {
        await using var app = await CustomerFlowTestHost.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(CustomerFlowTestHost.Address(app)) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "verification-driver");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsJsonAsync("/api/parking/recommendations", new { latitude = 6.9, longitude = 79.8 })).StatusCode);
    }
}
