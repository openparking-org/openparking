import os
import secrets
from typing import Any

import httpx
from dotenv import load_dotenv
from fastapi import FastAPI, Header, HTTPException, Request
from pydantic import BaseModel, Field

load_dotenv()

from agents.planner import ExecutionPlan, PlannerAgent, WorkflowState
from routing.astar import astar
from tools.workflow_persistence import fetch_workflow

app = FastAPI(
    title="OpenParking AI Service",
    description="Multi-Agent LangGraph Orchestration & A* Pathfinding Service",
    version="1.0.0"
)

planner = PlannerAgent()

from agents.parking_finder import ParkingFinderAgent, ParkingRequest

parking_finder = ParkingFinderAgent()

@app.post('/ai/parking/recommend')
async def recommend_parking(req: ParkingRequest, x_internal_token: str | None = Header(default=None)):
    expected = os.getenv('INTERNAL_API_TOKEN', '')
    if not expected or not secrets.compare_digest(x_internal_token or '', expected):
        raise HTTPException(status_code=401, detail='Internal service authentication required')
    return await parking_finder.recommend(req)

# ---------------------------------------------------------
# Request & Response Models
# ---------------------------------------------------------

class HealthResponse(BaseModel):
    status: str
    service: str
    cf_ai_mode: str

class RoutingRequest(BaseModel):
    graph: dict[str, dict[str, Any]]
    entryWaypointId: str
    slotWaypointId: str

class Coordinate(BaseModel):
    x: float
    y: float

class RoutingResponse(BaseModel):
    path: list[str]
    coordinates: list[Coordinate]
    distancePx: float

class PlanRequest(BaseModel):
    workflow_id: str
    workflow_type: str
    objective: str | None = None
    input_data: dict[str, Any] = Field(default_factory=dict)

class WorkflowRequest(BaseModel):
    workflow_id: str
    workflow_type: str
    objective: str | None = None
    zone_id: str | None = None
    session_id: str | None = None
    input_data: dict[str, Any] = Field(default_factory=dict)
    plan: dict[str, Any] | None = None

class ResumeWorkflowRequest(BaseModel):
    workflow_id: str
    decision: str = "APPROVE"
    approved_by: str | None = None
    reason: str | None = None

# ---------------------------------------------------------
# API Endpoints
# ---------------------------------------------------------

# --- Hardware Simulation / Mock ANPR (Proxy to C# Backend) ---
class SimulateEntryReq(BaseModel):
    licensePlate: str
    zoneCode: str

class SimulateExitReq(BaseModel):
    licensePlate: str

@app.post("/simulate/entry")
async def simulate_entry(req: SimulateEntryReq) -> dict:
    backend_url = os.getenv("BACKEND_API_URL", "http://localhost:5000")
    api_key = os.getenv("SIMULATION_API_KEY", "")
    
    async with httpx.AsyncClient() as client:
        resp = await client.post(
            f"{backend_url}/api/simulate/entry",
            json=req.model_dump(),
            headers={"X-Api-Key": api_key}
        )
        if resp.status_code != 200:
            raise HTTPException(status_code=resp.status_code, detail=resp.text)
        return resp.json()

@app.post("/simulate/exit")
async def simulate_exit(req: SimulateExitReq) -> dict:
    backend_url = os.getenv("BACKEND_API_URL", "http://localhost:5000")
    api_key = os.getenv("SIMULATION_API_KEY", "")
    
    async with httpx.AsyncClient() as client:
        resp = await client.post(
            f"{backend_url}/api/simulate/exit",
            json=req.model_dump(),
            headers={"X-Api-Key": api_key}
        )
        if resp.status_code != 200:
            raise HTTPException(status_code=resp.status_code, detail=resp.text)
        return resp.json()

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
    document_image_url: str | None = None

@app.post("/ai/permits/validate")
async def validate_permit(req: PermitValidationRequest, x_internal_token: str | None = Header(default=None)) -> dict:
    """
    Validates a disability permit against regulatory schema.
    Returns a confidence score for the admin's permit review workflow.
    """
    token = os.getenv("INTERNAL_API_TOKEN", "")
    if not token or not secrets.compare_digest(x_internal_token or "", token):
        raise HTTPException(status_code=401, detail="Internal service authentication required")
    return await validator.validate_permit_document(req.model_dump())

