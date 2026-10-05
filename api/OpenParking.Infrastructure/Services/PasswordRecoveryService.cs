using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OpenParking.Core.Interfaces;
using OpenParking.Core.Models;
using OpenParking.Infrastructure.Data;

namespace OpenParking.Infrastructure.Services;

/// <summary>Short-lived email recovery tokens, invalidated by a password change.</summary>
public class PasswordRecoveryService(AppDbContext db, IEmailService email, IConfiguration config)
{
    public async Task RequestAsync(string address)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == address.Trim().ToLower() && u.IsActive);
        if (user == null) return;
        var expires = DateTimeOffset.UtcNow.AddMinutes(20).ToUnixTimeSeconds();
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var payload = $"{user.Id:N}.{expires}.{nonce}";
        var token = $"{payload}.{Sign(payload, user.PasswordHash)}";
        await email.SendPasswordResetAsync(user.Email, token);
    }

    public async Task ResetAsync(string token, string password)
    {
        if (password.Length < 8 || password.Length > 128)
            throw new AppException(ErrorCodes.ValidationFailed, "Use a password with 8–128 characters.");
        var parts = token.Trim().Split('.');
        if (parts.Length != 4 || !Guid.TryParseExact(parts[0], "N", out var id) ||
            !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var expires) ||
            expires < DateTimeOffset.UtcNow.ToUnixTimeSeconds()) throw InvalidToken();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id && u.IsActive) ?? throw InvalidToken();
        var payload = string.Join('.', parts.Take(3));
        var expected = Encoding.UTF8.GetBytes(Sign(payload, user.PasswordHash));
        var supplied = Encoding.UTF8.GetBytes(parts[3]);
        if (!CryptographicOperations.FixedTimeEquals(expected, supplied)) throw InvalidToken();
        var oldHash = user.PasswordHash;
        var newHash = BCrypt.Net.BCrypt.HashPassword(password, 12);
        if (db.Database.IsRelational())
        {
            // Compare-and-set prevents concurrent replay of the same email token.
            var changed = await db.Users.Where(u => u.Id == id && u.PasswordHash == oldHash)
                .ExecuteUpdateAsync(update => update.SetProperty(u => u.PasswordHash, newHash)
                    .SetProperty(u => u.UpdatedAt, DateTime.UtcNow));
            if (changed != 1) throw InvalidToken();
        }
        else { user.PasswordHash = newHash; user.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(); }
    }

    private string Sign(string payload, string hash)
    {
        var secret = config["JWT_SECRET"] ?? config["JWT:Secret"] ?? throw new InvalidOperationException("JWT secret not configured.");
        return Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"password-reset:{payload}:{hash}")));
    }
    private static AppException InvalidToken() => new(ErrorCodes.ValidationFailed, "The reset code is invalid or expired. Request another email.");
}
