import heapq
from dataclasses import dataclass, field
from typing import Any


@dataclass(order=True)
class _Node:
    f_score: float
    waypoint_id: str = field(compare=False)

def astar(
    graph: dict[str, dict[str, Any]],   # { id: { x, y, neighbors: [id] } }
    start_id: str,                      # entry waypoint ID
    goal_id: str,                       # nearest waypoint to the booked slot
) -> list[str] | None:
    """
    Returns an ordered list of waypoint IDs from start to goal,
    or None if no path exists.
    Uses Euclidean distance as the heuristic h(n).
    """
    if start_id not in graph or goal_id not in graph:
        return None

    open_set: list[_Node] = []
    heapq.heappush(open_set, _Node(0.0, start_id))

    came_from: dict[str, str] = {}
    g_score: dict[str, float] = {start_id: 0.0}

    def h(node_id: str) -> float:
        """Euclidean distance heuristic."""
        a, b = graph[node_id], graph[goal_id]
        return ((a['x'] - b['x'])**2 + (a['y'] - b['y'])**2) ** 0.5

    while open_set:
        current = heapq.heappop(open_set).waypoint_id

        if current == goal_id:
            # Reconstruct path
            path = []
            while current in came_from:
                path.append(current)
                current = came_from[current]
            path.append(start_id)
            return list(reversed(path))

        for neighbor_id in graph[current].get('neighbors', []):
            if neighbor_id not in graph:
                continue
            dx = graph[current]['x'] - graph[neighbor_id]['x']
            dy = graph[current]['y'] - graph[neighbor_id]['y']
            tentative_g = g_score[current] + (dx**2 + dy**2)**0.5

            if tentative_g < g_score.get(neighbor_id, float('inf')):
                came_from[neighbor_id] = current
                g_score[neighbor_id] = tentative_g
                f = tentative_g + h(neighbor_id)
                heapq.heappush(open_set, _Node(f, neighbor_id))

    return None
