import pytest
from datetime import datetime, timedelta, timezone
from agents.validator import ValidatorAgent

@pytest.mark.asyncio
async def test_validator_valid_permit():
    agent = ValidatorAgent()
    future_date = (datetime.now(timezone.utc) + timedelta(days=365)).strftime("%Y-%m-%dT%H:%M:%SZ")
    result = await agent.validate_permit({
        "permit_number": "DIS-88392",
        "expiry_date": future_date,
        "jurisdiction": "City Dept of Transport"
    })
    
    assert result["valid"] is True
    assert result["confidence"] == 1.0
    assert "extracted_fields" in result

@pytest.mark.asyncio
async def test_validator_expired_permit():
    agent = ValidatorAgent()
    past_date = (datetime.now(timezone.utc) - timedelta(days=10)).strftime("%Y-%m-%dT%H:%M:%SZ")
    result = await agent.validate_permit({
        "permit_number": "DIS-88392",
        "expiry_date": past_date,
        "jurisdiction": "City Dept of Transport"
    })
    
    assert result["valid"] is False
    assert result["confidence"] < 0.5
    assert "expired" in result["reason"].lower()

@pytest.mark.asyncio
async def test_validator_missing_fields():
    agent = ValidatorAgent()
    result = await agent.validate_permit({
        "permit_number": "",
        "expiry_date": "invalid_date",
        "jurisdiction": ""
    })
    
    assert result["valid"] is False
    assert result["confidence"] == 0.0
    assert "missing" in result["reason"].lower()
