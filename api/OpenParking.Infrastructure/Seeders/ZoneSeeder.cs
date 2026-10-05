using Microsoft.EntityFrameworkCore;
using OpenParking.Core.Entities;
using OpenParking.Infrastructure.Data;

namespace OpenParking.Infrastructure.Seeders;

public static class ZoneSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Zones.AnyAsync())
        {
            return;
        }

        var zone1 = new Zone
        {
            Name = "Main Campus Lot",
            Code = "Z-MAIN",
            Latitude = 37.7749,
            Longitude = -122.4194,
            BaseHourlyRate = 5.00m,
            TotalCapacity = 20
        };

        var zone2 = new Zone
        {
            Name = "Downtown VIP",
            Code = "Z-VIP",
            Latitude = 37.7833,
            Longitude = -122.4167,
            BaseHourlyRate = 12.00m,
            TotalCapacity = 10
        };

        db.Zones.AddRange(zone1, zone2);
        await db.SaveChangesAsync(); // Save to generate IDs

        var slots = new List<Slot>();

        // Zone 1: 15 Standard, 3 Reserved (Status=Reserved), 2 Disabled (Accessible)
        for (int i = 1; i <= 15; i++)
        {
            slots.Add(new Slot { ZoneId = zone1.Id, SlotNumber = $"M-{i:D2}", Type = SlotType.Standard, Status = SlotStatus.Available });
        }
        for (int i = 16; i <= 18; i++)
        {
            slots.Add(new Slot { ZoneId = zone1.Id, SlotNumber = $"M-{i:D2}", Type = SlotType.Standard, Status = SlotStatus.Reserved });
        }
        for (int i = 19; i <= 20; i++)
        {
            slots.Add(new Slot { ZoneId = zone1.Id, SlotNumber = $"M-{i:D2}", Type = SlotType.Accessible, Status = SlotStatus.Available });
        }

        // Zone 2: 10 VIP slots (Large)
        for (int i = 1; i <= 10; i++)
        {
            slots.Add(new Slot { ZoneId = zone2.Id, SlotNumber = $"V-{i:D2}", Type = SlotType.Large, Status = SlotStatus.Available });
        }

        db.Slots.AddRange(slots);
        await db.SaveChangesAsync();
    }
}
