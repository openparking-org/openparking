"""
Input and output contracts for the Action Agent.

These models are the agent's interface. Validation lives here rather than in the
agent body so that a malformed request is rejected at the boundary with a clear
error, and so the contract is readable on its own — an evaluator can see exactly
what the agent accepts and what it promises to return without reading its logic.
"""

from __future__ import annotations

from decimal import Decimal
from enum import Enum
from typing import Any, Dict, List, Optional

from pydantic import BaseModel, Field, field_validator


class CongestionLevel(str, Enum):
    LOW = "LOW"
    MODERATE = "MODERATE"
    HIGH = "HIGH"
    CRITICAL = "CRITICAL"


class ActionType(str, Enum):
    SURGE_PRICING = "SURGE_PRICING"
    OVERSTAY_PENALTY = "OVERSTAY_PENALTY"


class ProposalStatus(str, Enum):
    #: Within policy and under the auto-approval threshold.
    AUTO_APPROVABLE = "AUTO_APPROVABLE"
    #: Within policy but consequential enough to need a human.
    NEEDS_HUMAN_APPROVAL = "NEEDS_HUMAN_APPROVAL"
    #: Policy could not be read, so no figure is trustworthy.
    FAILED_SAFE = "FAILED_SAFE"


# --------------------------------------------------------------------------
# Requests
# --------------------------------------------------------------------------

class SurgePricingRequest(BaseModel):
    """Ask for a surge multiplier given how busy a zone is."""

    zone_id: str = Field(min_length=1)
    congestion_level: CongestionLevel
    velocity_score: float = Field(ge=0.0, le=1.0, description="Normalised arrival rate, 0..1")
    base_hourly_rate: Optional[Decimal] = Field(
        default=None,
        gt=0,
        description="Zone rate. Omit to use pricing.base_hourly_rate from system settings.",
    )

    @field_validator("base_hourly_rate")
    @classmethod
    def _reject_absurd_rate(cls, value: Optional[Decimal]) -> Optional[Decimal]:
        # A caller-supplied rate is untrusted input. A typo of 50000 would be
        # multiplied by the surge and billed, so refuse it at the boundary.
        if value is not None and value > Decimal("1000"):
            raise ValueError("base_hourly_rate above 1000 is implausible; check the units")
        return value


class PenaltyProposalRequest(BaseModel):
    """Ask for an overstay penalty given how long a driver ran over."""

    session_id: str = Field(min_length=1)
    overstay_minutes: int = Field(ge=0, le=60 * 24 * 30, description="Whole minutes past the booked end time")
    base_penalty_per_hour: Optional[Decimal] = Field(
        default=None,
        gt=0,
        description="Omit to use overstay.penalty_per_hour from system settings.",
    )


# --------------------------------------------------------------------------
# Responses
# --------------------------------------------------------------------------

class PolicySnapshot(BaseModel):
    """
    The policy values the decision was actually computed from.

    Recorded on the proposal because settings are editable at runtime: without
    this, a figure questioned next week could not be reproduced, since the
    policy behind it may since have changed.
    """

    values: Dict[str, str] = Field(default_factory=dict)
    source: str = Field(default="system_settings", description="system_settings | fallback_defaults")


class TraceStep(BaseModel):
    step: str
    detail: str


class ActionProposal(BaseModel):
    """
    What the Action Agent returns. It is a *proposal*: the agent never writes to
    the booking or session tables, and nothing here takes effect until the
    orchestrator or an administrator approves it.
    """

    proposal_id: str
    action_type: ActionType
    status: ProposalStatus
    subject_id: str = Field(description="Zone id for pricing, session id for a penalty")

    amount: Decimal = Field(description="Proposed rate per hour, or total penalty")
    uncapped_amount: Decimal = Field(description="What the rule produced before policy limits")
    was_capped: bool

    rationale: str
    policy: PolicySnapshot
    trace: List[TraceStep] = Field(default_factory=list)
    tool_calls: List[Dict[str, Any]] = Field(default_factory=list)

    @property
    def requires_human_approval(self) -> bool:
        return self.status is not ProposalStatus.AUTO_APPROVABLE
