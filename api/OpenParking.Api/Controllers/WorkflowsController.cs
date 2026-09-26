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
