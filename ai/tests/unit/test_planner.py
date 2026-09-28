import pytest

from agents.planner import PlannerAgent, StepStatus
from tools.workflow_persistence import clear_local_workflows, get_local_workflow


@pytest.fixture(autouse=True)
def clean_workflow_store():
    clear_local_workflows()

@pytest.mark.asyncio
async def test_planner_creates_structured_plan_for_overstay():
    planner = PlannerAgent()
    plan = planner.create_plan(
        workflow_id="wf-plan-01",
        workflow_type="OVERSTAY_ENFORCEMENT",
        objective="Assess overstay penalty for vehicle sess-001",
        input_data={"overstay_minutes": 45, "base_penalty_per_hour": "25.00"}
    )

    assert plan.workflow_id == "wf-plan-01"
    assert plan.workflow_type == "OVERSTAY_ENFORCEMENT"
    assert len(plan.steps) == 4

    step_agents = [s.agent for s in plan.steps]
    assert step_agents == ["ACTION", "VALIDATOR", "PLANNER", "ACTION"]

    # Step 4 (apply) must require human approval
    assert plan.steps[3].requires_approval is True
    assert plan.steps[3].action == "apply_penalty_enforcement"

@pytest.mark.asyncio
async def test_planner_creates_structured_plan_for_pricing():
    planner = PlannerAgent()
    plan = planner.create_plan(
        workflow_id="wf-plan-02",
        workflow_type="DYNAMIC_PRICING",
        objective="Evaluate surge pricing for Zone B",
        input_data={"total_slots": 100, "occupied_slots": 85, "recent_arrivals": 18}
    )

    assert len(plan.steps) == 4
    step_agents = [s.agent for s in plan.steps]
    assert step_agents == ["ANALYZER", "ACTION", "PLANNER", "ACTION"]
    assert plan.steps[3].requires_approval is True

@pytest.mark.asyncio
async def test_planner_raises_error_for_unknown_workflow_type():
    planner = PlannerAgent()
    with pytest.raises(ValueError, match="Unsupported workflow type"):
        planner.create_plan(
            workflow_id="wf-unknown",
            workflow_type="INVALID_TYPE",
            objective="Do unknown action",
            input_data={}
        )

@pytest.mark.asyncio
async def test_approval_required_workflow_pauses_before_sensitive_action():
    """
    Critical requirement: When a penalty exceeds threshold ($100.00),
    the workflow MUST pause at AWAITING_APPROVAL/PENDING_APPROVAL,
    and the final side-effect step (apply_penalty) MUST NOT execute.
    """
    planner = PlannerAgent()
    plan = planner.create_plan(
        workflow_id="wf-pause-01",
        workflow_type="OVERSTAY_ENFORCEMENT",
        objective="Overstay enforcement for 300 minutes",
        input_data={"overstay_minutes": 300, "base_penalty_per_hour": "25.00"} # 5 hrs = $125 > $100
    )

    state = await planner.execute_plan(plan)

    # State must be paused
    assert state["status"] == "PENDING_APPROVAL"
    assert state["final_decision"] == "PENDING_APPROVAL"
    assert "Penalty exceeds auto-approval threshold" in state["reason"]

    # Step 1, 2, 3 completed; Step 4 NOT executed!
    assert plan.steps[0].status == StepStatus.COMPLETED
    assert plan.steps[1].status == StepStatus.COMPLETED
    assert plan.steps[2].status == StepStatus.COMPLETED
    assert plan.steps[3].status == StepStatus.AWAITING_APPROVAL
    assert plan.steps[3].output is None

    # Verify state was persisted in storage
    stored = get_local_workflow("wf-pause-01")
    assert stored is not None
    assert stored["status"] == "PENDING_APPROVAL"

