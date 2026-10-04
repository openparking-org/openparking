using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using OpenParking.Core.Entities;
using OpenParking.Core.Interfaces;
using OpenParking.Core.Models;
using OpenParking.Infrastructure.Data;

namespace OpenParking.Infrastructure.Services;

/// <summary>
/// User &amp; Access module service — Student 1 (Yowun).
/// Handles identity, JWT auth, role management, and disability permit verification.
/// Design reference: design.md §3, §14, §18
/// </summary>
public class UserService(
    AppDbContext db,
    IEmailService emailService,
    IConfiguration config,
    ILogger<UserService> logger,
    IHttpClientFactory httpClientFactory) : IUserService
{
    // ── IParkingModule ─────────────────────────────────────────────────────
    public string ModuleName => "User & Access";

    public async Task<HealthStatus> HealthCheckAsync()
    {
        try
        {
            await db.Users.CountAsync();
            return HealthStatus.Healthy;
        }
        catch
        {
            return HealthStatus.Unhealthy;
        }
    }

    public Task<ModuleMetrics> GetMetricsAsync() =>
        Task.FromResult(new ModuleMetrics { ModuleName = ModuleName });

    // ── Constants ──────────────────────────────────────────────────────────
    private const int BcryptWorkFactor = 12;

    private string JwtSecret =>
        config["JWT_SECRET"] ?? config["JWT:Secret"]
        ?? throw new InvalidOperationException("JWT secret not configured.");

    private int JwtExpiryHours =>
        int.TryParse(config["JWT_EXPIRY_HOURS"] ?? config["JWT:ExpiryHours"], out var h) ? h : 24;

    private string JwtIssuer   => config["JWT:Issuer"]   ?? "openparking-api";
    private string JwtAudience => config["JWT:Audience"] ?? "openparking-clients";

    // ── Auth ───────────────────────────────────────────────────────────────

    public async Task<AuthResult> RegisterAsync(RegisterRequest req)
    {
        // 1. Check email uniqueness
        var exists = await db.Users.AnyAsync(u => u.Email == req.Email.ToLowerInvariant());
        if (exists)
            throw new AppException(ErrorCodes.EmailTaken,
                "An account with this email address already exists.", 409);

        // 2. Hash password
        var hash = BCrypt.Net.BCrypt.HashPassword(req.Password, BcryptWorkFactor);

        // 3. Create user
        var user = new User
        {
            Id           = Guid.NewGuid(),
            Email        = req.Email.ToLowerInvariant().Trim(),
            PasswordHash = hash,
            FullName     = req.FullName.Trim(),
            PhoneNumber  = req.PhoneNumber?.Trim(),
            Role         = req.Role,
            IsActive     = true,
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow,
            CreatedBy    = "self-register"
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        logger.LogInformation("New user registered: {Email} ({Role})", user.Email, user.Role);

        // Send welcome email (non-fatal if it fails)
        await emailService.SendWelcomeAsync(user.Email, user.FullName);

        return BuildAuthResult(user);
    }

    public async Task<AuthResult> LoginAsync(string email, string password)
    {
        // Single query — never distinguish "not found" vs "wrong password" (prevents enumeration)
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Email == email.Trim().ToLowerInvariant());

        var validPassword = user is not null &&
                            BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);

        if (!validPassword || user is null)
            throw new AppException(ErrorCodes.InvalidCredentials,
                "Invalid email or password.", 401);

        if (!user.IsActive)
            throw new AppException(ErrorCodes.AccountDisabled,
                "This account has been disabled. Contact support.", 403);

        logger.LogInformation("User logged in: {Email}", user.Email);
        return BuildAuthResult(user);
    }

    // ── Profile ────────────────────────────────────────────────────────────

    public async Task<User?> GetByIdAsync(Guid userId)
    {
        return await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
    }

    public async Task<PagedResult<User>> ListAsync(PaginatedQuery query)
    {
        var q = db.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(u => u.FullName.Contains(query.Search) ||
                             u.Email.Contains(query.Search));

        q = query.SortBy switch
        {
            "email"     => query.SortDir == "asc" ? q.OrderBy(u => u.Email) : q.OrderByDescending(u => u.Email),
            "fullName"  => query.SortDir == "asc" ? q.OrderBy(u => u.FullName) : q.OrderByDescending(u => u.FullName),
            _           => query.SortDir == "asc" ? q.OrderBy(u => u.CreatedAt) : q.OrderByDescending(u => u.CreatedAt)
        };

        var total = await q.CountAsync();
        var items = await q.Skip(query.Skip).Take(query.Take).ToListAsync();
        return PagedResult<User>.Create(items, total, query.Page, query.Take);
    }

    public async Task<User> UpdateRoleAsync(Guid userId, UserRole newRole, Guid actorId)
    {
        var user = await db.Users.FindAsync(userId)
            ?? throw new AppException(ErrorCodes.NotFound, "User not found.", 404);

        user.Role      = newRole;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        logger.LogInformation("Role updated: user={UserId} -> {Role} by actor={ActorId}",
            userId, newRole, actorId);
        return user;
    }

    // ── Disability Permits ─────────────────────────────────────────────────

    public async Task<DisabilityPermit> SubmitPermitAsync(Guid userId, SubmitPermitRequest req)
    {
        var user = await db.Users.FindAsync(userId)
            ?? throw new AppException(ErrorCodes.NotFound, "User not found.", 404);

        // Block duplicate pending/verified permit submissions
        var hasPending = await db.DisabilityPermits.AnyAsync(p =>
            p.UserId == userId &&
            (p.Status == PermitStatus.Pending || p.Status == PermitStatus.Verified));

        if (hasPending)
            throw new AppException(ErrorCodes.PermitAlreadySubmitted,
                "You already have a pending or verified permit.", 409);

        var permit = new DisabilityPermit
        {
            Id             = Guid.NewGuid(),
            UserId         = userId,
            PermitNumber   = req.PermitNumber,
            DocumentImageUrl = req.DocumentImageUrl,
            Jurisdiction   = req.Jurisdiction,
            ExpiryDate     = req.ExpiryDate,
            Status         = PermitStatus.Pending,
            CreatedAt      = DateTime.UtcNow,
            UpdatedAt      = DateTime.UtcNow
        };

        db.DisabilityPermits.Add(permit);
        await db.SaveChangesAsync();

        logger.LogInformation("Permit submitted: permitId={PermitId} userId={UserId}", permit.Id, userId);

        try
        {
            var aiClient = httpClientFactory.CreateClient();
            var aiUrl = config["AI_SERVICE_URL"] ?? "http://localhost:8000";
            var reqBody = new
            {
                permit_number = req.PermitNumber,
                expiry_date = req.ExpiryDate.ToString("O"),
                jurisdiction = req.Jurisdiction,
                document_image_url = req.DocumentImageUrl
            };
            
            var res = await aiClient.PostAsJsonAsync($"{aiUrl}/ai/permits/validate", reqBody);
            if (res.IsSuccessStatusCode)
            {
                var aiResult = await res.Content.ReadFromJsonAsync<JsonElement>();
                if (aiResult.TryGetProperty("confidence", out var confidenceProp))
                {
                    var confidence = confidenceProp.GetDecimal();
                    var thresholdStr = config["permits.auto_approve_confidence"] ?? "0.90";
                    if (decimal.TryParse(thresholdStr, out var threshold) && confidence >= threshold)
                    {
                        permit.Status = PermitStatus.Verified;
                        permit.UpdatedAt = DateTime.UtcNow;
                        
                        user.HasDisabilityPermit = true;
                        user.UpdatedAt = DateTime.UtcNow;

                        await db.SaveChangesAsync();
                        
                        logger.LogInformation("Permit {PermitId} AUTO-APPROVED by AI (Confidence: {Confidence})", permit.Id, confidence);
                        await emailService.SendPermitOutcomeAsync(user.Email, user.FullName, permit);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to call AI validation service for permit {PermitId}. Falling back to manual review.", permit.Id);
        }

        return permit;
    }

    public async Task<DisabilityPermit> ReviewPermitAsync(
        Guid permitId, PermitStatus decision, string? notes, Guid reviewerId)
    {
        var permit = await db.DisabilityPermits
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == permitId)
            ?? throw new AppException(ErrorCodes.NotFound, "Permit not found.", 404);

        if (permit.Status != PermitStatus.Pending)
            throw new AppException(ErrorCodes.PermitAlreadyVerified,
                $"Permit is already in status '{permit.Status}' and cannot be reviewed.", 409);

        permit.Status          = decision;
        permit.RejectionReason = decision == PermitStatus.Rejected ? notes : null;
        permit.UpdatedAt       = DateTime.UtcNow;

        // Grant disabled slot access on approval
        if (decision == PermitStatus.Verified && permit.User is not null)
        {
            permit.User.HasDisabilityPermit = true;
            permit.User.UpdatedAt           = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        if (permit.User is not null)
            await emailService.SendPermitOutcomeAsync(permit.User.Email, permit.User.FullName, permit);

        logger.LogInformation("Permit {PermitId} {Decision} by reviewer {ReviewerId}",
            permitId, decision, reviewerId);
        return permit;
    }

    public async Task<List<DisabilityPermit>> GetPendingPermitsAsync()
    {
        return await db.DisabilityPermits
            .Include(p => p.User)
            .Where(p => p.Status == PermitStatus.Pending)
            .OrderBy(p => p.CreatedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    // ── Private Helpers ────────────────────────────────────────────────────

    private AuthResult BuildAuthResult(User user)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.NameIdentifier,     user.Id.ToString()),
            new Claim(ClaimTypes.Role,               user.Role.ToString()),
            new Claim("fullName",                    user.FullName),
            new Claim("hasDisabilityPermit",         user.HasDisabilityPermit.ToString().ToLower())
        };

        var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
        var creds   = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddHours(JwtExpiryHours);

        var token = new JwtSecurityToken(
            issuer:             JwtIssuer,
            audience:           JwtAudience,
            claims:             claims,
            expires:            expires,
            signingCredentials: creds);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return new AuthResult
        {
            Token     = tokenString,
            ExpiresAt = expires,
            User      = new UserProfile
            {
                Id                 = user.Id,
                Email              = user.Email,
                FullName           = user.FullName,
                Role               = user.Role.ToString(),
                HasDisabilityPermit = user.HasDisabilityPermit
            }
        };
    }
}
