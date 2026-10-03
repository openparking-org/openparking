using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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

public class GateAndCustomerFlowTests
{
    private static AppDbContext Database() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
    private static BookingService BookingService(AppDbContext db) => new(db, Mock.Of<IEmailService>(),
        Mock.Of<IRealtimeNotifier>(), Mock.Of<ILogger<BookingService>>(), new ConfigurationBuilder().Build(), Mock.Of<IHttpClientFactory>(),
        new FeePolicyProvider(new SettingsService(db, new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()))));
    private static async Task<Booking> Seed(AppDbContext db)
    {
        var user = new User { Email = "driver@example.test", FullName = "Test Driver" };
        var zone = new Zone { Code = "GATE", Name = "Gate test", BaseHourlyRate = 100m };
        var slot = new Slot { Zone = zone, ZoneId = zone.Id, SlotNumber = "A1", Status = SlotStatus.Reserved };
        var booking = new Booking { User = user, UserId = user.Id, Slot = slot, SlotId = slot.Id,
            VehiclePlate = "ABC-1234", StartTime = DateTime.UtcNow.AddHours(-1), EndTime = DateTime.UtcNow.AddHours(1), Status = BookingStatus.Confirmed };
        db.AddRange(user, zone, slot, booking); await db.SaveChangesAsync(); return booking;
    }

    [Fact]
    public async Task Gate_EntryThenExit_UsesReservationAndReleasesSpace()
    {
        using var db = Database(); var booking = await Seed(db);
        var gate = new GateService(db, BookingService(db)); var actor = Guid.NewGuid();
        var session = await gate.EnterAsync(" abc 1234 ", booking.Slot!.ZoneId, actor, "test");
        Assert.Equal(booking.Id, session.BookingId); Assert.Equal(booking.UserId, session.UserId);
        Assert.Equal(SlotStatus.Occupied, booking.Slot.Status);
        Assert.Single(db.Bookings); // Never creates an anonymous replacement.
        var result = await gate.ExitAsync("ABC-1234", booking.Slot.ZoneId, actor, "test");
        Assert.Equal(SessionStatus.Completed, result.Status); Assert.Equal(25m, result.TotalFee);
        Assert.Equal(SlotStatus.Available, booking.Slot.Status); Assert.Equal(BookingStatus.Completed, booking.Status);
        Assert.Equal(2, await db.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task Gate_DuplicateEntry_IsRejected()
    {
        using var db = Database(); var booking = await Seed(db); var gate = new GateService(db, BookingService(db));
        await gate.EnterAsync("ABC1234", booking.Slot!.ZoneId, Guid.NewGuid(), "test");
        var error = await Assert.ThrowsAsync<AppException>(() => gate.EnterAsync("ABC-1234", booking.Slot.ZoneId, Guid.NewGuid(), "test"));
        Assert.Equal(409, error.StatusCode); Assert.Single(db.ParkingSessions);
    }

    [Theory]
    [InlineData(BookingStatus.Cancelled, 0)]
    [InlineData(BookingStatus.Completed, 0)]
    [InlineData(BookingStatus.Expired, 0)]
    [InlineData(BookingStatus.Confirmed, -3)]
    [InlineData(BookingStatus.Confirmed, 3)]
    public async Task Gate_IneligibleReservation_DoesNotOpenSession(BookingStatus status, int shiftHours)
    {
        using var db = Database(); var booking = await Seed(db); booking.Status = status;
        booking.StartTime = booking.StartTime.AddHours(shiftHours); booking.EndTime = booking.EndTime.AddHours(shiftHours); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<AppException>(() => new GateService(db, BookingService(db)).EnterAsync("ABC1234", booking.Slot!.ZoneId, Guid.NewGuid(), "test"));
        Assert.Empty(db.ParkingSessions);
    }

    [Fact]
    public async Task Gate_ExitIncludesOverstaySessions()
    {
        using var db = Database(); var booking = await Seed(db); var gate = new GateService(db, BookingService(db));
        var session = await gate.EnterAsync("ABC1234", booking.Slot!.ZoneId, Guid.NewGuid(), "test");
        session.Status = SessionStatus.OverstayDetected; booking.EndTime = DateTime.UtcNow.AddMinutes(-30); await db.SaveChangesAsync();
        var result = await gate.ExitAsync("ABC1234", booking.Slot.ZoneId, Guid.NewGuid(), "test");
        Assert.Equal(SessionStatus.Completed, result.Status); Assert.True(result.OverstayMinutes >= 30);
    }

    [Fact]
    public async Task Gate_WrongZoneAndUnknownPlate_DoNotCreateAnonymousBookings()
    {
        using var db = Database(); var booking = await Seed(db); var gate = new GateService(db, BookingService(db));
        await Assert.ThrowsAsync<AppException>(() => gate.EnterAsync("ABC1234", Guid.NewGuid(), Guid.NewGuid(), "test"));
        await Assert.ThrowsAsync<AppException>(() => gate.EnterAsync("ZZZ9999", booking.Slot!.ZoneId, Guid.NewGuid(), "test"));
        Assert.Single(db.Bookings); Assert.Empty(db.ParkingSessions);
    }

    [Fact]
    public async Task Navigation_UsesMappedConnectedWaypoints()
    {
        using var db = Database(); var booking = await Seed(db);
        var plan = new FloorPlan { ZoneId = booking.Slot!.ZoneId, FloorName = "Ground", ImageWidthPx = 1000, ImageHeightPx = 600,
            WaypointGraphJson = "[{\"id\":\"entry\",\"x\":10,\"y\":20,\"neighbors\":[\"bend\"]},{\"id\":\"bend\",\"x\":110,\"y\":20,\"neighbors\":[\"bay\"]},{\"id\":\"bay\",\"x\":110,\"y\":120,\"neighbors\":[]}]" };
        db.FloorPlans.Add(plan); booking.Slot.FloorPlanId = plan.Id; booking.Slot.NearestWaypointId = "bay"; await db.SaveChangesAsync();
        var route = await new NavigationService(db).RouteAsync(booking.Slot.ZoneId, booking.SlotId);
        Assert.Equal(3, route.Points.Count); Assert.Equal(200, route.DistancePx); Assert.Equal(plan.Id, route.FloorPlanId);
    }

    [Fact]
    public async Task Navigation_UnmappedSpace_ReturnsErrorInsteadOfDummyRoute()
    {
        using var db = Database(); var booking = await Seed(db);
        await Assert.ThrowsAsync<AppException>(() => new NavigationService(db).RouteAsync(booking.Slot!.ZoneId, booking.SlotId));
    }

    [Fact]
    public async Task Recovery_ResetCodeCannotBeReused()
    {
        using var db = Database(); var user = new User { Email = "reset@example.test", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Original123!") };
        db.Users.Add(user); await db.SaveChangesAsync(); string? code = null;
        var email = new Mock<IEmailService>(); email.Setup(e => e.SendPasswordResetAsync(user.Email, It.IsAny<string>()))
            .Callback<string, string>((_, token) => code = token).Returns(Task.CompletedTask);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["JWT_SECRET"] = "TestSecretWithAtLeast32CharactersLong" }).Build();
        var service = new PasswordRecoveryService(db, email.Object, config);
        await service.RequestAsync(user.Email); Assert.NotNull(code);
        await service.ResetAsync(code!, "Replacement123!"); Assert.True(BCrypt.Net.BCrypt.Verify("Replacement123!", user.PasswordHash));
        await Assert.ThrowsAsync<AppException>(() => service.ResetAsync(code!, "ReplayPassword123!"));
    }
}
