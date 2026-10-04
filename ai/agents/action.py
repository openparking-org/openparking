import uuid
from decimal import Decimal, InvalidOperation
from typing import Any

from langchain_core.output_parsers import JsonOutputParser
from langchain_core.prompts import PromptTemplate
from pydantic import BaseModel, Field

from agents.action_guardrails import (
    KNOWN_CONGESTION,
    GuardrailStatus,
    PenaltyPolicy,
    bound_penalty,
    clamp_surge,
    clamp_velocity,
    money,
)
from tools.config_tools import get_config
from tools.llm import get_llm
from tools.rag import get_retriever
from tools.registry import ToolBroker, ToolRegistry, ToolUnavailableError

#: The only tools this agent may call. All are read-only: it proposes money and
#: never acts on a session, booking or permit itself.
ACTION_AGENT_TOOLS: frozenset[str] = frozenset({"settings.get", "ordinances.search"})


async def _ordinances_search(query: str) -> list[str]:
    docs = await get_retriever().ainvoke(query)
    return [doc.page_content for doc in docs]


def build_registry() -> ToolRegistry:
    """
    Every tool in the AI service. Tools owned by other slices are registered so
    the Action Agent's restriction is explicit and can be tested: asking for one
    raises ToolPermissionError rather than silently succeeding.
    """
    registry = ToolRegistry()
    registry.register("settings.get", get_config)
    registry.register("ordinances.search", _ordinances_search)

    async def _not_for_action_agent(**_: Any) -> None:  # pragma: no cover - unreachable by design
        raise AssertionError("The Action Agent must never reach a state-changing tool")

    registry.register("workflow.approve", _not_for_action_agent)
    registry.register("session.close", _not_for_action_agent)
    registry.register("permit.approve", _not_for_action_agent)
    return registry


class PenaltyProposal(BaseModel):
    billable_hours: int = Field(description="Calculated billable hours based on overstay duration and grace period")
    proposed_amount: float = Field(description="The final proposed penalty amount in dollars")
    reason: str = Field(description="The justification for this penalty amount based on ordinances and context")

