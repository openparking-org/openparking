from datetime import date, datetime, timezone
from decimal import Decimal
from typing import Any

from tools.config_tools import get_config
from tools.permit_vision import PermitVisionUnavailable, extract_permit
from tools.pricing import PricingPolicy, calculate_dynamic_rate


class ValidatorAgent:
    """
    Student 1 Ownership: User & Access slice.
    Checks regulatory schemas, validates permit documents, and enforces config caps.
    """
    def validate_pricing_proposal(self, proposal: dict[str, Any], policy: PricingPolicy,
                                   history: dict[str, Any], confidence: float) -> dict[str, Any]:
        """Authoritative guard: models/admins cannot override invalid math or policy."""
        issues = []
        try:
            current = Decimal(str(proposal['current_rate']))
            rate = Decimal(str(proposal['proposed_rate']))
            multiplier = Decimal(str(proposal['multiplier']))
            expected = current if proposal.get('action') in ('KEEP_PRICE', 'REQUEST_MANUAL_REVIEW') and multiplier == 1 else calculate_dynamic_rate(current, multiplier)
            if not rate.is_finite() or rate < 0 or rate != expected:
                issues.append('Invalid rate or pricing arithmetic')
            if multiplier > policy.max_surge_multiplier:
                issues.append('Multiplier exceeds pricing.max_surge_multiplier')
            if rate < policy.min_hourly_rate or rate > policy.max_hourly_rate:
                issues.append('Rate outside configured hourly limits')
            changed = current != rate
            action = proposal['action']
            if action not in ('KEEP_PRICE', 'INCREASE_PRICE', 'DECREASE_PRICE', 'REQUEST_MANUAL_REVIEW'):
                issues.append('Unsupported action')
            if ((action == 'INCREASE_PRICE' and rate <= current) or
                (action == 'DECREASE_PRICE' and rate >= current) or
                (action in ('KEEP_PRICE', 'REQUEST_MANUAL_REVIEW') and changed)):
                issues.append('Action does not match proposed rate')
            if changed and not policy.is_enabled:
                issues.append('Pricing is disabled')
            if changed:
                if 'last_price_change_at' not in history:
                    issues.append('Price history is unavailable')
                elif history['last_price_change_at']:
                    timestamp = datetime.fromisoformat(history['last_price_change_at'].replace('Z', '+00:00'))
                    age = (datetime.now(timezone.utc) - timestamp).total_seconds() / 60
                    if age < policy.price_change_cooldown_minutes:
                        issues.append('Pricing cooldown is active')
            if not 0 <= confidence <= 1:
                issues.append('Invalid analysis confidence')
        except (ValueError, KeyError, TypeError, ArithmeticError):
            changed = False
            issues.append('Missing or invalid pricing data')
        approval = changed or proposal.get('action') == 'REQUEST_MANUAL_REVIEW' or confidence < policy.auto_approve_confidence
        return {'valid': not issues, 'issues': issues, 'reason': '; '.join(issues) if issues else
                ('Administrator approval required for rate changes or uncertain evidence' if approval else 'Current rate maintained'),
                'requires_human_approval': approval}

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
                
            if expiry.tzinfo is None:
                expiry = expiry.replace(tzinfo=timezone.utc)
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

    async def validate_permit_document(self, permit_data: dict[str, Any]) -> dict[str, Any]:
        metadata = await self.validate_permit(permit_data)
        issues = list(metadata.get("issues", []))
        result = {
            "valid": False, "confidence": 0.0, "requires_human_approval": True,
            "authenticity_verified": False, "metadata_valid": metadata["valid"],
            "document_status": "unavailable", "extracted_fields": {},
            "issues": issues,
        }
        try:
            reading = await extract_permit(permit_data.get("document_image_url") or "")
        except (ValueError, PermitVisionUnavailable) as exc:
            issues.append(str(exc))
            result["reason"] = "; ".join(issues)
            return result
        fields = reading["fields"]
        result.update({
            "document_status": "read", "extracted_fields": fields,
            "model": reading["model"], "usage": reading["usage"],
        })
        if fields["readability"] != "readable":
            issues.append("The document is not fully readable. Administrator review is required.")
        def normalize(value):
            return " ".join(str(value or "").casefold().split())
        for name in ("permit_number", "jurisdiction"):
            if not fields[name]:
                issues.append(f"Document field {name} is missing or unreadable.")
            elif normalize(fields[name]) != normalize(permit_data.get(name)):
                issues.append(f"Document field {name} does not match the submitted information.")
        try:
            # The model must produce an unambiguous ISO calendar date.
            extracted_date = date.fromisoformat(fields["expiry_date"] or "")
            submitted_date = datetime.fromisoformat(str(permit_data["expiry_date"]).replace("Z", "+00:00")).date()
            if extracted_date != submitted_date:
                issues.append("Document expiry_date does not match the submitted information.")
            if extracted_date <= datetime.now(timezone.utc).date():
                issues.append("The document expiry date has passed or is today.")
        except (ValueError, TypeError, KeyError):
            issues.append("Document expiry_date is missing, ambiguous, or invalid.")
        result["valid"] = not issues
        # Evidence completeness score, not a probability of authenticity.
        result["confidence"] = 1.0 if not issues else 0.0
        result["reason"] = "; ".join(issues) if issues else (
            "Readable document fields match the submission. Administrator approval is required; authenticity is unverified."
        )
        return result

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
