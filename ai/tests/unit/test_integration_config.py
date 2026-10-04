from unittest.mock import AsyncMock, MagicMock, patch

import pytest

from tools.config_tools import get_config
from tools.llm import get_llm


def test_live_model_uses_native_endpoint_and_json_mode(monkeypatch):
    monkeypatch.setenv("CF_AI_MODE", "live")
    monkeypatch.setenv("CF_ACCOUNT_ID", "test-account")
    monkeypatch.setenv("CF_AI_TOKEN", "test-token")
    monkeypatch.delenv("CF_AI_GATEWAY", raising=False)
    with patch("langchain_cloudflare.ChatCloudflareWorkersAI") as model:
        get_llm()
    options = model.call_args.kwargs
    assert "base_url" not in options
    assert options["ai_gateway"] is None
    assert options["model_kwargs"]["response_format"] == {"type": "json_object"}
    assert options["max_tokens"] == 1024


def test_gateway_is_explicitly_configurable(monkeypatch):
    monkeypatch.setenv("CF_AI_MODE", "live")
    monkeypatch.setenv("CF_ACCOUNT_ID", "test-account")
    monkeypatch.setenv("CF_AI_TOKEN", "test-token")
    monkeypatch.setenv("CF_AI_GATEWAY", "configured-gateway")
    with patch("langchain_cloudflare.ChatCloudflareWorkersAI") as model:
        get_llm()
    assert model.call_args.kwargs["ai_gateway"] == "configured-gateway"


@pytest.mark.asyncio
async def test_settings_tool_reads_backend_response_envelope(monkeypatch):
    monkeypatch.setenv("CF_AI_MODE", "live")
    monkeypatch.delenv("ENVIRONMENT", raising=False)
    response = MagicMock()
    response.json.return_value = {"success": True, "data": {"key": "overstay.max_penalty_cap", "value": "75.00"}}
    client = AsyncMock()
    client.get.return_value = response
    with patch("tools.config_tools.httpx.AsyncClient") as factory:
        factory.return_value.__aenter__.return_value = client
        value = await get_config("overstay.max_penalty_cap", "150.00")
    assert value == "75.00"