class ActionAgent:
    """
    Student 3 Ownership: Booking & Payment slice.
    Calculates dynamic surge multipliers and proposes structured overstay penalty proposals.
    """
    def __init__(self, broker: ToolBroker | None = None) -> None:
        self.llm = get_llm()
        self.penalty_parser = JsonOutputParser(pydantic_object=PenaltyProposal)
        self.broker = broker or ToolBroker("action-agent", build_registry(), ACTION_AGENT_TOOLS)

    async def _setting(self, key: str, allow_zero: bool = False) -> Decimal | None:
        """A policy value read through the broker; None if missing or unusable."""
        try:
            raw = await self.broker.call("settings.get", key=key)
            value = Decimal(str(raw))
        except (ToolUnavailableError, InvalidOperation, ValueError):
            return None
        if not value.is_finite() or value < 0:
            return None
        # get_config answers "0.0" for a key it does not know. A zero grace
        # period is a real policy; a zero cap or rate is not, so those are
        # treated as unreadable rather than charging nothing as if it were policy.
        if value == 0 and not allow_zero:
            return None
        return value

    async def _penalty_policy(self) -> PenaltyPolicy | None:
        """Rate, grace and cap, all from settings. None if any cannot be read."""
        rate = await self._setting("overstay.penalty_per_hour")
        cap = await self._setting("overstay.max_penalty_cap")
        grace = await self._setting("overstay.grace_period_mins", allow_zero=True)
        if rate is None or cap is None or grace is None:
            return None
        return PenaltyPolicy(grace_minutes=int(grace), penalty_per_hour=rate, cap=cap)

    async def propose_dynamic_pricing(self, base_rate: Decimal, congestion_level: str, velocity_score: float, 
                                      critical_mult: float = 2.0, high_mult: float = 1.5, mod_mult: float = 1.2) -> dict[str, Any]:
        multiplier = Decimal("1.0")

        if congestion_level == "CRITICAL":
            multiplier = Decimal(str(critical_mult))
        elif congestion_level == "HIGH":
            multiplier = Decimal(str(high_mult))
        elif congestion_level == "MODERATE":
            multiplier = Decimal(str(mod_mult))

        mark = self.broker.mark()
        notes: list[str] = []

        if congestion_level not in KNOWN_CONGESTION:
            notes.append(f"Unknown congestion level {congestion_level!r}; no surge applied.")

        velocity = clamp_velocity(velocity_score)
        if velocity > 0.7:
            multiplier += Decimal("0.2")

        # The ceiling comes from settings, never from the caller: the per-band
        # multipliers above arrive in the request and are not trusted.
        max_surge = await self._setting("pricing.max_surge_multiplier")
        bounded, was_capped = clamp_surge(multiplier, max_surge)
        if max_surge is None:
            notes.append("pricing.max_surge_multiplier unreadable; surge held at 1.0x.")
            bounded, was_capped = Decimal("1.0"), multiplier != Decimal("1.0")
        elif was_capped:
            notes.append(f"Computed {multiplier}x exceeds the {max_surge}x ceiling; reduced to {bounded}x.")

        calculated_rate = money(base_rate * bounded)
        return {
            "multiplier": float(bounded),
            "calculated_rate": float(calculated_rate),
            "base_rate": float(base_rate),
            "rationale": f"Congestion is {congestion_level} with velocity {velocity}",
            "uncapped_multiplier": float(multiplier),
            "was_capped": was_capped,
            "guardrail_notes": notes,
            "tool_calls": [c.as_dict() for c in self.broker.calls_since(mark)],
        }

    async def propose_overstay_penalty(self, overstay_minutes: int, base_penalty_per_hour: Decimal, context_data: dict[str, Any] | None = None) -> dict[str, Any]:
        if context_data is None:
            context_data = {}
        mark = self.broker.mark()
        overstay_minutes = max(0, int(overstay_minutes))

        # 0. Policy first. Rate, grace and cap all come from settings; the
        #    caller's base_penalty_per_hour is only shown to the model as context.
        policy = await self._penalty_policy()

        # 1. RAG Retrieval: fetch parking ordinances
        query = f"Overstay penalty regulations. Base rate is {base_penalty_per_hour}/hr."
        try:
            rag_context = "\n".join(await self.broker.call("ordinances.search", query=query))
        except ToolUnavailableError:
            rag_context = "(ordinances unavailable)"

        # 2. Extract driver context
        is_vip = context_data.get("is_vip", False)
        first_offense = context_data.get("first_offense", True)

        # 3. LLM evaluation
        prompt = PromptTemplate(
            template="You are an AI parking enforcement officer.\n"
                     "Review the context and the driver's profile to calculate the correct overstay penalty.\n\n"
                     "Ordinance Context:\n{rag_context}\n\n"
                     "Driver Profile:\n"
                     "- Overstay duration: {overstay_minutes} minutes\n"
                     "- First Offense: {first_offense}\n"
                     "- VIP Status: {is_vip}\n"
                     "- Standard base penalty rate: ${base_penalty_per_hour}/hour\n\n"
                     "{format_instructions}\n",
            input_variables=["rag_context", "overstay_minutes", "first_offense", "is_vip", "base_penalty_per_hour"],
            partial_variables={"format_instructions": self.penalty_parser.get_format_instructions()},
        )

        chain = prompt | self.llm | self.penalty_parser

        llm_unavailable = False
        try:
            result = await chain.ainvoke({
                "rag_context": rag_context,
                "overstay_minutes": overstay_minutes,
                "first_offense": first_offense,
                "is_vip": is_vip,
                "base_penalty_per_hour": float(base_penalty_per_hour)
            })
            if not isinstance(result, dict):
                result = {}
        except Exception as exc:  # model or parser failure must not become a 500
            llm_unavailable = True
            result = {"reason": f"Model unavailable: {type(exc).__name__}"}

        # 4. Deterministic bound. The model can lower the amount, never raise it.
        decision = bound_penalty(result.get("proposed_amount"), overstay_minutes, policy)
        if llm_unavailable and decision.status is not GuardrailStatus.POLICY_UNAVAILABLE:
            decision.status = GuardrailStatus.LLM_UNAVAILABLE
            decision.notes = [f"Model could not be reached; used the policy amount {decision.policy_amount}."]

        return {
            "overstay_minutes": overstay_minutes,
            "billable_hours": decision.billable_hours,
            "proposed_amount": float(decision.amount),
            "proposal_id": f"prop-penalty-{uuid.uuid4().hex[:12]}",
            "reason": result.get("reason") or "; ".join(decision.notes),
            "llm_proposed_amount": None if decision.llm_amount is None else float(decision.llm_amount),
            "policy_max_amount": float(decision.policy_amount),
            "guardrail_status": decision.status.value,
            "requires_human_review": decision.requires_human_review,
            "guardrail_notes": decision.notes,
            "tool_calls": [c.as_dict() for c in self.broker.calls_since(mark)],
        }
