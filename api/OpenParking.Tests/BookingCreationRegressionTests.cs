using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using OpenParking.Api.Configuration;
using OpenParking.Core.Entities;
using OpenParking.Core.Interfaces;
using OpenParking.Core.Models;
using OpenParking.Infrastructure.Data;
using OpenParking.Infrastructure.Services;
using Xunit;

namespace OpenParking.Tests;

/// <summary>
/// Booking creation failed with a 500 from both clients against Postgres:
/// the Flutter app sent times with no timezone, and a successful booking could
/// not be serialised back because of an entity reference cycle.
/// </summary>
public class BookingCreationRegressionTests
{
    private static (BookingService service, AppDbContext db, Slot slot, User user) Arrange()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

        var user = new User { Id = Guid.NewGuid(), Email = "d@test.local", FullName = "Driver" };
        var zone = new Zone { Id = Guid.NewGuid(), Name = "Zone A", Code = "ZA", BaseHourlyRate = 4m };
        var slot = new Slot { Id = Guid.NewGuid(), ZoneId = zone.Id, Zone = zone, SlotNumber = "A-1", Status = SlotStatus.Available };
        zone.Slots.Add(slot);
        db.AddRange(user, zone, slot);
        db.SaveChanges();

        var http = new Mock<IHttpClientFactory>();
        http.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(new Mock<HttpMessageHandler>().Object));

        var service = new BookingService(
            db,
            new Mock<IEmailService>().Object,
            new Mock<IRealtimeNotifier>().Object,
            new Mock<ILogger<BookingService>>().Object,
            new Mock<IConfiguration>().Object,
            http.Object,
            new FeePolicyProvider(new SettingsService(db, new MemoryCache(new MemoryCacheOptions()))));

        return (service, db, slot, user);
    }

    [Fact]
    public async Task CreateBooking_RejectsATimeWithNoTimezone_WithA400NotA500()
    {
        // What the Flutter app sent: DateTime.now().toIso8601String().
        var (service, _, slot, user) = Arrange();
        var start = DateTime.SpecifyKind(DateTime.UtcNow.AddHours(1), DateTimeKind.Unspecified);

        var ex = await Assert.ThrowsAsync<AppException>(() => service.CreateBookingAsync(
            new CreateBookingRequest { SlotId = slot.Id, StartTime = start, EndTime = start.AddHours(2) }, user.Id));

        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("timezone", ex.Message);
    }

    [Fact]
    public async Task CreateBooking_ConvertsAnOffsetTimeToUtcBeforeSaving()
    {
        // A time sent as ...+05:30 arrives as Kind=Local; Npgsql only accepts UTC.
        var (service, db, slot, user) = Arrange();
        var startUtc = DateTime.UtcNow.AddHours(1);

        var booking = await service.CreateBookingAsync(new CreateBookingRequest
        {
            SlotId = slot.Id,
            StartTime = startUtc.ToLocalTime(),
            EndTime = startUtc.AddHours(2).ToLocalTime()
        }, user.Id);

        var saved = await db.Bookings.SingleAsync(b => b.Id == booking.Id);
        Assert.Equal(DateTimeKind.Utc, saved.StartTime.Kind);
        Assert.Equal(startUtc, saved.StartTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task CreatedBooking_SerialisesWithTheApiJsonSettings()
    {
        // The booking's Slot -> Zone -> Slots navigation is a cycle. With the
        // API's settings it must serialise instead of throwing after commit.
        var (service, _, slot, user) = Arrange();
        var start = DateTime.UtcNow.AddHours(1);
        var booking = await service.CreateBookingAsync(
            new CreateBookingRequest { SlotId = slot.Id, StartTime = start, EndTime = start.AddHours(2) }, user.Id);
        booking.Slot = slot;

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        ApiJson.Configure(options);

        var json = JsonSerializer.Serialize(ApiResponse<Booking>.Ok(booking, "trace"), options);

        Assert.Contains("\"slotNumber\":\"A-1\"", json);
        Assert.Contains("\"status\":\"Pending\"", json); // enums stay strings
    }
}
