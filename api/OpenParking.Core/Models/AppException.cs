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
    // ── Generic ───────────────────────────────────────────────────────────
    public const string NotFound         = "NOT_FOUND";
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string Unauthorized     = "UNAUTHORIZED";
    public const string Forbidden        = "FORBIDDEN";
    public const string Conflict         = "CONFLICT";
    public const string InternalError    = "INTERNAL_ERROR";

    // ── User & Access (Student 1 — Yowun) ─────────────────────────────────
    public const string EmailTaken              = "EMAIL_ALREADY_TAKEN";
    public const string InvalidCredentials      = "INVALID_CREDENTIALS";
    public const string AccountDisabled         = "ACCOUNT_DISABLED";
    public const string PermitAlreadySubmitted  = "PERMIT_ALREADY_SUBMITTED";
    public const string PermitAlreadyVerified   = "PERMIT_ALREADY_VERIFIED";

    // ── Space & Availability (Student 2 — Supun) ──────────────────────────
    public const string DuplicateZoneCode       = "DUPLICATE_ZONE_CODE";
    public const string ZoneHasActiveSlots      = "ZONE_HAS_ACTIVE_SLOTS";
    public const string InvalidWaypointGraph    = "INVALID_WAYPOINT_GRAPH";

    // ── Booking & Payment (Student 3 — Dev) ───────────────────────────────
    public const string SlotUnavailable         = "SLOT_NOT_AVAILABLE";
    public const string BookingOverlap          = "BOOKING_TIME_OVERLAP";
    public const string InvalidBookingTime      = "INVALID_BOOKING_TIME";
    public const string BookingExpired          = "BOOKING_EXPIRED";
    public const string CannotCancelBooking     = "CANNOT_CANCEL_ACTIVE_BOOKING";
    public const string SessionActive           = "SESSION_ALREADY_ACTIVE";

    // ── Enforcement & AI Orchestration (Student 4 — Karuna) ──────────────
    public const string WorkflowNotPending      = "WORKFLOW_NOT_IN_PENDING_STATE";
    public const string PenaltyAlreadyIssued    = "PENALTY_ALREADY_ISSUED";
    public const string DisputeWindowExpired    = "DISPUTE_WINDOW_EXPIRED";
    public const string AiServiceUnavailable    = "AI_SERVICE_UNAVAILABLE";
}
