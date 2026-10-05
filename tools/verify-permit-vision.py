"""Live Gemini smoke test using a synthetic permit image, never a customer document.

Usage: python tools/verify-permit-vision.py path/to/synthetic-permit.png
The root .env supplies the server-side API key; no credentials are printed.
"""
import asyncio
import base64
import json
import os
import sys
from pathlib import Path

import httpx

from dotenv import load_dotenv

root = Path(__file__).resolve().parents[1]
load_dotenv(root / ".env")
sys.path.insert(0, str(root / "ai"))

from agents.validator import ValidatorAgent  # noqa: E402


async def main():
    image = base64.b64encode(Path(sys.argv[1]).read_bytes()).decode()
    payload = {
        "permit_number": "DEMO-12345", "expiry_date": "2099-12-31",
        "jurisdiction": "Demo Transport Authority", "document_image_url": "data:image/png;base64," + image,
    }
    if "--endpoint" in sys.argv:
        url = sys.argv[sys.argv.index("--endpoint") + 1]
        async with httpx.AsyncClient(timeout=30) as client:
            response = await client.post(url, json=payload, headers={"X-Internal-Token": os.getenv("INTERNAL_API_TOKEN", "")})
            print("Endpoint status:", response.status_code)
            if response.status_code != 200:
                return 1
            result = response.json()
    else:
        result = await ValidatorAgent().validate_permit_document(payload)
    print(json.dumps(result, indent=2))
    return 0 if result["valid"] and result["requires_human_approval"] else 1


if __name__ == "__main__":
    sys.exit(asyncio.run(main()))
