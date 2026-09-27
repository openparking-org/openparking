using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenParking.Core.Entities;
using OpenParking.Core.Interfaces;
using OpenParking.Core.Models;
using OpenParking.Infrastructure.Data;
using System.Security.Claims;

namespace OpenParking.Api.Controllers;

/// <summary>
/// Manages LangGraph agent workflow runs (design.md §8.3).
/// Admins review pending proposals and click Approve or Reject.
/// </summary>
[ApiController]
[Route("api/agent/workflows")]
[Authorize(Roles = "ParkingAdmin,SystemAdmin")]
public class WorkflowsController(IEnforcementService enforcementService, ILogger<WorkflowsController> logger) : ControllerBase
{
    private string ActorEmail => User.FindFirstValue(ClaimTypes.Email) ?? "Unknown";
    private Guid? ActorId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    // GET /api/agent/workflows/pending — React admin dashboard polls this
    [HttpGet("pending")]
    public async Task<ActionResult<ApiResponse<List<WorkflowSummaryDto>>>> GetPendingWorkflows()
    {
        var pending = await enforcementService.GetPendingWorkflowsAsync();
        
        var dtos = pending.Select(w => new WorkflowSummaryDto
        {
            Id           = w.Id,
            WorkflowType = w.WorkflowType,
            Status       = w.Status.ToString(),
            Objective    = w.Objective,
            ZoneId       = w.ZoneId,
            SessionId    = w.SessionId,
            CreatedAt    = w.CreatedAt
        }).ToList();

        return Ok(ApiResponse<List<WorkflowSummaryDto>>.Ok(dtos, HttpContext.TraceIdentifier));
    }

    // GET /api/agent/workflows/{id} — full detail with step results
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<WorkflowDetailDto>>> GetWorkflow(Guid id)
    {
        var run = await enforcementService.GetWorkflowAsync(id);
        return Ok(ApiResponse<WorkflowDetailDto>.Ok(WorkflowDetailDto.From(run), HttpContext.TraceIdentifier));
    }

    // POST /api/agent/workflows/{id}/approve
    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<ApiResponse<WorkflowDetailDto>>> ApproveWorkflow(Guid id)
    {
        if (ActorId == null)
            throw new AppException(ErrorCodes.Unauthorized, "User context not found.", 401);

        await enforcementService.FinalizeApprovalAsync(id, ActorId.Value);
        logger.LogInformation("Workflow {WorkflowId} approved by {Actor}", id, ActorEmail);

        var run = await enforcementService.GetWorkflowAsync(id);
        return Ok(ApiResponse<WorkflowDetailDto>.Ok(WorkflowDetailDto.From(run), HttpContext.TraceIdentifier));
    }

    // POST /api/agent/workflows/{id}/reject
    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<ApiResponse<WorkflowDetailDto>>> RejectWorkflow(Guid id, [FromBody] RejectWorkflowRequest req)
    {
        if (!ModelState.IsValid)
            throw new AppException(ErrorCodes.ValidationFailed,
                string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

        if (ActorId == null)
            throw new AppException(ErrorCodes.Unauthorized, "User context not found.", 401);

        await enforcementService.FinalizeRejectionAsync(id, req.Reason, ActorId.Value);
        logger.LogInformation("Workflow {WorkflowId} rejected by {Actor}. Reason: {Reason}", id, ActorEmail, req.Reason);

        var run = await enforcementService.GetWorkflowAsync(id);
        return Ok(ApiResponse<WorkflowDetailDto>.Ok(WorkflowDetailDto.From(run), HttpContext.TraceIdentifier));
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────

public class RejectWorkflowRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(1000, MinimumLength = 5,
        ErrorMessage = "Rejection reason must be between 5 and 1000 characters.")]
    public string Reason { get; set; } = string.Empty;
}

public class WorkflowSummaryDto
{
    public Guid Id { get; set; }
    public string WorkflowType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public Guid? ZoneId { get; set; }
    public Guid? SessionId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class WorkflowDetailDto : WorkflowSummaryDto
{
    public string PlanJson { get; set; } = "{}";
    public string StepResultsJson { get; set; } = "{}";
    public string DecisionReason { get; set; } = string.Empty;
    public string? ErrorLog { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedBy { get; set; }

    public static WorkflowDetailDto From(AgentWorkflowRun run) => new()
    {
        Id             = run.Id,
        WorkflowType   = run.WorkflowType,
        Status         = run.Status.ToString(),
        Objective      = run.Objective,
        ZoneId         = run.ZoneId,
        SessionId      = run.SessionId,
        PlanJson       = run.PlanJson,
        StepResultsJson = run.StepResultsJson,
        DecisionReason = run.DecisionReason,
        ErrorLog       = run.ErrorLog,
        CreatedAt      = run.CreatedAt,
        UpdatedAt      = run.UpdatedAt,
        ApprovedAt     = run.ApprovedAt,
        ApprovedBy     = run.ApprovedBy
    };
}
