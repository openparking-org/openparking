using System.Security.Cryptography;
using OpenParking.Core.Interfaces;

namespace OpenParking.Infrastructure.Services;

/// <summary>
/// PBKDF2-HMAC-SHA256, from the .NET base library so the project takes on no
/// extra dependency for something this security-sensitive.
///
/// Each password gets its own random salt, so two users who choose the same
/// password do not share a hash and a precomputed rainbow table is useless. The
/// iteration count is stored alongside the hash rather than hardcoded at the
/// comparison site, so it can be raised later without invalidating existing
/// passwords — old hashes keep verifying at the count they were created with.
/// </summary>
public class PasswordHasher : IPasswordHasher
{
    // OWASP's 2023 floor for PBKDF2-HMAC-SHA256.
    private const int DefaultIterations = 210_000;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    private const char Separator = '.';

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Derive(password, salt, DefaultIterations);

        return string.Join(Separator,
            DefaultIterations,
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    public bool Verify(string password, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(storedHash))
            return false;

        var parts = storedHash.Split(Separator);
        if (parts.Length != 3
            || !int.TryParse(parts[0], out var iterations)
            || iterations <= 0)
            return false;

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[1]);
            expected = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Derive(password, salt, iterations, expected.Length);

        // Constant time: a length-sensitive or early-exit comparison would leak
        // how much of the hash matched through timing.
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static byte[] Derive(string password, byte[] salt, int iterations, int length = HashBytes) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, length);
}
