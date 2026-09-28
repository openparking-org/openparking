import json
import logging
from decimal import Decimal
from datetime import datetime, timezone
from enum import Enum
from typing import Dict, Any, TypedDict, Optional, List
from pydantic import BaseModel, Field

from agents.validator import ValidatorAgent
from agents.analyzer import AnalyzerAgent
from agents.action import ActionAgent
from tools.workflow_persistence import persist_workflow, update_workflow_progress, fetch_workflow
from langgraph.graph import StateGraph, START, END

logger = logging.getLogger("planner_agent")

class StepStatus(str, Enum):
    PENDING = "PENDING"
    RUNNING = "RUNNING"
    COMPLETED = "COMPLETED"
    FAILED = "FAILED"
    SKIPPED = "SKIPPED"
    AWAITING_APPROVAL = "AWAITING_APPROVAL"

class PlanStep(BaseModel):
    step_id: str
    agent: str                                 # ANALYZER | ACTION | VALIDATOR | PLANNER
    action: str                                # Method / operation identifier
    description: str
    requires_validation: bool = False
    requires_approval: bool = False
    status: StepStatus = StepStatus.PENDING
    input_parameters: Dict[str, Any] = Field(default_factory=dict)
    output: Optional[Dict[str, Any]] = None
    error: Optional[str] = None

class ExecutionPlan(BaseModel):
    workflow_id: str
    workflow_type: str                         # OVERSTAY_ENFORCEMENT | DYNAMIC_PRICING | PERMIT_VALIDATION
    objective: str
    steps: List[PlanStep]
    created_at: str = Field(default_factory=lambda: datetime.now(timezone.utc).isoformat())

class WorkflowState(TypedDict):
    workflow_id: str
    workflow_type: str                         # DYNAMIC_PRICING | OVERSTAY_ENFORCEMENT | PERMIT_VALIDATION
    zone_id: Optional[str]
    session_id: Optional[str]
    input_data: Dict[str, Any]
    analysis: Optional[Dict[str, Any]]
    action_proposal: Optional[Dict[str, Any]]
    validation: Optional[Dict[str, Any]]
    final_decision: Optional[str]              # PENDING_APPROVAL | AWAITING_APPROVAL | AUTO_APPROVED | APPROVED | REJECTED | COMPLETED
    reason: Optional[str]
    plan: Optional[Dict[str, Any]]
    current_step: Optional[str]
    step_results: Optional[Dict[str, Any]]
    status: Optional[str]

