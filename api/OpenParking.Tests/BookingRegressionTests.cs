using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using OpenParking.Core.Entities;
using OpenParking.Core.Interfaces;
using OpenParking.Core.Models;
using OpenParking.Infrastructure.Data;
using OpenParking.Infrastructure.Services;
using Xunit;

namespace OpenParking.Tests;

public class BookingRegressionTests
{
    private static DbContextOptions<AppDbContext> Options() =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private static BookingService Service(AppDbContext db, HttpMessageHandler? handler = null)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(handler ?? new PricingHandler(_ => Task.CompletedTask)));
        return new BookingService(db, Mock.Of<IEmailService>(), Mock.Of<IRealtimeNotifier>(),
            Mock.Of<ILogger<BookingService>>(), new ConfigurationBuilder().Build(), factory.Object);
    }

    private static async Task<(User User, Slot Slot)> SeedAsync(AppDbContext db)
    {
        var user = new User { Id = Guid.NewGuid(), Email = "regression@example.test", FullName = "Driver" };
        var zone = new Zone { Id = Guid.NewGuid(), Code = "REG", Name = "Regression", BaseHourlyRate = 10m };
        var slot = new Slot { Id = Guid.NewGuid(), ZoneId = zone.Id, Zone = zone, SlotNumber = "R1", Status = SlotStatus.Available };
        db.AddRange(user, zone, slot);
        await db.SaveChangesAsync();
        return (user, slot);
    }

    [Fact]
    public async Task CreateBooking_EmptyLot_UsesLowCongestionAndBaseRate()
    {
        using var db = new AppDbContext(Options());
        var (user, slot) = await SeedAsync(db);
        string? congestion = null;
        var handler = new PricingHandler(async request =>
        {
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            congestion = payload.RootElement.GetProperty("congestion_level").GetString();
        });
        var start = DateTime.UtcNow.AddHours(1);
        var booking = await Service(db, handler).CreateBookingAsync(
            new CreateBookingRequest { SlotId = slot.Id, StartTime = start, EndTime = start.AddHours(2) }, user.Id);
        Assert.Equal("LOW", congestion);
        Assert.Equal(20m, booking.EstimatedFee);
    }

    [Fact]
    public async Task CreateBooking_SlotReservedDuringPricing_ReturnsConflict()
    {
        var options = Options();
        using var db = new AppDbContext(options);
        var (user, slot) = await SeedAsync(db);
        var handler = new PricingHandler(async _ =>
        {
            using var competingDb = new AppDbContext(options);
            var competingSlot = await competingDb.Slots.SingleAsync(s => s.Id == slot.Id);
            competingSlot.Status = SlotStatus.Reserved;
            await competingDb.SaveChangesAsync();
        });
        var start = DateTime.UtcNow.AddHours(1);
        var error = await Assert.ThrowsAsync<AppException>(() => Service(db, handler).CreateBookingAsync(
            new CreateBookingRequest { SlotId = slot.Id, StartTime = start, EndTime = start.AddHours(1) }, user.Id));
        Assert.Equal(ErrorCodes.SlotUnavailable, error.Code);
    }

    [Fact]
    public async Task CheckIn_OtherUsersBooking_IsRejectedWithoutCreatingSession()
    {
        using var db = new AppDbContext(Options());
        var (user, slot) = await SeedAsync(db);
        var booking = new Booking { Id = Guid.NewGuid(), UserId = user.Id, SlotId = slot.Id, Status = BookingStatus.Pending };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<AppException>(() => Service(db).CheckInAsync(
            new CheckInRequest { BookingId = booking.Id, UserId = Guid.NewGuid() }, "test"));
        Assert.Equal(ErrorCodes.Forbidden, error.Code);
        Assert.Empty(db.ParkingSessions);
    }

    [Fact]
    public async Task CheckIn_UnknownBookingWithSlot_DoesNotCreateReplacementBooking()
    {
        using var db = new AppDbContext(Options());
        var (user, slot) = await SeedAsync(db);
        await Assert.ThrowsAsync<AppException>(() => Service(db).CheckInAsync(
            new CheckInRequest { BookingId = Guid.NewGuid(), SlotId = slot.Id, UserId = user.Id }, "test"));
        Assert.Empty(db.Bookings);
        Assert.Equal(SlotStatus.Available, slot.Status);
    }

    private sealed class PricingHandler(Func<HttpRequestMessage, Task> callback) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await callback(request);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"multiplier\":1}", Encoding.UTF8, "application/json")
            };
        }
    }
}
