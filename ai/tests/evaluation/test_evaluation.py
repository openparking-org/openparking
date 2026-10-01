import pytest

from agents.planner import PlannerAgent, WorkflowState


@pytest.mark.asyncio
async def test_golden_evaluation_flow():
    """
    Mandatory acceptance workflow test required by the specification:
    Tests dynamic pricing proposal under peak load and verifies human approval gating.
    """
    planner = PlannerAgent()
    state: WorkflowState = { # type: ignore[typeddict-item]
        "workflow_id": "eval-wf-001",
        "workflow_type": "DYNAMIC_PRICING",
        "zone_id": "zone-cbd-central",
        "session_id": None,
        "input_data": {
            "total_slots": 200,
            "occupied_slots": 190, # 95% occupancy
            "recent_arrivals": 25,
            "base_hourly_rate": "6.00"
        },
        "analysis": None,
        "action_proposal": None,
        "validation": None,
        "final_decision": None,
        "reason": None
    }
    result = await planner.execute_pricing_flow(state)
    assert result["analysis"]["congestion_level"] == "CRITICAL"
    assert result["final_decision"] == "PENDING_APPROVAL"
    assert result["action_proposal"]["multiplier"] >= 2.0
