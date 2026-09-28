using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OpenParking.Core.Entities;
using OpenParking.Core.Interfaces;
using OpenParking.Core.Models;
using OpenParking.Infrastructure.Data;
using OpenParking.Infrastructure.Services;
using Xunit;

namespace OpenParking.Tests;

public class ZoneServiceTests
{
    private static (AppDbContext db, ZoneService service) CreateService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options);
        var loggerMock = new Mock<ILogger<ZoneService>>();
        var notifierMock = new Mock<IRealtimeNotifier>();
        var service = new ZoneService(db, loggerMock.Object, notifierMock.Object);
        return (db, service);
    }

    [Fact]
    public async Task CreateZoneAsync_AddsZoneSuccessfully()
    {
        var (db, service) = CreateService();
        var adminId = Guid.NewGuid();

        var zone = new Zone
        {
            Code = "ZN-A",
            Name = "Zone Alpha",
            TotalCapacity = 50,
            BaseHourlyRate = 150m
        };

        var created = await service.CreateZoneAsync(zone, adminId);

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("ZN-A", created.Code);

        var inDb = await db.Zones.FindAsync(created.Id);
        Assert.NotNull(inDb);
        Assert.Equal("Zone Alpha", inDb.Name);
    }

    [Fact]
    public async Task CreateZoneAsync_ThrowsValidationException_WhenDuplicateCode()
    {
        var (db, service) = CreateService();
        var adminId = Guid.NewGuid();

        var zone1 = new Zone
        {
            Code = "ZN-DUP",
            Name = "Zone First",
            TotalCapacity = 20,
            BaseHourlyRate = 100m
        };
        await service.CreateZoneAsync(zone1, adminId);

        var zone2 = new Zone
        {
            Code = "ZN-DUP",
            Name = "Zone Second",
            TotalCapacity = 30,
            BaseHourlyRate = 120m
        };

        await Assert.ThrowsAsync<AppException>(() => service.CreateZoneAsync(zone2, adminId));
    }

    [Fact]
    public async Task UpdateZoneAsync_UpdatesSpecifiedProperties()
    {
        var (db, service) = CreateService();
        var adminId = Guid.NewGuid();

        var zone = new Zone
        {
            Code = "ZN-UPD",
            Name = "Original Name",
            TotalCapacity = 25,
            BaseHourlyRate = 100m
        };
        await service.CreateZoneAsync(zone, adminId);

        var req = new UpdateZoneRequest
        {
            Name = "Updated Name",
            BaseHourlyRate = 175m
        };

        var updated = await service.UpdateZoneAsync(zone.Id, req, adminId);

        Assert.Equal("Updated Name", updated.Name);
        Assert.Equal(175m, updated.BaseHourlyRate);
    }

    [Fact]
    public async Task CreateSlotAsync_AddsSlotToZone()
    {
        var (db, service) = CreateService();
        var adminId = Guid.NewGuid();

        var zone = new Zone
        {
            Code = "ZN-SLT",
            Name = "Slot Zone",
            TotalCapacity = 10,
            BaseHourlyRate = 100m
        };
        await service.CreateZoneAsync(zone, adminId);

        var slotReq = new CreateSlotRequest
        {
            SlotNumber = "A-01",
            Type = SlotType.EV,
            NearestWaypointId = "WP-1"
        };

        var slot = await service.CreateSlotAsync(zone.Id, slotReq, adminId);

        Assert.NotNull(slot);
        Assert.Equal("A-01", slot.SlotNumber);
        Assert.Equal(SlotType.EV, slot.Type);
        Assert.Equal(zone.Id, slot.ZoneId);
        Assert.Equal(SlotStatus.Available, slot.Status);
    }

    [Fact]
    public async Task UpdateSlotStatusAsync_ChangesStatusCorrectly()
    {
        var (db, service) = CreateService();
        var adminId = Guid.NewGuid();

        var zone = new Zone
        {
            Code = "ZN-STS",
            Name = "Status Zone",
            TotalCapacity = 5,
            BaseHourlyRate = 80m
        };
        await service.CreateZoneAsync(zone, adminId);

        var slot = await service.CreateSlotAsync(zone.Id, new CreateSlotRequest
        {
            SlotNumber = "B-01",
            Type = SlotType.Standard
        }, adminId);

        var updatedSlot = await service.UpdateSlotStatusAsync(slot.Id, SlotStatus.Maintenance, adminId);

        Assert.Equal(SlotStatus.Maintenance, updatedSlot.Status);
        var inDb = await db.Slots.FindAsync(slot.Id);
        Assert.Equal(SlotStatus.Maintenance, inDb!.Status);
    }

    [Fact]
    public async Task GetOccupancyStatsAsync_CalculatesBreakdownAccurately()
    {
        var (db, service) = CreateService();
        var adminId = Guid.NewGuid();

        var zone = new Zone
        {
            Code = "ZN-OCC",
            Name = "Occupancy Zone",
            TotalCapacity = 4,
            BaseHourlyRate = 100m
        };
        await service.CreateZoneAsync(zone, adminId);

        var s1 = await service.CreateSlotAsync(zone.Id, new CreateSlotRequest { SlotNumber = "O-1" }, adminId);
        var s2 = await service.CreateSlotAsync(zone.Id, new CreateSlotRequest { SlotNumber = "O-2" }, adminId);
        var s3 = await service.CreateSlotAsync(zone.Id, new CreateSlotRequest { SlotNumber = "O-3" }, adminId);
        var s4 = await service.CreateSlotAsync(zone.Id, new CreateSlotRequest { SlotNumber = "O-4" }, adminId);

        await service.UpdateSlotStatusAsync(s1.Id, SlotStatus.Occupied, adminId);
        await service.UpdateSlotStatusAsync(s2.Id, SlotStatus.Reserved, adminId);

        var stats = await service.GetOccupancyStatsAsync(zone.Id);

        Assert.Equal(4, stats.TotalSlots);
        Assert.Equal(1, stats.OccupiedSlots);
        Assert.Equal(1, stats.ReservedSlots);
        Assert.Equal(2, stats.AvailableSlots);
        Assert.Equal(25.0, stats.OccupancyPercent);
    }
}
