from datetime import datetime, timezone
from decimal import Decimal
from unittest.mock import AsyncMock
from uuid import uuid4

import pytest
from httpx import ASGITransport, AsyncClient

from agents.planner import PlannerAgent
from agents.validator import ValidatorAgent
from main import app
from tools.pricing import PricingPolicy, PricingTools, calculate_dynamic_rate
from tools.reasoning import ReasoningChoice
from tools.workflow_persistence import clear_local_workflows


async def evaluate(occupied=80, **data):
    planner = PlannerAgent()
    inputs = {
        "total_slots": 100,
        "occupied_slots": occupied,
        "base_hourly_rate": "5",
        "recent_arrivals": 5,
        "recent_departures": 3,
        "reservation_demand": 0,
        **data,
    }
    plan = planner.create_plan(
        str(uuid4()), "DYNAMIC_PRICING", "Evaluate this zone", inputs
    )
    return await planner.execute_plan(
        plan, {"zone_id": str(uuid4()), "input_data": inputs}
    )


@pytest.mark.asyncio
@pytest.mark.parametrize(
    "occupied,level,action,decision,rate",
    [
        (20, "LOW", "KEEP_PRICE", "AUTO_APPROVED", 5),
        (55, "MODERATE", "KEEP_PRICE", "AUTO_APPROVED", 5),
        (80, "HIGH", "INCREASE_PRICE", "PENDING_APPROVAL", 7.5),
        (95, "CRITICAL", "INCREASE_PRICE", "PENDING_APPROVAL", 10),
    ],
)
async def test_measured_demand_controls_proposal(
    occupied, level, action, decision, rate
):
    result = await evaluate(occupied)
    assert result["analysis"]["demand_level"] == level
    assert result["action_proposal"]["action"] == action
    assert result["action_proposal"]["calculated_rate"] == rate
    assert result["final_decision"] == decision
    assert result["applied"] is False
    assert result["iterations"] <= 5
    assert result["validation"]["valid"]


@pytest.mark.asyncio
async def test_arrival_velocity_and_departures_affect_assessment():
    slow = await evaluate(55)
    fast = await evaluate(55, recent_arrivals=19, recent_departures=2)
    assert fast["analysis"]["velocity_score"] > slow["analysis"]["velocity_score"]
    assert fast["analysis"]["trend"] == "INCREASING"
    assert fast["analysis"]["demand_level"] == "HIGH"
    assert fast["action_proposal"]["calculated_rate"] == 8.5


@pytest.mark.asyncio
async def test_reservations_raise_demand_without_invented_history():
    result = await evaluate(55, reservation_demand=35)
    assert result["analysis"]["demand_level"] == "HIGH"
    assert "historical_occupancy" in result["analysis"]["unavailable_data"]


@pytest.mark.asyncio
async def test_maximum_multiplier_is_authoritative():
    result = await evaluate(95, pricing_config={"max_surge_multiplier": "1.5"})
    assert result["final_decision"] == "REJECTED"
    assert "max_surge_multiplier" in result["reason"]


@pytest.mark.asyncio
@pytest.mark.parametrize(
    "data",
    [
        {"base_hourly_rate": "-5"},
        {"base_hourly_rate": "NaN"},
        {"occupied_slots": 101},
        {"occupied_slots": -1},
        {"total_slots": 0},
        {"recent_arrivals": -3},
    ],
)
async def test_invalid_inputs_rejected(data):
    result = await evaluate(**data)
    assert result["final_decision"] == "REJECTED"
    assert not result["validation"]["valid"]


@pytest.mark.asyncio
async def test_cooldown_retains_price():
    result = await evaluate(
        95, last_price_change_at=datetime.now(timezone.utc).isoformat()
    )
    assert result["action_proposal"]["action"] == "KEEP_PRICE"
    assert result["action_proposal"]["calculated_rate"] == 5
    assert "cooldown" in result["action_proposal"]["reason"].lower()


def test_validator_rejects_math_bypass_and_missing_history():
    validator = ValidatorAgent()
    proposal = {
        "current_rate": 5,
        "proposed_rate": 999,
        "multiplier": 1.5,
        "action": "INCREASE_PRICE",
    }
    assert not validator.validate_pricing_proposal(proposal, PricingPolicy(), {}, 0.95)[
        "valid"
    ]
    proposal["proposed_rate"] = 7.5
    assert not validator.validate_pricing_proposal(proposal, PricingPolicy(), {}, 0.95)[
        "valid"
    ]
    history = {"last_price_change_at": datetime.now(timezone.utc).isoformat()}
    assert not validator.validate_pricing_proposal(
        proposal, PricingPolicy(), history, 0.95
    )["valid"]


