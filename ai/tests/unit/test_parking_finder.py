import pytest
from pydantic import ValidationError
from agents.parking_finder import ParkingFinderAgent, ParkingRequest


def candidate(n, distance, rate=10):
    return {'zoneId': f'00000000-0000-0000-0000-{n:012d}', 'name': f'Zone {n}',
            'availableCount': 2, 'distanceMeters': distance, 'hourlyRate': rate, 'currency': 'LKR'}


@pytest.mark.asyncio
async def test_nearest_excludes_distant_candidates_and_returns_trace():
    req = ParkingRequest(candidates=[candidate(1, 1000), candidate(2, 100), candidate(3, 30000)])
    result = await ParkingFinderAgent().recommend(req)
    assert [r['name'] for r in result['recommendations']] == ['Zone 2', 'Zone 1']
    assert result['status'] == 'completed' and len(result['steps']) == 3
    assert result['distanceType'] == 'straight_line'


@pytest.mark.asyncio
async def test_cheapest_and_balanced_use_actual_rates():
    candidates = [candidate(1, 100, 100), candidate(2, 500, 5)]
    for preference in ('cheapest', 'balanced'):
        result = await ParkingFinderAgent().recommend(ParkingRequest(preference=preference, candidates=candidates))
        assert result['recommendations'][0]['name'] == 'Zone 2'
        assert result['recommendations'][0]['hourlyRate'] == 5


@pytest.mark.asyncio
async def test_empty_result_and_duplicates_do_not_invent_parking():
    agent = ParkingFinderAgent()
    assert (await agent.recommend(ParkingRequest()))['recommendations'] == []
    result = await agent.recommend(ParkingRequest(candidates=[candidate(1, 1)] * 2))
    assert len(result['recommendations']) == 1


@pytest.mark.parametrize('updates', [{'distanceMeters': float('nan')}, {'availableCount': 0}, {'hourlyRate': -1}])
def test_invalid_observations_rejected(updates):
    with pytest.raises(ValidationError):
        ParkingRequest(candidates=[{**candidate(1, 1), **updates}])


def test_invalid_preference_rejected():
    with pytest.raises(ValidationError):
        ParkingRequest(preference='invent')


def test_endpoint_requires_internal_auth(monkeypatch):
    from fastapi.testclient import TestClient
    from main import app
    monkeypatch.setenv('INTERNAL_API_TOKEN', 'parking-test')
    client = TestClient(app)
    assert client.post('/ai/parking/recommend', json={}).status_code == 401
    assert client.post('/ai/parking/recommend', headers={'X-Internal-Token': 'parking-test'}, json={}).status_code == 200
