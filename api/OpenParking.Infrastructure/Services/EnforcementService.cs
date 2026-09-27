using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenParking.Core.Entities;
using OpenParking.Core.Interfaces;
using OpenParking.Core.Models;
using OpenParking.Infrastructure.Data;
using OpenParking.Infrastructure.Extensions;

namespace OpenParking.Infrastructure.Services;

/// <summary>
/// Enforcement &amp; AI Orchestration module service — Student 4 (Karuna).
/// Handles overstay detection, penalty lifecycle, LangGraph workflow wiring, and audit logs.
/// Design reference: design.md §8, §21
/// </summary>
public class EnforcementService(
    AppDbContext db,
    HttpClient http,
    IEmailService emailService,
    IRealtimeNotifier notifier,
    ISettingsService settings,
    IConfiguration config,
    ILogger<EnforcementService> logger) : IEnforcementService
{
    // ── IParkingModule ─────────────────────────────────────────────────────
    public string ModuleName => "Enforcement & AI Orchestration";

    public async Task<HealthStatus> HealthCheckAsync()
    {
        try
        {
            await db.AgentWorkflowRuns.CountAsync();
            return HealthStatus.Healthy;
        }
        catch
        {
            return HealthStatus.Unhealthy;
        }
    }

    public Task<ModuleMetrics> GetMetricsAsync() =>
        Task.FromResult(new ModuleMetrics { ModuleName = ModuleName });

    // ── Workflow Types ─────────────────────────────────────────────────────
    private const string WorkflowTypeOverstay     = "OVERSTAY";
    private const string WorkflowTypeSurgePricing = "SURGE_PRICING";

    private string AiServiceUrl =>
        config["AI_SERVICE_URL"] ?? throw new InvalidOperationException("AI_SERVICE_URL is not configured.");

    // ── Overstay Detection ─────────────────────────────────────────────────

    public async Task<List<Guid>> DetectAndTriggerOverstaysAsync()
    {
        // Find all active sessions where the booking's end time has passed
        var overstayed = await db.ParkingSessions
            .Include(s => s.Booking)
            .Include(s => s.Slot)
            .Where(s => s.Status == SessionStatus.Active &&
                        s.Booking != null &&
                        s.Booking.EndTime < DateTime.UtcNow)
            .ToListAsync();

        var triggered = new List<Guid>();

        foreach (var session in overstayed)
        {
            // Idempotency: only trigger if no workflow already running for this session
            var alreadyRunning = await db.AgentWorkflowRuns.AnyAsync(w =>
                w.SessionId == session.Id &&
                (w.Status == WorkflowStatus.Running || w.Status == WorkflowStatus.AwaitingApproval));

            if (alreadyRunning) continue;

            // Mark session as overstay
            var overstayMins = (int)(DateTime.UtcNow - session.Booking!.EndTime).TotalMinutes;
            session.Status          = SessionStatus.OverstayDetected;
            session.OverstayMinutes = overstayMins;
            session.UpdatedAt       = DateTime.UtcNow;

            var objective = $"Overstay detected for session {session.Id}. " +
                            $"Driver overstayed by {overstayMins} minutes " +
                            $"in slot {session.Slot?.SlotNumber}.";

            await TriggerWorkflowAsync(
                WorkflowTypeOverstay, objective, session.Id, session.Slot?.ZoneId);

            triggered.Add(session.Id);
        }

        if (overstayed.Count > 0)
            await db.SaveChangesAsync();

        return triggered;
    }

    // ── Workflow Triggering ────────────────────────────────────────────────

    public async Task<AgentWorkflowRun> TriggerWorkflowAsync(
        string workflowType, string objective, Guid? sessionId, Guid? zoneId)
    {
        // 1. Create workflow run record first (so we have an ID before calling AI service)
        var run = new AgentWorkflowRun
        {
            Id           = Guid.NewGuid(),
            WorkflowType = workflowType,
            Objective    = objective,
            Status       = WorkflowStatus.Running,
            SessionId    = sessionId,
            ZoneId       = zoneId,
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow,
            CreatedBy    = "enforcement-service"
        };

        db.AgentWorkflowRuns.Add(run);
        await db.SaveChangesAsync();

        // 2. POST to LangGraph service
        try
        {
            var payload = new
            {
                run_id     = run.Id.ToString(),
                type       = workflowType,
                objective,
                session_id = sessionId?.ToString(),
                zone_id    = zoneId?.ToString()
            };

            var response = await http.PostAsJsonAsync($"{AiServiceUrl}/workflows", payload);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                logger.LogError("LangGraph POST failed for run {RunId}: {Error}", run.Id, error);
                run.Status   = WorkflowStatus.Failed;
                run.ErrorLog = JsonSerializer.Serialize(new { error, statusCode = (int)response.StatusCode });
            }
            else
            {
                logger.LogInformation("Workflow {RunId} ({Type}) triggered successfully.", run.Id, workflowType);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to contact AI service for workflow {RunId}.", run.Id);
            run.Status   = WorkflowStatus.Failed;
            run.ErrorLog = JsonSerializer.Serialize(new { error = ex.Message });
        }

        run.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return run;
    }

    // ── Workflow Retrieval ─────────────────────────────────────────────────

    public async Task<List<AgentWorkflowRun>> GetPendingWorkflowsAsync()
    {
        return await db.AgentWorkflowRuns
            .AsNoTracking()
            .Where(w => w.Status == WorkflowStatus.AwaitingApproval)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync();
    }

    public async Task<AgentWorkflowRun> GetWorkflowAsync(Guid workflowId)
    {
        var run = await db.AgentWorkflowRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == workflowId);

        if (run is null)
            throw new AppException(ErrorCodes.NotFound, $"Workflow run {workflowId} not found.", 404);

        return run;
    }

    // ── Workflow Finalization ──────────────────────────────────────────────

    public async Task FinalizeApprovalAsync(Guid workflowRunId, Guid approvedByUserId)
    {
        var run = await db.AgentWorkflowRuns
            .Include(r => r.Session)
            .FirstOrDefaultAsync(r => r.Id == workflowRunId)
            ?? throw new AppException(ErrorCodes.NotFound, "Workflow run not found.", 404);

        if (run.Status != WorkflowStatus.AwaitingApproval)
            throw new AppException(ErrorCodes.WorkflowNotPending,
                $"Workflow is in status '{run.Status}' — only AwaitingApproval workflows can be approved.", 409);

        // Read penalty cap from SystemSettings
        var maxPenalty = await settings.GetDecimalAsync("max_penalty_amount", 5000m);

        // Parse Action Agent's PenaltyProposal from StepResultsJson
        decimal penaltyAmount = 500m; // safe default
        string  penaltyReason = "Overstay penalty";

        if (!string.IsNullOrEmpty(run.StepResultsJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(run.StepResultsJson);
                if (doc.RootElement.TryGetProperty("amount", out var amountEl))
                    penaltyAmount = amountEl.GetDecimal();
                if (doc.RootElement.TryGetProperty("reason", out var reasonEl))
                    penaltyReason = reasonEl.GetString() ?? penaltyReason;
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Could not parse StepResultsJson for run {RunId}, using defaults.", workflowRunId);
            }
        }

        penaltyAmount = Math.Min(penaltyAmount, maxPenalty);

        // Create penalty record
        var penalty = new Penalty
        {
            Id            = Guid.NewGuid(),
            SessionId     = run.SessionId!.Value,
            UserId        = run.Session!.UserId,
            WorkflowRunId = workflowRunId,
            Amount        = penaltyAmount,
            Reason        = penaltyReason,
            Status        = PenaltyStatus.Approved,
            IssuedAt      = DateTime.UtcNow,
            CreatedAt     = DateTime.UtcNow,
            UpdatedAt     = DateTime.UtcNow
        };

        run.Status     = WorkflowStatus.Approved;
        run.ApprovedBy = approvedByUserId;
        run.ApprovedAt = DateTime.UtcNow;
        run.UpdatedAt  = DateTime.UtcNow;

        db.Penalties.Add(penalty);
        await db.SaveChangesAsync();

        // Notify driver via SignalR (Flutter receives this event)
        try
        {
            await notifier.NotifyPenaltyIssuedAsync(run.Session.UserId.ToString(), new
            {
                penaltyId = penalty.Id,
                amount    = penaltyAmount,
                reason    = penaltyReason
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SignalR PenaltyIssued notification failed for user {UserId}.", run.Session.UserId);
        }

        // Send email (non-fatal)
        var user = await db.Users.FindAsync(run.Session.UserId);
        if (user is not null)
            await emailService.SendPenaltyNoticeAsync(user.Email, user.FullName, penalty);

        await WriteAuditLogAsync("Penalty", penalty.Id, "Approved",
            new { penaltyId = penalty.Id, amount = penaltyAmount },
            approvedByUserId, user?.Email ?? "unknown", "system");

        logger.LogInformation("Workflow {RunId} approved. Penalty {PenaltyId} = LKR {Amount}",
            workflowRunId, penalty.Id, penaltyAmount);
    }

    public async Task FinalizeRejectionAsync(Guid workflowRunId, string reason, Guid rejectedByUserId)
    {
        var run = await db.AgentWorkflowRuns.FindAsync(workflowRunId)
            ?? throw new AppException(ErrorCodes.NotFound, "Workflow run not found.", 404);

        if (run.Status != WorkflowStatus.AwaitingApproval)
            throw new AppException(ErrorCodes.WorkflowNotPending,
                $"Workflow is in status '{run.Status}' — cannot reject.", 409);

        run.Status    = WorkflowStatus.Rejected;
        run.UpdatedAt = DateTime.UtcNow;
        run.ErrorLog  = JsonSerializer.Serialize(new { rejectionReason = reason });

        await db.SaveChangesAsync();

        await WriteAuditLogAsync("AgentWorkflowRun", workflowRunId, "Rejected",
            new { reason }, rejectedByUserId, "admin", "system");

        logger.LogInformation("Workflow {RunId} rejected by {UserId}. Reason: {Reason}",
            workflowRunId, rejectedByUserId, reason);
    }

    // ── Penalty Lifecycle ──────────────────────────────────────────────────

    public async Task<Penalty> GetPenaltyAsync(Guid penaltyId)
    {
        return await db.Penalties
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == penaltyId)
            ?? throw new AppException(ErrorCodes.NotFound, "Penalty not found.", 404);
    }

    public async Task<PagedResult<Penalty>> ListPenaltiesAsync(PaginatedQuery query, PenaltyStatus? status = null)
    {
        var q = db.Penalties.AsNoTracking().AsQueryable();

        if (status.HasValue)
            q = q.Where(p => p.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(p => p.Reason.Contains(query.Search));

        q = query.SortDir == "asc"
            ? q.OrderBy(p => p.IssuedAt)
            : q.OrderByDescending(p => p.IssuedAt);

        return await q.ToPagedResultAsync(query);
    }

    public async Task<Penalty> DisputePenaltyAsync(Guid penaltyId, string disputeNotes, Guid userId)
    {
        var penalty = await db.Penalties.FindAsync(penaltyId)
            ?? throw new AppException(ErrorCodes.NotFound, "Penalty not found.", 404);

        if (penalty.UserId != userId)
            throw new AppException(ErrorCodes.Forbidden, "You can only dispute your own penalties.", 403);

        if (penalty.Status != PenaltyStatus.Approved)
            throw new AppException(ErrorCodes.ValidationFailed,
                "Only approved penalties can be disputed.");

        // 7-day dispute window
        if (penalty.IssuedAt < DateTime.UtcNow.AddDays(-7))
            throw new AppException(ErrorCodes.DisputeWindowExpired,
                "The 7-day dispute window for this penalty has expired.", 409);

        penalty.Status      = PenaltyStatus.Disputed;
        penalty.UpdatedAt   = DateTime.UtcNow;

        await db.SaveChangesAsync();

        await WriteAuditLogAsync("Penalty", penaltyId, "Disputed",
            new { disputeNotes }, userId, "driver", "api");

        return penalty;
    }

    public async Task<Penalty> ResolvePenaltyAsync(Guid penaltyId, PenaltyStatus resolution, Guid adminUserId)
    {
        var penalty = await db.Penalties.FindAsync(penaltyId)
            ?? throw new AppException(ErrorCodes.NotFound, "Penalty not found.", 404);

        if (penalty.Status != PenaltyStatus.Disputed)
            throw new AppException(ErrorCodes.ValidationFailed,
                "Only disputed penalties can be resolved.");

        penalty.Status    = resolution;
        penalty.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await WriteAuditLogAsync("Penalty", penaltyId, $"Resolved:{resolution}",
            null, adminUserId, "admin", "api");

        return penalty;
    }

    // ── Audit Log ──────────────────────────────────────────────────────────

    public async Task WriteAuditLogAsync(
        string entityType, Guid? entityId, string action,
        object? payload, Guid? actorUserId, string actorEmail, string ipAddress)
    {
        // Append-only — never update audit log rows
        var log = new AuditLog
        {
            Id          = Guid.NewGuid(),
            EntityType  = entityType,
            EntityId    = entityId,
            Action      = action,
            PayloadJson = payload is null ? null : JsonSerializer.Serialize(payload),
            ActorUserId = actorUserId,
            ActorEmail  = actorEmail,
            IpAddress   = ipAddress,
            CreatedAt   = DateTime.UtcNow
        };

        await db.AuditLogs.AddAsync(log);
        await db.SaveChangesAsync();
    }

    public async Task<PagedResult<AuditLog>> GetAuditLogsAsync(
        PaginatedQuery query, string? entityType = null, Guid? entityId = null)
    {
        var q = db.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(entityType))
            q = q.Where(a => a.EntityType == entityType);

        if (entityId.HasValue)
            q = q.Where(a => a.EntityId == entityId);

        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(a => a.Action.Contains(query.Search) || a.ActorEmail.Contains(query.Search));

        q = q.OrderByDescending(a => a.CreatedAt); // Audit logs always newest-first

        return await q.ToPagedResultAsync(query);
    }
}
