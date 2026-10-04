"""Deterministic guard rails around the Action Agent's LLM penalty proposals."""

from decimal import Decimal

import pytest

from agents.action_guardrails import (
    GuardrailStatus,
    PenaltyPolicy,
    bound_penalty,
    chargeable_hours,
    clamp_surge,
    clamp_velocity,
    policy_penalty,
)

POLICY = PenaltyPolicy(grace_minutes=15, penalty_per_hour=Decimal("25.00"), cap=Decimal("150.00"))


def test_grace_minutes_are_forgiven_outright():
    assert chargeable_hours(10, 15) == 0
    assert chargeable_hours(16, 15) == 1  # 1 chargeable minute starts an hour
    assert chargeable_hours(80, 15) == 2  # 65 chargeable minutes


def test_policy_amount_is_capped():
    amount, hours = policy_penalty(600, POLICY)  # 585 min -> 10 h -> 250.00
    assert hours == 10
    assert amount == Decimal("150.00")


def test_model_may_be_more_lenient_than_policy():
    decision = bound_penalty(25, 300, POLICY)  # policy allows 125.00
    assert decision.status is GuardrailStatus.ACCEPTED
    assert decision.amount == Decimal("25.00")
    assert decision.requires_human_review is False


def test_model_can_never_charge_more_than_policy():
    decision = bound_penalty(999, 300, POLICY)
    assert decision.status is GuardrailStatus.REDUCED_TO_POLICY
    assert decision.amount == Decimal("125.00")
    assert decision.llm_amount == Decimal("999.00")
    assert decision.requires_human_review is True


def test_model_can_never_exceed_the_cap():
    decision = bound_penalty(400, 900, POLICY)
    assert decision.amount == Decimal("150.00")


@pytest.mark.parametrize("raw", [None, "", "abc", -5, "NaN", "Infinity", True, {"x": 1}])
def test_unusable_model_output_falls_back_to_policy(raw):
    decision = bound_penalty(raw, 300, POLICY)
    assert decision.status is GuardrailStatus.LLM_OUTPUT_REJECTED
    assert decision.amount == Decimal("125.00")
    assert decision.requires_human_review is True


def test_unreadable_policy_charges_nothing_and_refers_to_a_human():
    decision = bound_penalty(125, 300, None)
    assert decision.status is GuardrailStatus.POLICY_UNAVAILABLE
    assert decision.amount == Decimal("0.00")
    assert decision.requires_human_review is True


def test_inside_grace_the_policy_amount_is_zero_whatever_the_model_says():
    decision = bound_penalty(25, 10, POLICY)
    assert decision.amount == Decimal("0.00")
    assert decision.status is GuardrailStatus.REDUCED_TO_POLICY


def test_surge_is_held_between_one_and_the_ceiling():
    assert clamp_surge(Decimal(10), Decimal("2.5")) == (Decimal("2.5"), True)
    assert clamp_surge(Decimal("0.4"), Decimal("2.5")) == (Decimal("1.0"), True)
    assert clamp_surge(Decimal("1.5"), Decimal("2.5")) == (Decimal("1.5"), False)


def test_velocity_is_normalised():
    assert clamp_velocity(float("nan")) == 0.0
    assert clamp_velocity(7.0) == 1.0
    assert clamp_velocity(-1.0) == 0.0
