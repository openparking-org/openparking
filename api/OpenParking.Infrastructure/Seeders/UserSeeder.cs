using Microsoft.EntityFrameworkCore;
using OpenParking.Core.Entities;
using OpenParking.Infrastructure.Data;

namespace OpenParking.Infrastructure.Seeders;

public static class UserSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Users.AnyAsync())
        {
            return;
        }

        var defaultPasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!");

        var users = new List<User>
        {
            new User
            {
                Email = "admin@openparking.local",
                PasswordHash = defaultPasswordHash,
                FullName = "System Admin",
                Role = UserRole.SystemAdmin
            },
            new User
            {
                Email = "manager@openparking.local",
                PasswordHash = defaultPasswordHash,
                FullName = "Parking Manager",
                Role = UserRole.ParkingAdmin
            },
            new User
            {
                Email = "driver@openparking.local",
                PasswordHash = defaultPasswordHash,
                FullName = "Standard Driver",
                Role = UserRole.Driver
            }
        };

        var permitDriver = new User
        {
            Email = "permit@openparking.local",
            PasswordHash = defaultPasswordHash,
            FullName = "Permit Driver",
            Role = UserRole.Driver,
            HasDisabilityPermit = true
        };

        db.Users.AddRange(users);
        db.Users.Add(permitDriver);

        await db.SaveChangesAsync();

        var permit = new DisabilityPermit
        {
            UserId = permitDriver.Id,
            PermitNumber = "DP-99999",
            DocumentImageUrl = "https://example.com/permit.jpg",
            Jurisdiction = "Local",
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            Status = PermitStatus.Verified,
            AiConfidence = 0.95,
            ReviewedAt = DateTime.UtcNow,
            ReviewNotes = "Seeded pre-approved permit."
        };

        db.DisabilityPermits.Add(permit);
        await db.SaveChangesAsync();
    }
}
