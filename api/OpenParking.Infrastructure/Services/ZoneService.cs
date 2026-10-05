using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenParking.Core.Entities;
using OpenParking.Core.Interfaces;
using OpenParking.Core.Models;
using OpenParking.Infrastructure.Data;
using OpenParking.Infrastructure.Extensions;

namespace OpenParking.Infrastructure.Services;

/// <summary>
/// Space &amp; Availability module service — Student 2 (Supun).
/// Handles zone/slot CRUD, occupancy tracking, and floor plan management.
/// Design reference: design.md §3, §16, §17
/// </summary>
public class ZoneService(
    AppDbContext db,
    ILogger<ZoneService> logger,
    IRealtimeNotifier notifier) : IZoneService
{
    // ── IParkingModule ─────────────────────────────────────────────────────
    public string ModuleName => "Space & Availability";

    public async Task<HealthStatus> HealthCheckAsync()
    {
        try
        {
            await db.Zones.CountAsync();
            return HealthStatus.Healthy;
        }
        catch
        {
            return HealthStatus.Unhealthy;
        }
    }

    public Task<ModuleMetrics> GetMetricsAsync() =>
        Task.FromResult(new ModuleMetrics { ModuleName = ModuleName });

    // ── Zones ──────────────────────────────────────────────────────────────

    public async Task<PagedResult<Zone>> ListZonesAsync(PaginatedQuery query, string? filter = null)
    {
        var q = db.Zones
            .Include(z => z.Slots)
            .AsNoTracking()
            .AsQueryable();
        q = filter switch
        {
            "Available" => q.Where(z => z.Slots.Any(s => s.Status == SlotStatus.Available)),
            "Disability" => q.Where(z => z.Slots.Any(s => s.Type == SlotType.Accessible)),
            "EV Charging" => q.Where(z => z.Slots.Any(s => s.Type == SlotType.EV)),
            _ => q
        };


        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(z => z.Name.Contains(query.Search) || z.Code.Contains(query.Search));

        q = query.SortBy switch
        {
            "name"          => query.SortDir == "asc" ? q.OrderBy(z => z.Name) : q.OrderByDescending(z => z.Name),
            "totalCapacity" => query.SortDir == "asc" ? q.OrderBy(z => z.TotalCapacity) : q.OrderByDescending(z => z.TotalCapacity),
            _               => query.SortDir == "asc" ? q.OrderBy(z => z.CreatedAt) : q.OrderByDescending(z => z.CreatedAt)
        };

        return await q.ToPagedResultAsync(query);
    }

    public async Task<Zone> GetZoneAsync(Guid zoneId)
    {
        return await db.Zones
            .Include(z => z.Slots)
            .Include(z => z.FloorPlans)
            .AsNoTracking()
            .FirstOrDefaultAsync(z => z.Id == zoneId)
            ?? throw new AppException(ErrorCodes.NotFound, $"Zone '{zoneId}' not found.", 404);
    }

    public async Task<Zone> CreateZoneAsync(Zone zone, Guid actorId)
    {
        var duplicate = await db.Zones.AnyAsync(z => z.Code == zone.Code.ToUpperInvariant());
        if (duplicate)
            throw new AppException(ErrorCodes.DuplicateZoneCode,
                $"A zone with code '{zone.Code}' already exists.", 409);

        zone.Id        = Guid.NewGuid();
        zone.Code      = zone.Code.ToUpperInvariant().Trim();
        zone.CreatedAt = DateTime.UtcNow;
        zone.UpdatedAt = DateTime.UtcNow;
        zone.CreatedBy = actorId.ToString();

        db.Zones.Add(zone);
        await db.SaveChangesAsync();

        logger.LogInformation("Zone created: {Code} ({Name}) by {ActorId}", zone.Code, zone.Name, actorId);
        return zone;
    }

    public async Task<Zone> UpdateZoneAsync(Guid zoneId, UpdateZoneRequest req, Guid actorId)
    {
        if ((req.Name != null && (string.IsNullOrWhiteSpace(req.Name) || req.Name.Length > 128)) || req.BaseHourlyRate is < 0 or > 1000)
            throw new AppException(ErrorCodes.ValidationFailed, "Enter a valid zone name and hourly rate.");
        var zone = await db.Zones.FindAsync(zoneId)
            ?? throw new AppException(ErrorCodes.NotFound, "Zone not found.", 404);

        if (req.BaseHourlyRate.HasValue && req.BaseHourlyRate.Value != zone.BaseHourlyRate)
            db.AuditLogs.Add(new AuditLog { EntityType = "Zone", EntityId = zoneId, Action = "PriceChanged",
                ActorUserId = actorId, PayloadJson = JsonSerializer.Serialize(new { previous_rate = zone.BaseHourlyRate, rate = req.BaseHourlyRate.Value }) });
        if (req.Name is not null)        zone.Name = req.Name.Trim();
        if (req.BaseHourlyRate.HasValue) zone.BaseHourlyRate = req.BaseHourlyRate.Value;
        zone.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        logger.LogInformation("Zone updated: {ZoneId} by {ActorId}", zoneId, actorId);
        return zone;
    }

    public async Task DeleteZoneAsync(Guid zoneId, Guid actorId)
    {
        if (await db.Bookings.AnyAsync(b => b.Slot != null && b.Slot.ZoneId == zoneId))
            throw new AppException(ErrorCodes.ValidationFailed, "Zones with booking history cannot be deleted.", 409);
        var zone = await db.Zones.FindAsync(zoneId)
            ?? throw new AppException(ErrorCodes.NotFound, "Zone not found.", 404);

        // Prevent deleting zones that still have non-cancelled bookings
        var hasActive = await db.Slots
            .AnyAsync(s => s.ZoneId == zoneId &&
                           (s.Status == SlotStatus.Occupied || s.Status == SlotStatus.Reserved));

        if (hasActive)
            throw new AppException(ErrorCodes.ZoneHasActiveSlots,
                "Cannot delete a zone that has occupied or reserved slots.", 409);

        db.Zones.Remove(zone);
        await db.SaveChangesAsync();
        logger.LogInformation("Zone deleted: {ZoneId} by {ActorId}", zoneId, actorId);
    }

    // ── Slots ──────────────────────────────────────────────────────────────

    public async Task<List<Slot>> GetAvailableSlotsAsync(Guid zoneId, SlotType? type = null)
    {
        var q = db.Slots
            .Include(s => s.Zone)
            .Where(s => s.ZoneId == zoneId && s.Status == SlotStatus.Available)
            .AsNoTracking();

        if (type.HasValue)
            q = q.Where(s => s.Type == type.Value);

        return await q.OrderBy(s => s.SlotNumber).ToListAsync();
    }

    public async Task<Slot> GetSlotAsync(Guid slotId)
    {
        return await db.Slots.Include(s => s.Zone).AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == slotId)
            ?? throw new AppException(ErrorCodes.NotFound, "Slot not found.", 404);
    }

    public async Task<Slot> CreateSlotAsync(Guid zoneId, CreateSlotRequest req, Guid actorId)
    {
        // Zone must exist
        var zoneExists = await db.Zones.AnyAsync(z => z.Id == zoneId);
        if (!zoneExists)
            throw new AppException(ErrorCodes.NotFound, "Zone not found.", 404);

        var slot = new Slot
        {
            Id          = Guid.NewGuid(),
            ZoneId      = zoneId,
            SlotNumber  = req.SlotNumber.Trim().ToUpperInvariant(),
            Type        = req.Type,
            Status      = SlotStatus.Available,
            Floor       = req.Floor,
            BoundingBoxJson = req.BoundingBoxJson,
            AssignedSensorId = req.AssignedSensorId,
            AssignedCameraId = req.AssignedCameraId,
            FloorPlanId = req.FloorPlanId,
            CanvasX     = req.CanvasX,
            CanvasY     = req.CanvasY,
            CanvasWidth = req.CanvasWidth,
            CanvasHeight = req.CanvasHeight,
            NearestWaypointId = req.NearestWaypointId,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow,
            CreatedBy   = actorId.ToString()
        };

        db.Slots.Add(slot);
        await db.SaveChangesAsync();

        logger.LogInformation("Slot created: {Number} in zone {ZoneId}", slot.SlotNumber, zoneId);
        return slot;
    }

    public async Task<List<Slot>> BatchCreateSlotsAsync(Guid zoneId, BatchCreateSlotsRequest req, Guid actorId)
    {
        var zoneExists = await db.Zones.AnyAsync(z => z.Id == zoneId);
        if (!zoneExists)
            throw new AppException(ErrorCodes.NotFound, "Zone not found.", 404);

        var createdSlots = new List<Slot>();
        foreach (var item in req.Slots)
        {
            var slot = new Slot
            {
                Id                = Guid.NewGuid(),
                ZoneId            = zoneId,
                SlotNumber        = item.SlotNumber.Trim().ToUpperInvariant(),
                Type              = item.Type,
                Status            = SlotStatus.Available,
                Floor             = item.Floor,
                BoundingBoxJson   = item.BoundingBoxJson,
                AssignedSensorId  = item.AssignedSensorId,
                AssignedCameraId  = item.AssignedCameraId,
                FloorPlanId       = item.FloorPlanId,
                CanvasX           = item.CanvasX,
                CanvasY           = item.CanvasY,
                CanvasWidth       = item.CanvasWidth,
                CanvasHeight      = item.CanvasHeight,
                NearestWaypointId = item.NearestWaypointId,
                CreatedAt         = DateTime.UtcNow,
                UpdatedAt         = DateTime.UtcNow,
                CreatedBy         = actorId.ToString()
            };
            createdSlots.Add(slot);
        }

        db.Slots.AddRange(createdSlots);
        await db.SaveChangesAsync();

        logger.LogInformation("Batch created {Count} slots in zone {ZoneId} by {ActorId}", createdSlots.Count, zoneId, actorId);
        return createdSlots;
    }

    public async Task<Slot> UpdateSlotStatusAsync(Guid slotId, SlotStatus newStatus, Guid actorId)
    {
        var slot = await db.Slots.FindAsync(slotId)
            ?? throw new AppException(ErrorCodes.NotFound, "Slot not found.", 404);

        slot.Status    = newStatus;
        slot.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        logger.LogInformation("Slot {SlotId} status → {Status} by {ActorId}", slotId, newStatus, actorId);
        
        // Broadcast the real-time update
        await notifier.NotifySlotUpdatedAsync(slot.ZoneId.ToString(), slot.Id.ToString(), newStatus.ToString());
        
        return slot;
    }

    // ── Floor Plans ────────────────────────────────────────────────────────

    public async Task<FloorPlan> GetFloorPlanAsync(Guid floorPlanId)
    {
        return await db.FloorPlans
            .Include(f => f.Slots)
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == floorPlanId)
            ?? throw new AppException(ErrorCodes.NotFound, "Floor plan not found.", 404);
    }

    public async Task<List<FloorPlan>> GetFloorPlansForZoneAsync(Guid zoneId)
    {
        return await db.FloorPlans
            .Where(f => f.ZoneId == zoneId)
            .OrderBy(f => f.FloorOrder)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<FloorPlan> SaveFloorPlanAsync(Guid zoneId, SaveFloorPlanRequest req, Guid actorId)
    {
        if (!await db.Zones.AnyAsync(z => z.Id == zoneId))
            throw new AppException(ErrorCodes.NotFound, "Zone not found.", 404);
        if (string.IsNullOrWhiteSpace(req.FloorName))
            throw new AppException(ErrorCodes.ValidationFailed, "Floor name is required.");
        // Validate waypoint JSON before touching the DB
        try { JsonDocument.Parse(req.WaypointGraphJson); }
        catch (JsonException ex)
        {
            throw new AppException(ErrorCodes.InvalidWaypointGraph,
                $"WaypointGraphJson is not valid JSON: {ex.Message}");
        }

        // Upsert: update existing floor or create new
        var existing = await db.FloorPlans
            .FirstOrDefaultAsync(f => f.ZoneId == zoneId && f.FloorOrder == req.FloorOrder);

        if (existing is not null)
        {
            existing.FloorName        = req.FloorName;
            existing.ImageUrl         = req.ImageUrl;
            existing.ImageWidthPx     = req.ImageWidthPx;
            existing.ImageHeightPx    = req.ImageHeightPx;
            existing.AnchorNorthWestLat = req.AnchorNorthWestLat;
            existing.AnchorNorthWestLng = req.AnchorNorthWestLng;
            existing.AnchorSouthEastLat = req.AnchorSouthEastLat;
            existing.AnchorSouthEastLng = req.AnchorSouthEastLng;
            existing.WaypointGraphJson = req.WaypointGraphJson;
            existing.UpdatedAt        = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return existing;
        }

        var plan = new FloorPlan
        {
            Id              = Guid.NewGuid(),
            ZoneId          = zoneId,
            FloorName       = req.FloorName,
            FloorOrder      = req.FloorOrder,
            ImageUrl        = req.ImageUrl,
            ImageWidthPx    = req.ImageWidthPx,
            ImageHeightPx   = req.ImageHeightPx,
            AnchorNorthWestLat = req.AnchorNorthWestLat,
            AnchorNorthWestLng = req.AnchorNorthWestLng,
            AnchorSouthEastLat = req.AnchorSouthEastLat,
            AnchorSouthEastLng = req.AnchorSouthEastLng,
            WaypointGraphJson = req.WaypointGraphJson,
            CreatedAt       = DateTime.UtcNow,
            UpdatedAt       = DateTime.UtcNow,
            CreatedBy       = actorId.ToString()
        };

        db.FloorPlans.Add(plan);
        await db.SaveChangesAsync();

        logger.LogInformation("FloorPlan saved: {FloorName} for zone {ZoneId}", req.FloorName, zoneId);
        return plan;
    }

    // ── Occupancy ──────────────────────────────────────────────────────────

    public async Task<OccupancyStats> GetOccupancyStatsAsync(Guid zoneId)
    {
        var zoneExists = await db.Zones.AnyAsync(z => z.Id == zoneId);
        if (!zoneExists)
            throw new AppException(ErrorCodes.NotFound, "Zone not found.", 404);

        var slots = await db.Slots
            .Where(s => s.ZoneId == zoneId)
            .AsNoTracking()
            .ToListAsync();

        var occupied  = slots.Count(s => s.Status == SlotStatus.Occupied);
        var reserved  = slots.Count(s => s.Status == SlotStatus.Reserved);
        var available = slots.Count(s => s.Status == SlotStatus.Available);
        var total     = slots.Count;

        return new OccupancyStats
        {
            ZoneId          = zoneId,
            TotalSlots      = total,
            OccupiedSlots   = occupied,
            ReservedSlots   = reserved,
            AvailableSlots  = available,
            OccupancyPercent = total == 0 ? 0 : Math.Round((double)occupied / total * 100, 1),
            ComputedAt      = DateTime.UtcNow
        };
    }
}
