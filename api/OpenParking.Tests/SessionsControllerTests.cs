using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenParking.Api.Controllers;
using OpenParking.Core.Entities;
using OpenParking.Infrastructure.Data;
using Xunit;

namespace OpenParking.Tests;

public class SessionsControllerTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task CheckIn_ReturnsNotFound_WhenBookingDoesNotExist()
    {
        using var db = CreateInMemoryDbContext();
        var controller = new SessionsController(db);

        var result = await controller.CheckIn(new CheckInRequest
        {
            BookingId = Guid.NewGuid()
        });

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.NotNull(notFoundResult.Value);
    }

    [Fact]
    public async Task CheckIn_CreatesSession_AndMarksSlotOccupied_WhenValidBookingProvided()
    {
        using var db = CreateInMemoryDbContext();
        var zone = new Zone { Id = Guid.NewGuid(), Name = "Zone A", Code = "ZA", BaseHourlyRate = 5.0m };
        var slot = new Slot { Id = Guid.NewGuid(), ZoneId = zone.Id, SlotNumber = "A-102", Status = SlotStatus.Available };
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            SlotId = slot.Id,
            Status = BookingStatus.Confirmed,
            StartTime = DateTime.UtcNow.AddMinutes(-5),
            EndTime = DateTime.UtcNow.AddHours(2)
        };

        db.Zones.Add(zone);
        db.Slots.Add(slot);
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        var controller = new SessionsController(db);
        var result = await controller.CheckIn(new CheckInRequest
        {
            BookingId = booking.Id
        });

        var okResult = Assert.IsType<OkObjectResult>(result);
        var sessionDto = Assert.IsType<SessionDto>(okResult.Value);

        Assert.Equal(booking.Id, sessionDto.BookingId);
        Assert.Equal("Active", sessionDto.Status);
        Assert.Equal("A-102", sessionDto.SlotNumber);

        // Verify database state
        var dbSlot = await db.Slots.FindAsync(slot.Id);
        Assert.NotNull(dbSlot);
        Assert.Equal(SlotStatus.Occupied, dbSlot.Status);

        var dbSession = await db.ParkingSessions.FirstOrDefaultAsync(s => s.BookingId == booking.Id);
        Assert.NotNull(dbSession);
        Assert.Equal(SessionStatus.Active, dbSession.Status);
    }

    [Fact]
    public async Task CheckIn_ReturnsConflict_WhenActiveSessionAlreadyExists()
    {
        using var db = CreateInMemoryDbContext();
        var zone = new Zone { Id = Guid.NewGuid(), Name = "Zone A", Code = "ZA", BaseHourlyRate = 5.0m };
        var slot = new Slot { Id = Guid.NewGuid(), ZoneId = zone.Id, SlotNumber = "A-102", Status = SlotStatus.Occupied };
        var bookingId = Guid.NewGuid();
        var booking = new Booking
        {
            Id = bookingId,
            UserId = Guid.NewGuid(),
            SlotId = slot.Id,
            Status = BookingStatus.Active
        };
        var existingSession = new ParkingSession
        {
            BookingId = bookingId,
            UserId = booking.UserId,
            SlotId = slot.Id,
            Status = SessionStatus.Active,
            CheckInTime = DateTime.UtcNow.AddMinutes(-30)
        };

        db.Zones.Add(zone);
        db.Slots.Add(slot);
        db.Bookings.Add(booking);
        db.ParkingSessions.Add(existingSession);
        await db.SaveChangesAsync();

        var controller = new SessionsController(db);
        var result = await controller.CheckIn(new CheckInRequest
        {
            BookingId = bookingId
        });

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task CheckOut_ReturnsNotFound_WhenNoActiveSessionExists()
    {
        using var db = CreateInMemoryDbContext();
        var controller = new SessionsController(db);

        var result = await controller.CheckOut(new CheckOutRequest
        {
            SessionId = Guid.NewGuid()
        });

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task CheckOut_ClosesSession_CalculatesFee_AndFreesSlot()
    {
        using var db = CreateInMemoryDbContext();
        var zone = new Zone { Id = Guid.NewGuid(), Name = "Zone A", Code = "ZA", BaseHourlyRate = 5.0m };
        var slot = new Slot { Id = Guid.NewGuid(), ZoneId = zone.Id, SlotNumber = "A-102", Status = SlotStatus.Occupied };
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            SlotId = slot.Id,
            Status = BookingStatus.Active,
            StartTime = DateTime.UtcNow.AddHours(-2),
            EndTime = DateTime.UtcNow.AddHours(-1) // Overstayed by ~1 hr
        };

        var session = new ParkingSession
        {
            Id = Guid.NewGuid(),
            BookingId = booking.Id,
            UserId = booking.UserId,
            SlotId = slot.Id,
            Status = SessionStatus.Active,
            CheckInTime = DateTime.UtcNow.AddHours(-2),
            TotalFee = 0m,
            PenaltyFee = 0m
        };

        db.Zones.Add(zone);
        db.Slots.Add(slot);
        db.Bookings.Add(booking);
        db.ParkingSessions.Add(session);
        await db.SaveChangesAsync();

        var controller = new SessionsController(db);
        var result = await controller.CheckOut(new CheckOutRequest
        {
            SessionId = session.Id
        });

        var okResult = Assert.IsType<OkObjectResult>(result);
        var sessionDto = Assert.IsType<SessionDto>(okResult.Value);

        Assert.Equal("Completed", sessionDto.Status);
        Assert.NotNull(sessionDto.CheckOutTime);
        Assert.True(sessionDto.TotalFee > 0m);

        // Verify slot freed
        var dbSlot = await db.Slots.FindAsync(slot.Id);
        Assert.NotNull(dbSlot);
        Assert.Equal(SlotStatus.Available, dbSlot.Status);

        // Verify booking completed
        var dbBooking = await db.Bookings.FindAsync(booking.Id);
        Assert.NotNull(dbBooking);
        Assert.Equal(BookingStatus.Completed, dbBooking.Status);
    }

    [Fact]
    public async Task GetActiveSession_ReturnsActiveSession_WhenPresent()
    {
        using var db = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SlotId = Guid.NewGuid(),
            Status = BookingStatus.Active
        };
        var session = new ParkingSession
        {
            BookingId = booking.Id,
            UserId = userId,
            SlotId = booking.SlotId,
            Status = SessionStatus.Active,
            CheckInTime = DateTime.UtcNow.AddMinutes(-20)
        };

        db.Bookings.Add(booking);
        db.ParkingSessions.Add(session);
        await db.SaveChangesAsync();

        var controller = new SessionsController(db);
        var result = await controller.GetActiveSession(userId: userId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<SessionDto>(okResult.Value);
        Assert.Equal(session.Id, dto.Id);
        Assert.Equal("Active", dto.Status);
    }
}