def test_decimal_calculation_repeatable():
    assert {
        calculate_dynamic_rate(Decimal("5.05"), Decimal("1.5")) for _ in range(100)
    } == {Decimal("7.58")}


@pytest.mark.asyncio
async def test_llm_outage_falls_back_without_unsafe_price(monkeypatch):
    monkeypatch.setattr(
        "tools.reasoning.choose", AsyncMock(side_effect=RuntimeError("SECRET"))
    )
    result = await evaluate(95, pricing_config={"max_surge_multiplier": "1.5"})
    assert result["llm_failed"]
    assert result["final_decision"] == "REJECTED"
    assert "SECRET" not in str(result)


@pytest.mark.asyncio
async def test_llm_cannot_surge_low_demand(monkeypatch):
    monkeypatch.setattr(
        "tools.reasoning.choose",
        AsyncMock(return_value=ReasoningChoice(action="INCREASE_PRICE")),
    )
    result = await evaluate(20)
    assert result["action_proposal"]["calculated_rate"] == 5
    assert result["final_decision"] == "PENDING_APPROVAL"
    assert result["action_proposal"]["action"] == "REQUEST_MANUAL_REVIEW"


class FakeBackend:
    names = PricingTools.names

    def __init__(self, fail=None):
        self.fail, self.saved, self.called = fail, None, []

    async def call(self, name, zone_id):
        self.called.append(name)
        if name == self.fail:
            raise RuntimeError("token and sensitive response must not escape")
        return {
            "get_zone_status": {
                "total_slots": 100,
                "occupied_slots": 90,
                "base_hourly_rate": 5,
            },
            "get_pricing_config": PricingPolicy().model_dump(mode="json"),
            "get_recent_price_changes": {"last_price_change_at": None},
            "get_reservation_demand": {
                "recent_arrivals": 18,
                "recent_departures": 2,
                "reservation_demand": 10,
            },
        }[name]

    async def fetch(self, workflow_id):
        return self.saved

    async def submit(self, state):
        self.saved = state
        return state


@pytest.mark.asyncio
@pytest.mark.parametrize(
    "fail",
    [
        None,
        "get_zone_status",
        "get_pricing_config",
        "get_recent_price_changes",
        "get_reservation_demand",
    ],
)
async def test_live_tools_override_request_data_and_fail_safely(monkeypatch, fail):
    monkeypatch.setenv("ENVIRONMENT", "production")
    backend = FakeBackend(fail)
    planner = PlannerAgent()
    planner.pricing.tools = backend
    plan = planner.create_plan(str(uuid4()), "DYNAMIC_PRICING", "Evaluate Zone A", {})
    result = await planner.execute_plan(
        plan, {"zone_id": str(uuid4()), "input_data": {"base_hourly_rate": 999}}
    )
    if fail in ("get_zone_status", "get_pricing_config", "get_recent_price_changes"):
        assert result["final_decision"] == "REJECTED"
    else:
        assert result["action_proposal"]["base_rate"] == 5
        assert result["final_decision"] == "PENDING_APPROVAL"
    assert backend.saved is not None
    assert result["iterations"] <= planner.pricing.MAX_ITERATIONS
    assert "sensitive response" not in str(result)


@pytest.mark.asyncio
async def test_live_persistence_failure_cannot_claim_success(monkeypatch):
    monkeypatch.setenv("ENVIRONMENT", "production")
    backend = FakeBackend()
    backend.submit = AsyncMock(side_effect=RuntimeError("offline"))
    planner = PlannerAgent()
    planner.pricing.tools = backend
    plan = planner.create_plan(str(uuid4()), "DYNAMIC_PRICING", "Evaluate zone", {})
    result = await planner.execute_plan(plan, {"zone_id": str(uuid4())})
    assert result["final_decision"] == "FAILED"
    assert not result["applied"]


@pytest.mark.asyncio
async def test_resume_requires_actual_backend_approval():
    backend = FakeBackend()
    backend.saved = {
        "workflow_type": "DYNAMIC_PRICING",
        "backend_status": "AwaitingApproval",
    }
    planner = PlannerAgent()
    planner.pricing.tools = backend
    result = await planner.pricing.resume(str(uuid4()), "APPROVE")
    assert result["final_decision"] == "PENDING_APPROVAL"
    assert not result["applied"]
    backend.saved.update(
        backend_status="Approved",
        approved_by=str(uuid4()),
        approved_at="2026-10-05T00:00:00Z",
        action_proposal={"current_rate": 5, "proposed_rate": 7.5},
    )
    assert (await planner.pricing.resume(str(uuid4()), "APPROVE"))[
        "final_decision"
    ] == "APPROVED"
    backend.saved["backend_status"] = "Rejected"
    assert (await planner.pricing.resume(str(uuid4()), "APPROVE"))[
        "final_decision"
    ] == "REJECTED"


