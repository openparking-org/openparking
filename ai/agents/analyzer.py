from typing import Any, Literal

from pydantic import BaseModel, Field

from tools.pricing import ZoneObservation


class OccupancyAnalysis(BaseModel):
    occupancy_rate: float = Field(ge=0, le=1)
    velocity_score: float = Field(ge=0, le=1)
    congestion_level: Literal["LOW", "MODERATE", "HIGH", "CRITICAL"]
    demand_level: Literal["LOW", "MODERATE", "HIGH", "CRITICAL"]
    trend: Literal["INCREASING", "DECREASING", "STABLE", "UNKNOWN"]
    requires_surge_pricing: bool
    confidence: float = Field(ge=0, le=1)
    factors: list[str]
    unavailable_data: list[str]


class AnalyzerAgent:
    """Calculate measurements from observations; never ask a model to invent them."""

    async def analyze_zone_occupancy(
        self,
        total_slots: int,
        occupied_slots: int,
        recent_arrivals: int | None,
        recent_departures: int | None = None,
        reservation_demand: int | None = None,
    ) -> dict[str, Any]:
        data = ZoneObservation(
            total_slots=total_slots,
            occupied_slots=occupied_slots,
            base_hourly_rate=0,
            recent_arrivals=recent_arrivals,
            recent_departures=recent_departures,
            reservation_demand=reservation_demand,
        )
        occupancy = data.occupied_slots / data.total_slots
        # Arrival pressure: 20% of capacity entering in the measured 15-minute window = 1.
        velocity = (
            min(1.0, data.recent_arrivals / (data.total_slots * 0.2))
            if data.recent_arrivals is not None
            else 0.0
        )
        level = (
            "CRITICAL"
            if occupancy >= 0.9
            else "HIGH"
            if occupancy >= 0.7
            else "MODERATE"
            if occupancy >= 0.5
            else "LOW"
        )
        trend = "UNKNOWN"
        factors = [f"{occupancy:.0%} occupancy"]
        if recent_arrivals is not None and recent_departures is not None:
            delta = recent_arrivals - recent_departures
            trend = (
                "INCREASING" if delta > 0 else "DECREASING" if delta < 0 else "STABLE"
            )
            factors.append(
                f"{recent_arrivals} arrivals / {recent_departures} departures in 15 minutes"
            )
        if velocity > 0.7:
            factors.append("High measured arrival pressure")
        if reservation_demand is not None:
            factors.append(
                f"{reservation_demand} confirmed upcoming reservations in one hour"
            )
        unavailable = [
            name
            for name, value in [
                ("recent_arrivals", recent_arrivals),
                ("recent_departures", recent_departures),
                ("reservation_demand", reservation_demand),
            ]
            if value is None
        ]
        unavailable.append("historical_occupancy")
        # Evidence completeness heuristic, not a calibrated probability.
        confidence = 0.95 - 0.03 * (len(unavailable) - 1)
        demand = level
        if level == "MODERATE" and (
            velocity > 0.7 or (reservation_demand or 0) / total_slots >= 0.3
        ):
            demand = "HIGH"
        return OccupancyAnalysis(
            occupancy_rate=occupancy,
            velocity_score=velocity,
            congestion_level=level,
            demand_level=demand,
            trend=trend,
            requires_surge_pricing=demand in ("HIGH", "CRITICAL"),
            confidence=confidence,
            factors=factors,
            unavailable_data=unavailable,
        ).model_dump()
