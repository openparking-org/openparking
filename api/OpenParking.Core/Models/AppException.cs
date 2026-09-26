namespace OpenParking.Core.Models;

/// <summary>
/// Thrown by service layer for known business rule violations.
/// The global error handler maps these to structured HTTP responses.
/// </summary>
public class AppException : Exception
{
    public string Code { get; }
    public int StatusCode { get; }

    public AppException(string code, string message, int statusCode = 400)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }
}

/// <summary>Pre-defined error codes to keep responses consistent across the codebase.</summary>
public static class ErrorCodes
{
    // Generic
    public const string NotFound         = "NOT_FOUND";
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string Unauthorized     = "UNAUTHORIZED";
    public const string Forbidden        = "FORBIDDEN";
    public const string Conflict         = "CONFLICT";
    public const string InternalError    = "INTERNAL_ERROR";

    // Booking domain
    public const string SlotUnavailable  = "SLOT_UNAVAILABLE";
    public const string BookingExpired   = "BOOKING_EXPIRED";
    public const string SessionActive    = "SESSION_ALREADY_ACTIVE";

    // User domain
    public const string EmailTaken       = "EMAIL_ALREADY_TAKEN";
    public const string InvalidCredentials = "INVALID_CREDENTIALS";

    // Workflow domain
    public const string WorkflowNotPending = "WORKFLOW_NOT_IN_PENDING_STATE";
}
