"""
Deterministic guard rails for the Action Agent (Booking & Payment slice).

The agent asks an LLM, grounded with retrieved ordinances, to propose an
overstay penalty and explain it. A language model is good at the explanation
and at weighing context such as a first offence; it is not a reliable source of
an amount of money. Everything here is plain arithmetic over policy, so the
figure a driver can be charged is bounded no matter what the model returns:

  - the LLM may propose *less* than policy allows (discretion, leniency);
  - it may never propose *more* than the policy amount, or more than the cap;
  - output that is missing, non-numeric, negative or non-finite is discarded
    and the policy amount is used instead.

No I/O, no clock, no model: the same inputs always give the same answer.
"""

from __future__ import annotations

import math
from dataclasses import dataclass, field
from decimal import ROUND_HALF_UP, Decimal, InvalidOperation
from enum import Enum
from typing import Any

CENT = Decimal("0.01")


class GuardrailStatus(str, Enum):
    #: The model's amount was within policy and used as proposed.
    ACCEPTED = "ACCEPTED"
    #: The model proposed more than policy allows; reduced to the policy amount.
    REDUCED_TO_POLICY = "REDUCED_TO_POLICY"
    #: The model's output was unusable; the policy amount was used instead.
    LLM_OUTPUT_REJECTED = "LLM_OUTPUT_REJECTED"
    #: The model could not be reached; the policy amount was used instead.
    LLM_UNAVAILABLE = "LLM_UNAVAILABLE"
    #: Policy could not be read, so no amount is trustworthy. Nothing is charged
    #: automatically and the case is referred to a human.
    POLICY_UNAVAILABLE = "POLICY_UNAVAILABLE"


@dataclass(frozen=True)
class PenaltyPolicy:
    grace_minutes: int
    penalty_per_hour: Decimal
    cap: Decimal


@dataclass
class PenaltyDecision:
    amount: Decimal
    billable_hours: int
    policy_amount: Decimal
    status: GuardrailStatus
    llm_amount: Decimal | None = None
    notes: list[str] = field(default_factory=list)

    @property
    def requires_human_review(self) -> bool:
        # A reduced or rejected model output is exactly the case a person should
        # look at, even when the final amount is within policy.
        return self.status is not GuardrailStatus.ACCEPTED


def money(value: Decimal) -> Decimal:
    return value.quantize(CENT, rounding=ROUND_HALF_UP)


def chargeable_hours(overstay_minutes: int, grace_minutes: int) -> int:
    """Started hours after the grace period, which is forgiven outright."""
    chargeable = max(0, overstay_minutes - max(0, grace_minutes))
    return (chargeable + 59) // 60


def policy_penalty(overstay_minutes: int, policy: PenaltyPolicy) -> tuple[Decimal, int]:
    """The most policy allows for this overstay: started hours x rate, capped."""
    hours = chargeable_hours(overstay_minutes, policy.grace_minutes)
    return min(money(policy.penalty_per_hour * hours), money(policy.cap)), hours


def parse_amount(raw: Any) -> Decimal | None:
    """Accepts a finite, non-negative number from model output; anything else is None."""
    if isinstance(raw, bool) or raw is None:
        return None
    try:
        value = Decimal(str(raw))
    except (InvalidOperation, ValueError):
        return None
    if not value.is_finite() or value < 0:
        return None
    return money(value)


def bound_penalty(llm_raw_amount: Any, overstay_minutes: int, policy: PenaltyPolicy | None) -> PenaltyDecision:
    """Combines the model's proposal with policy so the result is always within policy."""
    if policy is None:
        return PenaltyDecision(
            amount=Decimal("0.00"),
            billable_hours=0,
            policy_amount=Decimal("0.00"),
            status=GuardrailStatus.POLICY_UNAVAILABLE,
            notes=["Penalty policy could not be read; nothing charged automatically, referred for review."],
        )

    ceiling, hours = policy_penalty(overstay_minutes, policy)
    llm_amount = parse_amount(llm_raw_amount)

    if llm_amount is None:
        return PenaltyDecision(
            amount=ceiling,
            billable_hours=hours,
            policy_amount=ceiling,
            status=GuardrailStatus.LLM_OUTPUT_REJECTED,
            notes=[f"Model returned an unusable amount ({llm_raw_amount!r}); used the policy amount {ceiling}."],
        )

    if llm_amount > ceiling:
        return PenaltyDecision(
            amount=ceiling,
            billable_hours=hours,
            policy_amount=ceiling,
            status=GuardrailStatus.REDUCED_TO_POLICY,
            llm_amount=llm_amount,
            notes=[f"Model proposed {llm_amount}, above the policy amount {ceiling}; reduced to {ceiling}."],
        )

    return PenaltyDecision(
        amount=llm_amount,
        billable_hours=hours,
        policy_amount=ceiling,
        status=GuardrailStatus.ACCEPTED,
        llm_amount=llm_amount,
    )


KNOWN_CONGESTION = ("LOW", "MODERATE", "HIGH", "CRITICAL")


def clamp_surge(multiplier: Decimal, max_surge: Decimal | None) -> tuple[Decimal, bool]:
    """Surge never discounts below 1.0 and never exceeds the policy ceiling."""
    floor = Decimal("1.0")
    ceiling = max(floor, max_surge) if max_surge is not None else None
    bounded = max(floor, multiplier)
    if ceiling is not None:
        bounded = min(bounded, ceiling)
    return bounded, bounded != multiplier


def clamp_velocity(velocity: float) -> float:
    if not isinstance(velocity, (int, float)) or math.isnan(velocity):
        return 0.0
    return min(1.0, max(0.0, float(velocity)))
