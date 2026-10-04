from unittest.mock import AsyncMock, patch

import pytest

from agents.cartographer import CartographerAgent


@pytest.mark.asyncio
async def test_cartographer_detect_slots_no_key():
    cartographer = CartographerAgent()
    with patch("os.getenv", return_value=None):
        result = await cartographer.detect_slots(
            north=37.7754,
            south=37.7744,
            east=-122.4188,
            west=-122.4200,
            cols=2,
            rows=2,
            slot_prefix="TEST"
        )
        assert result.total_detected == 4
        assert len(result.detected_slots) == 4
        assert result.satellite_image_path is None
        assert result.algorithm == "Geometric-grid-preview"
        assert all(slot.confidence == 0.0 for slot in result.detected_slots)
        # Check that codes are formatted correctly
        codes = [s.slot_code for s in result.detected_slots]
        assert "TEST-001" in codes

@pytest.mark.asyncio
async def test_cartographer_detect_slots_with_key():
    cartographer = CartographerAgent()
    with patch("os.getenv", return_value="MOCK_API_KEY"):
        # Mock httpx AsyncClient
        with patch("httpx.AsyncClient") as mock_client_class:
            mock_client = AsyncMock()
            mock_response = AsyncMock()
            mock_response.status_code = 200
            mock_response.content = b"mock image data"
            mock_client.get.return_value = mock_response
            mock_client_class.return_value.__aenter__.return_value = mock_client
            
            with patch("builtins.open"), patch("os.makedirs"):
                result = await cartographer.detect_slots(
                    north=37.7754,
                    south=37.7744,
                    east=-122.4188,
                    west=-122.4200,
                    cols=1,
                    rows=1,
                    slot_prefix="MOCK"
                )
                assert result.total_detected == 1
                assert result.satellite_image_path is not None
                assert "satellite_" in result.satellite_image_path
