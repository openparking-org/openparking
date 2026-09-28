using OpenParking.Core.Entities;
using OpenParking.Core.Models;

namespace OpenParking.Core.Interfaces;

/// <summary>
/// Space &amp; Availability module — owned by Supun (Student 2).
/// Covers: Zone/slot CRUD, vehicle-size allocation, occupancy tracking, QR code generation,
/// floor plan management, and A* routing data.
/// </summary>
public interface IZoneService : IParkingModule
{
    // ── Zones ─────────────────────────────────────────────────────────────
    Task<PagedResult<Zone>> ListZonesAsync(PaginatedQuery query);
    Task<Zone> GetZoneAsync(Guid zoneId);
    Task<Zone> CreateZoneAsync(Zone zone, Guid actorId);
    Task<Zone> UpdateZoneAsync(Guid zoneId, UpdateZoneRequest req, Guid actorId);
    Task DeleteZoneAsync(Guid zoneId, Guid actorId);

    // ── Slots ─────────────────────────────────────────────────────────────
    Task<List<Slot>> GetAvailableSlotsAsync(Guid zoneId, SlotType? type = null);
    Task<Slot> GetSlotAsync(Guid slotId);
    Task<Slot> CreateSlotAsync(Guid zoneId, CreateSlotRequest req, Guid actorId);
    Task<List<Slot>> BatchCreateSlotsAsync(Guid zoneId, BatchCreateSlotsRequest req, Guid actorId);
    Task<Slot> UpdateSlotStatusAsync(Guid slotId, SlotStatus newStatus, Guid actorId);

    // ── Floor Plans (A* routing data) ─────────────────────────────────────
    Task<FloorPlan> GetFloorPlanAsync(Guid floorPlanId);
    Task<List<FloorPlan>> GetFloorPlansForZoneAsync(Guid zoneId);
    Task<FloorPlan> SaveFloorPlanAsync(Guid zoneId, SaveFloorPlanRequest req, Guid actorId);

    // ── Occupancy (Analyzer Agent hook) ───────────────────────────────────
    Task<OccupancyStats> GetOccupancyStatsAsync(Guid zoneId);
}

// ── Request / Result DTOs ─────────────────────────────────────────────────

public class UpdateZoneRequest
{
    public string? Name { get; set; }
    public decimal? BaseHourlyRate { get; set; }
}

public class CreateSlotRequest
{
    public string SlotNumber { get; set; } = string.Empty;
    public SlotType Type { get; set; } = SlotType.Standard;
    public int Floor { get; set; } = 0;
    public string? BoundingBoxJson { get; set; }
    public Guid? AssignedSensorId { get; set; }
    public Guid? AssignedCameraId { get; set; }
    public Guid? FloorPlanId { get; set; }
    public string? NearestWaypointId { get; set; }
    public double? CanvasX { get; set; }
    public double? CanvasY { get; set; }
    public double? CanvasWidth { get; set; }
    public double? CanvasHeight { get; set; }
}

public class BatchCreateSlotsRequest
{
    public List<CreateSlotRequest> Slots { get; set; } = [];
}

public class SaveFloorPlanRequest
{
    public string FloorName { get; set; } = string.Empty;
    public int FloorOrder { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public double ImageWidthPx { get; set; }
    public double ImageHeightPx { get; set; }
    public double? AnchorNorthWestLat { get; set; }
    public double? AnchorNorthWestLng { get; set; }
    public double? AnchorSouthEastLat { get; set; }
    public double? AnchorSouthEastLng { get; set; }
    /// <summary>Full waypoint graph JSON (array of {id, x, y, neighbors}).</summary>
    public string WaypointGraphJson { get; set; } = "[]";
}

public class OccupancyStats
{
    public Guid ZoneId { get; set; }
    public int TotalSlots { get; set; }
    public int OccupiedSlots { get; set; }
    public int ReservedSlots { get; set; }
    public int AvailableSlots { get; set; }
    public double OccupancyPercent { get; set; }
    public DateTime ComputedAt { get; set; } = DateTime.UtcNow;
}
