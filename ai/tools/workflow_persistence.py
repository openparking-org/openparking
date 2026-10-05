import json
import logging
import os
from typing import Any

import httpx

logger = logging.getLogger("workflow_persistence")

API_BASE_URL = os.getenv("API_BASE_URL", "http://localhost:5000").rstrip("/")
INTERNAL_API_TOKEN = os.getenv("INTERNAL_API_TOKEN", "mock-internal-secret")

# In-memory store for unit tests, offline operation, and mock execution mode
_local_workflows: dict[str, dict[str, Any]] = {}

def clear_local_workflows() -> None:
    _local_workflows.clear()

def get_local_workflow(workflow_id: str) -> dict[str, Any] | None:
    return _local_workflows.get(workflow_id)

async def persist_workflow(workflow_data: dict[str, Any]) -> dict[str, Any]:
    """
    Saves or registers an AgentWorkflowRun record.
    Integrates with ASP.NET Core /api/agent/workflows when accessible,
    falling back to local memory store for testing/mock modes.
    (design.md §8.2, §8.3)
    """
    workflow_id = str(workflow_data.get("id") or workflow_data.get("workflow_id", ""))
    if not workflow_id:
        raise ValueError("workflow_id must be provided to persist workflow")

    # Always update local store
    _local_workflows[workflow_id] = {**workflow_data, "id": workflow_id}

    if os.getenv("CF_AI_MODE") == "mock" or os.getenv("ENVIRONMENT") == "testing":
        return _local_workflows[workflow_id]

    try:
        async with httpx.AsyncClient(timeout=4.0) as client:
            payload = {
                "id": workflow_id,
                "objective": workflow_data.get("objective", ""),
                "workflowType": workflow_data.get("workflow_type", "OVERSTAY_ENFORCEMENT"),
                "zoneId": workflow_data.get("zone_id"),
                "sessionId": workflow_data.get("session_id"),
                "planJson": json.dumps(workflow_data.get("plan")) if isinstance(workflow_data.get("plan"), (dict, list)) else str(workflow_data.get("plan") or "{}"),
                "currentStep": str(workflow_data.get("current_step") or ""),
                "stepResultsJson": json.dumps(workflow_data.get("step_results")) if isinstance(workflow_data.get("step_results"), (dict, list)) else str(workflow_data.get("step_results") or "{}"),
                "inputPayloadJson": json.dumps(workflow_data.get("input_data")) if isinstance(workflow_data.get("input_data"), (dict, list)) else str(workflow_data.get("input_data") or "{}"),
                "executionSummaryJson": json.dumps(workflow_data.get("execution_summary")) if isinstance(workflow_data.get("execution_summary"), (dict, list)) else str(workflow_data.get("execution_summary") or "{}"),
                "status": workflow_data.get("status", "RUNNING"),
                "decisionReason": workflow_data.get("decision_reason") or workflow_data.get("reason") or ""
            }

            resp = await client.post(
                f"{API_BASE_URL}/api/agent/workflows",
                headers={
                    "Content-Type": "application/json",
                    "X-Internal-Token": INTERNAL_API_TOKEN
                },
                json=payload
            )
            if resp.status_code in (200, 201):
                return resp.json()
    except Exception as e:
        logger.debug(f"Direct API persistence skipped (running with local state): {e}")

    return _local_workflows[workflow_id]

async def update_workflow_progress(workflow_id: str, updates: dict[str, Any]) -> dict[str, Any]:
    """
    Updates the execution progress, step results, and status of an existing workflow.
    """
    if workflow_id not in _local_workflows:
        _local_workflows[workflow_id] = {}
    _local_workflows[workflow_id].update(updates)

    if os.getenv("CF_AI_MODE") == "mock" or os.getenv("ENVIRONMENT") == "testing":
        return _local_workflows[workflow_id]

    try:
        async with httpx.AsyncClient(timeout=4.0) as client:
            payload = {}
            if "status" in updates:
                payload["status"] = updates["status"]
            if "current_step" in updates:
                payload["currentStep"] = updates["current_step"]
            if "plan" in updates:
                payload["planJson"] = json.dumps(updates["plan"]) if isinstance(updates["plan"], (dict, list)) else str(updates["plan"])
            if "step_results" in updates:
                payload["stepResultsJson"] = json.dumps(updates["step_results"]) if isinstance(updates["step_results"], (dict, list)) else str(updates["step_results"])
            if "execution_summary" in updates:
                payload["executionSummaryJson"] = json.dumps(updates["execution_summary"]) if isinstance(updates["execution_summary"], (dict, list)) else str(updates["execution_summary"])
            if "error_log" in updates:
                payload["errorLogJson"] = json.dumps(updates["error_log"]) if isinstance(updates["error_log"], (dict, list)) else str(updates["error_log"])
            if "reason" in updates or "decision_reason" in updates:
                payload["decisionReason"] = updates.get("decision_reason") or updates.get("reason")

            resp = await client.put(
                f"{API_BASE_URL}/api/agent/workflows/{workflow_id}",
                headers={
                    "Content-Type": "application/json",
                    "X-Internal-Token": INTERNAL_API_TOKEN
                },
                json=payload
            )
            if resp.status_code == 200:
                return resp.json()
    except Exception as e:
        logger.debug(f"Direct API progress update skipped (running with local state): {e}")

    return _local_workflows.get(workflow_id, updates)

async def fetch_workflow(workflow_id: str) -> dict[str, Any] | None:
    """
    Fetches the latest workflow state from ASP.NET Core or in-memory fallback.
    """
    if os.getenv('ENVIRONMENT') != 'testing':
        from tools.pricing import PricingTools
        try:
            pricing = await PricingTools().fetch(workflow_id)
            if pricing:
                return pricing
        except Exception:
            logger.debug('Pricing workflow record unavailable')
    if os.getenv("CF_AI_MODE") != "mock" and os.getenv("ENVIRONMENT") != "testing":
        try:
            async with httpx.AsyncClient(timeout=4.0) as client:
                resp = await client.get(
                    f"{API_BASE_URL}/api/agent/workflows/{workflow_id}",
                    headers={"X-Internal-Token": INTERNAL_API_TOKEN}
                )
                if resp.status_code == 200:
                    data = resp.json()
                    _local_workflows[workflow_id] = data
                    return data
        except Exception as e:
            logger.debug(f"Direct API fetch skipped: {e}")

    return _local_workflows.get(workflow_id)
