from decimal import Decimal
from typing import Any

from langchain_core.output_parsers import JsonOutputParser
from langchain_core.prompts import PromptTemplate
from pydantic import BaseModel, Field

from tools.llm import get_llm
from tools.rag import get_retriever


class PenaltyProposal(BaseModel):
    billable_hours: int = Field(description="Calculated billable hours based on overstay duration and grace period")
    proposed_amount: float = Field(description="The final proposed penalty amount in dollars")
    reason: str = Field(description="The justification for this penalty amount based on ordinances and context")

class ActionAgent:
    """
    Student 3 Ownership: Booking & Payment slice.
    Calculates dynamic surge multipliers and proposes structured overstay penalty proposals.
    """
    def __init__(self):
        self.llm = get_llm()
        self.retriever = get_retriever()
        self.penalty_parser = JsonOutputParser(pydantic_object=PenaltyProposal)

    async def propose_dynamic_pricing(self, base_rate: Decimal, congestion_level: str, velocity_score: float, 
                                      critical_mult: float = 2.0, high_mult: float = 1.5, mod_mult: float = 1.2) -> dict[str, Any]:
        multiplier = Decimal("1.0")

        if congestion_level == "CRITICAL":
            multiplier = Decimal(str(critical_mult))
        elif congestion_level == "HIGH":
            multiplier = Decimal(str(high_mult))
        elif congestion_level == "MODERATE":
            multiplier = Decimal(str(mod_mult))

        if velocity_score > 0.7:
            multiplier += Decimal("0.2")

        calculated_rate = round(base_rate * multiplier, 2)
        return {
            "multiplier": float(multiplier),
            "calculated_rate": float(calculated_rate),
            "base_rate": float(base_rate),
            "rationale": f"Congestion is {congestion_level} with velocity {velocity_score}"
        }

    async def propose_overstay_penalty(self, overstay_minutes: int, base_penalty_per_hour: Decimal, context_data: dict[str, Any] = None) -> dict[str, Any]:
        if context_data is None:
            context_data = {}
            
        # 1. RAG Retrieval: fetch parking ordinances
        query = f"Overstay penalty regulations. Base rate is {base_penalty_per_hour}/hr."
        docs = await self.retriever.ainvoke(query)
        rag_context = "\n".join([doc.page_content for doc in docs])

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
        
        result = await chain.ainvoke({
            "rag_context": rag_context,
            "overstay_minutes": overstay_minutes,
            "first_offense": first_offense,
            "is_vip": is_vip,
            "base_penalty_per_hour": float(base_penalty_per_hour)
        })
        
        return {
            "overstay_minutes": overstay_minutes,
            "billable_hours": result.get("billable_hours"),
            "proposed_amount": result.get("proposed_amount"),
            "proposal_id": f"prop-{overstay_minutes}m-{int(result.get('proposed_amount', 0))}",
            "reason": result.get("reason")
        }
