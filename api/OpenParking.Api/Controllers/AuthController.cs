using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenParking.Api.Dtos;
using OpenParking.Core.Entities;
using OpenParking.Core.Interfaces;
using OpenParking.Infrastructure.Data;

namespace OpenParking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(
    AppDbContext db,
    IPasswordHasher passwordHasher,
    ITokenService tokens,
    ILogger<AuthController> logger) : ControllerBase
{
    /// <summary>
    /// Self-registration always creates a Driver. Elevated roles are assigned by
    /// an administrator, never chosen by the caller, or anyone could register
    /// themselves as a SystemAdmin.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (await db.Users.AnyAsync(u => u.Email == email))
            return Conflict(new { error = "An account with that email already exists" });

        var user = new User
        {
            Email = email,
            PasswordHash = passwordHasher.Hash(request.Password),
            FullName = request.FullName.Trim(),
            PhoneNumber = request.PhoneNumber?.Trim() ?? string.Empty,
            Role = UserRole.Driver
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        logger.LogInformation("Registered driver {UserId}", user.Id);

        var token = tokens.Issue(user);
        return Ok(new AuthResponse
        {
            AccessToken = token.Token,
            ExpiresAtUtc = token.ExpiresAtUtc,
            User = UserResponse.From(user)
        });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);

        // One message for both an unknown address and a wrong password, so the
        // endpoint cannot be used to enumerate which emails hold accounts.
        if (user == null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            logger.LogWarning("Failed login for {Email}", email);
            return Unauthorized(new { error = "Invalid email or password" });
        }

        var token = tokens.Issue(user);
        return Ok(new AuthResponse
        {
            AccessToken = token.Token,
            ExpiresAtUtc = token.ExpiresAtUtc,
            User = UserResponse.From(user)
        });
    }

    /// <summary>Resolves the bearer token back to the account it represents.</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(id, out var userId))
            return Unauthorized(new { error = "Token does not identify a user" });

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return Unauthorized(new { error = "Account no longer exists" });

        return Ok(UserResponse.From(user));
    }
}
