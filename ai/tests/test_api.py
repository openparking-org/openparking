import os

import pytest
from httpx import AsyncClient

from main import app

# Ensure tests use mock mode
os.environ["CF_AI_MODE"] = "mock"

@pytest.mark.asyncio
async def test_health_check():
    async with AsyncClient(app=app, base_url="http://test") as ac:
        response = await ac.get("/health")
    assert response.status_code == 200
    assert response.json() == {
        "status": "healthy",
        "service": "openparking-ai-service",
        "cf_ai_mode": "mock"
    }

@pytest.mark.asyncio
async def test_routing_endpoint():
    payload = {
        "graph": {
            "node1": {"x": 0, "y": 0, "neighbors": ["node2"]},
            "node2": {"x": 10, "y": 0, "neighbors": ["node1"]}
        },
        "entryWaypointId": "node1",
        "slotWaypointId": "node2"
    }
    
    async with AsyncClient(app=app, base_url="http://test") as ac:
        response = await ac.post("/routing/path", json=payload)
        
    assert response.status_code == 200
    data = response.json()
    assert data["path"] == ["node1", "node2"]
    assert data["distancePx"] == 10.0

@pytest.mark.asyncio
async def test_cartographer_endpoint():
    payload = {
        "north": 37.7754,
        "south": 37.7744,
        "east": -122.4188,
        "west": -122.4200,
        "cols": 2,
        "rows": 2,
        "slot_prefix": "AI"
    }

    async with AsyncClient(app=app, base_url="http://test") as ac:
        response = await ac.post("/ai/cartographer/detect", json=payload)
        
    assert response.status_code == 200
    data = response.json()
    assert data["total_detected"] == 4
    assert len(data["detected_slots"]) == 4
    assert data["algorithm"] == "YOLOv8-Aerial-PKLot-Inference"