@pytest.mark.asyncio
async def test_approved_workflow_resumes_and_completes():
    """
    Tests resuming an approved workflow: it should execute remaining steps
    without repeating completed ones.
    """
    planner = PlannerAgent()
    plan = planner.create_plan(
        workflow_id="wf-resume-01",
        workflow_type="OVERSTAY_ENFORCEMENT",
        objective="Overstay enforcement for 300 minutes",
        input_data={"overstay_minutes": 300, "base_penalty_per_hour": "25.00"}
    )

    # 1. Execute initial steps (pauses at approval gate)
    await planner.execute_plan(plan)
    assert plan.steps[3].status == StepStatus.AWAITING_APPROVAL

    # 2. Resume with admin approval
    resumed_state = await planner.resume_workflow(
        workflow_id="wf-resume-01",
        decision="APPROVE",
        approved_by="admin@openparking.org",
        reason="Approved by operations manager",
        plan=plan
    )

    # State must be COMPLETED
    assert resumed_state["status"] == "COMPLETED"
    assert resumed_state["final_decision"] == "COMPLETED"

    # Step 4 must now be completed
    assert plan.steps[3].status == StepStatus.COMPLETED
    assert plan.steps[3].output is not None
    assert plan.steps[3].output["applied"] is True

@pytest.mark.asyncio
async def test_rejected_workflow_does_not_execute_remaining_steps():
    """
    Tests rejecting a workflow: remaining steps must be SKIPPED,
    and sensitive actions must NOT execute.
    """
    planner = PlannerAgent()
    plan = planner.create_plan(
        workflow_id="wf-reject-01",
        workflow_type="OVERSTAY_ENFORCEMENT",
        objective="Overstay enforcement for 300 minutes",
        input_data={"overstay_minutes": 300, "base_penalty_per_hour": "25.00"}
    )

    await planner.execute_plan(plan)

    resumed_state = await planner.resume_workflow(
        workflow_id="wf-reject-01",
        decision="REJECT",
        approved_by="admin@openparking.org",
        reason="Driver had medical emergency exemption",
        plan=plan
    )

    assert resumed_state["status"] == "REJECTED"
    assert resumed_state["final_decision"] == "REJECTED"

    # Step 4 must be SKIPPED, NOT executed
    assert plan.steps[3].status == StepStatus.SKIPPED
    assert plan.steps[3].output is None

@pytest.mark.asyncio
async def test_idempotency_prevents_duplicate_step_execution():
    """
    Idempotency: Re-running execute_plan on an already-completed plan
    does not re-execute completed steps.
    """
    planner = PlannerAgent()
    plan = planner.create_plan(
        workflow_id="wf-idem-01",
        workflow_type="OVERSTAY_ENFORCEMENT",
        objective="Short overstay",
        input_data={"overstay_minutes": 20, "base_penalty_per_hour": "25.00"}
    )

    # Run initially (under $100 -> auto-approved)
    state1 = await planner.execute_plan(plan)
    assert plan.steps[0].status == StepStatus.COMPLETED
    output1 = plan.steps[0].output

    # Mark step 0 with a sentinel to verify it is NOT overwritten
    plan.steps[0].output = {**output1, "sentinel": "untouched"}

    # Re-run
    await planner.execute_plan(plan, state1)
    assert plan.steps[0].output.get("sentinel") == "untouched"

@pytest.mark.asyncio
async def test_step_failure_produces_controlled_error_state():
    """
    Tests error handling: an unexpected failure inside a step marks
    the workflow as FAILED without raising an unhandled exception.
    """
    planner = PlannerAgent()
    plan = planner.create_plan(
        workflow_id="wf-err-01",
        workflow_type="OVERSTAY_ENFORCEMENT",
        objective="Invalid inputs test",
        input_data={"overstay_minutes": "invalid-non-int", "base_penalty_per_hour": "25.00"}
    )

    state = await planner.execute_plan(plan)

    assert state["status"] == "FAILED"
    assert state["final_decision"] == "FAILED"
    assert "Execution error in step" in state["reason"]
    assert plan.steps[0].status == StepStatus.FAILED
    assert plan.steps[0].error is not None
