import pytest
from agents.planner import PlannerAgent, WorkflowState

@pytest.mark.asyncio
async def test_overstay_workflow_auto_approval():
    planner = PlannerAgent()
    state: WorkflowState = {
        "workflow_id": "wf-test-01",
        "workflow_type": "OVERSTAY_ENFORCEMENT",
        "zone_id": "zone-1",
        "session_id": "sess-1",
        "input_data": {
            "overstay_minutes": 30,
            "base_penalty_per_hour": "25.00"
        },
        "analysis": None,
        "action_proposal": None,
        "validation": None,
        "final_decision": None,
        "reason": None
    }
    result = await planner.execute_overstay_flow(state)
    assert result["final_decision"] == "AUTO_APPROVED"
    assert result["action_proposal"]["proposed_amount"] == 25.0

@pytest.mark.asyncio
async def test_overstay_workflow_high_penalty_pending():
    planner = PlannerAgent()
    state: WorkflowState = {
        "workflow_id": "wf-test-02",
        "workflow_type": "OVERSTAY_ENFORCEMENT",
        "zone_id": "zone-1",
        "session_id": "sess-2",
        "input_data": {
            "overstay_minutes": 300, # 5 hours = 5 * 25 = 125 > 100 threshold
            "base_penalty_per_hour": "25.00"
        },
        "analysis": None,
        "action_proposal": None,
        "validation": None,
        "final_decision": None,
        "reason": None
    }
    result = await planner.execute_overstay_flow(state)
    assert result["final_decision"] == "PENDING_APPROVAL"
