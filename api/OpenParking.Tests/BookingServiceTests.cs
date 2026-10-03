using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OpenParking.Core.Entities;
using OpenParking.Core.Interfaces;
using OpenParking.Core.Models;
using OpenParking.Infrastructure.Data;
using OpenParking.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using Xunit;

namespace OpenParking.Tests;

public class BookingServiceTests
{
    /// <summary>Real policy provider over the test database, so fees follow seeded settings.</summary>
    private static IFeePolicyProvider Policies(AppDbContext db) =>
        new FeePolicyProvider(new SettingsService(db, new MemoryCache(new MemoryCacheOptions())));

    private AppDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task CheckInAsync_ValidRequest_CreatesSession()
    {
        var db = GetInMemoryDbContext();
        
        var user = new User { Id = Guid.NewGuid(), Email = "test@openparking.local", FullName = "Test User" };
        var zone = new Zone { Id = Guid.NewGuid(), Name = "Zone A", Code = "ZA", BaseHourlyRate = 5 };
        var slot = new Slot { Id = Guid.NewGuid(), ZoneId = zone.Id, SlotNumber = "A1", Status = SlotStatus.Available };
        
        db.Users.Add(user);
        db.Zones.Add(zone);
        db.Slots.Add(slot);
        await db.SaveChangesAsync();

        var emailMock = new Mock<IEmailService>();
        var notifierMock = new Mock<IRealtimeNotifier>();
        var loggerMock = new Mock<ILogger<BookingService>>();
        var configMock = new Mock<IConfiguration>();
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();

        var service = new BookingService(db, emailMock.Object, notifierMock.Object, loggerMock.Object, configMock.Object, httpClientFactoryMock.Object, Policies(db));

        var req = new CheckInRequest
        {
            UserId = user.Id,
            SlotId = slot.Id
        };

        var session = await service.CheckInAsync(req, "127.0.0.1");

        Assert.NotNull(session);
        Assert.Equal(user.Id, session.UserId);
        Assert.Equal(slot.Id, session.SlotId);
        Assert.Equal(SessionStatus.Active, session.Status);

        var updatedSlot = await db.Slots.FindAsync(slot.Id);
        Assert.Equal(SlotStatus.Occupied, updatedSlot!.Status);
    }

    [Fact]
    public async Task CheckInAsync_AlreadyActiveSession_ThrowsAppException()
    {
        var db = GetInMemoryDbContext();
        
        var user = new User { Id = Guid.NewGuid(), Email = "test@openparking.local", FullName = "Test User" };
        var zone = new Zone { Id = Guid.NewGuid(), Name = "Zone A", Code = "ZA", BaseHourlyRate = 5 };
        var slot = new Slot { Id = Guid.NewGuid(), ZoneId = zone.Id, SlotNumber = "A1", Status = SlotStatus.Occupied };
        var booking = new Booking { Id = Guid.NewGuid(), UserId = user.Id, SlotId = slot.Id, StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddHours(1), Status = BookingStatus.Active };
        var existingSession = new ParkingSession 
        { 
            Id = Guid.NewGuid(), 
            BookingId = booking.Id,
            UserId = user.Id, 
            SlotId = slot.Id, 
            Status = SessionStatus.Active,
            CheckInTime = DateTime.UtcNow
        };
        
        db.Users.Add(user);
        db.Zones.Add(zone);
        db.Slots.Add(slot);
        db.Bookings.Add(booking);
        db.ParkingSessions.Add(existingSession);
        await db.SaveChangesAsync();

        var emailMock = new Mock<IEmailService>();
        var notifierMock = new Mock<IRealtimeNotifier>();
        var loggerMock = new Mock<ILogger<BookingService>>();
        var configMock = new Mock<IConfiguration>();
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();

        var service = new BookingService(db, emailMock.Object, notifierMock.Object, loggerMock.Object, configMock.Object, httpClientFactoryMock.Object, Policies(db));

        var req = new CheckInRequest
        {
            BookingId = booking.Id,
            UserId = user.Id,
            SlotId = slot.Id
        };

        await Assert.ThrowsAsync<AppException>(() => service.CheckInAsync(req, "127.0.0.1"));
    }

    [Fact]
    public async Task CreateBookingAsync_ValidRequest_ReservesSlotAndSendsEmail()
    {
        var db = GetInMemoryDbContext();

        var user = new User { Id = Guid.NewGuid(), Email = "driver@openparking.local", FullName = "Driver John" };
        var zone = new Zone { Id = Guid.NewGuid(), Name = "Zone Central", Code = "ZC", BaseHourlyRate = 100m };
        var slot = new Slot { Id = Guid.NewGuid(), ZoneId = zone.Id, SlotNumber = "C-1", Status = SlotStatus.Available };

        db.Users.Add(user);
        db.Zones.Add(zone);
        db.Slots.Add(slot);
        await db.SaveChangesAsync();

        var emailMock = new Mock<IEmailService>();
        var notifierMock = new Mock<IRealtimeNotifier>();
        var loggerMock = new Mock<ILogger<BookingService>>();
        var configMock = new Mock<IConfiguration>();
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();

        // Setup mock HttpClient to not throw during tests
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object);
        httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var service = new BookingService(db, emailMock.Object, notifierMock.Object, loggerMock.Object, configMock.Object, httpClientFactoryMock.Object, Policies(db));

