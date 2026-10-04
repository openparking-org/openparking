using OpenParking.Core.Entities;
using OpenParking.Core.Models;

namespace OpenParking.Core.Interfaces;

/// <summary>
/// User &amp; Access module — owned by Yowun (Student 1).
/// Covers: Identity, JWT auth, role management, disabled permit verification.
/// </summary>
public interface IUserService : IParkingModule
{
    // ── Auth ──────────────────────────────────────────────────────────────
    /// <summary>Register a new user. Throws AppException(EMAIL_ALREADY_TAKEN) if duplicate.</summary>
    Task<AuthResult> RegisterAsync(RegisterRequest req);

    /// <summary>Validate credentials and return a signed JWT. Throws AppException(INVALID_CREDENTIALS) on failure.</summary>
    Task<AuthResult> LoginAsync(string email, string password);

    // ── Profile ───────────────────────────────────────────────────────────
    Task<User?> GetByIdAsync(Guid userId);
    Task<PagedResult<User>> ListAsync(PaginatedQuery query);
    Task<User> UpdateRoleAsync(Guid userId, UserRole newRole, Guid actorId);

    // ── Disability permit verification (Validator Agent hook) ─────────────
    Task<DisabilityPermit?> GetUserPermitAsync(Guid userId);
    Task<DisabilityPermit> SubmitPermitAsync(Guid userId, SubmitPermitRequest req);
    Task<DisabilityPermit> ReviewPermitAsync(Guid permitId, PermitStatus decision, string? notes, Guid reviewerId);
    Task<List<DisabilityPermit>> GetPendingPermitsAsync();
}

// ── Request / Result DTOs ─────────────────────────────────────────────────

public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Driver;
}

public class AuthResult
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserProfile User { get; set; } = new();
}

public class UserProfile
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool HasDisabilityPermit { get; set; }
}

public class SubmitPermitRequest
{
    public string PermitNumber { get; set; } = string.Empty;
    public string DocumentImageUrl { get; set; } = string.Empty;
    public string? DocumentBase64 { get; set; }
    public string Jurisdiction { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
}
