"""
Action Agent — Booking & Payment slice.

Responsibility: turn a reading of the world (how congested a zone is, how long a
driver overstayed) into a *proposed* monetary action, bounded by policy.

What it deliberately does not do:

  - It never executes. It returns a proposal; applying it is the orchestrator's
    job, behind human approval where the amount warrants it.
  - It never trusts a caller for a limit. Rates, caps and thresholds are read
    from system settings through a tool, so a caller cannot talk the agent past
    a cap by supplying its own.
  - It holds no tool that can modify a session, a permit or a booking. Its
    allow-list is two read-only settings lookups, enforced by the broker.

If policy cannot be read, it fails safe: it returns FAILED_SAFE with a zero
amount rather than guessing with a built-in default, because silently charging
a driver from a stale constant is worse than charging nothing and escalating.
"""

from __future__ import annotations

import os
import uuid
from decimal import Decimal, ROUND_HALF_UP
from typing import Any, Dict, FrozenSet, List, Optional

from agents.contracts import (
    ActionProposal,
    ActionType,
    CongestionLevel,
    PenaltyProposalRequest,
    PolicySnapshot,
    ProposalStatus,
    SurgePricingRequest,
    TraceStep,
)
from tools.config_tools import get_config
from tools.registry import ToolBroker, ToolRegistry, ToolUnavailableError

#: Settings the agent reads. Named here so the allow-list is reviewable.
SETTING_BASE_RATE = "pricing.base_hourly_rate"
SETTING_MAX_SURGE = "pricing.max_surge_multiplier"
SETTING_PENALTY_PER_HOUR = "overstay.penalty_per_hour"
SETTING_PENALTY_CAP = "overstay.max_penalty_cap"

#: The only tools this agent may call. Both are read-only.
ACTION_AGENT_TOOLS: FrozenSet[str] = frozenset({"settings.get", "settings.get_many"})

#: Surge applied per congestion band, before the policy ceiling is applied.
_SURGE_BY_CONGESTION: Dict[CongestionLevel, Decimal] = {
    CongestionLevel.LOW: Decimal("1.0"),
    CongestionLevel.MODERATE: Decimal("1.2"),
    CongestionLevel.HIGH: Decimal("1.5"),
    CongestionLevel.CRITICAL: Decimal("2.0"),
}

#: Extra surge when arrivals are coming in fast, and the point at which that applies.
_VELOCITY_SURCHARGE = Decimal("0.2")
_VELOCITY_THRESHOLD = 0.7

#: A penalty at or above this needs a person, however compliant it is.
_PENALTY_APPROVAL_THRESHOLD = Decimal("100.00")

#: Any surge above the base rate is consequential enough to need a person.
_SURGE_APPROVAL_THRESHOLD = Decimal("1.0")


def _money(value: Decimal) -> Decimal:
    return value.quantize(Decimal("0.01"), rounding=ROUND_HALF_UP)


async def _settings_get(key: str, default: Optional[str] = None) -> str:
    return await get_config(key, default=default)


async def _settings_get_many(keys: List[str]) -> Dict[str, str]:
    return {key: await get_config(key) for key in keys}


def build_default_registry() -> ToolRegistry:
    """
    The system's tools. Registering enforcement tools here that the Action Agent
    is *not* permitted to call is the point: the restriction is visible, and a
    test can prove the agent is refused.
    """
    registry = ToolRegistry()
    registry.register("settings.get", _settings_get)
    registry.register("settings.get_many", _settings_get_many)

    async def _denied(**_: Any) -> None:  # pragma: no cover - never reachable for this agent
        raise AssertionError("Action Agent must never reach an enforcement tool")

    # Owned by other slices. Present so the boundary is explicit and testable.
    registry.register("enforcement.close_session", _denied)
    registry.register("permits.approve", _denied)
    registry.register("bookings.cancel", _denied)
    return registry


