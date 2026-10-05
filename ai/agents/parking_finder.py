"""Read-only, evidence-based parking recommendation agent. No model guesses or bookings."""
from typing import Literal, TypedDict
from uuid import UUID, uuid4

from langgraph.graph import END, START, StateGraph
from pydantic import BaseModel, Field


class ParkingCandidate(BaseModel):
    zoneId: UUID
    name: str = Field(max_length=200)
    availableCount: int = Field(ge=1)
    distanceMeters: float = Field(ge=0, allow_inf_nan=False)
    hourlyRate: float = Field(ge=0, allow_inf_nan=False)
    currency: str = Field(max_length=12)


class ParkingRequest(BaseModel):
    preference: Literal['nearest', 'cheapest', 'balanced'] = 'nearest'
    radiusMeters: float = Field(default=25000, gt=0, le=100000, allow_inf_nan=False)
    candidates: list[ParkingCandidate] = Field(default_factory=list, max_length=50)


class ParkingState(TypedDict, total=False):
    request: ParkingRequest
    eligible: list[ParkingCandidate]
    ranked: list[ParkingCandidate]
    recommendations: list[dict]
    steps: list[str]


class ParkingFinderAgent:
    def __init__(self):
        graph = StateGraph(ParkingState)
        graph.add_node('filter', self._filter)
        graph.add_node('rank', self._rank)
        graph.add_node('validate', self._validate)
        graph.add_edge(START, 'filter')
        graph.add_edge('filter', 'rank')
        graph.add_edge('rank', 'validate')
        graph.add_edge('validate', END)
        self.graph = graph.compile()

    def _filter(self, state):
        req = state['request']
        # Deduplicate authoritative backend observations before selecting alternatives.
        candidates = {str(c.zoneId): c for c in req.candidates
                      if c.distanceMeters <= req.radiusMeters and c.availableCount > 0}
        return {'eligible': list(candidates.values()), 'steps': ['Checked live availability and search radius']}

    def _rank(self, state):
        req = state['request']
        eligible = state['eligible']
        max_rate = max(1, max((c.hourlyRate for c in eligible), default=1))
        def key(c):
            if req.preference == 'cheapest':
                # Rates in different currencies cannot be compared without exchange rates.
                return (c.currency, c.hourlyRate, c.distanceMeters, str(c.zoneId))
            if req.preference == 'balanced':
                score = .7 * c.distanceMeters / req.radiusMeters + .3 * c.hourlyRate / max_rate
                return (score, c.distanceMeters, str(c.zoneId))
            return (c.distanceMeters, c.hourlyRate, str(c.zoneId))
        return {'ranked': sorted(eligible, key=key)[:3],
                'steps': state['steps'] + [f'Ranked candidates using {req.preference} preference']}

    def _validate(self, state):
        req = state['request']
        observations = {str(c.zoneId): c for c in state['eligible']}
        recommendations = []
        for c in state['ranked']:
            assert observations[str(c.zoneId)] == c and c.availableCount > 0
            priority = {'nearest': 'Closest available parking', 'cheapest': 'Lowest hourly price',
                        'balanced': 'Balances distance and hourly price'}[req.preference]
            recommendations.append({**c.model_dump(mode='json'),
                'reason': f'{priority}: {c.distanceMeters / 1000:.2f} km away, '
                          f'{c.availableCount} matching spaces available.'})
        return {'recommendations': recommendations,
                'steps': state['steps'] + ['Validated selections against backend observations']}

    async def recommend(self, request: ParkingRequest):
        state = await self.graph.ainvoke({'request': request}, config={'recursion_limit': 6})
        return {'requestId': str(uuid4()), 'agent': 'ParkingFinderAgent', 'status': 'completed',
                'distanceType': 'straight_line', 'steps': state['steps'],
                'recommendations': state['recommendations'],
                'message': 'Availability may change. Select a zone to choose and reserve a space.'
                    if state['recommendations'] else 'No matching available parking within your search radius.'}
