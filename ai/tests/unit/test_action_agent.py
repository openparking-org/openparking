"""Tests for the Action Agent: contract, permissions, policy limits, failure and audit."""

from decimal import Decimal

import pytest
from pydantic import ValidationError

from agents.action import (
    ACTION_AGENT_TOOLS,
    ActionAgent,
    build_default_registry,
)
from agents.contracts import (
    ActionType,
    CongestionLevel,
    PenaltyProposalRequest,
    ProposalStatus,
    SurgePricingRequest,
)
from tools.registry import ToolBroker, ToolPermissionError, ToolRegistry, ToolUnavailableError

POLICY = {
    "pricing.base_hourly_rate": "5.00",
    "pricing.max_surge_multiplier": "2.50",
    "overstay.penalty_per_hour": "25.00",
    "overstay.max_penalty_cap": "150.00",
}


def agent_with_policy(policy=None, fail=False) -> ActionAgent:
    """An agent whose settings tool is a stub, so tests pin the policy exactly."""
    values = POLICY if policy is None else policy
    registry = ToolRegistry()

    async def _get(key: str, default=None) -> str:
        if fail:
            raise ConnectionError("settings service unreachable")
        return values[key]

    async def _get_many(keys) -> dict:
        if fail:
            raise ConnectionError("settings service unreachable")
        return {k: values[k] for k in keys}

    registry.register("settings.get", _get)
    registry.register("settings.get_many", _get_many)
    registry.register("enforcement.close_session", _get)
    return ActionAgent(ToolBroker("action-agent", registry, ACTION_AGENT_TOOLS))


# ---------------------------------------------------------------- contract

@pytest.mark.asyncio
async def test_rejects_a_velocity_score_outside_its_range():
    with pytest.raises(ValidationError):
        SurgePricingRequest(zone_id="z1", congestion_level=CongestionLevel.HIGH, velocity_score=1.8)


@pytest.mark.asyncio
async def test_rejects_an_implausible_base_rate():
    # A units mistake would otherwise be multiplied by the surge and billed.
    with pytest.raises(ValidationError):
        SurgePricingRequest(
            zone_id="z1",
            congestion_level=CongestionLevel.LOW,
            velocity_score=0.1,
            base_hourly_rate=Decimal("50000"),
        )


@pytest.mark.asyncio
async def test_rejects_a_negative_overstay():
    with pytest.raises(ValidationError):
        PenaltyProposalRequest(session_id="s1", overstay_minutes=-5)


@pytest.mark.asyncio
async def test_rejects_an_unknown_congestion_level():
    with pytest.raises(ValidationError):
        SurgePricingRequest(zone_id="z1", congestion_level="MELTDOWN", velocity_score=0.5)


# ------------------------------------------------------------- permissions

@pytest.mark.asyncio
async def test_allow_list_is_read_only():
    assert ACTION_AGENT_TOOLS == {"settings.get", "settings.get_many"}


@pytest.mark.asyncio
async def test_agent_cannot_call_an_enforcement_tool():
    # The Action Agent proposes money; acting on a session belongs to another
    # slice. The restriction is structural, not a matter of it choosing not to.
    agent = agent_with_policy()

    with pytest.raises(ToolPermissionError):
        await agent.broker.call("enforcement.close_session", key="anything")


@pytest.mark.asyncio
async def test_a_refused_tool_call_is_recorded_for_audit():
    agent = agent_with_policy()

    with pytest.raises(ToolPermissionError):
        await agent.broker.call("enforcement.close_session", key="anything")

    refused = [c for c in agent.broker.audit_log if c.error == "permission_denied"]
    assert len(refused) == 1
    assert refused[0].tool == "enforcement.close_session"


def test_an_allow_list_naming_an_unregistered_tool_fails_at_construction():
    # A typo must not lie dormant until a workflow runs in production.
    with pytest.raises(ValueError):
        ToolBroker("action-agent", build_default_registry(), frozenset({"settings.nonexistent"}))


# ----------------------------------------------------------- surge pricing

@pytest.mark.asyncio
async def test_low_congestion_proposes_the_base_rate_and_needs_no_human():
    proposal = await agent_with_policy().propose_surge_pricing(
        SurgePricingRequest(zone_id="zone-a", congestion_level=CongestionLevel.LOW, velocity_score=0.1)
    )

    assert proposal.amount == Decimal("5.00")
    assert proposal.status is ProposalStatus.AUTO_APPROVABLE
    assert proposal.requires_human_approval is False


@pytest.mark.asyncio
async def test_any_surge_above_the_base_rate_is_referred_to_a_human():
    proposal = await agent_with_policy().propose_surge_pricing(
        SurgePricingRequest(zone_id="zone-a", congestion_level=CongestionLevel.HIGH, velocity_score=0.2)
    )

    assert proposal.amount == Decimal("7.50")  # 5.00 x 1.5
    assert proposal.status is ProposalStatus.NEEDS_HUMAN_APPROVAL


