using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using OpenParking.Core.Entities;
using OpenParking.Infrastructure.Services;
using Xunit;

namespace OpenParking.Tests;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Verifies_A_Correct_Password()
    {
        var hash = _hasher.Hash("correct horse battery staple");

        Assert.True(_hasher.Verify("correct horse battery staple", hash));
    }

    [Fact]
    public void Rejects_A_Wrong_Password()
    {
        var hash = _hasher.Hash("correct horse battery staple");

        Assert.False(_hasher.Verify("Correct horse battery staple", hash));
    }

    [Fact]
    public void Never_Stores_The_Password_In_Clear()
    {
        var hash = _hasher.Hash("hunter2");

        Assert.DoesNotContain("hunter2", hash);
    }

    [Fact]
    public void Salts_Each_Password_Separately()
    {
        // Two users choosing the same password must not share a hash, or one
        // leaked hash would reveal every account using that password.
        var first = _hasher.Hash("same-password");
        var second = _hasher.Hash("same-password");

        Assert.NotEqual(first, second);
        Assert.True(_hasher.Verify("same-password", first));
        Assert.True(_hasher.Verify("same-password", second));
    }

    [Fact]
    public void Records_The_Iteration_Count_So_It_Can_Be_Raised_Later()
    {
        var hash = _hasher.Hash("whatever");

        Assert.StartsWith("210000.", hash);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-real-hash")]
    [InlineData("abc.def.ghi")]
    [InlineData("0.c2FsdA==.aGFzaA==")]
    public void Treats_A_Malformed_Stored_Hash_As_A_Failed_Verification(string stored)
    {
        // A corrupt or truncated column must deny access, not throw a 500 that
        // leaks a stack trace.
        Assert.False(_hasher.Verify("anything", stored));
    }
}

public class TokenServiceTests
{
    private const string Secret = "unit-test-signing-key-that-is-long-enough-32";

    private static TokenService Service(string secret = Secret, int expiryHours = 24) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JWT:Secret"] = secret,
            ["JWT:ExpiryHours"] = expiryHours.ToString()
        }).Build());

    private static User Admin() => new()
    {
        Email = "admin@example.com",
        FullName = "Ada Admin",
        Role = UserRole.ParkingAdmin
    };

    [Fact]
    public void Issues_A_Token_Carrying_Identity_And_Role()
    {
        var user = Admin();

        var token = new JwtSecurityTokenHandler().ReadJwtToken(Service().Issue(user).Token);

        Assert.Equal(user.Id.ToString(), token.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value);
        Assert.Equal("ParkingAdmin", token.Claims.First(c => c.Type == ClaimTypes.Role).Value);
        Assert.Equal(TokenService.Issuer, token.Issuer);
    }

    [Fact]
    public void Never_Puts_The_Password_Hash_In_The_Token()
    {
        // A JWT is signed, not encrypted: anyone holding it can read every claim.
        var user = Admin();
        user.PasswordHash = "210000.c2FsdA==.c2VjcmV0";

        var raw = Service().Issue(user).Token;

        Assert.DoesNotContain("c2VjcmV0", raw);
    }

    [Fact]
    public void Honours_The_Configured_Lifetime()
    {
        var issued = Service(expiryHours: 2).Issue(Admin());

        Assert.InRange(issued.ExpiresAtUtc, DateTime.UtcNow.AddHours(1.9), DateTime.UtcNow.AddHours(2.1));
    }

    [Fact]
    public void Refuses_To_Start_Without_A_Configured_Secret()
    {
        Assert.Throws<InvalidOperationException>(() => Service(secret: ""));
    }

    [Fact]
    public void Refuses_A_Secret_Too_Short_For_HmacSha256()
    {
        // Shorter than the 256-bit output makes the signature weaker than it looks.
        Assert.Throws<InvalidOperationException>(() => Service(secret: "too-short"));
    }
}