# --- Action Agent (Booking & Payment) ---
from decimal import Decimal

from agents.action import ActionAgent

action_agent = ActionAgent()

class SurgePricingRequest(BaseModel):
    base_rate: float = Field(ge=0, le=1000, allow_inf_nan=False)
    congestion_level: str
    velocity_score: float = Field(ge=0, le=1, allow_inf_nan=False)
    critical_mult: float = Field(default=2.0, ge=1, le=100, allow_inf_nan=False)
    high_mult: float = Field(default=1.5, ge=1, le=100, allow_inf_nan=False)
    mod_mult: float = Field(default=1.2, ge=1, le=100, allow_inf_nan=False)
    max_surge_multiplier: float = Field(default=2.5, ge=1, le=100, allow_inf_nan=False)

@app.post("/ai/pricing/surge")
async def calculate_surge_pricing(req: SurgePricingRequest) -> dict:
    """
    Calculates dynamic surge multipliers based on current lot capacity.
    """
    try:
        result = await action_agent.propose_dynamic_pricing(
            base_rate=Decimal(str(req.base_rate)),
            congestion_level=req.congestion_level,
            velocity_score=req.velocity_score,
            critical_mult=req.critical_mult,
            high_mult=req.high_mult,
            mod_mult=req.mod_mult
        )
    except ValueError as exc:
        raise HTTPException(status_code=422, detail=str(exc))
    # Legacy booking calculator: cap deterministically; backend also enforces its own configured cap.
    from tools.pricing import calculate_dynamic_rate
    result['multiplier'] = min(result['multiplier'], req.max_surge_multiplier)
    result['calculated_rate'] = float(calculate_dynamic_rate(Decimal(str(req.base_rate)), Decimal(str(result['multiplier']))))
    return result


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

    coordinates: list[Coordinate] = []
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

@app.post("/workflows/plan", response_model=dict[str, Any])
async def create_workflow_plan(req: PlanRequest) -> dict[str, Any]:
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
async def execute_workflow(req: WorkflowRequest, request: Request) -> dict[str, Any]:
    """
    Executes a multi-agent workflow run. Decomposes into a structured ExecutionPlan,
    executes steps, validates outputs, and pauses if human approval is required.
    (design.md §8.1, §8.3)
    """
    try:
        import secrets
        expected = os.getenv('INTERNAL_API_TOKEN', '')
        req.input_data['backend_managed'] = bool(expected and secrets.compare_digest(
            request.headers.get('X-Internal-Token', ''), expected))
        if req.plan:
            plan = ExecutionPlan(**req.plan)
            if plan.workflow_id != req.workflow_id or plan.workflow_type != req.workflow_type:
                raise ValueError('Plan identity must match the workflow request')
        else:
            plan = planner.create_plan(
                workflow_id=req.workflow_id,
                workflow_type=req.workflow_type,
                objective=req.objective or f"Execute {req.workflow_type} workflow",
                input_data=req.input_data
            )

        if any(step.action == "validate_permit" for step in plan.steps) and not req.input_data['backend_managed']:
            raise HTTPException(status_code=401, detail="Internal service authentication required for permit document reading")

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
        return dict(result)
    except ValueError as e:
        raise HTTPException(status_code=400, detail=str(e))

@app.post("/workflows/resume")
async def resume_workflow(req: ResumeWorkflowRequest) -> dict[str, Any]:
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
        return dict(result)
    except ValueError as e:
        raise HTTPException(status_code=400, detail=str(e))

@app.get("/workflows/{workflow_id}")
async def get_workflow_status(workflow_id: str) -> dict[str, Any]:
    """Fetches the latest execution state and step results of a workflow run."""
    wf = await fetch_workflow(workflow_id)
    if not wf:
        raise HTTPException(status_code=404, detail=f"Workflow '{workflow_id}' not found.")
    return wf

if __name__ == "__main__":
    import uvicorn
    uvicorn.run("main:app", host="0.0.0.0", port=8000, reload=True)
