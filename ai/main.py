import os
from typing import Dict, Any, List, Optional
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field

from routing.astar import astar
from agents.planner import PlannerAgent, WorkflowState, ExecutionPlan
from tools.workflow_persistence import fetch_workflow

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

class PlanRequest(BaseModel):
    workflow_id: str
    workflow_type: str
    objective: Optional[str] = None
    input_data: Dict[str, Any] = Field(default_factory=dict)

class WorkflowRequest(BaseModel):
    workflow_id: str
    workflow_type: str
    objective: Optional[str] = None
    zone_id: Optional[str] = None
    session_id: Optional[str] = None
    input_data: Dict[str, Any] = Field(default_factory=dict)
    plan: Optional[Dict[str, Any]] = None

class ResumeWorkflowRequest(BaseModel):
    workflow_id: str
    decision: str = "APPROVE"
    approved_by: Optional[str] = None
    reason: Optional[str] = None

# ---------------------------------------------------------
# API Endpoints
# ---------------------------------------------------------

from fastapi.responses import RedirectResponse

@app.get("/", include_in_schema=False)
async def root():
    """Redirects the root URL to the Swagger UI docs."""
    return RedirectResponse(url="/docs")

from agents.cartographer import CartographerAgent, CartographerResult

cartographer = CartographerAgent()

class CartographerRequest(BaseModel):
    north: float
    south: float
    east: float
    west: float
    cols: int = 5
    rows: int = 4
    slot_prefix: str = "AI"

@app.post("/ai/cartographer/detect", response_model=CartographerResult)
async def detect_slots(req: CartographerRequest) -> CartographerResult:
    """
    Detects parking bays from geospatial bounding coordinates using aerial line segmentation.
    (Phase 9.4)
    """
    return await cartographer.detect_slots(
        north=req.north,
        south=req.south,
        east=req.east,
        west=req.west,
        cols=req.cols,
        rows=req.rows,
        slot_prefix=req.slot_prefix
    )

from agents.validator import ValidatorAgent
validator = ValidatorAgent()

class PermitValidationRequest(BaseModel):
    permit_number: str
    expiry_date: str
    jurisdiction: str
    document_image_url: Optional[str] = None

@app.post("/ai/permits/validate")
async def validate_permit(req: PermitValidationRequest) -> dict:
    """
    Validates a disability permit against regulatory schema.
    Returns a confidence score for the admin's permit review workflow.
    """
    return await validator.validate_permit(req.model_dump())


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

@app.post("/workflows/plan", response_model=Dict[str, Any])
async def create_workflow_plan(req: PlanRequest) -> Dict[str, Any]:
    """Generates a structured multi-step ExecutionPlan for an objective."""
    try:
        plan = planner.create_plan(
            workflow_id=req.workflow_id,
            workflow_type=req.workflow_type,
            objective=req.objective or f"Execute {req.workflow_type} workflow",
            input_data=req.input_data
        )
        return plan.model_dump()
    except ValueError as e:
        raise HTTPException(status_code=400, detail=str(e))

@app.post("/workflows/execute")
async def execute_workflow(req: WorkflowRequest) -> Dict[str, Any]:
    """
    Executes a multi-agent workflow run. Decomposes into a structured ExecutionPlan,
    executes steps, validates outputs, and pauses if human approval is required.
    (design.md §8.1, §8.3)
    """
    try:
        if req.plan:
            plan = ExecutionPlan(**req.plan)
        else:
            plan = planner.create_plan(
                workflow_id=req.workflow_id,
                workflow_type=req.workflow_type,
                objective=req.objective or f"Execute {req.workflow_type} workflow",
                input_data=req.input_data
            )

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
            "reason": None,
            "plan": plan.model_dump(),
            "current_step": None,
            "step_results": {},
            "status": "RUNNING"
        }

        result = await planner.execute_plan(plan, state)
        return result
    except ValueError as e:
        raise HTTPException(status_code=400, detail=str(e))

@app.post("/workflows/resume")
async def resume_workflow(req: ResumeWorkflowRequest) -> Dict[str, Any]:
    """
    Resumes a paused workflow run after human administrator approval/rejection.
    (design.md §8.3)
    """
    try:
        result = await planner.resume_workflow(
            workflow_id=req.workflow_id,
            decision=req.decision,
            approved_by=req.approved_by,
            reason=req.reason
        )
        return result
    except ValueError as e:
        raise HTTPException(status_code=400, detail=str(e))

@app.get("/workflows/{workflow_id}")
async def get_workflow_status(workflow_id: str) -> Dict[str, Any]:
    """Fetches the latest execution state and step results of a workflow run."""
    wf = await fetch_workflow(workflow_id)
    if not wf:
        raise HTTPException(status_code=404, detail=f"Workflow '{workflow_id}' not found.")
    return wf

if __name__ == "__main__":
    import uvicorn
    uvicorn.run("main:app", host="0.0.0.0", port=8000, reload=True)
