using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenParking.Core.Entities;
using OpenParking.Infrastructure.Data;

namespace OpenParking.Api.Controllers;

[ApiController]
[Route("api/agent/[controller]")]
[Route("api/[controller]")]
[Authorize(Roles = "ParkingAdmin,SystemAdmin")]
public class WorkflowsController(AppDbContext db) : ControllerBase
{
    /// <summary>
    /// Retrieves all workflows, optionally filtered by status (e.g. ?status=PENDING_APPROVAL).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetWorkflows([FromQuery] string? status = null)
    {
        var query = db.AgentWorkflowRuns.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(w => w.Status == status);
        }

        var runs = await query
            .OrderByDescending(w => w.TriggeredAt)
            .ToListAsync();

        return Ok(runs.Select(WorkflowResponseDto.FromEntity));
    }

    /// <summary>
    /// Retrieves all workflows pending admin approval.
    /// Preserved for direct compatibility with React Admin Dashboard polling (design.md §8.3).
    /// </summary>
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingWorkflows()
    {
        var pending = await db.AgentWorkflowRuns
            .AsNoTracking()
            .Where(w => w.Status == "PENDING_APPROVAL" || w.Status == "AWAITING_APPROVAL")
            .OrderByDescending(w => w.TriggeredAt)
            .ToListAsync();

        return Ok(pending.Select(WorkflowResponseDto.FromEntity));
    }

    /// <summary>
    /// Retrieves a single workflow by ID for detailed inspection prior to approval.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetWorkflowById(Guid id)
    {
        var run = await db.AgentWorkflowRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == id);

        if (run == null)
        {
            return NotFound(new { error = $"Workflow with ID '{id}' was not found." });
        }

        return Ok(WorkflowResponseDto.FromEntity(run));
    }

    /// <summary>
    /// Registers or creates an AgentWorkflowRun for tracking and orchestration.
    /// (design.md §8.2, §8.3)
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> CreateWorkflow([FromBody] CreateWorkflowRequest req)
    {
        if (req == null)
        {
            return BadRequest(new { error = "Workflow request payload cannot be empty." });
        }

        var runId = req.Id ?? Guid.NewGuid();
        var existing = await db.AgentWorkflowRuns.FindAsync(runId);

        if (existing != null)
        {
            if (req.Objective != null) existing.Objective = req.Objective;
            if (req.WorkflowType != null) existing.WorkflowType = req.WorkflowType;
            if (req.ZoneId.HasValue) existing.ZoneId = req.ZoneId;
            if (req.SessionId.HasValue) existing.SessionId = req.SessionId;
            if (req.PlanJson != null) existing.PlanJson = req.PlanJson;
            if (req.CurrentStep != null) existing.CurrentStep = req.CurrentStep;
            if (req.StepResultsJson != null) existing.StepResultsJson = req.StepResultsJson;
            if (req.InputPayloadJson != null) existing.InputPayloadJson = req.InputPayloadJson;
            if (req.ExecutionSummaryJson != null) existing.ExecutionSummaryJson = req.ExecutionSummaryJson;
            if (req.Status != null) existing.Status = req.Status;
            if (req.DecisionReason != null) existing.DecisionReason = req.DecisionReason;
            existing.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();
            return Ok(WorkflowResponseDto.FromEntity(existing));
        }

        var run = new AgentWorkflowRun
        {
            Id = runId,
            Objective = req.Objective ?? string.Empty,
            WorkflowType = req.WorkflowType ?? "OVERSTAY_ENFORCEMENT",
            ZoneId = req.ZoneId,
            SessionId = req.SessionId,
            PlanJson = req.PlanJson ?? "{}",
            CurrentStep = req.CurrentStep ?? string.Empty,
            StepResultsJson = req.StepResultsJson ?? "{}",
            InputPayloadJson = req.InputPayloadJson ?? "{}",
            ExecutionSummaryJson = req.ExecutionSummaryJson ?? "{}",
            Status = req.Status ?? "RUNNING",
            DecisionReason = req.DecisionReason ?? string.Empty,
            TriggeredAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.AgentWorkflowRuns.Add(run);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetWorkflowById), new { id = run.Id }, WorkflowResponseDto.FromEntity(run));
    }

    /// <summary>
    /// Updates the execution progress, step results, and status of an active workflow.
    /// (design.md §8.2, §8.3)
    /// </summary>
    [HttpPut("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> UpdateWorkflow(Guid id, [FromBody] UpdateWorkflowRequest req)
    {
        if (req == null)
        {
            return BadRequest(new { error = "Update request payload cannot be empty." });
        }

        var run = await db.AgentWorkflowRuns.FindAsync(id);
        if (run == null)
        {
            return NotFound(new { error = $"Workflow with ID '{id}' was not found." });
        }

        if (req.CurrentStep != null) run.CurrentStep = req.CurrentStep;
        if (req.PlanJson != null) run.PlanJson = req.PlanJson;
        if (req.StepResultsJson != null) run.StepResultsJson = req.StepResultsJson;
        if (req.ExecutionSummaryJson != null) run.ExecutionSummaryJson = req.ExecutionSummaryJson;
        if (req.ErrorLogJson != null) run.ErrorLogJson = req.ErrorLogJson;
        if (req.Status != null) run.Status = req.Status;
        if (req.DecisionReason != null) run.DecisionReason = req.DecisionReason;
        run.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return Ok(WorkflowResponseDto.FromEntity(run));
    }

    /// <summary>
    /// Approves an AI workflow proposal, transitioning it from PENDING_APPROVAL to APPROVED.
    /// Records approving user and UTC timestamp.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> ApproveWorkflow(Guid id, [FromQuery] string? approvedBy = null)
    {
        var run = await db.AgentWorkflowRuns.FindAsync(id);
        if (run == null)
        {
            return NotFound(new { error = $"Workflow with ID '{id}' was not found." });
        }

        // Validate state transition — only pending/awaiting workflows can be approved
        if (run.Status != "PENDING_APPROVAL" && run.Status != "AWAITING_APPROVAL")
        {
            return BadRequest(new 
            { 
                error = $"Workflow cannot be approved because its current status is '{run.Status}'. Only workflows in 'PENDING_APPROVAL' status can be approved." 
            });
        }

        var (userId, actorName) = ResolveCurrentActor(approvedBy);

        run.Status = "APPROVED";
        run.ApprovedBy = userId;
        run.ResolvedBy = actorName;
        run.ResolvedAt = DateTime.UtcNow;
        run.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return Ok(new 
        { 
            message = "Workflow proposal approved successfully", 
            run = WorkflowResponseDto.FromEntity(run) 
        });
    }

    /// <summary>
    /// Rejects an AI workflow proposal, transitioning it from PENDING_APPROVAL to REJECTED.
    /// Records rejecting user, rejection reason, and UTC timestamp.
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> RejectWorkflow(Guid id, [FromBody] RejectWorkflowRequest req, [FromQuery] string? rejectedBy = null)
    {
        var run = await db.AgentWorkflowRuns.FindAsync(id);
        if (run == null)
        {
            return NotFound(new { error = $"Workflow with ID '{id}' was not found." });
        }

        // Validate state transition — only pending/awaiting workflows can be rejected
        if (run.Status != "PENDING_APPROVAL" && run.Status != "AWAITING_APPROVAL")
        {
            return BadRequest(new 
            { 
                error = $"Workflow cannot be rejected because its current status is '{run.Status}'. Only workflows in 'PENDING_APPROVAL' status can be rejected." 
            });
        }

        var (userId, actorName) = ResolveCurrentActor(rejectedBy);

        run.Status = "REJECTED";
        run.DecisionReason = req.Reason;
        run.ApprovedBy = userId;
        run.ResolvedBy = actorName;
        run.ResolvedAt = DateTime.UtcNow;
        run.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return Ok(new 
        { 
            message = "Workflow proposal rejected", 
            run = WorkflowResponseDto.FromEntity(run) 
        });
    }

    private (Guid? userId, string actorName) ResolveCurrentActor(string? fallback)
    {
        Guid? userId = null;
        var subClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? User.FindFirst("sub")?.Value
                       ?? User.FindFirst("userId")?.Value;

        if (!string.IsNullOrEmpty(subClaim) && Guid.TryParse(subClaim, out var parsedGuid))
        {
            userId = parsedGuid;
        }

        var actorName = User.FindFirst(ClaimTypes.Email)?.Value
                        ?? User.FindFirst(ClaimTypes.Name)?.Value
                        ?? User.Identity?.Name;

        if (string.IsNullOrWhiteSpace(actorName))
        {
            actorName = !string.IsNullOrWhiteSpace(fallback) ? fallback : "Admin";
        }

        return (userId, actorName);
    }
}

public class RejectWorkflowRequest
{
    public string Reason { get; set; } = string.Empty;
}

// Backward compatibility alias for existing callers
public class RejectRequest : RejectWorkflowRequest
{
}

public class CreateWorkflowRequest
{
    public Guid? Id { get; set; }
    public string? Objective { get; set; }
    public string? WorkflowType { get; set; }
    public Guid? ZoneId { get; set; }
    public Guid? SessionId { get; set; }
    public string? PlanJson { get; set; }
    public string? CurrentStep { get; set; }
    public string? StepResultsJson { get; set; }
    public string? InputPayloadJson { get; set; }
    public string? ExecutionSummaryJson { get; set; }
    public string? Status { get; set; }
    public string? DecisionReason { get; set; }
}

public class UpdateWorkflowRequest
{
    public string? CurrentStep { get; set; }
    public string? PlanJson { get; set; }
    public string? StepResultsJson { get; set; }
    public string? ExecutionSummaryJson { get; set; }
    public string? ErrorLogJson { get; set; }
    public string? Status { get; set; }
    public string? DecisionReason { get; set; }
}

public class WorkflowResponseDto
{
    public Guid Id { get; set; }
    public string Objective { get; set; } = string.Empty;
    public string WorkflowType { get; set; } = string.Empty;
    public Guid? ZoneId { get; set; }
    public Guid? SessionId { get; set; }
    public string PlanJson { get; set; } = "{}";
    public string CurrentStep { get; set; } = string.Empty;
    public string StepResultsJson { get; set; } = "{}";
    public string InputPayloadJson { get; set; } = "{}";
    public string ExecutionSummaryJson { get; set; } = "{}";
    public string Status { get; set; } = string.Empty;
    public string DecisionReason { get; set; } = string.Empty;
    public Guid? ApprovedBy { get; set; }
    public string? ResolvedBy { get; set; }
    public DateTime TriggeredAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public static WorkflowResponseDto FromEntity(AgentWorkflowRun run) => new()
    {
        Id = run.Id,
        Objective = run.Objective,
        WorkflowType = run.WorkflowType,
        ZoneId = run.ZoneId,
        SessionId = run.SessionId,
        PlanJson = run.PlanJson,
        CurrentStep = run.CurrentStep,
        StepResultsJson = run.StepResultsJson,
        InputPayloadJson = run.InputPayloadJson,
        ExecutionSummaryJson = run.ExecutionSummaryJson,
        Status = run.Status,
        DecisionReason = run.DecisionReason,
        ApprovedBy = run.ApprovedBy,
        ResolvedBy = run.ResolvedBy,
        TriggeredAt = run.TriggeredAt,
        ResolvedAt = run.ResolvedAt,
        CreatedAt = run.CreatedAt,
        UpdatedAt = run.UpdatedAt
    };
}
