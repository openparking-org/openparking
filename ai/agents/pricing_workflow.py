"""Bounded LangGraph evidence-gathering loop followed by deterministic policy checks."""

import copy
import json
import logging
import os
from datetime import datetime, timezone
from decimal import Decimal
from typing import TypedDict

from langgraph.graph import END, START, StateGraph

from tools import reasoning
from tools.pricing import (
    PricingHistory,
    PricingPolicy,
    PricingTools,
    ZoneObservation,
    calculate_dynamic_rate,
)
from tools.workflow_persistence import get_local_workflow, update_workflow_progress

logger = logging.getLogger("pricing_workflow")


class PricingState(TypedDict, total=False):
    workflow_id: str
    workflow_type: str
    zone_id: str | None
    session_id: str | None
    goal: str
    input_data: dict
    plan: dict
    observations: dict
    tool_results: dict
    analysis: dict
    action_proposal: dict
    proposed_actions: list
    validation: dict
    validation_results: list
    final_decision: str
    reason: str
    confidence: float
    requires_human_approval: bool
    status: str
    current_step: str
    step_results: dict
    iterations: int
    next_tool: str | None
    trace: list
    errors: list
    llm_failed: bool
    applied: bool


class PricingWorkflow:
    MAX_ITERATIONS = 5

    def __init__(self, analyzer, action, validator, tools=None):
        self.analyzer, self.action, self.validator = analyzer, action, validator
        self.tools = tools or PricingTools()
        graph = StateGraph(PricingState)
        for name, node in [
            ("ORCHESTRATOR", self.orchestrate),
            ("TOOL", self.tool),
            ("ANALYZER", self.analyze),
            ("ACTION", self.propose),
            ("VALIDATOR", self.validate),
        ]:
            graph.add_node(name, node)
        graph.add_edge(START, "ORCHESTRATOR")
        graph.add_conditional_edges(
            "ORCHESTRATOR", lambda s: "TOOL" if s.get("next_tool") else "ANALYZER"
        )
        graph.add_edge("TOOL", "ORCHESTRATOR")
        graph.add_edge("ANALYZER", "ACTION")
        graph.add_edge("ACTION", "VALIDATOR")
        graph.add_edge("VALIDATOR", END)
        self.graph = graph.compile()

    def record(self, state, node, **details):
        entry = {"node": node, **details}
        state["trace"] = [*state.get("trace", []), entry]
        state["current_step"] = node
        logger.info(
            "pricing_node %s",
            json.dumps({"workflow_id": state["workflow_id"], **entry}),
        )

    async def reason(self, state, phase):
        try:
            return await reasoning.choose(
                {
                    "phase": phase,
                    "goal": state["goal"],
                    "observations": state["observations"],
                    "analysis": state.get("analysis"),
                    "available_tools": [
                        n for n in self.tools.names if n not in state["tool_results"]
                    ],
                }
            )
        except Exception:
            # Do not log HTTP exception strings: request URLs/headers can contain credentials.
            state["llm_failed"] = True
            self.record(
                state, "REASONING", outcome="unavailable; deterministic fallback"
            )
            return None

    async def orchestrate(self, state):
        results = state["tool_results"]
        state["next_tool"] = None
        mandatory = [
            "get_zone_status",
            "get_pricing_config",
            "get_recent_price_changes",
        ]
        missing = next((name for name in mandatory if name not in results), None)
        if missing and state["iterations"] < self.MAX_ITERATIONS:
            state["next_tool"] = missing
        elif not state["errors"] and state["iterations"] < self.MAX_ITERATIONS:
            choice = await self.reason(state, "evidence")
            # Optional demand evidence is useful near capacity thresholds or when an event requests it.
            zone = state["observations"].get("get_zone_status", {})
            total = zone.get("total_slots", 0)
            occupancy = zone.get("occupied_slots", 0) / total if total else 0
            needed = occupancy >= 0.5 or state["input_data"].get("trigger_event") in (
                "arrival_spike",
                "reservation_demand_spike",
            )
            if "get_reservation_demand" not in results and (
                needed or (choice and choice.tool == "get_reservation_demand")
            ):
                state["next_tool"] = "get_reservation_demand"
        if missing and not state["next_tool"]:
            state["errors"].append("Required evidence iteration budget exhausted")
        self.record(state, "ORCHESTRATOR", selected_tool=state["next_tool"])
        return state

    async def tool(self, state):
        name = state["next_tool"]
        state["iterations"] += 1
        try:
            if (
                os.getenv("ENVIRONMENT") == "testing"
                and type(self.tools) is PricingTools
            ):
                # Explicit test snapshots only; CF mock does NOT disable backend tools in deployed service.
                data = state["input_data"]
                if name == "get_zone_status":
                    value = {
                        k: data[k]
                        for k in ("total_slots", "occupied_slots", "base_hourly_rate")
                    }
                elif name == "get_pricing_config":
                    value = PricingPolicy.model_validate(
                        data.get("pricing_config", {})
                    ).model_dump(mode="json")
                elif name == "get_recent_price_changes":
                    value = {
                        "last_price_change_at": data.get("last_price_change_at"),
                        "last_approved_rate": data.get("last_approved_rate"),
                    }
                else:
                    value = {
                        k: data[k]
                        for k in (
                            "recent_arrivals",
                            "recent_departures",
                            "reservation_demand",
                        )
                        if k in data
                    }
            else:
                value = await self.tools.call(name, state["zone_id"])
            if name == "get_zone_status":
                value = ZoneObservation.model_validate(value).model_dump(
                    mode="json", exclude_none=True
                )
            if name == "get_pricing_config":
                value = PricingPolicy.model_validate(value).model_dump(mode="json")
            if name == "get_recent_price_changes":
                value = PricingHistory.model_validate(value).model_dump(mode="json")
            state["tool_results"][name] = {"ok": True, "data": value}
            state["observations"][name] = value
            self.record(state, "TOOL", tool=name, outcome="ok")
        except Exception:
            state["tool_results"][name] = {
                "ok": False,
                "error": "Unavailable or invalid backend data",
            }
            if name != "get_reservation_demand":
                state["errors"].append(f"Required tool {name} unavailable or invalid")
            self.record(state, "TOOL", tool=name, outcome="unavailable")
        return state

    async def analyze(self, state):
        if not state["errors"]:
            try:
                raw = {
                    **state["observations"]["get_zone_status"],
                    **state["observations"].get("get_reservation_demand", {}),
                }
                zone = ZoneObservation.model_validate(raw)
                state["analysis"] = await self.analyzer.analyze_zone_occupancy(
                    zone.total_slots,
                    zone.occupied_slots,
                    zone.recent_arrivals,
                    zone.recent_departures,
                    zone.reservation_demand,
                )
                state["confidence"] = state["analysis"]["confidence"]
            except (ValueError, TypeError):
                state["errors"].append("Invalid zone measurements")
        self.record(
            state, "ANALYZER", outcome="failed" if state["errors"] else "assessed"
        )
        return state

    async def propose(self, state):
        if not state["errors"]:
            analysis = state["analysis"]
            policy = PricingPolicy.model_validate(
                state["observations"]["get_pricing_config"]
            )
            current = Decimal(
                str(state["observations"]["get_zone_status"]["base_hourly_rate"])
            )
            action = (
                "INCREASE_PRICE"
                if analysis["requires_surge_pricing"] and policy.is_enabled
                else "KEEP_PRICE"
            )
            choice = await self.reason(state, "action")
            if choice and choice.action:
                action = choice.action
                self.record(state, "REASONING", selected_action=action)
            # Low occupancy never surges; a model request outside measured demand becomes manual review.
            if action == "INCREASE_PRICE" and (
                not analysis["requires_surge_pricing"] or not policy.is_enabled
            ):
                action = "REQUEST_MANUAL_REVIEW"
            history = state["observations"]["get_recent_price_changes"]
            try:
                last = history.get("last_price_change_at")
                cooldown = (
                    last
                    and (
                        datetime.now(timezone.utc)
                        - datetime.fromisoformat(last.replace("Z", "+00:00"))
                    ).total_seconds()
                    / 60
                    < policy.price_change_cooldown_minutes
                )
            except (ValueError, TypeError):
                cooldown = True
                state["errors"].append("Invalid price-change timestamp")
            reason = "; ".join(analysis["factors"])
            multiplier = Decimal(1)
            already_priced = (
                history.get("last_approved_rate") is not None
                and Decimal(str(history["last_approved_rate"])) == current
            )
            if already_priced and action == "INCREASE_PRICE":
                action = "KEEP_PRICE"
                reason = (
                    "Current rate already matches the most recent approved proposal; avoid compounding surge. "
                    + reason
                )
            elif cooldown:
                action = "KEEP_PRICE"
                reason = "Pricing cooldown active; retain current rate. " + reason
            elif action == "INCREASE_PRICE" and current > 0:
                calculated = await self.action.propose_dynamic_pricing(
                    current,
                    analysis["demand_level"],
                    analysis["velocity_score"],
                    float(policy.surge_critical_multiplier),
                    float(policy.surge_high_multiplier),
                    float(policy.surge_moderate_multiplier),
                )
                multiplier = Decimal(str(calculated["multiplier"]))
            elif action == "INCREASE_PRICE":
                action = "REQUEST_MANUAL_REVIEW"
            rate = (
                current
                if action in ("KEEP_PRICE", "REQUEST_MANUAL_REVIEW")
                else calculate_dynamic_rate(current, multiplier)
            )
            if action == "INCREASE_PRICE" and rate == current:
                action = "KEEP_PRICE"
            proposal = {
                "action": action,
                "current_rate": float(current),
                "base_rate": float(current),
                "proposed_rate": float(rate),
                "calculated_rate": float(rate),
                "multiplier": float(multiplier),
                "reason": reason,
                "confidence": analysis["confidence"],
            }
            state["action_proposal"] = proposal
            state["proposed_actions"] = [proposal]
        self.record(
            state, "ACTION", action=state.get("action_proposal", {}).get("action")
        )
        return state

    async def validate(self, state):
        if state["errors"]:
            validation = {
                "valid": False,
                "issues": state["errors"],
                "reason": "; ".join(state["errors"]),
                "requires_human_approval": False,
            }
        else:
            policy = PricingPolicy.model_validate(
                state["observations"]["get_pricing_config"]
            )
            validation = self.validator.validate_pricing_proposal(
                state["action_proposal"],
                policy,
                state["observations"]["get_recent_price_changes"],
                state["confidence"],
            )
        state["validation"] = validation
        state["validation_results"] = [validation]
        state["requires_human_approval"] = (
            validation["valid"] and validation["requires_human_approval"]
        )
        decision = (
            "REJECTED"
            if not validation["valid"]
            else "PENDING_APPROVAL"
            if state["requires_human_approval"]
            else "AUTO_APPROVED"
        )
        state.update(
            status=decision,
            final_decision=decision,
            reason=validation["reason"],
            applied=False,
        )
        self.record(state, "VALIDATOR", decision=decision, valid=validation["valid"])
        state["step_results"] = {
            "analysis": state.get("analysis"),
            "proposal": state.get("action_proposal"),
            "validation": validation,
            "trace": state["trace"],
        }
        return state

    async def execute(self, plan, state=None):
        state = copy.deepcopy(state or {})
        # Legacy plan inputs are preserved for explicit offline fixtures, never used instead of live backend data.
        inputs = state.get("input_data") or plan.input_data
        cached = (
            get_local_workflow(plan.workflow_id)
            if os.getenv("ENVIRONMENT") == "testing"
            else None
        )
        if os.getenv("ENVIRONMENT") != "testing":
            try:
                cached = await self.tools.fetch(plan.workflow_id)
            except Exception:
                cached = None
        if cached and cached.get("final_decision") in (
            "AUTO_APPROVED",
            "PENDING_APPROVAL",
            "REJECTED",
            "APPROVED",
        ):
            return cached
        initial = {
            **state,
            "workflow_id": plan.workflow_id,
            "workflow_type": "DYNAMIC_PRICING",
            "zone_id": state.get("zone_id"),
            "session_id": state.get("session_id"),
            "goal": plan.objective,
            "input_data": inputs,
            "plan": plan.model_dump(mode="json"),
            "observations": {},
            "tool_results": {},
            "analysis": {},
            "action_proposal": {},
            "iterations": 0,
            "trace": [],
            "errors": [],
            "status": "RUNNING",
            "confidence": 0.0,
            "llm_failed": False,
        }
        result = await self.graph.ainvoke(
            initial, config={"recursion_limit": 2 * self.MAX_ITERATIONS + 8}
        )
        for step, node in zip(
            result["plan"]["steps"], ("ORCHESTRATOR", "ANALYZER", "ACTION", "VALIDATOR")
        ):
            step["status"] = "COMPLETED"
            step["output"] = {
                "trace": [entry for entry in result["trace"] if entry["node"] == node]
            }
        plan.steps = type(plan).model_validate(result["plan"]).steps
        if os.getenv("ENVIRONMENT") == "testing":
            await update_workflow_progress(plan.workflow_id, result)
        elif not inputs.get("backend_managed"):
            try:
                await self.tools.submit(result)
            except Exception:
                result.update(
                    status="FAILED",
                    final_decision="FAILED",
                    reason="Backend proposal persistence unavailable",
                    applied=False,
                )
        return result

    async def resume(self, workflow_id, decision):
        # Decisions and actor names supplied to FastAPI are not proof of admin authorization.
        try:
            state = await self.tools.fetch(workflow_id)
        except Exception:
            raise ValueError(
                "Authoritative backend approval record unavailable"
            ) from None
        backend_status = state.get("backend_status")
        if (
            backend_status == "Approved"
            and state.get("approved_by")
            and state.get("approved_at")
        ):
            proposal = state.get("action_proposal", {})
            changed = proposal.get("current_rate") != proposal.get("proposed_rate")
            state.update(
                status="APPROVED",
                final_decision="APPROVED",
                applied=changed,
                requires_human_approval=False,
            )
        elif backend_status == "Rejected":
            state.update(
                status="REJECTED",
                final_decision="REJECTED",
                applied=False,
                requires_human_approval=False,
            )
        else:
            state.update(
                status="PENDING_APPROVAL",
                final_decision="PENDING_APPROVAL",
                applied=False,
            )
        return state
