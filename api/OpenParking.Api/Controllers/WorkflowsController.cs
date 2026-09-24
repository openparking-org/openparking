using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenParking.Core.Entities;
using OpenParking.Infrastructure.Data;

namespace OpenParking.Api.Controllers;

[ApiController]
[Route("api/agent/[controller]")]
public class WorkflowsController(AppDbContext db) : ControllerBase
{
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingWorkflows()
    {
        var pending = await db.AgentWorkflowRuns
            .Where(w => w.Status == "PENDING_APPROVAL")
            .OrderByDescending(w => w.TriggeredAt)
            .ToListAsync();

        return Ok(pending);
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> ApproveWorkflow(Guid id, [FromQuery] string approvedBy = "Admin")
    {
        var run = await db.AgentWorkflowRuns.FindAsync(id);
        if (run == null)
            return NotFound();

        run.Status = "APPROVED";
        run.ResolvedAt = DateTime.UtcNow;
        run.ResolvedBy = approvedBy;
        await db.SaveChangesAsync();

        return Ok(new { message = "Workflow proposal approved successfully", run });
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> RejectWorkflow(Guid id, [FromBody] RejectRequest req, [FromQuery] string rejectedBy = "Admin")
    {
        var run = await db.AgentWorkflowRuns.FindAsync(id);
        if (run == null)
            return NotFound();

        run.Status = "REJECTED";
        run.DecisionReason = req.Reason;
        run.ResolvedAt = DateTime.UtcNow;
        run.ResolvedBy = rejectedBy;
        await db.SaveChangesAsync();

        return Ok(new { message = "Workflow proposal rejected", run });
    }
}

public class RejectRequest
{
    public string Reason { get; set; } = string.Empty;
}
