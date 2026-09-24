from typing import Dict, Any

class AnalyzerAgent:
    """
    Student 2 Ownership: Space & Availability slice.
    Analyzes occupancy rates, arrival velocity, and detects bottlenecks or anomalies.
    """
    async def analyze_zone_occupancy(self, total_slots: int, occupied_slots: int, recent_arrivals: int) -> Dict[str, Any]:
        if total_slots <= 0:
            occupancy_rate = 0.0
        else:
            occupancy_rate = occupied_slots / total_slots

        # Velocity score (0.0 to 1.0)
        velocity_score = min(recent_arrivals / 20.0, 1.0)

        congestion_level = "LOW"
        if occupancy_rate >= 0.85 or velocity_score >= 0.8:
            congestion_level = "CRITICAL"
        elif occupancy_rate >= 0.65 or velocity_score >= 0.5:
            congestion_level = "HIGH"
        elif occupancy_rate >= 0.40:
            congestion_level = "MODERATE"

        return {
            "occupancy_rate": round(occupancy_rate, 4),
            "velocity_score": round(velocity_score, 2),
            "congestion_level": congestion_level,
            "requires_surge_pricing": congestion_level in ["HIGH", "CRITICAL"]
        }
