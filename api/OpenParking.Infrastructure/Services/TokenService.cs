using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using OpenParking.Core.Entities;
using OpenParking.Core.Interfaces;

namespace OpenParking.Infrastructure.Services;

/// <summary>
/// Issues the signed JWTs that both the React dashboard and the Flutter driver
/// app present to the API.
/// </summary>
public class TokenService : ITokenService
{
    public const string Issuer = "openparking";
    public const string Audience = "openparking-clients";

    private readonly SymmetricSecurityKey _signingKey;
    private readonly int _expiryHours;

    public TokenService(IConfiguration configuration)
    {
        var secret = configuration["JWT:Secret"];

        // Refuse to start rather than fall back to a built-in key: a default
        // signing secret in a deployed build lets anyone mint valid admin
        // tokens, and it would fail silently.
        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("JWT:Secret is not configured.");

        // HMAC-SHA256 needs at least as much key material as its output.
        if (Encoding.UTF8.GetByteCount(secret) < 32)
            throw new InvalidOperationException("JWT:Secret must be at least 32 bytes for HMAC-SHA256.");

        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        _expiryHours = configuration.GetValue("JWT:ExpiryHours", 24);
    }

    public AccessToken Issue(User user)
    {
        var expiresAt = DateTime.UtcNow.AddHours(_expiryHours);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),

            // Role travels as a claim so authorization is decided from the token
            // alone, with no database round trip on every request.
            new(ClaimTypes.Role, user.Role.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256));

        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
