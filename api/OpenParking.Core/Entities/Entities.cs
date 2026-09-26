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

// ── Penalty ──────────────────────────────────────────────
public enum PenaltyStatus
{
    Proposed,
    PendingApproval,
    Approved,
    Rejected,
    Collected,
    Waived
}

// ── Workflow Status ───────────────────────────────────────
public enum WorkflowStatus
{
    Running,
    AwaitingApproval,
    Approved,
    Rejected,
    Failed
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
    // Audit fields (design.md §9: every table must have these)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = "System";
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
    public int OverstayMinutes { get; set; }
    public decimal TotalFee { get; set; }
    public decimal PenaltyFee { get; set; }
    public string? ReceiptPdfUrl { get; set; }
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

public class AgentWorkflowRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>OVERSTAY | SURGE_PRICING — matches design.md §8.2</summary>
    public string WorkflowType { get; set; } = string.Empty;
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Running;
    public Guid? ZoneId { get; set; }
    public Guid? SessionId { get; set; }
    /// <summary>Objective string passed to the Planner Agent.</summary>
    public string Objective { get; set; } = string.Empty;
    /// <summary>Full LangGraph plan returned by Planner — stored as JSONB.</summary>
    public string PlanJson { get; set; } = "{}";
    /// <summary>Per-step results from Analyzer/Action/Validator agents — JSONB.</summary>
    public string StepResultsJson { get; set; } = "{}";
    public string DecisionReason { get; set; } = string.Empty;
    public string? ErrorLog { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedBy { get; set; }
}

/// <summary>
/// Enforcement module — represents a concrete penalty issued to a driver.
/// Created by the Action Agent after APPROVED workflow run (design.md §8.3 step 7).
/// </summary>
public class Penalty
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public ParkingSession? Session { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid? WorkflowRunId { get; set; }
    public AgentWorkflowRun? WorkflowRun { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public PenaltyStatus Status { get; set; } = PenaltyStatus.Proposed;
    public string? DisputeNotes { get; set; }
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public string CreatedBy { get; set; } = "System";
}

/// <summary>
/// Immutable audit trail entry. Append-only — never updated or deleted.
/// Required by design.md §9 (Enforcement module scope).
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>Entity type affected, e.g. "Booking", "Penalty", "AgentWorkflowRun".</summary>
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    /// <summary>e.g. CREATE, UPDATE, APPROVE, REJECT, CHECKIN, CHECKOUT</summary>
    public string Action { get; set; } = string.Empty;
    /// <summary>JSON snapshot of changes (before/after or payload).</summary>
    public string PayloadJson { get; set; } = "{}";
    public Guid? ActorUserId { get; set; }
    public string ActorEmail { get; set; } = "System";
    public string IpAddress { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
