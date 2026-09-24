from decimal import Decimal
from typing import Dict, Any
from tools.config_tools import get_config

class ValidatorAgent:
    """
    Student 1 Ownership: User & Access slice.
    Checks regulatory schemas, validates permit documents, and enforces config caps.
    """
    async def validate_permit(self, permit_data: Dict[str, Any]) -> Dict[str, Any]:
        permit_number = permit_data.get("permit_number", "")
        expiry_date = permit_data.get("expiry_date", "")
        jurisdiction = permit_data.get("jurisdiction", "")

        if not permit_number or len(permit_number) < 5:
            return {
                "valid": False,
                "confidence": 0.2,
                "reason": "Invalid or missing permit number"
            }

        if not jurisdiction:
            return {
                "valid": False,
                "confidence": 0.4,
                "reason": "Issuing jurisdiction authority is missing"
            }

        return {
            "valid": True,
            "confidence": 0.95,
            "extracted_fields": {
                "permit_number": permit_number,
                "expiry_date": expiry_date,
                "jurisdiction": jurisdiction
            }
        }

    async def validate_penalty_cap(self, proposed_penalty: Decimal) -> Dict[str, Any]:
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
