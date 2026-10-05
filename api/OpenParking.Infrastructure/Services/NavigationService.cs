using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpenParking.Core.Models;
using OpenParking.Infrastructure.Data;

namespace OpenParking.Infrastructure.Services;

public record NavigationPoint(double X, double Y);
public record NavigationResult(Guid FloorPlanId, double ImageWidthPx, double ImageHeightPx,
    List<NavigationPoint> Points, double DistancePx, List<string> Instructions);

/// <summary>Shortest path through the saved floor-plan graph; never fabricates a route.</summary>
public class NavigationService(AppDbContext db)
{
    private sealed record Waypoint(string Id, double X, double Y, string[] Neighbors);

    public async Task<NavigationResult> RouteAsync(Guid zoneId, Guid slotId)
    {
        var slot = await db.Slots.AsNoTracking().FirstOrDefaultAsync(s => s.Id == slotId && s.ZoneId == zoneId)
            ?? throw new AppException(ErrorCodes.NotFound, "Space not found in this zone.", 404);
        var plan = await db.FloorPlans.AsNoTracking().FirstOrDefaultAsync(f => f.Id == slot.FloorPlanId && f.ZoneId == zoneId)
            ?? throw new AppException(ErrorCodes.NotFound, "This space does not have a mapped floor plan yet.", 404);
        List<Waypoint> nodes;
        try { nodes = JsonSerializer.Deserialize<List<Waypoint>>(plan.WaypointGraphJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? []; }
        catch (JsonException) { throw new AppException(ErrorCodes.ValidationFailed, "The floor-plan navigation graph needs correction."); }
        if (nodes.Count == 0 || string.IsNullOrEmpty(slot.NearestWaypointId))
            throw new AppException(ErrorCodes.NotFound, "Navigation has not been mapped for this space yet.", 404);
        var graph = nodes.GroupBy(n => n.Id).ToDictionary(g => g.Key, g => g.First());
        // The editor saves the entrance first. Explicit entrance IDs take precedence.
        var start = nodes.FirstOrDefault(n => n.Id.Equals("entry", StringComparison.OrdinalIgnoreCase) ||
            n.Id.Equals("entrance", StringComparison.OrdinalIgnoreCase)) ?? nodes[0];
        if (!graph.ContainsKey(slot.NearestWaypointId))
            throw new AppException(ErrorCodes.NotFound, "The space's waypoint is missing from the floor plan.", 404);
        var distances = new Dictionary<string, double> { [start.Id] = 0 };
        var previous = new Dictionary<string, string>();
        var queue = new PriorityQueue<string, double>();
        queue.Enqueue(start.Id, 0);
        while (queue.TryDequeue(out var currentId, out var distance))
        {
            if (distance > distances[currentId]) continue;
            if (currentId == slot.NearestWaypointId) break;
            var current = graph[currentId];
            foreach (var nextId in current.Neighbors ?? [])
            {
                if (!graph.TryGetValue(nextId, out var next)) continue;
                var candidate = distance + Math.Sqrt(Math.Pow(current.X - next.X, 2) + Math.Pow(current.Y - next.Y, 2));
                if (distances.TryGetValue(nextId, out var old) && candidate >= old) continue;
                distances[nextId] = candidate;
                previous[nextId] = currentId;
                queue.Enqueue(nextId, candidate);
            }
        }
        if (!distances.ContainsKey(slot.NearestWaypointId))
            throw new AppException(ErrorCodes.NotFound, "No connected path to this space. Ask the attendant for directions.", 404);
        var ids = new List<string> { slot.NearestWaypointId };
        while (previous.TryGetValue(ids[^1], out var predecessor)) ids.Add(predecessor);
        ids.Reverse();
        var points = ids.Select(id => new NavigationPoint(graph[id].X, graph[id].Y)).ToList();
        return new NavigationResult(plan.Id, plan.ImageWidthPx > 0 ? plan.ImageWidthPx : 1000,
            plan.ImageHeightPx > 0 ? plan.ImageHeightPx : 600, points, distances[slot.NearestWaypointId],
            [$"Start at the entrance on {plan.FloorName}.", $"Follow the marked path to space {slot.SlotNumber}."]);
    }
}
