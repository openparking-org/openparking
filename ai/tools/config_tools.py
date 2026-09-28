import os

import httpx

API_BASE_URL = os.getenv("API_BASE_URL", "http://localhost:5000")
INTERNAL_API_TOKEN = os.getenv("INTERNAL_API_TOKEN", "mock-internal-secret")

async def get_config(key: str, default: str | None = None) -> str:
    """Validator Agent tool: fetch a system setting value dynamically from ASP.NET Core."""
    # When testing or running in mock mode without active API server, return configured defaults
    if os.getenv("CF_AI_MODE") == "mock" or os.getenv("ENVIRONMENT") == "testing":
        defaults = {
            "overstay.max_penalty_cap": "150.00",
            "overstay.grace_period_mins": "15",
            "pricing.base_hourly_rate": "5.00",
            "pricing.max_surge_multiplier": "2.5"
        }
        return defaults.get(key, default or "0.0")

    try:
        async with httpx.AsyncClient(timeout=5.0) as client:
            r = await client.get(
                f"{API_BASE_URL}/api/settings/{key}",
                headers={"X-Internal-Token": INTERNAL_API_TOKEN}
            )
            r.raise_for_status()
            return str(r.json().get("value", default))
    except Exception:
        if default is not None:
            return default
        raise
