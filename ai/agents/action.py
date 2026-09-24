from decimal import Decimal
from typing import Dict, Any

class ActionAgent:
    """
    Student 3 Ownership: Booking & Payment slice.
    Calculates dynamic surge multipliers and proposes structured overstay penalty proposals.
    """
    async def propose_dynamic_pricing(self, base_rate: Decimal, congestion_level: str, velocity_score: float) -> Dict[str, Any]:
        multiplier = Decimal("1.0")

        if congestion_level == "CRITICAL":
            multiplier = Decimal("2.0")
        elif congestion_level == "HIGH":
            multiplier = Decimal("1.5")
        elif congestion_level == "MODERATE":
            multiplier = Decimal("1.2")

        if velocity_score > 0.7:
            multiplier += Decimal("0.2")

        calculated_rate = round(base_rate * multiplier, 2)
        return {
            "multiplier": float(multiplier),
            "calculated_rate": float(calculated_rate),
            "base_rate": float(base_rate),
            "rationale": f"Congestion is {congestion_level} with velocity {velocity_score}"
        }

    async def propose_overstay_penalty(self, overstay_minutes: int, base_penalty_per_hour: Decimal) -> Dict[str, Any]:
        hours = max(1, (overstay_minutes + 59) // 60)
        penalty_amount = base_penalty_per_hour * Decimal(hours)

        return {
            "overstay_minutes": overstay_minutes,
            "billable_hours": hours,
            "proposed_amount": float(penalty_amount),
            "proposal_id": f"prop-{overstay_minutes}m-{int(penalty_amount)}"
        }
