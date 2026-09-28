from routing.astar import astar


def test_astar_simple_path():
    graph = {
        "A": {"x": 0, "y": 0, "neighbors": ["B"]},
        "B": {"x": 10, "y": 0, "neighbors": ["A", "C"]},
        "C": {"x": 10, "y": 10, "neighbors": ["B"]}
    }
    
    path = astar(graph, "A", "C")
    assert path == ["A", "B", "C"]

def test_astar_no_path():
    graph = {
        "A": {"x": 0, "y": 0, "neighbors": []},
        "B": {"x": 10, "y": 10, "neighbors": []}
    }
    
    path = astar(graph, "A", "B")
    assert path is None

def test_astar_invalid_nodes():
    graph = {
        "A": {"x": 0, "y": 0, "neighbors": ["B"]}
    }
    
    path = astar(graph, "A", "Z")
    assert path is None
