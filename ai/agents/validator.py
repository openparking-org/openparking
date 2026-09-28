from datetime import datetime, timezone
from decimal import Decimal
from typing import Any

from tools.config_tools import get_config


class ValidatorAgent:
    """
    Student 1 Ownership: User & Access slice.
    Checks regulatory schemas, validates permit documents, and enforces config caps.
    """
    async def validate_permit(self, permit_data: dict[str, Any]) -> dict[str, Any]:
        permit_number = permit_data.get("permit_number", "")
        expiry_date_str = permit_data.get("expiry_date", "")
        jurisdiction = permit_data.get("jurisdiction", "")

        issues = []
        confidence = 1.0

        if not permit_number or len(permit_number) < 5:
            issues.append("Invalid or missing permit number")
            confidence -= 0.5

        if not jurisdiction:
            issues.append("Issuing jurisdiction authority is missing")
            confidence -= 0.3

        try:
            # e.g., "2027-12-31T00:00:00Z" or "2027-12-31"
            if "T" in expiry_date_str:
                expiry = datetime.fromisoformat(expiry_date_str.replace("Z", "+00:00"))
            else:
                expiry = datetime.strptime(expiry_date_str, "%Y-%m-%d").replace(tzinfo=timezone.utc)
                
            if expiry < datetime.now(timezone.utc):
                issues.append(f"Permit expired on {expiry_date_str}")
                confidence -= 0.8
        except (ValueError, TypeError):
            issues.append("Invalid or missing expiry date format")
            confidence -= 0.4

        confidence = max(0.0, round(confidence, 2))

        if issues:
            return {
                "valid": False,
                "confidence": confidence,
                "issues": issues,
                "reason": "; ".join(issues)
            }

        return {
            "valid": True,
            "confidence": confidence,
            "extracted_fields": {
                "permit_number": permit_number,
                "expiry_date": expiry_date_str,
                "jurisdiction": jurisdiction
            }
        }

    async def validate_penalty_cap(self, proposed_penalty: Decimal) -> dict[str, Any]:
        cap_str = await get_config("overstay.max_penalty_cap", default="150.00")
        max_penalty = Decimal(cap_str)
        if proposed_penalty > max_penalty:
            return {
                "valid": False,
                "reason": f"Proposed penalty {proposed_penalty} exceeds system cap of {max_penalty}",
                "adjusted_penalty": max_penalty
            }
        return {
            "valid": True,
            "penalty": proposed_penalty
        }