@pytest.mark.asyncio
async def test_fast_arrivals_add_a_velocity_surcharge():
    proposal = await agent_with_policy().propose_surge_pricing(
        SurgePricingRequest(zone_id="zone-a", congestion_level=CongestionLevel.MODERATE, velocity_score=0.9)
    )

    assert proposal.amount == Decimal("7.00")  # 5.00 x (1.2 + 0.2)
    assert any(s.step == "velocity_surcharge" for s in proposal.trace)


@pytest.mark.asyncio
async def test_surge_is_held_to_the_policy_ceiling():
    # CRITICAL (2.0) plus the velocity surcharge (0.2) exceeds the 2.5 ceiling
    # only if the ceiling is lowered, so pin a stricter policy.
    strict = dict(POLICY, **{"pricing.max_surge_multiplier": "1.50"})

    proposal = await agent_with_policy(strict).propose_surge_pricing(
        SurgePricingRequest(zone_id="zone-a", congestion_level=CongestionLevel.CRITICAL, velocity_score=0.9)
    )

    assert proposal.was_capped is True
    assert proposal.amount == Decimal("7.50")        # 5.00 x 1.50 ceiling
    assert proposal.uncapped_amount == Decimal("11.00")  # 5.00 x 2.20
    assert "ceiling" in proposal.rationale


@pytest.mark.asyncio
async def test_a_caller_cannot_raise_the_ceiling_by_supplying_a_rate():
    # The rate may come from the caller, but the cap never does.
    proposal = await agent_with_policy().propose_surge_pricing(
        SurgePricingRequest(
            zone_id="zone-a",
            congestion_level=CongestionLevel.CRITICAL,
            velocity_score=0.95,
            base_hourly_rate=Decimal("10.00"),
        )
    )

    multiplier = proposal.amount / Decimal("10.00")
    assert multiplier <= Decimal("2.50")


# --------------------------------------------------------------- penalties

@pytest.mark.asyncio
async def test_no_overstay_proposes_nothing():
    proposal = await agent_with_policy().propose_penalty(
        PenaltyProposalRequest(session_id="s1", overstay_minutes=0)
    )

    assert proposal.amount == Decimal("0.00")
    assert proposal.status is ProposalStatus.AUTO_APPROVABLE


@pytest.mark.asyncio
async def test_penalty_bills_per_started_hour():
    proposal = await agent_with_policy().propose_penalty(
        PenaltyProposalRequest(session_id="s1", overstay_minutes=61)
    )

    assert proposal.amount == Decimal("50.00")  # 2 started hours x 25.00
    assert proposal.status is ProposalStatus.AUTO_APPROVABLE


@pytest.mark.asyncio
async def test_a_large_penalty_is_referred_to_a_human():
    proposal = await agent_with_policy().propose_penalty(
        PenaltyProposalRequest(session_id="s1", overstay_minutes=4 * 60)
    )

    assert proposal.amount == Decimal("100.00")
    assert proposal.status is ProposalStatus.NEEDS_HUMAN_APPROVAL
    assert proposal.requires_human_approval is True


@pytest.mark.asyncio
async def test_penalty_is_held_to_the_policy_cap():
    proposal = await agent_with_policy().propose_penalty(
        PenaltyProposalRequest(session_id="s1", overstay_minutes=10 * 60)
    )

    assert proposal.uncapped_amount == Decimal("250.00")
    assert proposal.amount == Decimal("150.00")
    assert proposal.was_capped is True


@pytest.mark.asyncio
async def test_policy_change_moves_the_outcome_without_a_code_change():
    lenient = dict(POLICY, **{"overstay.penalty_per_hour": "5.00"})

    proposal = await agent_with_policy(lenient).propose_penalty(
        PenaltyProposalRequest(session_id="s1", overstay_minutes=61)
    )

    assert proposal.amount == Decimal("10.00")
    assert proposal.policy.values["overstay.penalty_per_hour"] == "5.00"


# ------------------------------------------------- failure and observability

@pytest.mark.asyncio
async def test_unreadable_policy_fails_safe_rather_than_guessing():
    # Charging from a stale built-in constant is worse than charging nothing.
    proposal = await agent_with_policy(fail=True).propose_penalty(
        PenaltyProposalRequest(session_id="s1", overstay_minutes=600)
    )

    assert proposal.status is ProposalStatus.FAILED_SAFE
    assert proposal.amount == Decimal("0.00")
    assert proposal.requires_human_approval is True
    assert any(s.step == "failed_safe" for s in proposal.trace)


@pytest.mark.asyncio
async def test_a_tool_failure_is_surfaced_not_swallowed():
    agent = agent_with_policy(fail=True)

    with pytest.raises(ToolUnavailableError):
        await agent.broker.call("settings.get", key="pricing.base_hourly_rate")


@pytest.mark.asyncio
async def test_a_proposal_records_the_policy_it_was_computed_from():
    # Settings are editable at runtime, so without this a figure questioned
    # next week could not be reproduced.
    proposal = await agent_with_policy().propose_penalty(
        PenaltyProposalRequest(session_id="s1", overstay_minutes=90)
    )

    assert proposal.policy.values["overstay.penalty_per_hour"] == "25.00"
    assert proposal.policy.values["overstay.max_penalty_cap"] == "150.00"


