import os
from typing import Optional

import httpx

API_BASE_URL = os.getenv("API_BASE_URL", "http://localhost:5000")
INTERNAL_API_TOKEN = os.getenv("INTERNAL_API_TOKEN", "mock-internal-secret")


class SettingNotFoundError(KeyError):
    """Raised when a policy key is unknown and no explicit default was supplied."""


# Mirrors SystemSettingsSeeder on the API side. Used when the AI service runs
# without a live backend, for tests and offline demos.
MOCK_SETTINGS = {
    "pricing.base_hourly_rate": "5.00",
    "pricing.peak_multiplier": "1.50",
    "pricing.max_surge_multiplier": "2.50",
    "overstay.grace_period_mins": "15",
    "overstay.penalty_per_hour": "25.00",
    "overstay.max_penalty_cap": "150.00",
    "permits.auto_approve_confidence": "0.90",
    "booking.early_checkin_mins": "15",
}


async def get_config(key: str, default: Optional[str] = None) -> str:
    """
    Fetch a system setting from the ASP.NET API, or from MOCK_SETTINGS when
    running without a backend.

    An unknown key raises rather than returning a neutral-looking value. A
    missing pricing or penalty setting that quietly resolved to "0.0" would make
    an agent propose a zero charge and report it as a success, which is worse
    than failing: the caller cannot tell a genuine zero from a lookup that never
    found anything.
    """
    if os.getenv("CF_AI_MODE") == "mock" or os.getenv("ENVIRONMENT") == "testing":
        if key in MOCK_SETTINGS:
            return MOCK_SETTINGS[key]
        if default is not None:
            return default
        raise SettingNotFoundError(key)

    try:
        async with httpx.AsyncClient(timeout=5.0) as client:
            r = await client.get(
                f"{API_BASE_URL}/api/settings/{key}",
                headers={"X-Internal-Token": INTERNAL_API_TOKEN},
            )
            r.raise_for_status()
            value = r.json().get("value")
            if value is None:
                if default is not None:
                    return default
                raise SettingNotFoundError(key)
            return str(value)
    except SettingNotFoundError:
        raise
    except Exception:
        if default is not None:
            return default
        raise
