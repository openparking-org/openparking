from unittest.mock import AsyncMock, MagicMock

import pytest

from tools.reasoning import ReasoningChoice, choose


@pytest.mark.asyncio
async def test_gemini_uses_header_auth_and_validates_structured_output(monkeypatch):
    monkeypatch.setenv("AI_REASONING_PROVIDER", "gemini")
    monkeypatch.setenv("GEMINI_API_KEY", "test-key")
    monkeypatch.setenv("GEMINI_MODEL", "gemini-2.5-flash")
    response = MagicMock()
    response.json.return_value = {
        "candidates": [
            {
                "content": {
                    "parts": [
                        {
                            "text": '{"tool":"finish","action":"KEEP_PRICE","explanation":"Observed low demand"}'
                        }
                    ]
                }
            }
        ]
    }
    client = AsyncMock()
    client.post.return_value = response
    factory = MagicMock()
    factory.return_value.__aenter__ = AsyncMock(return_value=client)
    factory.return_value.__aexit__ = AsyncMock()
    monkeypatch.setattr("tools.reasoning.httpx.AsyncClient", factory)
    result = await choose({"phase": "action", "observations": {"occupancy_rate": 0.2}})
    assert result.action == "KEEP_PRICE"
    args, kwargs = client.post.call_args
    assert "test-key" not in args[0]
    assert kwargs["headers"]["x-goog-api-key"] == "test-key"
    assert "responseJsonSchema" in kwargs["json"]["generationConfig"]
    response.json.return_value["candidates"][0]["content"]["parts"][0]["text"] = (
        '{"action":"INCREASE_PRICE","proposed_rate":900}'
    )
    with pytest.raises(ValueError):
        await choose({})


def test_reasoning_schema_rejects_unknown_tools_and_actions():
    with pytest.raises(ValueError):
        ReasoningChoice(tool="delete_all_reservations")
    with pytest.raises(ValueError):
        ReasoningChoice(action="LIMIT_RESERVATIONS")