@pytest.mark.asyncio
async def test_a_proposal_carries_an_auditable_trace_and_tool_log():
    proposal = await agent_with_policy().propose_surge_pricing(
        SurgePricingRequest(zone_id="zone-a", congestion_level=CongestionLevel.HIGH, velocity_score=0.8)
    )

    steps = [s.step for s in proposal.trace]
    assert steps[0] == "received"
    assert "policy_loaded" in steps
    assert steps[-1] == "decision"

    assert len(proposal.tool_calls) == 1
    assert proposal.tool_calls[0]["tool"] == "settings.get_many"
    assert proposal.tool_calls[0]["ok"] is True


@pytest.mark.asyncio
async def test_the_agent_never_executes_only_proposes():
    agent = agent_with_policy()
    proposal = await agent.propose_penalty(PenaltyProposalRequest(session_id="s1", overstay_minutes=90))

    # Everything it touched was a read. No tool it holds can mutate state.
    assert proposal.action_type is ActionType.OVERSTAY_PENALTY
    assert all(call.tool.startswith("settings.") for call in agent.broker.audit_log)


@pytest.mark.asyncio
async def test_each_proposal_reports_only_its_own_tool_calls():
    # Agents are long-lived, so the broker's cumulative log would otherwise
    # attribute an earlier request's lookups to this one.
    agent = agent_with_policy()

    await agent.propose_penalty(PenaltyProposalRequest(session_id="s1", overstay_minutes=90))
    second = await agent.propose_penalty(PenaltyProposalRequest(session_id="s2", overstay_minutes=90))

    assert len(second.tool_calls) == 1
    assert len(agent.broker.audit_log) == 2


@pytest.mark.asyncio
async def test_a_missing_policy_key_is_an_error_not_a_zero_charge():
    from tools.config_tools import SettingNotFoundError, get_config

    # A key that silently resolved to "0.0" would make the agent propose a zero
    # penalty and report it as a success.
    with pytest.raises(SettingNotFoundError):
        await get_config("overstay.no_such_setting")


@pytest.mark.asyncio
async def test_a_missing_policy_key_makes_the_agent_fail_safe():
    incomplete = {k: v for k, v in POLICY.items() if k != "overstay.penalty_per_hour"}
    registry = ToolRegistry()

    async def _get_many(keys):
        return {k: incomplete[k] for k in keys}  # raises KeyError for the missing one

    async def _get(key, default=None):
        return incomplete[key]

    registry.register("settings.get", _get)
    registry.register("settings.get_many", _get_many)
    agent = ActionAgent(ToolBroker("action-agent", registry, ACTION_AGENT_TOOLS))

    proposal = await agent.propose_penalty(PenaltyProposalRequest(session_id="s1", overstay_minutes=600))

    assert proposal.status is ProposalStatus.FAILED_SAFE
    assert proposal.amount == Decimal("0.00")


# ------------------------------------------------------- regression guards

@pytest.mark.asyncio
async def test_each_proposal_reports_only_its_own_tool_calls():
    # The agent is a long-lived singleton in the service. Without scoping, the
    # second proposal reported the first proposal's lookups as its own.
    agent = agent_with_policy()

    await agent.propose_penalty(PenaltyProposalRequest(session_id="s1", overstay_minutes=90))
    second = await agent.propose_penalty(PenaltyProposalRequest(session_id="s2", overstay_minutes=90))

    assert len(second.tool_calls) == 1
    assert len(agent.broker.audit_log) == 2


@pytest.mark.asyncio
async def test_an_unknown_setting_raises_instead_of_resolving_to_zero(monkeypatch):
    # A missing penalty rate used to come back as "0.0", so the agent proposed
    # a zero penalty and reported it as a success.
    from tools.config_tools import SettingNotFoundError, get_config

    monkeypatch.setenv("ENVIRONMENT", "testing")

    with pytest.raises(SettingNotFoundError):
        await get_config("overstay.no_such_setting")


@pytest.mark.asyncio
async def test_a_missing_setting_makes_the_real_agent_fail_safe(monkeypatch):
    import tools.config_tools as config_tools

    monkeypatch.setenv("ENVIRONMENT", "testing")
    monkeypatch.delitem(config_tools.MOCK_SETTINGS, "overstay.penalty_per_hour")

    proposal = await ActionAgent().propose_penalty(
        PenaltyProposalRequest(session_id="s1", overstay_minutes=600)
    )

    assert proposal.status is ProposalStatus.FAILED_SAFE
    assert proposal.amount == Decimal("0.00")


@pytest.mark.asyncio
async def test_the_real_agent_reads_the_seeded_penalty_rate(monkeypatch):
    monkeypatch.setenv("ENVIRONMENT", "testing")

    proposal = await ActionAgent().propose_penalty(
        PenaltyProposalRequest(session_id="s1", overstay_minutes=600)
    )

    assert proposal.uncapped_amount == Decimal("250.00")  # 10 h x 25.00
    assert proposal.amount == Decimal("150.00")
