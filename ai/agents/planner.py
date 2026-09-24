from decimal import Decimal
from typing import Dict, Any, TypedDict, Optional
from agents.validator import ValidatorAgent
from agents.analyzer import AnalyzerAgent
from agents.action import ActionAgent

class WorkflowState(TypedDict):
    workflow_id: str
    workflow_type: str # DYNAMIC_PRICING | OVERSTAY_ENFORCEMENT | PERMIT_VALIDATION
    zone_id: Optional[str]
    session_id: Optional[str]
    input_data: Dict[str, Any]
    analysis: Optional[Dict[str, Any]]
    action_proposal: Optional[Dict[str, Any]]
    validation: Optional[Dict[str, Any]]
    final_decision: Optional[str] # PENDING_APPROVAL | AUTO_APPROVED | REJECTED
    reason: Optional[str]

class PlannerAgent:
    """
    Student 4 Ownership: Enforcement & AI Orchestration slice.
    Coordinates the multi-agent workflow state machine, routing through Analyzer,
    Action, Validator, and preparing human-in-the-loop checkpoints.
    """
    def __init__(self) -> None:
        self.validator = ValidatorAgent()
        self.analyzer = AnalyzerAgent()
        self.action = ActionAgent()

    async def execute_overstay_flow(self, state: WorkflowState) -> WorkflowState:
        data = state["input_data"]
        overstay_mins = int(data.get("overstay_minutes", 0))
        hourly_penalty = Decimal(str(data.get("base_penalty_per_hour", "25.00")))

        # 1. Action proposal
        proposal = await self.action.propose_overstay_penalty(overstay_mins, hourly_penalty)
        state["action_proposal"] = proposal

        # 2. Validation check against regulatory cap
        validation = await self.validator.validate_penalty_cap(Decimal(str(proposal["proposed_amount"])))
        state["validation"] = validation

        # 3. Decision routing: Cap violation or high amounts require Admin Approval
        if not validation.get("valid", True):
            state["final_decision"] = "PENDING_APPROVAL"
            state["reason"] = f"Requires manual review: {validation.get('reason')}"
        elif proposal["proposed_amount"] > 100.0:
            state["final_decision"] = "PENDING_APPROVAL"
            state["reason"] = "Penalty exceeds auto-approval threshold ($100.00)"
        else:
            state["final_decision"] = "AUTO_APPROVED"
            state["reason"] = "Within standard automatic compliance parameters"

        return state

    async def execute_pricing_flow(self, state: WorkflowState) -> WorkflowState:
        data = state["input_data"]
        total = int(data.get("total_slots", 100))
        occupied = int(data.get("occupied_slots", 80))
        arrivals = int(data.get("recent_arrivals", 15))
        base_rate = Decimal(str(data.get("base_hourly_rate", "5.00")))

        # 1. Analysis
        analysis = await self.analyzer.analyze_zone_occupancy(total, occupied, arrivals)
        state["analysis"] = analysis

        # 2. Action
        proposal = await self.action.propose_dynamic_pricing(
            base_rate=base_rate,
            congestion_level=analysis["congestion_level"],
            velocity_score=analysis["velocity_score"]
        )
        state["action_proposal"] = proposal

        # 3. Decision
        if analysis["requires_surge_pricing"]:
            state["final_decision"] = "PENDING_APPROVAL"
            state["reason"] = f"Surge rate proposed ({proposal['multiplier']}x). Requires Admin approval."
        else:
            state["final_decision"] = "AUTO_APPROVED"
            state["reason"] = "Normal operating rates maintained"

        return state
