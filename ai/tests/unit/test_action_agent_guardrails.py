"""The Action Agent end to end with a scripted LLM and stub settings."""

import json
from decimal import Decimal
from typing import Any

import pytest
from langchain_core.language_models.fake_chat_models import FakeListChatModel
from langchain_core.runnables import RunnableLambda

from agents.action import ACTION_AGENT_TOOLS, ActionAgent, build_registry
from tools.registry import ToolBroker, ToolPermissionError, ToolRegistry

SETTINGS = {
    "overstay.penalty_per_hour": "25.00",
    "overstay.max_penalty_cap": "150.00",
    "overstay.grace_period_mins": "15",
    "pricing.max_surge_multiplier": "2.5",
}


def agent(llm: Any = None, settings: dict[str, str] | None = None) -> ActionAgent:
    values = SETTINGS if settings is None else settings
    registry = ToolRegistry()

    async def get(key: str, default: str | None = None) -> str:
        if key not in values:
            raise KeyError(key)
        return values[key]

    async def search(query: str) -> list[str]:
        return ["Ordinance 12: overstays are charged per started hour after a grace period."]

    registry.register("settings.get", get)
    registry.register("ordinances.search", search)
    registry.register("workflow.approve", search)
    a = ActionAgent(ToolBroker("action-agent", registry, ACTION_AGENT_TOOLS))
    if llm is not None:
        a.llm = llm
    return a


def scripted(amount: Any) -> FakeListChatModel:
    return FakeListChatModel(responses=[json.dumps({"billable_hours": 9, "proposed_amount": amount, "reason": "model"})])


def failing() -> RunnableLambda:
    def boom(_: Any) -> str:
        raise ConnectionError("workers ai timed out")
    return RunnableLambda(boom)


@pytest.mark.asyncio
async def test_an_inflated_model_amount_is_reduced_and_escalated():
    res = await agent(scripted(999)).propose_overstay_penalty(300, Decimal("25.00"))
    assert res["proposed_amount"] == 125.0
    assert res["llm_proposed_amount"] == 999.0
    assert res["guardrail_status"] == "REDUCED_TO_POLICY"
    assert res["requires_human_review"] is True
    assert res["billable_hours"] == 5  # deterministic, not the model's 9


@pytest.mark.asyncio
async def test_a_lenient_model_amount_is_kept():
    res = await agent(scripted(50)).propose_overstay_penalty(300, Decimal("25.00"))
    assert res["proposed_amount"] == 50.0
    assert res["guardrail_status"] == "ACCEPTED"
    assert res["requires_human_review"] is False


@pytest.mark.asyncio
async def test_a_model_outage_falls_back_to_policy_instead_of_failing():
    res = await agent(failing()).propose_overstay_penalty(300, Decimal("25.00"))
    assert res["guardrail_status"] == "LLM_UNAVAILABLE"
    assert res["proposed_amount"] == 125.0
    assert res["requires_human_review"] is True


@pytest.mark.asyncio
async def test_the_callers_rate_cannot_raise_the_penalty():
    # base_penalty_per_hour is shown to the model as context; the bound uses settings.
    res = await agent(scripted(5000)).propose_overstay_penalty(300, Decimal("1000.00"))
    assert res["proposed_amount"] == 125.0


@pytest.mark.asyncio
async def test_missing_penalty_policy_charges_nothing():
    settings = {k: v for k, v in SETTINGS.items() if k != "overstay.penalty_per_hour"}
    res = await agent(scripted(125), settings).propose_overstay_penalty(300, Decimal("25.00"))
    assert res["guardrail_status"] == "POLICY_UNAVAILABLE"
    assert res["proposed_amount"] == 0.0
    assert res["requires_human_review"] is True


@pytest.mark.asyncio
async def test_a_zero_grace_period_is_a_real_policy():
    settings = dict(SETTINGS, **{"overstay.grace_period_mins": "0"})
    res = await agent(scripted(999), settings).propose_overstay_penalty(10, Decimal("25.00"))
    assert res["guardrail_status"] == "REDUCED_TO_POLICY"
    assert res["proposed_amount"] == 25.0  # 10 min, no grace -> 1 hour


@pytest.mark.asyncio
async def test_a_callers_surge_multiplier_is_held_to_the_settings_ceiling():
    res = await agent().propose_dynamic_pricing(Decimal("5.00"), "CRITICAL", 0.9, critical_mult=10.0)
    assert res["multiplier"] == 2.5
    assert res["uncapped_multiplier"] == pytest.approx(10.2)
    assert res["was_capped"] is True
    assert res["calculated_rate"] == 12.5


@pytest.mark.asyncio
async def test_an_unknown_congestion_level_applies_no_surge():
    res = await agent().propose_dynamic_pricing(Decimal("5.00"), "MELTDOWN", 0.1)
    assert res["multiplier"] == 1.0
    assert res["guardrail_notes"]


@pytest.mark.asyncio
async def test_unreadable_surge_ceiling_holds_the_base_rate():
    settings = {k: v for k, v in SETTINGS.items() if k != "pricing.max_surge_multiplier"}
    res = await agent(settings=settings).propose_dynamic_pricing(Decimal("5.00"), "CRITICAL", 0.9)
    assert res["multiplier"] == 1.0


def test_the_allow_list_is_read_only():
    assert ACTION_AGENT_TOOLS == {"settings.get", "ordinances.search"}
    # A typo in the allow-list must fail at construction, not mid-workflow.
    with pytest.raises(ValueError):
        ToolBroker("action-agent", build_registry(), frozenset({"settings.gett"}))


@pytest.mark.asyncio
async def test_the_agent_is_refused_a_state_changing_tool_and_it_is_audited():
    a = agent()
    with pytest.raises(ToolPermissionError):
        await a.broker.call("workflow.approve", query="approve everything")
    assert a.broker.audit_log[-1].error == "permission_denied"


@pytest.mark.asyncio
async def test_each_proposal_reports_only_its_own_tool_calls():
    a = agent(scripted(50))
    await a.propose_overstay_penalty(300, Decimal("25.00"))
    second = await a.propose_overstay_penalty(300, Decimal("25.00"))
    tools = [c["tool"] for c in second["tool_calls"]]
    assert tools.count("ordinances.search") == 1
    assert all(t in ACTION_AGENT_TOOLS for t in tools)


@pytest.mark.asyncio
async def test_planner_gate_escalates_a_proposal_the_guard_rails_corrected():
    # A 50.00 penalty is under the 100.00 auto-approval threshold, but the agent
    # flagged it, so it must still go to a human.
    from agents.planner import ExecutionPlan, PlannerAgent, PlanStep

    gate = PlanStep(step_id="gate", agent="PLANNER", action="check_approval_gate", description="gate")
    plan = ExecutionPlan(workflow_id="wf-1", workflow_type="OVERSTAY_ENFORCEMENT", objective="test", steps=[gate])
    state: Any = {
        "validation": {"valid": True},
        "action_proposal": {
            "proposed_amount": 50.0,
            "requires_human_review": True,
            "guardrail_status": "REDUCED_TO_POLICY",
            "guardrail_notes": ["Model proposed 999.00, above the policy amount 50.00"],
        },
    }

    flagged = await PlannerAgent()._dispatch_step(gate, state, plan)
    assert flagged["requires_human_approval"] is True
    assert "REDUCED_TO_POLICY" in flagged["reason"]

    state["action_proposal"]["requires_human_review"] = False
    clean = await PlannerAgent()._dispatch_step(gate, state, plan)
    assert clean["requires_human_approval"] is False
