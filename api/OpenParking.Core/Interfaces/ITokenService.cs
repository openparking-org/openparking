using OpenParking.Core.Entities;

namespace OpenParking.Core.Interfaces;

public record AccessToken(string Token, DateTime ExpiresAtUtc);

public interface ITokenService
{
    /// <summary>Issues a signed JWT carrying the user's identity and role.</summary>
    AccessToken Issue(User user);
}
