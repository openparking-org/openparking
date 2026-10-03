namespace OpenParking.Core.Interfaces;

public interface IPasswordHasher
{
    /// <summary>Derives a storable hash. The salt is generated per call and embedded in the result.</summary>
    string Hash(string password);

    /// <summary>Verifies a candidate password against a stored hash in constant time.</summary>
    bool Verify(string password, string storedHash);
}
