import os
from typing import Dict, Any, List, Optional
from fastapi import FastAPI, HTTPException, Header
from pydantic import BaseModel, Field

from routing.astar import astar
from agents.planner import PlannerAgent, WorkflowState

app = FastAPI(
    title="OpenParking AI Service",
    description="Multi-Agent LangGraph Orchestration & A* Pathfinding Service",
    version="1.0.0"
)

planner = PlannerAgent()

# ---------------------------------------------------------
# Request & Response Models
# ---------------------------------------------------------

class HealthResponse(BaseModel):
    status: str
    service: str
    cf_ai_mode: str

class RoutingRequest(BaseModel):
    graph: Dict[str, Dict[str, Any]]
    entryWaypointId: str
    slotWaypointId: str

class Coordinate(BaseModel):
    x: float
    y: float

class RoutingResponse(BaseModel):
    path: List[str]
    coordinates: List[Coordinate]
    distancePx: float

class WorkflowRequest(BaseModel):
    workflow_id: str
    workflow_type: str
    zone_id: Optional[str] = None
    session_id: Optional[str] = None
    input_data: Dict[str, Any] = Field(default_factory=dict)

# ---------------------------------------------------------
# API Endpoints
# ---------------------------------------------------------

@app.get("/health", response_model=HealthResponse)
async def health_check() -> HealthResponse:
    return HealthResponse(
        status="healthy",
        service="openparking-ai-service",
        cf_ai_mode=os.getenv("CF_AI_MODE", "mock")
    )

@app.post("/routing/path", response_model=RoutingResponse)
async def calculate_route(req: RoutingRequest) -> RoutingResponse:
    path = astar(req.graph, req.entryWaypointId, req.slotWaypointId)
    if not path:
        raise HTTPException(status_code=404, detail="No navigable path found between specified waypoints")

    coordinates: List[Coordinate] = []
    total_dist = 0.0
    for i, wp_id in enumerate(path):
        wp = req.graph[wp_id]
        coordinates.append(Coordinate(x=float(wp["x"]), y=float(wp["y"])))
        if i > 0:
            prev_wp = req.graph[path[i-1]]
            dx = float(wp["x"]) - float(prev_wp["x"])
            dy = float(wp["y"]) - float(prev_wp["y"])
            total_dist += (dx**2 + dy**2)**0.5

    return RoutingResponse(
        path=path,
        coordinates=coordinates,
        distancePx=round(total_dist, 2)
    )

@app.post("/workflows/execute")
async def execute_workflow(req: WorkflowRequest) -> Dict[str, Any]:
    state: WorkflowState = {
        "workflow_id": req.workflow_id,
        "workflow_type": req.workflow_type,
        "zone_id": req.zone_id,
        "session_id": req.session_id,
        "input_data": req.input_data,
        "analysis": None,
        "action_proposal": None,
        "validation": None,
        "final_decision": None,
        "reason": None
    }

    if req.workflow_type == "OVERSTAY_ENFORCEMENT":
        result = await planner.execute_overstay_flow(state)
    elif req.workflow_type == "DYNAMIC_PRICING":
        result = await planner.execute_pricing_flow(state)
    else:
        raise HTTPException(status_code=400, detail=f"Unsupported workflow type: {req.workflow_type}")

    return result

if __name__ == "__main__":
    import uvicorn
    uvicorn.run("main:app", host="0.0.0.0", port=8000, reload=True)
