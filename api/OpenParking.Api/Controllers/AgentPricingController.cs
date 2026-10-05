using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenParking.Core.Entities;
using OpenParking.Infrastructure.Data;

namespace OpenParking.Api.Controllers;

/// <summary>Narrow service-token API: aggregate evidence and proposals, never admin approval.</summary>
[ApiController]
[Route("api/agent/pricing")]
public class AgentPricingController(AppDbContext db, IConfiguration config) : ControllerBase
{
    private bool Authorized()
    {
        var expected = config["INTERNAL_API_TOKEN"];
        var supplied = Request.Headers["X-Internal-Token"].ToString();
        return !string.IsNullOrEmpty(expected) && !string.IsNullOrEmpty(supplied) &&
            CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(supplied));
    }

    [HttpGet("zones/{id:guid}/demand")]
    public async Task<IActionResult> Demand(Guid id)
    {
        if (!Authorized()) return Unauthorized();
        if (!await db.Zones.AnyAsync(z => z.Id == id)) return NotFound();
        var now = DateTime.UtcNow;
        var since = now.AddMinutes(-15);
        var nextHour = now.AddHours(1);
        var sessions = db.ParkingSessions.AsNoTracking().Where(s => s.Slot != null && s.Slot.ZoneId == id);
        var arrivals = await sessions.CountAsync(s => s.CheckInTime >= since && s.CheckInTime <= now);
        var departures = await sessions.CountAsync(s => s.CheckOutTime >= since && s.CheckOutTime <= now);
        var demand = await db.Bookings.CountAsync(b => b.Slot != null && b.Slot.ZoneId == id &&
            b.Status == BookingStatus.Confirmed && b.StartTime >= now && b.StartTime < nextHour);
        return Ok(new { recent_arrivals = arrivals, recent_departures = departures, reservation_demand = demand });
    }

    [HttpGet("zones/{id:guid}/price-changes")]
    public async Task<IActionResult> PriceChanges(Guid id)
    {
        if (!Authorized()) return Unauthorized();
        if (!await db.Zones.AnyAsync(z => z.Id == id)) return NotFound();
        // Only actual administrator-applied changes count; evaluation/no-op runs do not.
        var latest = await db.AgentWorkflowRuns.AsNoTracking()
            .Where(w => w.ZoneId == id && w.WorkflowType == "SURGE_PRICING" &&
                        w.Status == WorkflowStatus.Approved && w.ApprovedAt != null)
            .Where(w => !w.StepResultsJson.Contains("\"action\"") ||
                        w.StepResultsJson.Contains("\"INCREASE_PRICE\"") || w.StepResultsJson.Contains("\"DECREASE_PRICE\""))
            .OrderByDescending(w => w.ApprovedAt).FirstOrDefaultAsync();
        var last = latest?.ApprovedAt;
        decimal? approvedRate = null;
        if (latest != null)
        {
            using var proposal = JsonDocument.Parse(latest.StepResultsJson);
            if (proposal.RootElement.TryGetProperty("calculated_rate", out var rate) && rate.TryGetDecimal(out var value)) approvedRate = value;
        }
        // Manual zone rate edits are recorded by ZoneService in the same audit store.
        var manual = await db.AuditLogs.AsNoTracking().Where(a => a.EntityType == "Zone" &&
            a.EntityId == id && a.Action == "PriceChanged").MaxAsync(a => (DateTime?)a.CreatedAt);
        if (manual > last || last == null) { last = manual; approvedRate = null; }
        return Ok(new { last_price_change_at = last, last_approved_rate = approvedRate });
    }

    [HttpGet("workflows/{id:guid}")]
    public async Task<IActionResult> Workflow(Guid id)
    {
        if (!Authorized()) return Unauthorized();
        var run = await db.AgentWorkflowRuns.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id && w.WorkflowType == "SURGE_PRICING");
        if (run == null || run.PlanJson == "{}") return NotFound();
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(run.PlanJson)!;
        if (!state.ContainsKey("workflow_type")) return NotFound();
        state["backend_status"] = JsonSerializer.SerializeToElement(run.Status.ToString());
        state["approved_by"] = JsonSerializer.SerializeToElement(run.ApprovedBy);
        state["approved_at"] = JsonSerializer.SerializeToElement(run.ApprovedAt);
        if (run.Status == WorkflowStatus.Approved && run.ApprovedBy != null && run.ApprovedAt != null)
        {
            state["status"] = JsonSerializer.SerializeToElement("APPROVED");
            state["final_decision"] = JsonSerializer.SerializeToElement("APPROVED");
            state["requires_human_approval"] = JsonSerializer.SerializeToElement(false);
            state["applied"] = JsonSerializer.SerializeToElement(
                state["action_proposal"].GetProperty("current_rate").GetDecimal() != state["action_proposal"].GetProperty("proposed_rate").GetDecimal());
        }
        if (run.Status == WorkflowStatus.Rejected)
        {
            state["status"] = JsonSerializer.SerializeToElement("REJECTED");
            state["final_decision"] = JsonSerializer.SerializeToElement("REJECTED");
            state["reason"] = JsonSerializer.SerializeToElement(run.DecisionReason);
        }
        return Ok(state);
    }

    [HttpPut("workflows/{id:guid}")]
    public async Task<IActionResult> SaveProposal(Guid id, [FromBody] JsonElement state)
    {
        if (!Authorized()) return Unauthorized();
        if (state.ValueKind != JsonValueKind.Object ||
            !state.TryGetProperty("workflow_id", out var wf) || wf.ValueKind != JsonValueKind.String || wf.GetString() != id.ToString() ||
            !state.TryGetProperty("zone_id", out var zoneId) || zoneId.ValueKind != JsonValueKind.String || !Guid.TryParse(zoneId.GetString(), out var zone) ||
            !state.TryGetProperty("workflow_type", out var type) || type.ValueKind != JsonValueKind.String || type.GetString() != "DYNAMIC_PRICING" ||
            !state.TryGetProperty("status", out var status) || status.ValueKind != JsonValueKind.String ||
            !state.TryGetProperty("goal", out var goal) || goal.ValueKind != JsonValueKind.String ||
            !state.TryGetProperty("reason", out var reason) || reason.ValueKind != JsonValueKind.String ||
            !state.TryGetProperty("action_proposal", out var proposal) || proposal.ValueKind != JsonValueKind.Object)
            return BadRequest();
        var decision = status.GetString();
        if (decision is not ("PENDING_APPROVAL" or "AUTO_APPROVED" or "REJECTED")) return BadRequest();
        if (decision != "REJECTED" && (!state.TryGetProperty("validation", out var validation) ||
            validation.ValueKind != JsonValueKind.Object || !validation.TryGetProperty("valid", out var valid) || valid.ValueKind != JsonValueKind.True))
            return BadRequest();
        // Only no-change evaluations may finish without administrator approval.
        if (decision == "AUTO_APPROVED" &&
            (!proposal.TryGetProperty("action", out var action) || action.ValueKind != JsonValueKind.String || action.GetString() != "KEEP_PRICE" ||
             !proposal.TryGetProperty("current_rate", out var original) || !original.TryGetDecimal(out var currentRate) ||
             !proposal.TryGetProperty("proposed_rate", out var proposed) || !proposed.TryGetDecimal(out var proposedRate) || currentRate != proposedRate || currentRate < 0))
            return BadRequest();
        if (!await db.Zones.AnyAsync(z => z.Id == zone)) return NotFound();
        var run = await db.AgentWorkflowRuns.FirstOrDefaultAsync(w => w.Id == id);
        if (run != null && (run.WorkflowType != "SURGE_PRICING" || run.ZoneId != zone)) return Conflict();
        // Never overwrite a reviewed or already-submitted proposal with a replay.
        if (run != null && run.Status != WorkflowStatus.Running) return Conflict();
        if (run == null)
        {
            run = new AgentWorkflowRun { Id = id, ZoneId = zone, WorkflowType = "SURGE_PRICING", CreatedBy = "ai-service" };
            db.AgentWorkflowRuns.Add(run);
        }
        run.Objective = state.GetProperty("goal").GetString() ?? "Evaluate zone pricing";
        run.PlanJson = state.GetRawText();
        run.StepResultsJson = state.GetProperty("action_proposal").GetRawText();
        run.DecisionReason = state.GetProperty("reason").GetString() ?? "";
        run.Status = decision == "REJECTED" ? WorkflowStatus.Rejected :
            decision == "AUTO_APPROVED" ? WorkflowStatus.Completed : WorkflowStatus.AwaitingApproval;
        run.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { id, status = run.Status.ToString() });
    }
}
