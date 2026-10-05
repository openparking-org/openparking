using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OpenParking.Core.Entities;
using OpenParking.Core.Interfaces;
using OpenParking.Infrastructure.Data;
using OpenParking.Infrastructure.Services;
using Xunit;

namespace OpenParking.Tests;

public class PermitVisionTests
{
    [Fact]
    public async Task AdminHttpReview_LoadsSavedReading_ProtectsAccess_AndAppliesDecision()
    {
        await using var app = await CustomerFlowTestHost.StartAsync();
        var permitId = Guid.NewGuid();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.DisabilityPermits.Add(new DisabilityPermit { Id = permitId, UserId = CustomerFlowTestHost.DriverId,
                PermitNumber = "DEMO-12345", Jurisdiction = "Demo Authority", ExpiryDate = DateTime.UtcNow.AddYears(1) });
            db.AuditLogs.Add(new AuditLog { EntityType = "DisabilityPermit", EntityId = permitId,
                Action = "PERMIT_DOCUMENT_VALIDATION", PayloadJson = "{\"valid\":true,\"document_status\":\"read\",\"requires_human_approval\":true,\"extracted_fields\":{\"permit_number\":\"DEMO-12345\"}}" });
            await db.SaveChangesAsync();
        }
        using var client = new HttpClient { BaseAddress = new Uri(CustomerFlowTestHost.Address(app)) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "verification-driver");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/admin/permits/{permitId}/validation", new { })).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "verification-admin");
        var list = await client.GetFromJsonAsync<JsonElement>("/api/admin/permits?status=Pending");
        var reading = list.GetProperty("data").GetProperty("items")[0].GetProperty("aiValidation");
        Assert.Equal("DEMO-12345", reading.GetProperty("extracted_fields").GetProperty("permit_number").GetString());
        var response = await client.PostAsJsonAsync($"/api/admin/permits/{permitId}/validation", new { });
        response.EnsureSuccessStatusCode();
        var cached = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(cached.GetProperty("data").GetProperty("requires_human_approval").GetBoolean());
        (await client.PatchAsJsonAsync($"/api/users/permits/{permitId}/review", new { decision = "Verified", notes = "Reviewed the document" })).EnsureSuccessStatusCode();
        using var finalScope = app.Services.CreateScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(PermitStatus.Verified, (await finalDb.DisabilityPermits.FindAsync(permitId))!.Status);
        Assert.True((await finalDb.Users.FindAsync(CustomerFlowTestHost.DriverId))!.HasDisabilityPermit);
        Assert.Single(finalDb.AuditLogs);
    }

    private sealed class AiHandler(string response) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            Assert.Equal("test-token", request.Headers.GetValues("X-Internal-Token").Single());
            var body = JsonSerializer.Deserialize<JsonElement>(await request.Content!.ReadAsStringAsync(token));
            Assert.Equal("PERMIT-123", body.GetProperty("permit_number").GetString());
            Assert.StartsWith("data:image/png;base64,", body.GetProperty("document_image_url").GetString());
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response, Encoding.UTF8, "application/json") };
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Submission_StaysPending_AndCompletedReadingIsReused(bool matches)
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var user = new User { Email = "driver@test.local" }; db.Users.Add(user); await db.SaveChangesAsync();
        var handler = new AiHandler(JsonSerializer.Serialize(new { valid = matches, confidence = 1.0,
            document_status = "read", requires_human_approval = true, authenticity_verified = false,
            extracted_fields = new { permit_number = "PERMIT-123", expiry_date = "2099-12-31", jurisdiction = "Test Authority", readability = "readable" } }));
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler, false));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["INTERNAL_API_TOKEN"] = "test-token" }).Build();
        var service = new UserService(db, Mock.Of<IEmailService>(), configuration, Mock.Of<ILogger<UserService>>(), factory.Object);
        var permit = await service.SubmitPermitAsync(user.Id, new SubmitPermitRequest {
            PermitNumber = "PERMIT-123", Jurisdiction = "Test Authority", ExpiryDate = new DateTime(2099, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            DocumentBase64 = Convert.ToBase64String(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) });
        Assert.Equal(PermitStatus.Pending, permit.Status);
        Assert.False(user.HasDisabilityPermit);
        Assert.Single(db.AuditLogs);
        var reading = await service.ValidatePermitDocumentAsync(permit.Id);
        Assert.Equal(matches, reading.GetProperty("valid").GetBoolean());
        Assert.Equal(1, handler.Calls);
        await service.ReviewPermitAsync(permit.Id, PermitStatus.Verified, "Document inspected", Guid.NewGuid());
        Assert.True(user.HasDisabilityPermit);
        Assert.Equal(PermitStatus.Verified, permit.Status);
    }

    [Fact]
    public async Task UnavailableReading_IsNotCached_AndCanBeRetried()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var permit = new DisabilityPermit { PermitNumber = "PERMIT-123", DocumentImageUrl = "data:image/png;base64,fixture" };
        db.DisabilityPermits.Add(permit); await db.SaveChangesAsync();
        var handler = new AiHandler("{\"document_status\":\"unavailable\",\"valid\":false}");
        var factory = new Mock<IHttpClientFactory>(); factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler, false));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["INTERNAL_API_TOKEN"] = "test-token" }).Build();
        var service = new UserService(db, Mock.Of<IEmailService>(), configuration, Mock.Of<ILogger<UserService>>(), factory.Object);
        await service.ValidatePermitDocumentAsync(permit.Id); await service.ValidatePermitDocumentAsync(permit.Id);
        Assert.Equal(2, handler.Calls); Assert.Empty(db.AuditLogs); Assert.Equal(PermitStatus.Pending, permit.Status);
    }
}
