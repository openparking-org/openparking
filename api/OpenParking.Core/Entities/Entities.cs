namespace OpenParking.Core.Entities;

public enum UserRole
{
    Driver,
    ParkingAdmin,
    SystemAdmin
}

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Driver;
    public bool HasDisabilityPermit { get; set; } = false;
    public string? FcmToken { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class Zone
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public decimal BaseHourlyRate { get; set; } = 5.0m;
    public int TotalCapacity { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Slot> Slots { get; set; } = new List<Slot>();
    public ICollection<FloorPlan> FloorPlans { get; set; } = new List<FloorPlan>();
}

public enum SlotType
{
    Standard,
    Compact,
    Large,
    EV,
    Accessible
}

public enum SlotStatus
{
    Available,
    Reserved,
    Occupied,
    Maintenance
}

public class Slot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ZoneId { get; set; }
    public Zone? Zone { get; set; }
    public Guid? FloorPlanId { get; set; }
    public string SlotNumber { get; set; } = string.Empty;
    public SlotType Type { get; set; } = SlotType.Standard;
    public SlotStatus Status { get; set; } = SlotStatus.Available;
    public string? NearestWaypointId { get; set; }
    public double? CanvasX { get; set; }
    public double? CanvasY { get; set; }
    public double? CanvasWidth { get; set; }
    public double? CanvasHeight { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class FloorPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ZoneId { get; set; }
    public Zone? Zone { get; set; }
    public string FloorName { get; set; } = string.Empty;
    public int FloorOrder { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public double ImageWidthPx { get; set; }
    public double ImageHeightPx { get; set; }
    public string WaypointGraphJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum BookingStatus
{
    Pending,
    Confirmed,
    Active,
    Completed,
    Cancelled,
    Expired
}

public class Booking
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid SlotId { get; set; }
    public Slot? Slot { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Confirmed;
    public string QrCodeContent { get; set; } = string.Empty;
    public decimal EstimatedFee { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum SessionStatus
{
    Active,
    Completed,
    OverstayDetected,
    Disputed
}

public class ParkingSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BookingId { get; set; }
    public Booking? Booking { get; set; }
    public Guid UserId { get; set; }
    public Guid SlotId { get; set; }
    public DateTime CheckInTime { get; set; } = DateTime.UtcNow;
    public DateTime? CheckOutTime { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.Active;
    /// <summary>Minutes beyond booking end time (0 when no overstay).</summary>
    public int OverstayMinutes { get; set; }
    public decimal TotalFee { get; set; }
    public decimal PenaltyFee { get; set; }
    public string? ReceiptPdfUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum PermitStatus
{
    PendingReview,
    Approved,
    Rejected
}

public class DisabilityPermit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string PermitNumber { get; set; } = string.Empty;
    public string DocumentImageUrl { get; set; } = string.Empty;
    public string Jurisdiction { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public PermitStatus Status { get; set; } = PermitStatus.PendingReview;
    public double AiConfidence { get; set; }
    public string? ReviewNotes { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
}

public class SystemSetting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = "System";
}

/// <summary>
/// Persists the full state of a LangGraph multi-agent workflow run.
/// Statuses: RUNNING | PENDING_APPROVAL | AUTO_APPROVED | APPROVED | REJECTED | FAILED
/// </summary>
public class AgentWorkflowRun
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // --- Core workflow identity ---
    /// <summary>Human-readable goal string passed to the Planner Agent.</summary>
    public string Objective { get; set; } = string.Empty;
    /// <summary>Workflow domain: OVERSTAY_ENFORCEMENT | DYNAMIC_PRICING | PERMIT_VALIDATION</summary>
    public string WorkflowType { get; set; } = string.Empty;

    // --- Scoping (nullable — not all workflows relate to a zone or session) ---
    public Guid? ZoneId { get; set; }
    public Guid? SessionId { get; set; }

    // --- Execution state (JSONB columns) ---
    /// <summary>Structured ExecutionPlan produced by the Planner Agent (JSONB).</summary>
    public string PlanJson { get; set; } = "{}";
    /// <summary>Name of the currently executing agent step (e.g. "ANALYZER", "VALIDATOR").</summary>
    public string CurrentStep { get; set; } = string.Empty;
    /// <summary>Per-step outputs keyed by agent name (JSONB).</summary>
    public string StepResultsJson { get; set; } = "{}";
    /// <summary>Raw input payload forwarded to the AI service (JSONB).</summary>
    public string InputPayloadJson { get; set; } = "{}";
    /// <summary>Final execution summary returned by the AI service (JSONB).</summary>
    public string ExecutionSummaryJson { get; set; } = "{}";
    /// <summary>Serialised error details if Status == FAILED (JSONB).</summary>
    public string ErrorLogJson { get; set; } = "{}";

    // --- Decision ---
    /// <summary>RUNNING | PENDING_APPROVAL | AUTO_APPROVED | APPROVED | REJECTED | FAILED</summary>
    public string Status { get; set; } = "PENDING_APPROVAL";
    public string DecisionReason { get; set; } = string.Empty;

    // --- Human approval audit ---
    /// <summary>FK to the User who approved/rejected (nullable — null when AUTO_APPROVED or RUNNING).</summary>
    public Guid? ApprovedBy { get; set; }
    public User? ApprovedByUser { get; set; }

    // --- Timestamps ---
    public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    /// <summary>Free-text actor identifier (email/name) for cases before full auth is wired.</summary>
    public string? ResolvedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Active surge pricing rules set by an approved AI DYNAMIC_PRICING workflow.
/// A zone's active rule overrides the system-wide base hourly rate.
/// (design.md §19.1)
/// </summary>
public class ZonePricingRule
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ZoneId { get; set; }
    public Zone? Zone { get; set; }

    /// <summary>Surge multiplier to apply (e.g. 1.50 = 50% surge). Must be >= 1.00.</summary>
    public decimal Multiplier { get; set; } = 1.00m;

    /// <summary>AI-generated justification for this surge rule.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>FK to the admin User who approved the workflow that created this rule (nullable).</summary>
    public Guid? ApprovedBy { get; set; }
    public User? ApprovedByUser { get; set; }

    /// <summary>FK to the AgentWorkflowRun that produced this rule.</summary>
    public Guid? WorkflowRunId { get; set; }
    public AgentWorkflowRun? WorkflowRun { get; set; }

    public DateTime ActiveFrom { get; set; } = DateTime.UtcNow;
    /// <summary>NULL means the rule is indefinite until manually deactivated.</summary>
    public DateTime? ActiveUntil { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
