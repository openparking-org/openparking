using OpenParking.Core.Entities;
using OpenParking.Core.Models;

namespace OpenParking.Core.Interfaces;

/// <summary>
/// Enforcement &amp; AI Orchestration module — owned by Karuna (Student 4).
/// Covers: Overstay detection, penalty lifecycle, LangGraph workflow wiring, and audit logs.
/// </summary>
public interface IEnforcementService : IParkingModule
{
    // ── Overstay Detection (BackgroundService polls this) ─────────────────
    /// <summary>
    /// Scans all active sessions for overstays. For each one found,
    /// triggers a LangGraph OVERSTAY workflow (design.md §8.3).
    /// Called by the hosted BackgroundService every N minutes.
    /// </summary>
    Task<List<Guid>> DetectAndTriggerOverstaysAsync();

    // ── Workflow Triggering (internal — called by overstay detector) ──────
    /// <summary>
    /// POSTs to the Python LangGraph service to start a new workflow run.
    /// Creates an AgentWorkflowRun record with status Running.
    /// </summary>
    Task<AgentWorkflowRun> TriggerWorkflowAsync(string workflowType, string objective, Guid? sessionId, Guid? zoneId);

    // ── Workflow Resolution (called by WorkflowsController after approval) ─
    /// <summary>
    /// Finalises an APPROVED workflow: creates a Penalty record, 
    /// sends Resend email to the driver, updates Flutter via SignalR.
    /// </summary>
    Task FinalizeApprovalAsync(Guid workflowRunId, Guid approvedByUserId);

    /// <summary>Records rejection; no Penalty is created.</summary>
    Task FinalizeRejectionAsync(Guid workflowRunId, string reason, Guid rejectedByUserId);

    // ── Penalty Lifecycle ─────────────────────────────────────────────────
    Task<Penalty> GetPenaltyAsync(Guid penaltyId);
    Task<PagedResult<Penalty>> ListPenaltiesAsync(PaginatedQuery query, PenaltyStatus? status = null);
    Task<Penalty> DisputePenaltyAsync(Guid penaltyId, string disputeNotes, Guid userId);
    Task<Penalty> ResolvePenaltyAsync(Guid penaltyId, PenaltyStatus resolution, Guid adminUserId);

    // ── Audit Log ────────────────────────────────────────────────────────
    Task WriteAuditLogAsync(string entityType, Guid? entityId, string action,
        object? payload, Guid? actorUserId, string actorEmail, string ipAddress);

    Task<PagedResult<AuditLog>> GetAuditLogsAsync(PaginatedQuery query,
        string? entityType = null, Guid? entityId = null);
}
