using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using OpenParking.Api.Controllers;
using OpenParking.Core.Entities;
using OpenParking.Core.Interfaces;
using OpenParking.Core.Models;
using OpenParking.Infrastructure.Data;
using Xunit;

namespace OpenParking.Tests;

public class AdminControllerTests
{
    private static AppDbContext Database() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static AdminController Controller(AppDbContext db, IBookingService? bookings = null)
    {
        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.GetStringAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("USD");
        var result = new AdminController(db, bookings ?? Mock.Of<IBookingService>(), settings.Object,
            Mock.Of<IHttpClientFactory>(), new ConfigurationBuilder().Build());
        result.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Email, "admin@example.test"), new Claim(ClaimTypes.Role, "SystemAdmin") }, "test")) } };
        return result;
    }

    [Fact]
    public async Task Pay_CompletedBooking_PersistsOneSimulationMarker()
    {
        using var db = Database();
        var booking = new Booking { Status = BookingStatus.Completed };
        var service = new Mock<IBookingService>();
        service.Setup(s => s.GetBookingAsync(booking.Id)).ReturnsAsync(booking);
        var controller = Controller(db, service.Object);
        Assert.IsType<OkObjectResult>(await controller.Pay(booking.Id));
        Assert.IsType<OkObjectResult>(await controller.Pay(booking.Id));
        Assert.Single(db.AuditLogs);
        Assert.Equal("SIMULATED_PAY", db.AuditLogs.Single().Action);
        Assert.Equal(booking.Id, db.AuditLogs.Single().EntityId);
    }

    [Fact]
    public async Task Pay_ActiveBooking_RejectsAndDoesNotRecordPayment()
    {
        using var db = Database();
        var booking = new Booking { Status = BookingStatus.Active };
        var service = new Mock<IBookingService>();
        service.Setup(s => s.GetBookingAsync(booking.Id)).ReturnsAsync(booking);
        await Assert.ThrowsAsync<AppException>(() => Controller(db, service.Object).Pay(booking.Id));
        Assert.Empty(db.AuditLogs);
    }

    [Fact]
    public async Task SlotLayout_UpdatesStableIds_WithoutDuplicatingBaysOrResettingOccupancy()
    {
        using var db = Database();
        var zone = new Zone { Code = "TEST" };
        var slot = new Slot { ZoneId = zone.Id, Floor = 1, SlotNumber = "A1", Status = SlotStatus.Occupied };
        db.AddRange(zone, slot); await db.SaveChangesAsync();
        var request = new AdminSlotLayoutRequest { Slots = [new AdminSlotRequest { Id = slot.Id, SlotNumber = "A1", CanvasX = 100, CanvasY = 100, CanvasWidth = 80, CanvasHeight = 55 }] };
        var controller = Controller(db);
        await controller.SaveSlots(zone.Id, 1, request);
        await controller.SaveSlots(zone.Id, 1, request);
        Assert.Single(db.Slots);
        Assert.Equal(SlotStatus.Occupied, db.Slots.Single().Status);
        Assert.Equal(100, db.Slots.Single().CanvasX);
    }

    [Fact]
    public async Task SlotLayout_CannotRemoveReservedBay()
    {
        using var db = Database();
        var zone = new Zone { Code = "TEST" };
        db.AddRange(zone, new Slot { ZoneId = zone.Id, SlotNumber = "A1", Status = SlotStatus.Reserved });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<AppException>(() => Controller(db).SaveSlots(zone.Id, 0, new AdminSlotLayoutRequest()));
        Assert.Single(db.Slots);
    }

    [Fact]
    public async Task AdminBooking_UsesSelectedDriverIdentity()
    {
        using var db = Database();
        var driver = new User { Role = UserRole.Driver };
        db.Users.Add(driver); await db.SaveChangesAsync();
        var service = new Mock<IBookingService>();
        service.Setup(s => s.CreateBookingAsync(It.IsAny<CreateBookingRequest>(), driver.Id)).ReturnsAsync(new Booking());
        await Controller(db, service.Object).CreateBooking(new AdminBookingRequest { DriverId = driver.Id, VehiclePlate = "ABC123" });
        service.Verify(s => s.CreateBookingAsync(It.IsAny<CreateBookingRequest>(), driver.Id), Times.Once);
    }

    [Fact]
    public void AdminAndSettingWrites_RequireServerRoles()
    {
        var adminPolicy = Assert.Single(typeof(AdminController).GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>());
        Assert.Equal("ParkingAdmin,SystemAdmin", adminPolicy.Roles);
        var settingsPolicy = Assert.Single(typeof(SettingsController).GetMethod(nameof(SettingsController.UpdateSetting))!.GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>());
        Assert.Equal("SystemAdmin", settingsPolicy.Roles);
    }
}
