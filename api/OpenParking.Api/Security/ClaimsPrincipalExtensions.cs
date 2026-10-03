using System.Security.Claims;
using OpenParking.Core.Entities;

namespace OpenParking.Api.Security;

public static class Roles
{
    public const string Driver = nameof(UserRole.Driver);
    public const string ParkingAdmin = nameof(UserRole.ParkingAdmin);
    public const string SystemAdmin = nameof(UserRole.SystemAdmin);

    /// <summary>Staff who operate the gate and oversee a lot.</summary>
    public const string Staff = ParkingAdmin + "," + SystemAdmin;
}

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The authenticated user's id, taken from the token rather than anything
    /// the caller supplied in a request body.
    /// </summary>
    public static Guid? UserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    /// <summary>True for roles that may see and act on other people's records.</summary>
    public static bool IsStaff(this ClaimsPrincipal principal) =>
        principal.IsInRole(Roles.ParkingAdmin) || principal.IsInRole(Roles.SystemAdmin);
}
