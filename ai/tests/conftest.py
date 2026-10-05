import pytest


@pytest.fixture(autouse=True)
def isolated_ai_environment(monkeypatch):
    monkeypatch.setenv("ENVIRONMENT", "testing")
    monkeypatch.setenv("CF_AI_MODE", "mock")
    monkeypatch.setenv("AI_REASONING_PROVIDER", "disabled")
