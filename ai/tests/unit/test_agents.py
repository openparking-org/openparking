import pytest
from decimal import Decimal
from routing.astar import astar
from agents.validator import ValidatorAgent
from agents.analyzer import AnalyzerAgent
from agents.action import ActionAgent

@pytest.mark.asyncio
async def test_astar_pathfinding():
    graph = {
        "wp-1": {"x": 0.0, "y": 0.0, "neighbors": ["wp-2"]},
        "wp-2": {"x": 10.0, "y": 0.0, "neighbors": ["wp-1", "wp-3"]},
        "wp-3": {"x": 10.0, "y": 10.0, "neighbors": ["wp-2"]}
    }
    path = astar(graph, "wp-1", "wp-3")
    assert path == ["wp-1", "wp-2", "wp-3"]

@pytest.mark.asyncio
async def test_validator_permit():
    validator = ValidatorAgent()
    valid_res = await validator.validate_permit({
        "permit_number": "DIS-88392",
        "jurisdiction": "City Department of Transport",
        "expiry_date": "2027-12-31"
    })
    assert valid_res["valid"] is True
    assert valid_res["confidence"] > 0.9

    invalid_res = await validator.validate_permit({"permit_number": "12"})
    assert invalid_res["valid"] is False

@pytest.mark.asyncio
async def test_analyzer_occupancy():
    analyzer = AnalyzerAgent()
    res = await analyzer.analyze_zone_occupancy(100, 90, 18)
    assert res["congestion_level"] == "CRITICAL"
    assert res["requires_surge_pricing"] is True

@pytest.mark.asyncio
async def test_action_pricing():
    action = ActionAgent()
    res = await action.propose_dynamic_pricing(Decimal("5.00"), "HIGH", 0.6)
    assert res["multiplier"] >= 1.5
    assert res["calculated_rate"] >= 7.50
