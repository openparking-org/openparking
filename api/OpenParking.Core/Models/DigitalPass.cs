using System.Security.Cryptography;

namespace OpenParking.Core.Models;

/// <summary>
/// Booking &amp; Payment slice. Produces the code carried by a driver's digital
/// parking pass and rendered as the QR image in the mobile app.
/// </summary>
public static class DigitalPass
{
    private const string Prefix = "OP-";
    private const int EntropyBytes = 16;

    /// <summary>
    /// Mints an opaque, unguessable pass code.
    ///
    /// The code deliberately encodes nothing: it is a random reference that the
    /// scanner exchanges for a booking at check-in. A self-describing code
    /// containing the booking or slot id would let anyone photographing a
    /// windscreen enumerate other reservations, and would need a signature to be
    /// trustworthy. 128 bits from a cryptographic source makes guessing one
    /// infeasible without any key management.
    ///
    /// Uppercase hex keeps the QR symbol in alphanumeric mode, which encodes more
    /// densely and scans more reliably on a phone screen than mixed case.
    /// </summary>
    public static string NewCode() =>
        Prefix + Convert.ToHexString(RandomNumberGenerator.GetBytes(EntropyBytes));

    /// <summary>Shape check only; a real code is still verified by database lookup.</summary>
    public static bool IsWellFormed(string? code) =>
        !string.IsNullOrWhiteSpace(code)
        && code.StartsWith(Prefix, StringComparison.Ordinal)
        && code.Length == Prefix.Length + (EntropyBytes * 2);
}
