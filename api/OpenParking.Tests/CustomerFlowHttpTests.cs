using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenParking.Core.Entities;
using OpenParking.Infrastructure.Data;
using Xunit;

namespace OpenParking.Tests;

public class CustomerFlowHttpTests
{
    [Fact]
    public async Task Reservation_GateEntry_Exit_Payment_CustomerReceipt_WorksAcrossHttp()
    {
        await using var app = await CustomerFlowTestHost.StartAsync();
        using var driver = new HttpClient { BaseAddress = new Uri(CustomerFlowTestHost.Address(app)) };
        using var admin = new HttpClient { BaseAddress = driver.BaseAddress };
        driver.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "verification-driver");
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "verification-admin");
        var created = await driver.PostAsJsonAsync("/api/bookings", new { slotId = CustomerFlowTestHost.SlotId,
            startTime = DateTime.UtcNow, endTime = DateTime.UtcNow.AddHours(2), vehiclePlate = "abc-1234" });
        created.EnsureSuccessStatusCode(); var createdJson = await created.Content.ReadFromJsonAsync<JsonElement>();
        var bookingId = createdJson.GetProperty("data").GetProperty("id").GetGuid();
        Assert.DoesNotContain("passwordHash", await created.Content.ReadAsStringAsync());
        Assert.DoesNotContain("qrCode", await created.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        var payload = new { vehiclePlate = "ABC 1234", zoneId = CustomerFlowTestHost.ZoneId };
        Assert.Equal(HttpStatusCode.Forbidden, (await driver.PostAsJsonAsync("/api/gate/entry", payload)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await driver.PostAsJsonAsync("/api/sessions/check-in", new { bookingId })).StatusCode);
        (await admin.PostAsJsonAsync("/api/gate/entry", payload)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/gate/entry", payload)).StatusCode);
        var active = await driver.GetFromJsonAsync<JsonElement>("/api/sessions/active");
        Assert.Equal("ABC1234", active.GetProperty("data").GetProperty("vehiclePlate").GetString());
        Assert.Equal("LKR", active.GetProperty("data").GetProperty("currency").GetString());
        var exited = await admin.PostAsJsonAsync("/api/gate/exit", payload); exited.EnsureSuccessStatusCode();
        var exitJson = await exited.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(25, exitJson.GetProperty("data").GetProperty("totalFee").GetDecimal());
        (await admin.PostAsJsonAsync($"/api/admin/bookings/{bookingId}/pay", new { })).EnsureSuccessStatusCode();
        var history = await driver.GetFromJsonAsync<JsonElement>("/api/customer/bookings");
        var row = history.GetProperty("data").GetProperty("items")[0];
        Assert.True(row.GetProperty("isPaid").GetBoolean());
        Assert.Equal("Completed", row.GetProperty("status").GetString());
        Assert.Equal(25, row.GetProperty("session").GetProperty("totalFee").GetDecimal());
        Assert.Equal(HttpStatusCode.NotFound, (await driver.GetAsync("/api/sessions/active")).StatusCode);
    }

    [Fact]
    public async Task Permit_SubmissionAndStatus_ReturnStoredDocumentAndAuthority()
    {
        await using var app = await CustomerFlowTestHost.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(CustomerFlowTestHost.Address(app)) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "verification-driver");
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/users/me/permit")).StatusCode);
        var response = await client.PostAsJsonAsync($"/api/users/{CustomerFlowTestHost.DriverId}/permits", new {
            permitNumber = "PERMIT-TEST", jurisdiction = "Test Authority", expiryDate = DateTime.UtcNow.AddYears(1),
            documentBase64 = Convert.ToBase64String(new byte[] { 255, 216, 255, 224, 0, 0 }) });
        response.EnsureSuccessStatusCode();
        var result = (await client.GetFromJsonAsync<JsonElement>("/api/users/me/permit")).GetProperty("data");
        Assert.Equal("PERMIT-TEST", result.GetProperty("permitNumber").GetString());
        Assert.Equal("Test Authority", result.GetProperty("jurisdiction").GetString());
        Assert.StartsWith("data:image/jpeg;base64,", result.GetProperty("documentImageUrl").GetString());
        Assert.Equal("Pending", result.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/users/{CustomerFlowTestHost.AdminId}/permits",
            new { permitNumber = "OTHER" })).StatusCode);
    }

    [Fact]
    public async Task Penalties_FilterBeforePaginationAndPersistDisputeNotes()
    {
        await using var app = await CustomerFlowTestHost.StartAsync();
        Guid penaltyId;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            for (var i = 0; i < 105; i++) db.Penalties.Add(new Penalty { UserId = CustomerFlowTestHost.AdminId, Status = PenaltyStatus.Approved });
            var own = new Penalty { UserId = CustomerFlowTestHost.DriverId, Amount = 100, Status = PenaltyStatus.Approved, Reason = "Overstay" };
            penaltyId = own.Id; db.Penalties.Add(own); await db.SaveChangesAsync();
        }
        using var client = new HttpClient { BaseAddress = new Uri(CustomerFlowTestHost.Address(app)) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "verification-driver");
        var response = await client.GetFromJsonAsync<JsonElement>("/api/penalties/my?page=1&pageSize=10");
        Assert.Equal(1, response.GetProperty("data").GetProperty("totalCount").GetInt32());
        Assert.Single(response.GetProperty("data").GetProperty("items").EnumerateArray());
        (await client.PostAsJsonAsync($"/api/penalties/{penaltyId}/dispute", new { notes = "I left on time." })).EnsureSuccessStatusCode();
        using var verificationScope = app.Services.CreateScope();
        var updated = await verificationScope.ServiceProvider.GetRequiredService<AppDbContext>().Penalties.SingleAsync(p => p.Id == penaltyId);
        Assert.Equal("I left on time.", updated.DisputeNotes); Assert.Equal(PenaltyStatus.Disputed, updated.Status);
    }
}
