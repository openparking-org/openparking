"""Backend data tools and deterministic money/policy models for pricing."""

import os
from datetime import datetime
from decimal import ROUND_HALF_EVEN, Decimal
from typing import Any
from uuid import UUID

import httpx
from pydantic import BaseModel, ConfigDict, Field, model_validator


class PricingPolicy(BaseModel):
    model_config = ConfigDict(allow_inf_nan=False)
    is_enabled: bool = True
    max_surge_multiplier: Decimal = Field(default=Decimal("2.5"), ge=1, le=100)
    surge_critical_multiplier: Decimal = Field(default=Decimal(2), ge=1, le=100)
    surge_high_multiplier: Decimal = Field(default=Decimal("1.5"), ge=1, le=100)
    surge_moderate_multiplier: Decimal = Field(default=Decimal("1.2"), ge=1, le=100)
    min_hourly_rate: Decimal = Field(default=Decimal(0), ge=0)
    max_hourly_rate: Decimal = Field(default=Decimal(1000), ge=0, le=1000)
    price_change_cooldown_minutes: int = Field(default=30, ge=0, le=10080)
    auto_approve_confidence: float = Field(default=0.85, ge=0, le=1)

    @model_validator(mode="after")
    def check_bounds(self):
        if self.min_hourly_rate > self.max_hourly_rate:
            raise ValueError("Minimum rate exceeds maximum rate")
        return self


class ZoneObservation(BaseModel):
    model_config = ConfigDict(allow_inf_nan=False)
    total_slots: int = Field(gt=0, le=10000)
    occupied_slots: int = Field(ge=0)
    base_hourly_rate: Decimal = Field(ge=0, le=1000)
    recent_arrivals: int | None = Field(default=None, ge=0)
    recent_departures: int | None = Field(default=None, ge=0)
    reservation_demand: int | None = Field(default=None, ge=0)

    @model_validator(mode="after")
    def check_capacity(self):
        if self.occupied_slots > self.total_slots:
            raise ValueError("Occupied slots exceed capacity")
        return self


class PricingHistory(BaseModel):
    model_config = ConfigDict(allow_inf_nan=False)
    last_price_change_at: datetime | None
    last_approved_rate: Decimal | None = Field(default=None, ge=0, le=1000)

    @model_validator(mode="after")
    def check_timestamp(self):
        if (
            self.last_price_change_at is not None
            and self.last_price_change_at.tzinfo is None
        ):
            raise ValueError("Price-change timestamp must include a timezone")
        return self


def calculate_dynamic_rate(base_rate: Decimal, multiplier: Decimal) -> Decimal:
    if (
        not base_rate.is_finite()
        or not multiplier.is_finite()
        or base_rate < 0
        or multiplier <= 0
    ):
        raise ValueError("Rate and multiplier must be finite and nonnegative")
    return (base_rate * multiplier).quantize(Decimal(".01"), rounding=ROUND_HALF_EVEN)


class PricingTools:
    """Only aggregate data crosses the boundary; admin writes stay JWT-protected."""

    names = (
        "get_zone_status",
        "get_pricing_config",
        "get_recent_price_changes",
        "get_reservation_demand",
    )

    async def request(self, method: str, path: str, payload=None) -> dict[str, Any]:
        token = os.getenv("INTERNAL_API_TOKEN", "")
        if not token:
            raise ValueError("Internal backend credential is not configured")
        base = os.getenv("API_BASE_URL", "http://localhost:5000").rstrip("/")
        async with httpx.AsyncClient(timeout=5) as client:
            response = await client.request(
                method, base + path, json=payload, headers={"X-Internal-Token": token}
            )
            response.raise_for_status()
            body = response.json()
            return body.get("data", body)

    async def call(self, name: str, zone_id: str | None) -> dict[str, Any]:
        if name not in self.names:
            raise ValueError("Unknown pricing tool")
        if name == "get_pricing_config":
            # Existing public, read-only settings endpoint; no invented settings API.
            async with httpx.AsyncClient(timeout=5) as client:
                base = os.getenv("API_BASE_URL", "http://localhost:5000").rstrip("/")
                response = await client.get(base + "/api/settings")
                response.raise_for_status()
                groups = response.json()["data"]
            settings = {
                s["key"].removeprefix("pricing."): s["value"]
                for items in groups.values()
                for s in items
                if s["key"].startswith("pricing.")
            }
            required = {
                "max_surge_multiplier",
                "surge_critical_multiplier",
                "surge_high_multiplier",
                "surge_moderate_multiplier",
                "is_enabled",
            }
            if not required <= settings.keys():
                raise ValueError("Required pricing settings are missing")
            return PricingPolicy.model_validate(settings).model_dump(mode="json")
        zone = str(UUID(str(zone_id)))
        if name == "get_zone_status":
            # Reuse zone detail and its actual slot statuses, excluding maintenance capacity.
            async with httpx.AsyncClient(timeout=5) as client:
                base = os.getenv("API_BASE_URL", "http://localhost:5000").rstrip("/")
                response = await client.get(f"{base}/api/zones/{zone}")
                response.raise_for_status()
                data = response.json()["data"]
            slots = [s for s in data["slots"] if s["status"] != "Maintenance"]
            return {
                "total_slots": len(slots),
                "occupied_slots": sum(s["status"] == "Occupied" for s in slots),
                "base_hourly_rate": data["baseHourlyRate"],
            }
        suffix = "price-changes" if name == "get_recent_price_changes" else "demand"
        return await self.request("GET", f"/api/agent/pricing/zones/{zone}/{suffix}")

    async def submit(self, state: dict[str, Any]) -> dict[str, Any]:
        return await self.request(
            "PUT", f"/api/agent/pricing/workflows/{UUID(state['workflow_id'])}", state
        )

    async def fetch(self, workflow_id: str) -> dict[str, Any]:
        return await self.request(
            "GET", f"/api/agent/pricing/workflows/{UUID(workflow_id)}"
        )