@pytest.mark.asyncio
async def test_execute_endpoint_accepts_existing_contract():
    clear_local_workflows()
    async with AsyncClient(
        transport=ASGITransport(app=app), base_url="http://test"
    ) as client:
        response = await client.post(
            "/workflows/execute",
            json={
                "workflow_id": str(uuid4()),
                "workflow_type": "DYNAMIC_PRICING",
                "zone_id": str(uuid4()),
                "input_data": {
                    "total_slots": 100,
                    "occupied_slots": 95,
                    "base_hourly_rate": 5,
                    "recent_arrivals": 18,
                },
            },
        )
    assert response.status_code == 200
    assert response.json()["final_decision"] == "PENDING_APPROVAL"


@pytest.mark.asyncio
async def test_legacy_surge_endpoint_caps_and_rejects_negative():
    async with AsyncClient(
        transport=ASGITransport(app=app), base_url="http://test"
    ) as client:
        payload = {
            "base_rate": 5,
            "congestion_level": "CRITICAL",
            "velocity_score": 0.9,
            "critical_mult": 9,
            "max_surge_multiplier": 1.5,
        }
        response = await client.post("/ai/pricing/surge", json=payload)
        assert response.json()["calculated_rate"] == 7.5
        payload["base_rate"] = -5
        assert (await client.post("/ai/pricing/surge", json=payload)).status_code == 422


@pytest.mark.asyncio
async def test_repeated_high_demand_does_not_compound_an_approved_surge():
    result = await evaluate(
        95,
        base_hourly_rate="10",
        last_approved_rate=10,
        last_price_change_at="2020-01-01T00:00:00Z",
    )
    assert result["action_proposal"]["action"] == "KEEP_PRICE"
    assert result["action_proposal"]["calculated_rate"] == 10
    assert "compounding" in result["action_proposal"]["reason"]


@pytest.mark.asyncio
async def test_uncertain_analysis_stays_pending():
    result = await evaluate(20, pricing_config={"auto_approve_confidence": 0.99})
    assert result["final_decision"] == "PENDING_APPROVAL"
    assert not result["applied"]


@pytest.mark.asyncio
async def test_actual_http_tool_contracts_and_service_headers(monkeypatch):
    import httpx

    from tools.pricing import PricingTools

    monkeypatch.setenv("INTERNAL_API_TOKEN", "test-service-token")
    monkeypatch.setenv("API_BASE_URL", "http://backend.test")
    real_client = httpx.AsyncClient
    paths = []

    def respond(request):
        paths.append(request.url.path)
        if request.url.path.startswith("/api/agent/pricing/"):
            assert request.headers["X-Internal-Token"] == "test-service-token"
        if request.url.path == "/api/settings":
            settings = [
                {"key": "pricing." + key, "value": str(value)}
                for key, value in {
                    "is_enabled": "true",
                    "max_surge_multiplier": "2.5",
                    "surge_critical_multiplier": "2",
                    "surge_high_multiplier": "1.5",
                    "surge_moderate_multiplier": "1.2",
                }.items()
            ]
            return httpx.Response(200, json={"data": {"Pricing": settings}})
        if request.url.path.startswith("/api/zones/"):
            return httpx.Response(
                200,
                json={
                    "data": {
                        "baseHourlyRate": 5,
                        "slots": [
                            {"status": "Occupied"},
                            {"status": "Available"},
                            {"status": "Maintenance"},
                        ],
                    }
                },
            )
        if request.url.path.endswith("/demand"):
            return httpx.Response(
                200,
                json={
                    "recent_arrivals": 1,
                    "recent_departures": 0,
                    "reservation_demand": 0,
                },
            )
        return httpx.Response(
            200, json={"last_price_change_at": None, "last_approved_rate": None}
        )

    transport = httpx.MockTransport(respond)
    monkeypatch.setattr(
        "tools.pricing.httpx.AsyncClient",
        lambda **kwargs: real_client(transport=transport),
    )
    tools = PricingTools()
    zone = str(uuid4())
    observation = await tools.call("get_zone_status", zone)
    assert observation["total_slots"] == 2
    assert observation["occupied_slots"] == 1
    assert (await tools.call("get_pricing_config", zone))[
        "max_surge_multiplier"
    ] == "2.5"
    assert (await tools.call("get_reservation_demand", zone))["recent_arrivals"] == 1
    assert (await tools.call("get_recent_price_changes", zone))[
        "last_price_change_at"
    ] is None
    assert len(paths) == 4


@pytest.mark.asyncio
async def test_keep_price_preserves_existing_rate_precision():
    result = await evaluate(20, base_hourly_rate="5.555")
    assert result["action_proposal"]["proposed_rate"] == 5.555
    assert result["final_decision"] == "AUTO_APPROVED"