class PlannerAgent:
    """
    Student 4 Ownership: Enforcement & AI Orchestration slice.
    Coordinates the multi-agent workflow state machine, routing through Analyzer,
    Action, Validator, generating structured ExecutionPlans, tracking step execution,
    and enforcing human-in-the-loop checkpoints before sensitive side-effects.
    (design.md §8.1, §8.3)
    """
    def __init__(self) -> None:
        self.validator = ValidatorAgent()
        self.analyzer = AnalyzerAgent()
        self.action = ActionAgent()
        self.graph = self._build_graph()

    def _build_graph(self):
        """Builds the LangGraph orchestration state machine."""
        workflow = StateGraph(WorkflowState)

        # We map our sub-agents to nodes in the graph
        workflow.add_node("ANALYZER", self._node_analyzer)
        workflow.add_node("ACTION", self._node_action)
        workflow.add_node("VALIDATOR", self._node_validator)
        workflow.add_node("PLANNER", self._node_planner_checkpoint)

        # Conditional routing based on the current step's agent assignment
        workflow.add_conditional_edges(START, self._route_step)
        workflow.add_conditional_edges("ANALYZER", self._route_step)
        workflow.add_conditional_edges("ACTION", self._route_step)
        workflow.add_conditional_edges("VALIDATOR", self._route_step)
        workflow.add_conditional_edges("PLANNER", self._route_step)

        # Compile with a memory saver (or persist manually as we do)
        # We interrupt execution before the Action step if it requires approval
        return workflow.compile(interrupt_before=["ACTION"])

    def _route_step(self, state: WorkflowState) -> str:
        """Determines which agent node to run next based on the plan."""
        plan_dict = state.get("plan")
        if not plan_dict or "steps" not in plan_dict:
            return END
            
        plan = ExecutionPlan(**plan_dict)
        
        for step in plan.steps:
            if step.status in (StepStatus.PENDING, StepStatus.AWAITING_APPROVAL):
                # If it's awaiting approval, but we are running routing, it means we paused.
                # If we resumed and it's still awaiting, we should route it to Action or planner to finalize?
                # Actually, our manual loop handled this nicely. We'll adapt it.
                return step.agent
                
        return END

    async def _node_analyzer(self, state: WorkflowState) -> WorkflowState:
        return await self._process_current_step(state, "ANALYZER")

    async def _node_action(self, state: WorkflowState) -> WorkflowState:
        return await self._process_current_step(state, "ACTION")

    async def _node_validator(self, state: WorkflowState) -> WorkflowState:
        return await self._process_current_step(state, "VALIDATOR")

    async def _node_planner_checkpoint(self, state: WorkflowState) -> WorkflowState:
        return await self._process_current_step(state, "PLANNER")

    async def _process_current_step(self, state: WorkflowState, expected_agent: str) -> WorkflowState:
        """Executes the current pending step for the designated agent."""
        plan_dict = state.get("plan")
        plan = ExecutionPlan(**plan_dict)
        
        for step in plan.steps:
            if step.status == StepStatus.PENDING and step.agent == expected_agent:
                step.status = StepStatus.RUNNING
                state["current_step"] = step.step_id
                
                try:
                    output = await self._dispatch_step(step, state, plan)
                    step.output = output
                    step.status = StepStatus.COMPLETED
                    state["step_results"][step.step_id] = output
                    
                    if step.action == "check_approval_gate":
                        if output.get("requires_human_approval", False):
                            state["final_decision"] = "PENDING_APPROVAL"
                            state["status"] = "PENDING_APPROVAL"
                            state["reason"] = output.get("reason")
                        else:
                            state["final_decision"] = "AUTO_APPROVED"
                            state["status"] = "AUTO_APPROVED"
                            
                    state["plan"] = plan.model_dump()
                except Exception as e:
                    logger.error(f"Error in step {step.step_id}: {e}")
                    step.status = StepStatus.FAILED
                    state["status"] = "FAILED"
                    state["plan"] = plan.model_dump()
                break
                
        return state

    def create_plan(
        self,
        workflow_id: str,
        workflow_type: str,
        objective: str,
        input_data: Dict[str, Any]
    ) -> ExecutionPlan:
        """
        Decomposes domain objective into structured, ordered PlanStep items.
        (PART 2 — STRUCTURED PLAN)
        """
        steps: List[PlanStep] = []

        if workflow_type == "OVERSTAY_ENFORCEMENT":
            overstay_mins = input_data.get("overstay_minutes", 0)
            hourly_penalty = input_data.get("base_penalty_per_hour", "25.00")

            steps.append(PlanStep(
                step_id="step_1_propose_penalty",
                agent="ACTION",
                action="propose_overstay_penalty",
                description="Calculate billable hours and propose overstay penalty amount",
                input_parameters={"overstay_minutes": overstay_mins, "base_penalty_per_hour": hourly_penalty, "is_vip": input_data.get("is_vip", False), "first_offense": input_data.get("first_offense", True)}
            ))

            steps.append(PlanStep(
                step_id="step_2_validate_cap",
                agent="VALIDATOR",
                action="validate_penalty_cap",
                description="Validate proposed penalty against regulatory system cap",
                requires_validation=True,
                input_parameters={}
            ))

            steps.append(PlanStep(
                step_id="step_3_approval_checkpoint",
                agent="PLANNER",
                action="check_approval_gate",
                description="Evaluate if penalty requires human-in-the-loop admin validation",
                input_parameters={}
            ))

            steps.append(PlanStep(
                step_id="step_4_apply_penalty",
                agent="ACTION",
                action="apply_penalty_enforcement",
                description="Finalize penalty record in persistence store and trigger notifications",
                requires_approval=True,
                input_parameters={}
            ))

        elif workflow_type == "DYNAMIC_PRICING":
            total = input_data.get("total_slots", 100)
            occupied = input_data.get("occupied_slots", 80)
            arrivals = input_data.get("recent_arrivals", 15)
            base_rate = input_data.get("base_hourly_rate", "5.00")

            steps.append(PlanStep(
                step_id="step_1_analyze_occupancy",
                agent="ANALYZER",
                action="analyze_zone_occupancy",
                description="Analyze zone occupancy percentage and traffic velocity score",
                input_parameters={"total_slots": total, "occupied_slots": occupied, "recent_arrivals": arrivals}
            ))

            steps.append(PlanStep(
                step_id="step_2_propose_pricing",
                agent="ACTION",
                action="propose_dynamic_pricing",
                description="Propose dynamic surge pricing multiplier based on congestion level",
                input_parameters={"base_rate": base_rate}
            ))

            steps.append(PlanStep(
                step_id="step_3_approval_checkpoint",
                agent="PLANNER",
                action="check_approval_gate",
                description="Verify if surge multiplier requires administrator authorization",
                input_parameters={}
            ))

            steps.append(PlanStep(
                step_id="step_4_apply_pricing",
                agent="ACTION",
                action="apply_pricing_rule",
                description="Persist active zone pricing surge multiplier in database",
                requires_approval=True,
                input_parameters={}
            ))

        elif workflow_type == "PERMIT_VALIDATION":
            steps.append(PlanStep(
                step_id="step_1_validate_permit",
                agent="VALIDATOR",
                action="validate_permit",
                description="Verify disability permit credential and issuing authority",
                requires_validation=True,
                input_parameters=input_data
            ))

            steps.append(PlanStep(
                step_id="step_2_approval_checkpoint",
                agent="PLANNER",
                action="check_approval_gate",
                description="Check permit AI confidence score against auto-approval threshold",
                input_parameters={}
            ))

            steps.append(PlanStep(
                step_id="step_3_apply_permit",
                agent="ACTION",
                action="apply_permit_status",
                description="Update permit approval record and allocate accessible privileges",
                requires_approval=True,
                input_parameters={}
            ))
        else:
            raise ValueError(f"Unsupported workflow type: {workflow_type}")

        return ExecutionPlan(
            workflow_id=workflow_id,
            workflow_type=workflow_type,
            objective=objective,
            steps=steps
        )

    async def execute_plan(
        self,
        plan: ExecutionPlan,
        state: Optional[WorkflowState] = None
    ) -> WorkflowState:
        """
        Orchestrates execution using LangGraph StateGraph engine.
        (PART 3 — AGENT ORCHESTRATION & PART 5 — HUMAN APPROVAL)
        """
        if state is None:
            state = {
                "workflow_id": plan.workflow_id,
                "workflow_type": plan.workflow_type,
                "zone_id": None,
                "session_id": None,
                "input_data": {},
                "analysis": None,
                "action_proposal": None,
                "validation": None,
                "final_decision": None,
                "reason": None,
                "plan": plan.model_dump(),
                "current_step": None,
                "step_results": {},
                "status": "RUNNING"
            }
        else:
            state["plan"] = plan.model_dump()
            state["step_results"] = state.get("step_results") or {}
            state["status"] = state.get("status") or "RUNNING"

        # Check for pause condition (simulating LangGraph interrupt)
        for step in plan.steps:
            if step.requires_approval and step.status == StepStatus.PENDING and state.get("status") not in ("APPROVED", "AUTO_APPROVED"):
                step.status = StepStatus.AWAITING_APPROVAL
                state["current_step"] = step.step_id
                state["final_decision"] = "PENDING_APPROVAL"
                state["status"] = "PENDING_APPROVAL"
                state["plan"] = plan.model_dump()
                await update_workflow_progress(plan.workflow_id, state)
                return state

        # Run the graph
        result = await self.graph.ainvoke(state)
        
        # Persist final state
        if result.get("status") not in ("FAILED", "PENDING_APPROVAL", "REJECTED"):
            result["status"] = "COMPLETED"
            result["final_decision"] = "COMPLETED"
            
        await update_workflow_progress(plan.workflow_id, result)
        return result

    async def resume_workflow(
        self,
        workflow_id: str,
        decision: str = "APPROVE",
        approved_by: Optional[str] = None,
        reason: Optional[str] = None,
        plan: Optional[ExecutionPlan] = None
    ) -> WorkflowState:
        """
        Resumes a paused workflow run after human administrator approval/rejection.
        (PART 6 — RESUME AFTER APPROVAL)
        """
        workflow_record = await fetch_workflow(workflow_id)
        if not workflow_record:
            raise ValueError(f"Workflow '{workflow_id}' not found for resumption.")

        if plan is None:
            raw_plan = workflow_record.get("plan")
            if isinstance(raw_plan, str):
                try:
                    raw_plan = json.loads(raw_plan)
                except Exception:
                    raw_plan = {}

            if not raw_plan or "steps" not in raw_plan:
                raise ValueError(f"Cannot resume workflow '{workflow_id}': persisted plan is missing or malformed.")

            plan = ExecutionPlan(**raw_plan)

        state: WorkflowState = {
            "workflow_id": workflow_id,
            "workflow_type": workflow_record.get("workflowType") or workflow_record.get("workflow_type", "OVERSTAY_ENFORCEMENT"),
            "zone_id": workflow_record.get("zoneId") or workflow_record.get("zone_id"),
            "session_id": workflow_record.get("sessionId") or workflow_record.get("session_id"),
            "input_data": workflow_record.get("inputPayload") or workflow_record.get("input_data") or {},
            "analysis": None,
            "action_proposal": None,
            "validation": None,
            "final_decision": None,
            "reason": reason,
            "plan": plan.model_dump(),
            "current_step": workflow_record.get("currentStep") or workflow_record.get("current_step"),
            "step_results": workflow_record.get("stepResults") or workflow_record.get("step_results") or {},
            "status": workflow_record.get("status", "PENDING_APPROVAL")
        }

        # Handle Admin Rejection
        if decision.upper() == "REJECT" or state["status"] == "REJECTED":
            state["status"] = "REJECTED"
            state["final_decision"] = "REJECTED"
            state["reason"] = reason or "Workflow proposal rejected by administrator"

            for step in plan.steps:
                if step.status != StepStatus.COMPLETED:
                    step.status = StepStatus.SKIPPED

            await update_workflow_progress(workflow_id, {
                "status": "REJECTED",
                "plan": plan.model_dump(),
                "reason": state["reason"]
            })
            logger.info(f"Workflow {workflow_id} rejected. Remaining steps skipped.")
            return state

        # Handle Admin Approval: resume remaining steps
        state["status"] = "APPROVED"
        state["final_decision"] = "APPROVED"

        logger.info(f"Resuming workflow {workflow_id} after administrator approval by {approved_by or 'Admin'}")
        return await self.execute_plan(plan, state)

    async def _dispatch_step(
        self,
        step: PlanStep,
        state: WorkflowState,
        plan: ExecutionPlan
    ) -> Dict[str, Any]:
        """Dispatches an individual plan step to the responsible sub-agent."""
        if step.agent == "ACTION":
            if step.action == "propose_overstay_penalty":
                overstay_mins = int(step.input_parameters.get("overstay_minutes", 0))
                hourly_rate = Decimal(str(step.input_parameters.get("base_penalty_per_hour", "25.00")))
                res = await self.action.propose_overstay_penalty(
                    overstay_minutes=overstay_mins, 
                    base_penalty_per_hour=hourly_rate, 
                    context_data=step.input_parameters
                )
                state["action_proposal"] = res
                return res

            elif step.action == "propose_dynamic_pricing":
                base_rate = Decimal(str(step.input_parameters.get("base_rate", "5.00")))
                analysis = state.get("analysis") or {}
                congestion = analysis.get("congestion_level", "LOW")
                velocity = analysis.get("velocity_score", 0.0)
                res = await self.action.propose_dynamic_pricing(base_rate, congestion, velocity)
                state["action_proposal"] = res
                return res

            elif step.action in ("apply_penalty_enforcement", "apply_pricing_rule", "apply_permit_status"):
                # Side-effect execution
                return {
                    "applied": True,
                    "action": step.action,
                    "timestamp": datetime.now(timezone.utc).isoformat(),
                    "summary": f"Executed action {step.action} after authorization check"
                }

        elif step.agent == "VALIDATOR":
            if step.action == "validate_penalty_cap":
                proposal = state.get("action_proposal") or {}
                proposed_amount = Decimal(str(proposal.get("proposed_amount", "0.00")))
                res = await self.validator.validate_penalty_cap(proposed_amount)
                state["validation"] = res
                return res

            elif step.action == "validate_permit":
                res = await self.validator.validate_permit(step.input_parameters)
                state["validation"] = res
                return res

        elif step.agent == "ANALYZER":
            if step.action == "analyze_zone_occupancy":
                total = int(step.input_parameters.get("total_slots", 100))
                occupied = int(step.input_parameters.get("occupied_slots", 0))
                arrivals = int(step.input_parameters.get("recent_arrivals", 0))
                res = await self.analyzer.analyze_zone_occupancy(total, occupied, arrivals)
                state["analysis"] = res
                return res

        elif step.agent == "PLANNER":
            if step.action == "check_approval_gate":
                # Evaluate whether workflow requires human approval
                if plan.workflow_type == "OVERSTAY_ENFORCEMENT":
                    validation = state.get("validation") or {}
                    proposal = state.get("action_proposal") or {}
                    proposed_amount = float(proposal.get("proposed_amount", 0.0))

                    if not validation.get("valid", True):
                        return {
                            "requires_human_approval": True,
                            "reason": f"Requires manual review: {validation.get('reason')}"
                        }
                    elif proposed_amount > 100.0:
                        return {
                            "requires_human_approval": True,
                            "reason": "Penalty exceeds auto-approval threshold ($100.00)"
                        }
                    else:
                        return {
                            "requires_human_approval": False,
                            "reason": "Within standard automatic compliance parameters"
                        }

                elif plan.workflow_type == "DYNAMIC_PRICING":
                    analysis = state.get("analysis") or {}
                    proposal = state.get("action_proposal") or {}
                    if analysis.get("requires_surge_pricing", False):
                        return {
                            "requires_human_approval": True,
                            "reason": f"Surge rate proposed ({proposal.get('multiplier', 1.0)}x). Requires Admin approval."
                        }
                    else:
                        return {
                            "requires_human_approval": False,
                            "reason": "Normal operating rates maintained"
                        }

                elif plan.workflow_type == "PERMIT_VALIDATION":
                    validation = state.get("validation") or {}
                    confidence = float(validation.get("confidence", 0.0))
                    if confidence < 0.90 or not validation.get("valid", False):
                        return {
                            "requires_human_approval": True,
                            "reason": f"Permit confidence {confidence:.2f} below threshold (0.90)"
                        }
                    else:
                        return {
                            "requires_human_approval": False,
                            "reason": "Permit verified with high AI confidence"
                        }

        return {"status": "executed", "step_id": step.step_id}

    # -------------------------------------------------------------------
    # Backward Compatibility Methods for Existing Tests and Slices
    # -------------------------------------------------------------------
    async def execute_overstay_flow(self, state: WorkflowState) -> WorkflowState:
        data = state.get("input_data", {})
        plan = self.create_plan(
            workflow_id=state.get("workflow_id", "wf-overstay"),
            workflow_type="OVERSTAY_ENFORCEMENT",
            objective="Evaluate vehicle overstay duration and enforce penalty",
            input_data=data
        )
        return await self.execute_plan(plan, state)

    async def execute_pricing_flow(self, state: WorkflowState) -> WorkflowState:
        data = state.get("input_data", {})
        plan = self.create_plan(
            workflow_id=state.get("workflow_id", "wf-pricing"),
            workflow_type="DYNAMIC_PRICING",
            objective="Analyze occupancy velocity and determine dynamic surge pricing",
            input_data=data
        )
        return await self.execute_plan(plan, state)