class ActionAgent:
    """Proposes surge pricing and overstay penalties for the Booking slice."""

    name = "action-agent"

    def __init__(self, broker: Optional[ToolBroker] = None) -> None:
        self.broker = broker or ToolBroker(
            agent=self.name,
            registry=build_default_registry(),
            allowed=ACTION_AGENT_TOOLS,
        )

    # ------------------------------------------------------------------
    # Surge pricing
    # ------------------------------------------------------------------

    async def propose_surge_pricing(self, request: SurgePricingRequest) -> ActionProposal:
        mark = self.broker.mark()
        trace: List[TraceStep] = [
            TraceStep(
                step="received",
                detail=(
                    f"zone={request.zone_id} congestion={request.congestion_level.value} "
                    f"velocity={request.velocity_score}"
                ),
            )
        ]

        try:
            policy = await self.broker.call(
                "settings.get_many",
                keys=[SETTING_BASE_RATE, SETTING_MAX_SURGE],
            )
        except ToolUnavailableError as exc:
            return self._failed_safe(ActionType.SURGE_PRICING, request.zone_id, trace, str(exc), mark)

        snapshot = PolicySnapshot(values=dict(policy))
        base_rate = request.base_hourly_rate or Decimal(policy[SETTING_BASE_RATE])
        max_surge = Decimal(policy[SETTING_MAX_SURGE])

        trace.append(
            TraceStep(
                step="policy_loaded",
                detail=f"base_rate={base_rate} max_surge={max_surge} "
                       f"(rate source: {'request' if request.base_hourly_rate else 'settings'})",
            )
        )

        multiplier = _SURGE_BY_CONGESTION[request.congestion_level]
        if request.velocity_score > _VELOCITY_THRESHOLD:
            multiplier += _VELOCITY_SURCHARGE
            trace.append(
                TraceStep(
                    step="velocity_surcharge",
                    detail=f"velocity {request.velocity_score} > {_VELOCITY_THRESHOLD}, +{_VELOCITY_SURCHARGE}",
                )
            )

        capped_multiplier = min(multiplier, max_surge)
        was_capped = capped_multiplier < multiplier

        if was_capped:
            trace.append(
                TraceStep(step="capped", detail=f"multiplier {multiplier} reduced to policy ceiling {max_surge}")
            )

        uncapped_rate = _money(base_rate * multiplier)
        rate = _money(base_rate * capped_multiplier)

        status = (
            ProposalStatus.NEEDS_HUMAN_APPROVAL
            if capped_multiplier > _SURGE_APPROVAL_THRESHOLD
            else ProposalStatus.AUTO_APPROVABLE
        )
        trace.append(TraceStep(step="decision", detail=f"status={status.value} multiplier={capped_multiplier}"))

        rationale = (
            f"Congestion is {request.congestion_level.value} with arrival velocity "
            f"{request.velocity_score}; proposing {capped_multiplier}x on a base rate of {base_rate} "
            f"for an effective {rate} per hour."
        )
        if was_capped:
            rationale += f" The computed {multiplier}x was reduced to the {max_surge}x policy ceiling."

        return ActionProposal(
            proposal_id=self._proposal_id("surge"),
            action_type=ActionType.SURGE_PRICING,
            status=status,
            subject_id=request.zone_id,
            amount=rate,
            uncapped_amount=uncapped_rate,
            was_capped=was_capped,
            rationale=rationale,
            policy=snapshot,
            trace=trace,
            tool_calls=[call.as_dict() for call in self.broker.calls_since(mark)],
        )

    # ------------------------------------------------------------------
    # Overstay penalties
    # ------------------------------------------------------------------

    async def propose_penalty(self, request: PenaltyProposalRequest) -> ActionProposal:
        mark = self.broker.mark()
        trace: List[TraceStep] = [
            TraceStep(
                step="received",
                detail=f"session={request.session_id} overstay_minutes={request.overstay_minutes}",
            )
        ]

        try:
            policy = await self.broker.call(
                "settings.get_many",
                keys=[SETTING_PENALTY_PER_HOUR, SETTING_PENALTY_CAP],
            )
        except ToolUnavailableError as exc:
            return self._failed_safe(ActionType.OVERSTAY_PENALTY, request.session_id, trace, str(exc), mark)

        snapshot = PolicySnapshot(values=dict(policy))
        per_hour = request.base_penalty_per_hour or Decimal(policy[SETTING_PENALTY_PER_HOUR])
        cap = Decimal(policy[SETTING_PENALTY_CAP])

        trace.append(TraceStep(step="policy_loaded", detail=f"penalty_per_hour={per_hour} cap={cap}"))

        # No overstay means no proposal to make. Returning a zero-value
        # AUTO_APPROVABLE keeps the contract uniform for the caller.
        if request.overstay_minutes == 0:
            trace.append(TraceStep(step="decision", detail="no overstay, nothing to propose"))
            return ActionProposal(
                proposal_id=self._proposal_id("penalty"),
                action_type=ActionType.OVERSTAY_PENALTY,
                status=ProposalStatus.AUTO_APPROVABLE,
                subject_id=request.session_id,
                amount=Decimal("0.00"),
                uncapped_amount=Decimal("0.00"),
                was_capped=False,
                rationale="No overstay recorded for this session.",
                policy=snapshot,
                trace=trace,
                tool_calls=[call.as_dict() for call in self.broker.calls_since(mark)],
            )

        # Billed per started hour, matching how the API settles a session.
        billable_hours = (request.overstay_minutes + 59) // 60
        uncapped = _money(per_hour * billable_hours)
        amount = min(uncapped, cap)
        was_capped = uncapped > cap

        trace.append(
            TraceStep(step="computed", detail=f"{billable_hours} started hour(s) x {per_hour} = {uncapped}")
        )
        if was_capped:
            trace.append(TraceStep(step="capped", detail=f"{uncapped} reduced to policy cap {cap}"))

        status = (
            ProposalStatus.NEEDS_HUMAN_APPROVAL
            if amount >= _PENALTY_APPROVAL_THRESHOLD
            else ProposalStatus.AUTO_APPROVABLE
        )
        trace.append(TraceStep(step="decision", detail=f"status={status.value} amount={amount}"))

        rationale = (
            f"Overstay of {request.overstay_minutes} minutes bills as {billable_hours} started "
            f"hour(s) at {per_hour} per hour, giving {amount}."
        )
        if was_capped:
            rationale += f" The computed {uncapped} was reduced to the {cap} policy cap."
        if status is ProposalStatus.NEEDS_HUMAN_APPROVAL:
            rationale += (
                f" At or above {_PENALTY_APPROVAL_THRESHOLD} a penalty is referred to an "
                f"administrator before it is applied."
            )

        return ActionProposal(
            proposal_id=self._proposal_id("penalty"),
            action_type=ActionType.OVERSTAY_PENALTY,
            status=status,
            subject_id=request.session_id,
            amount=amount,
            uncapped_amount=uncapped,
            was_capped=was_capped,
            rationale=rationale,
            policy=snapshot,
            trace=trace,
            tool_calls=[call.as_dict() for call in self.broker.calls_since(mark)],
        )

    # ------------------------------------------------------------------
    # Internals
    # ------------------------------------------------------------------

    def _failed_safe(
        self,
        action_type: ActionType,
        subject_id: str,
        trace: List[TraceStep],
        error: str,
        mark: int,
    ) -> ActionProposal:
        trace.append(TraceStep(step="failed_safe", detail=error))
        return ActionProposal(
            proposal_id=self._proposal_id("failsafe"),
            action_type=action_type,
            status=ProposalStatus.FAILED_SAFE,
            subject_id=subject_id,
            amount=Decimal("0.00"),
            uncapped_amount=Decimal("0.00"),
            was_capped=False,
            rationale=(
                "Policy could not be read, so no amount is trustworthy. "
                "Escalating rather than charging from a built-in default."
            ),
            policy=PolicySnapshot(values={}, source="unavailable"),
            trace=trace,
            tool_calls=[call.as_dict() for call in self.broker.calls_since(mark)],
        )

    @staticmethod
    def _proposal_id(kind: str) -> str:
        return f"prop-{kind}-{uuid.uuid4().hex[:12]}"

    # ------------------------------------------------------------------
    # Backwards-compatible adapters
    #
    # PlannerAgent (Enforcement slice) calls these. Kept delegating to the
    # contract-based methods above so the orchestration keeps working while
    # that slice migrates, rather than breaking another member's module.
    # ------------------------------------------------------------------

    async def propose_dynamic_pricing(
        self, base_rate: Decimal, congestion_level: str, velocity_score: float
    ) -> Dict[str, Any]:
        proposal = await self.propose_surge_pricing(
            SurgePricingRequest(
                zone_id="legacy",
                congestion_level=CongestionLevel(congestion_level),
                velocity_score=velocity_score,
                base_hourly_rate=base_rate,
            )
        )
        multiplier = (proposal.amount / base_rate) if base_rate else Decimal("1.0")
        return {
            "multiplier": float(multiplier),
            "calculated_rate": float(proposal.amount),
            "base_rate": float(base_rate),
            "rationale": proposal.rationale,
        }

    async def propose_overstay_penalty(
        self, overstay_minutes: int, base_penalty_per_hour: Decimal
    ) -> Dict[str, Any]:
        proposal = await self.propose_penalty(
            PenaltyProposalRequest(
                session_id="legacy",
                overstay_minutes=overstay_minutes,
                base_penalty_per_hour=base_penalty_per_hour,
            )
        )
        # Reports the uncapped figure, because the Planner's Validator applies
        # the regulatory cap as its own step and would otherwise never see a
        # breach to flag.
        return {
            "overstay_minutes": overstay_minutes,
            "billable_hours": (overstay_minutes + 59) // 60,
            "proposed_amount": float(proposal.uncapped_amount),
            "proposal_id": proposal.proposal_id,
        }