        var req = new CreateBookingRequest
        {
            SlotId = slot.Id,
            StartTime = DateTime.UtcNow.AddHours(1),
            EndTime = DateTime.UtcNow.AddHours(3),
            VehiclePlate = "WP-CAB-1234"
        };

        var booking = await service.CreateBookingAsync(req, user.Id);

        Assert.NotNull(booking);
        Assert.Equal(user.Id, booking.UserId);
        Assert.Equal(slot.Id, booking.SlotId);
        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.Equal(200m, booking.EstimatedFee); // 2 hours * 100

        var updatedSlot = await db.Slots.FindAsync(slot.Id);
        Assert.Equal(SlotStatus.Reserved, updatedSlot!.Status);

        emailMock.Verify(e => e.SendBookingConfirmationAsync(user.Email, user.FullName, It.IsAny<Booking>()), Times.Once);
    }

    [Fact]
    public async Task CancelBookingAsync_PendingBooking_ReleasesSlot()
    {
        var db = GetInMemoryDbContext();

        var userId = Guid.NewGuid();
        var slot = new Slot { Id = Guid.NewGuid(), SlotNumber = "D-1", Status = SlotStatus.Reserved };
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SlotId = slot.Id,
            Slot = slot,
            Status = BookingStatus.Pending,
            StartTime = DateTime.UtcNow.AddHours(2),
            EndTime = DateTime.UtcNow.AddHours(4)
        };

        db.Slots.Add(slot);
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        var emailMock = new Mock<IEmailService>();
        var notifierMock = new Mock<IRealtimeNotifier>();
        var loggerMock = new Mock<ILogger<BookingService>>();
        var configMock = new Mock<IConfiguration>();
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();

        var service = new BookingService(db, emailMock.Object, notifierMock.Object, loggerMock.Object, configMock.Object, httpClientFactoryMock.Object, Policies(db));

        var cancelled = await service.CancelBookingAsync(booking.Id, userId);

        Assert.Equal(BookingStatus.Cancelled, cancelled.Status);

        var updatedSlot = await db.Slots.FindAsync(slot.Id);
        Assert.Equal(SlotStatus.Available, updatedSlot!.Status);
    }

    [Fact]
    public async Task CheckOutAsync_ActiveSession_CompletesAndReleasesSlot()
    {
        var db = GetInMemoryDbContext();

        var user = new User { Id = Guid.NewGuid(), Email = "user@test.local", FullName = "User Test" };
        var zone = new Zone { Id = Guid.NewGuid(), Name = "Zone B", Code = "ZB", BaseHourlyRate = 60m };
        var slot = new Slot { Id = Guid.NewGuid(), ZoneId = zone.Id, SlotNumber = "B-2", Status = SlotStatus.Occupied };
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SlotId = slot.Id,
            StartTime = DateTime.UtcNow.AddHours(-2),
            EndTime = DateTime.UtcNow.AddHours(1),
            Status = BookingStatus.Active
        };
        var session = new ParkingSession
        {
            Id = Guid.NewGuid(),
            BookingId = booking.Id,
            UserId = user.Id,
            SlotId = slot.Id,
            CheckInTime = DateTime.UtcNow.AddMinutes(-45),
            Status = SessionStatus.Active
        };

        db.Users.Add(user);
        db.Zones.Add(zone);
        db.Slots.Add(slot);
        db.Bookings.Add(booking);
        db.ParkingSessions.Add(session);
        await db.SaveChangesAsync();

        var emailMock = new Mock<IEmailService>();
        var notifierMock = new Mock<IRealtimeNotifier>();
        var loggerMock = new Mock<ILogger<BookingService>>();
        var configMock = new Mock<IConfiguration>();
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();

        var service = new BookingService(db, emailMock.Object, notifierMock.Object, loggerMock.Object, configMock.Object, httpClientFactoryMock.Object, Policies(db));

        var result = await service.CheckOutAsync(new CheckOutRequest { SessionId = session.Id }, "127.0.0.1");

        Assert.NotNull(result);
        Assert.Equal(SessionStatus.Completed, result.Session.Status);
        Assert.True(result.Session.TotalFee > 0);

        var updatedSlot = await db.Slots.FindAsync(slot.Id);
        Assert.Equal(SlotStatus.Available, updatedSlot!.Status);
    }
}
