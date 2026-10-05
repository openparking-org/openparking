import base64
import json

import httpx
import pytest
from httpx import ASGITransport, AsyncClient

from agents.planner import PlannerAgent
from agents.validator import ValidatorAgent
from main import app
from tools.permit_vision import (
    PermitVisionUnavailable,
    decode_permit_image,
    extract_permit,
)

IMAGE = "data:image/png;base64," + base64.b64encode(b"\x89PNG\r\n\x1a\nfixture").decode()
FIELDS = {"permit_number": "DIS-88392", "expiry_date": "2099-12-31", "jurisdiction": "City Transport", "readability": "readable"}
SUBMISSION = {"permit_number": "DIS-88392", "expiry_date": "2099-12-31T00:00:00Z", "jurisdiction": "City Transport", "document_image_url": IMAGE}


def provider(monkeypatch, handler):
    monkeypatch.setenv("GEMINI_API_KEY", "test-secret")
    monkeypatch.setenv("GEMINI_PERMIT_MODEL", "gemini-3.5-flash-lite")
    monkeypatch.setattr("tools.permit_vision.httpx.AsyncClient", lambda **kwargs: AsyncClient(transport=httpx.MockTransport(handler)))


def answer(fields=FIELDS):
    return httpx.Response(200, json={"candidates": [{"content": {"parts": [{"text": json.dumps(fields)}]}}],
                                    "usageMetadata": {"promptTokenCount": 1200, "candidatesTokenCount": 100}})


@pytest.mark.asyncio
async def test_image_sent_to_gemini_with_schema_and_usage(monkeypatch):
    def handler(request):
        body = json.loads(request.content)
        assert request.headers["x-goog-api-key"] == "test-secret"
        assert body["contents"][0]["parts"][1]["inlineData"]["mimeType"] == "image/png"
        assert body["generationConfig"]["responseJsonSchema"]["additionalProperties"] is False
        assert "2099" not in body["contents"][0]["parts"][0]["text"]  # no supplied answers in extraction prompt
        return answer()
    provider(monkeypatch, handler)
    result = await ValidatorAgent().validate_permit_document(SUBMISSION)
    assert result["valid"] is True
    assert result["requires_human_approval"] is True
    assert result["authenticity_verified"] is False
    assert result["usage"]["promptTokenCount"] == 1200


@pytest.mark.asyncio
@pytest.mark.parametrize("changes,issue", [
    ({"permit_number": "OTHER-99"}, "does not match"),
    ({"jurisdiction": "Different Authority"}, "does not match"),
    ({"expiry_date": "2020-01-01"}, "passed"),
    ({"expiry_date": "31/12/2099"}, "invalid"),
    ({"permit_number": None, "readability": "partial"}, "unreadable"),
])
async def test_mismatch_expiry_and_unreadable_fields_require_review(monkeypatch, changes, issue):
    provider(monkeypatch, lambda _: answer({**FIELDS, **changes}))
    result = await ValidatorAgent().validate_permit_document(SUBMISSION)
    assert result["valid"] is False
    assert result["document_status"] == "read"
    assert issue in result["reason"]
    assert result["requires_human_approval"] is True


@pytest.mark.asyncio
async def test_invalid_submitted_metadata_does_not_pass_even_when_image_matches(monkeypatch):
    provider(monkeypatch, lambda _: answer({**FIELDS, "permit_number": "123"}))
    result = await ValidatorAgent().validate_permit_document({**SUBMISSION, "permit_number": "123"})
    assert result["metadata_valid"] is False
    assert result["valid"] is False


@pytest.mark.parametrize("image", ["https://127.0.0.1/secret", "data:image/svg+xml;base64,AAAA", "data:image/png;base64,AAAA", "data:image/png;base64,=", ""])
def test_unsupported_or_invalid_images_are_rejected_without_fetching(image):
    with pytest.raises(ValueError):
        decode_permit_image(image)


def test_oversized_image_rejected():
    image = "data:image/png;base64," + base64.b64encode(b"\x89PNG\r\n\x1a\n" + b"x" * (5 * 1024 * 1024)).decode()
    with pytest.raises(ValueError):
        decode_permit_image(image)


@pytest.mark.asyncio
async def test_missing_key_leaves_manual_review_available(monkeypatch):
    monkeypatch.delenv("GEMINI_API_KEY", raising=False)
    result = await ValidatorAgent().validate_permit_document(SUBMISSION)
    assert result["document_status"] == "unavailable"
    assert "not configured" in result["reason"]


@pytest.mark.asyncio
@pytest.mark.parametrize("kind", ["http", "timeout", "malformed", "schema"])
async def test_provider_errors_are_sanitized(monkeypatch, kind):
    def handler(request):
        if kind == "http":
            return httpx.Response(429, text="test-secret provider details")
        if kind == "timeout":
            raise httpx.ReadTimeout("test-secret", request=request)
        if kind == "malformed":
            return httpx.Response(200, json={"candidates": []})
        return answer({**FIELDS, "approval": True})
    provider(monkeypatch, handler)
    with pytest.raises(PermitVisionUnavailable) as error:
        await extract_permit(IMAGE)
    assert "test-secret" not in str(error.value)


@pytest.mark.asyncio
async def test_permit_endpoint_requires_internal_token(monkeypatch):
    monkeypatch.setenv("INTERNAL_API_TOKEN", "service-secret")
    async with AsyncClient(transport=ASGITransport(app=app), base_url="http://test") as client:
        for headers in ({}, {"X-Internal-Token": "wrong"}):
            assert (await client.post("/ai/permits/validate", json=SUBMISSION, headers=headers)).status_code == 401
        monkeypatch.delenv("GEMINI_API_KEY", raising=False)
        response = await client.post("/ai/permits/validate", json=SUBMISSION, headers={"X-Internal-Token": "service-secret"})
        assert response.status_code == 200
        assert response.json()["document_status"] == "unavailable"


@pytest.mark.asyncio
async def test_permit_workflow_cannot_bypass_service_authentication(monkeypatch):
    monkeypatch.setenv("INTERNAL_API_TOKEN", "service-secret")
    async with AsyncClient(transport=ASGITransport(app=app), base_url="http://test") as client:
        response = await client.post("/workflows/execute", json={
            "workflow_id": "permit-auth", "workflow_type": "PERMIT_VALIDATION",
            "input_data": {**SUBMISSION, "backend_managed": True},
        })
        assert response.status_code == 401


@pytest.mark.asyncio
async def test_planner_routes_permit_to_document_reader_and_admin_gate(monkeypatch):
    from unittest.mock import AsyncMock
    planner = PlannerAgent()
    reading = {"valid": True, "confidence": 1.0, "requires_human_approval": True}
    monkeypatch.setattr(planner.validator, "validate_permit_document", AsyncMock(return_value=reading))
    plan = planner.create_plan("permit-demo", "PERMIT_VALIDATION", "Review permit", SUBMISSION)
    state = {}
    result = await planner._dispatch_step(plan.steps[0], state, plan)
    assert result == reading
    planner.validator.validate_permit_document.assert_awaited_once_with(SUBMISSION)
    gate = await planner._dispatch_step(plan.steps[1], state, plan)
    assert gate["requires_human_approval"] is True
