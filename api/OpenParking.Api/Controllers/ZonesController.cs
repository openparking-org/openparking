using System.Security.Claims;
using OpenParking.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenParking.Core.Entities;
using OpenParking.Core.Interfaces;
using OpenParking.Core.Models;

namespace OpenParking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ZonesController(IZoneService zoneService, ISettingsService settingsService, NavigationService navigation) : ControllerBase
{
    // GET /api/zones — public, used by Flutter to list nearby lots
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<ZoneDto>>>> GetZones([FromQuery] PaginatedQuery query, [FromQuery] string? filter = null)
    {
        var result = await zoneService.ListZonesAsync(query, filter);

        var currency = await settingsService.GetStringAsync("pricing.default_currency", "USD");
        var dtos = result.Items.Select(z => new ZoneDto
        {
            Id          = z.Id,
            Name        = z.Name,
            Code        = z.Code,
            Latitude    = z.Latitude,
            Longitude   = z.Longitude,
            BaseHourlyRate = z.BaseHourlyRate,
            TotalCapacity  = z.TotalCapacity,
            AvailableCount = z.Slots.Count(s => s.Status == SlotStatus.Available),
            Currency = currency
        }).ToList();

        return Ok(ApiResponse<PagedResult<ZoneDto>>.Ok(new PagedResult<ZoneDto> { Items = dtos, TotalCount = result.TotalCount, Page = result.Page, PageSize = result.PageSize }, HttpContext.TraceIdentifier));
    }

    // GET /api/zones/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ZoneDetailDto>>> GetZone(Guid id)
    {
        var zone = await zoneService.GetZoneAsync(id);
        var currency = await settingsService.GetStringAsync("pricing.default_currency", "USD");
        return Ok(ApiResponse<ZoneDetailDto>.Ok(ZoneDetailDto.From(zone, currency), HttpContext.TraceIdentifier));
    }

    // POST /api/zones — ParkingAdmin or SystemAdmin only
    [Authorize(Roles = "ParkingAdmin,SystemAdmin")]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<ZoneDto>>> CreateZone([FromBody] CreateZoneRequest req)
    {
        if (!ModelState.IsValid)
            throw new AppException(ErrorCodes.ValidationFailed,
                string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

        var actorId = GetCurrentUserId() ?? throw new AppException(ErrorCodes.Unauthorized, "User context not found.", 401);

        var zone = new Zone
        {
            Name           = req.Name,
            Code           = req.Code,
            Latitude       = req.Latitude,
            Longitude      = req.Longitude,
            BaseHourlyRate = req.BaseHourlyRate,
            TotalCapacity  = req.TotalCapacity
        };

        var createdZone = await zoneService.CreateZoneAsync(zone, actorId);

        var currency = await settingsService.GetStringAsync("pricing.default_currency", "USD");
        var dto = new ZoneDto
        {
            Id = createdZone.Id, 
            Name = createdZone.Name, 
            Code = createdZone.Code,
            Latitude = createdZone.Latitude, 
            Longitude = createdZone.Longitude,
            BaseHourlyRate = createdZone.BaseHourlyRate, 
            TotalCapacity = createdZone.TotalCapacity,
            AvailableCount = 0,
            Currency = currency
        };

        return CreatedAtAction(nameof(GetZone), new { id = createdZone.Id },
            ApiResponse<ZoneDto>.Ok(dto, HttpContext.TraceIdentifier));
    }

    // PUT /api/zones/{id}
    [Authorize(Roles = "ParkingAdmin,SystemAdmin")]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ZoneDto>>> UpdateZone(Guid id, [FromBody] UpdateZoneRequest req)
    {
        var actorId = GetCurrentUserId() ?? throw new AppException(ErrorCodes.Unauthorized, "User context not found.", 401);
        var updatedZone = await zoneService.UpdateZoneAsync(id, req, actorId);
        
        var currency = await settingsService.GetStringAsync("pricing.default_currency", "USD");
        var dto = new ZoneDto
        {
            Id = updatedZone.Id, 
            Name = updatedZone.Name, 
            Code = updatedZone.Code,
            Latitude = updatedZone.Latitude, 
            Longitude = updatedZone.Longitude,
            BaseHourlyRate = updatedZone.BaseHourlyRate, 
            TotalCapacity = updatedZone.TotalCapacity,
            AvailableCount = 0,
            Currency = currency
        };

        return Ok(ApiResponse<ZoneDto>.Ok(dto, HttpContext.TraceIdentifier));
    }

    // DELETE /api/zones/{id}
    [Authorize(Roles = "ParkingAdmin,SystemAdmin")]
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteZone(Guid id)
    {
        var actorId = GetCurrentUserId() ?? throw new AppException(ErrorCodes.Unauthorized, "User context not found.", 401);
        await zoneService.DeleteZoneAsync(id, actorId);
        return Ok(ApiResponse<object>.Ok(new { }, HttpContext.TraceIdentifier));
    }

    // ── Slots ─────────────────────────────────────────────────────────────

    // GET /api/zones/{id}/slots/available
    [HttpGet("{id:guid}/slots/available")]
    public async Task<ActionResult<ApiResponse<List<SlotSummaryDto>>>> GetAvailableSlots(Guid id, [FromQuery] SlotType? type = null)
    {
        var slots = await zoneService.GetAvailableSlotsAsync(id, type);
        var dtos = slots.Select(s => new SlotSummaryDto
        {
            FloorPlanId = s.FloorPlanId, Id = s.Id, SlotNumber = s.SlotNumber, Type = s.Type.ToString(), Status = s.Status.ToString()
        }).ToList();
        return Ok(ApiResponse<List<SlotSummaryDto>>.Ok(dtos, HttpContext.TraceIdentifier));
    }

    // POST /api/zones/{id}/slots
    [Authorize(Roles = "ParkingAdmin,SystemAdmin")]
    [HttpPost("{id:guid}/slots")]
    public async Task<ActionResult<ApiResponse<SlotSummaryDto>>> CreateSlot(Guid id, [FromBody] CreateSlotRequest req)
    {
        if (!ModelState.IsValid)
            throw new AppException(ErrorCodes.ValidationFailed, "Invalid slot request");

        var actorId = GetCurrentUserId() ?? throw new AppException(ErrorCodes.Unauthorized, "User context not found.", 401);
        var slot = await zoneService.CreateSlotAsync(id, req, actorId);
        
        var dto = new SlotSummaryDto
        {
            Id = slot.Id, SlotNumber = slot.SlotNumber, Type = slot.Type.ToString(), Status = slot.Status.ToString(),
            Floor = slot.Floor, BoundingBoxJson = slot.BoundingBoxJson,
            AssignedSensorId = slot.AssignedSensorId, AssignedCameraId = slot.AssignedCameraId
        };
        return Ok(ApiResponse<SlotSummaryDto>.Ok(dto, HttpContext.TraceIdentifier));
    }

    // POST /api/zones/{id}/slots/batch
    [Authorize(Roles = "ParkingAdmin,SystemAdmin")]
    [HttpPost("{id:guid}/slots/batch")]
    public async Task<ActionResult<ApiResponse<List<SlotSummaryDto>>>> BatchCreateSlots(Guid id, [FromBody] BatchCreateSlotsRequest req)
    {
        if (!ModelState.IsValid)
            throw new AppException(ErrorCodes.ValidationFailed, "Invalid batch slot request");

        var actorId = GetCurrentUserId() ?? throw new AppException(ErrorCodes.Unauthorized, "User context not found.", 401);
        var slots = await zoneService.BatchCreateSlotsAsync(id, req, actorId);
        
        var dtos = slots.Select(slot => new SlotSummaryDto
        {
            Id = slot.Id, SlotNumber = slot.SlotNumber, Type = slot.Type.ToString(), Status = slot.Status.ToString(),
            Floor = slot.Floor, BoundingBoxJson = slot.BoundingBoxJson,
            AssignedSensorId = slot.AssignedSensorId, AssignedCameraId = slot.AssignedCameraId
        }).ToList();
        return Ok(ApiResponse<List<SlotSummaryDto>>.Ok(dtos, HttpContext.TraceIdentifier));
    }

    // GET /api/zones/slots/{slotId}
    [HttpGet("slots/{slotId:guid}")]
    public async Task<ActionResult<ApiResponse<SlotSummaryDto>>> GetSlot(Guid slotId)
    {
        var slot = await zoneService.GetSlotAsync(slotId);
        var dto = new SlotSummaryDto
        {
            Id = slot.Id, SlotNumber = slot.SlotNumber, Type = slot.Type.ToString(), Status = slot.Status.ToString(),
            Floor = slot.Floor, BoundingBoxJson = slot.BoundingBoxJson,
            AssignedSensorId = slot.AssignedSensorId, AssignedCameraId = slot.AssignedCameraId
        };
        return Ok(ApiResponse<SlotSummaryDto>.Ok(dto, HttpContext.TraceIdentifier));
    }

    // PATCH /api/zones/slots/{slotId}/status
    [Authorize(Roles = "ParkingAdmin,SystemAdmin")]
    [HttpPatch("slots/{slotId:guid}/status")]
    public async Task<ActionResult<ApiResponse<SlotSummaryDto>>> UpdateSlotStatus(Guid slotId, [FromBody] SlotStatus newStatus)
    {
        var actorId = GetCurrentUserId() ?? throw new AppException(ErrorCodes.Unauthorized, "User context not found.", 401);
        var slot = await zoneService.UpdateSlotStatusAsync(slotId, newStatus, actorId);
        
        var dto = new SlotSummaryDto
        {
            Id = slot.Id, SlotNumber = slot.SlotNumber, Type = slot.Type.ToString(), Status = slot.Status.ToString(),
            Floor = slot.Floor, BoundingBoxJson = slot.BoundingBoxJson,
            AssignedSensorId = slot.AssignedSensorId, AssignedCameraId = slot.AssignedCameraId
        };
        return Ok(ApiResponse<SlotSummaryDto>.Ok(dto, HttpContext.TraceIdentifier));
    }

    // ── Floor Plans ───────────────────────────────────────────────────────

    // GET /api/zones/{id}/floor-plans
    [HttpGet("{id:guid}/floor-plans")]
    public async Task<ActionResult<ApiResponse<List<FloorPlanSummaryDto>>>> GetFloorPlans(Guid id)
    {
        var plans = await zoneService.GetFloorPlansForZoneAsync(id);
        var dtos = plans.Select(f => new FloorPlanSummaryDto
        {
            Id = f.Id, FloorName = f.FloorName, FloorOrder = f.FloorOrder, ImageUrl = f.ImageUrl, ImageWidthPx = f.ImageWidthPx, ImageHeightPx = f.ImageHeightPx,
            AnchorNorthWestLat = f.AnchorNorthWestLat, AnchorNorthWestLng = f.AnchorNorthWestLng,
            AnchorSouthEastLat = f.AnchorSouthEastLat, AnchorSouthEastLng = f.AnchorSouthEastLng
        }).ToList();
        return Ok(ApiResponse<List<FloorPlanSummaryDto>>.Ok(dtos, HttpContext.TraceIdentifier));
    }

    // GET /api/zones/floor-plans/{floorPlanId}
    [HttpGet("floor-plans/{floorPlanId:guid}")]
    public async Task<ActionResult<ApiResponse<FloorPlanSummaryDto>>> GetFloorPlan(Guid floorPlanId)
    {
        var plan = await zoneService.GetFloorPlanAsync(floorPlanId);
        var dto = new FloorPlanSummaryDto
        {
            Id = plan.Id, FloorName = plan.FloorName, FloorOrder = plan.FloorOrder, ImageUrl = plan.ImageUrl, ImageWidthPx = plan.ImageWidthPx, ImageHeightPx = plan.ImageHeightPx,
            AnchorNorthWestLat = plan.AnchorNorthWestLat, AnchorNorthWestLng = plan.AnchorNorthWestLng,
            AnchorSouthEastLat = plan.AnchorSouthEastLat, AnchorSouthEastLng = plan.AnchorSouthEastLng
        };
        return Ok(ApiResponse<FloorPlanSummaryDto>.Ok(dto, HttpContext.TraceIdentifier));
    }

    // POST /api/zones/{id}/floor-plans
    [Authorize(Roles = "ParkingAdmin,SystemAdmin")]
    [HttpPost("{id:guid}/floor-plans")]
    public async Task<ActionResult<ApiResponse<FloorPlanSummaryDto>>> SaveFloorPlan(Guid id, [FromBody] SaveFloorPlanRequest req)
    {
        if (!ModelState.IsValid)
            throw new AppException(ErrorCodes.ValidationFailed, "Invalid floor plan request");

        var actorId = GetCurrentUserId() ?? throw new AppException(ErrorCodes.Unauthorized, "User context not found.", 401);
        var plan = await zoneService.SaveFloorPlanAsync(id, req, actorId);
        
        var dto = new FloorPlanSummaryDto
        {
            Id = plan.Id, FloorName = plan.FloorName, FloorOrder = plan.FloorOrder, ImageUrl = plan.ImageUrl, ImageWidthPx = plan.ImageWidthPx, ImageHeightPx = plan.ImageHeightPx,
            AnchorNorthWestLat = plan.AnchorNorthWestLat, AnchorNorthWestLng = plan.AnchorNorthWestLng,
            AnchorSouthEastLat = plan.AnchorSouthEastLat, AnchorSouthEastLng = plan.AnchorSouthEastLng
        };
        return Ok(ApiResponse<FloorPlanSummaryDto>.Ok(dto, HttpContext.TraceIdentifier));
    }

    // ── Occupancy ─────────────────────────────────────────────────────────

    // GET /api/zones/{id}/occupancy
    [HttpGet("{id:guid}/occupancy")]
    public async Task<ActionResult<ApiResponse<OccupancyStats>>> GetOccupancy(Guid id)
    {
        var stats = await zoneService.GetOccupancyStatsAsync(id);
        return Ok(ApiResponse<OccupancyStats>.Ok(stats, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:guid}/route")]
    public async Task<ActionResult<ApiResponse<NavigationResult>>> GetZoneRoute(Guid id, [FromQuery] Guid targetSlotId)
    {
        var route = await navigation.RouteAsync(id, targetSlotId);
        return Ok(ApiResponse<NavigationResult>.Ok(route, HttpContext.TraceIdentifier));
    }

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (Guid.TryParse(sub, out var guid)) return guid;
        return null;
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────

public class CreateZoneRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(128, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(16, MinimumLength = 1)]
    public string Code { get; set; } = string.Empty;

    public double Latitude { get; set; }
    public double Longitude { get; set; }

    [System.ComponentModel.DataAnnotations.Range(0, 1000)]
    public decimal BaseHourlyRate { get; set; } = 5.0m;

    [System.ComponentModel.DataAnnotations.Range(1, 10000)]
    public int TotalCapacity { get; set; }
}

public class ZoneDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public decimal BaseHourlyRate { get; set; }
    public int TotalCapacity { get; set; }
    public int AvailableCount { get; set; }
    public string Currency { get; set; } = "USD";
}

public class ZoneDetailDto : ZoneDto
{
    public List<SlotSummaryDto> Slots { get; set; } = [];
    public List<FloorPlanSummaryDto> FloorPlans { get; set; } = [];

    public static ZoneDetailDto From(Zone zone, string currency) => new()
    {
        Id             = zone.Id,
        Name           = zone.Name,
        Code           = zone.Code,
        Latitude       = zone.Latitude,
        Longitude      = zone.Longitude,
        BaseHourlyRate = zone.BaseHourlyRate,
        TotalCapacity  = zone.TotalCapacity,
        AvailableCount = zone.Slots.Count(s => s.Status == SlotStatus.Available),
        Currency       = currency,
        Slots = zone.Slots.Select(s => new SlotSummaryDto
        {
            FloorPlanId = s.FloorPlanId, Id = s.Id, SlotNumber = s.SlotNumber, Type = s.Type.ToString(), Status = s.Status.ToString(),
            Floor = s.Floor, BoundingBoxJson = s.BoundingBoxJson,
            AssignedSensorId = s.AssignedSensorId, AssignedCameraId = s.AssignedCameraId,
            CanvasX = s.CanvasX, CanvasY = s.CanvasY, CanvasWidth = s.CanvasWidth, CanvasHeight = s.CanvasHeight,
            NearestWaypointId = s.NearestWaypointId
        }).ToList(),
        FloorPlans = zone.FloorPlans.Select(f => new FloorPlanSummaryDto
        {
            Id = f.Id, FloorName = f.FloorName, FloorOrder = f.FloorOrder, ImageUrl = f.ImageUrl,
            AnchorNorthWestLat = f.AnchorNorthWestLat, AnchorNorthWestLng = f.AnchorNorthWestLng,
            AnchorSouthEastLat = f.AnchorSouthEastLat, AnchorSouthEastLng = f.AnchorSouthEastLng
        }).ToList()
    };
}

public class SlotSummaryDto
{
    public Guid Id { get; set; }
    public Guid? FloorPlanId { get; set; }
    public string SlotNumber { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Floor { get; set; }
    public string? BoundingBoxJson { get; set; }
    public Guid? AssignedSensorId { get; set; }
    public Guid? AssignedCameraId { get; set; }
    public double? CanvasX { get; set; }
    public double? CanvasY { get; set; }
    public double? CanvasWidth { get; set; }
    public double? CanvasHeight { get; set; }
    public string? NearestWaypointId { get; set; }
}

public class FloorPlanSummaryDto
{
    public double ImageWidthPx { get; set; }
    public double ImageHeightPx { get; set; }
    public Guid Id { get; set; }
    public string FloorName { get; set; } = string.Empty;
    public int FloorOrder { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public double? AnchorNorthWestLat { get; set; }
    public double? AnchorNorthWestLng { get; set; }
    public double? AnchorSouthEastLat { get; set; }
    public double? AnchorSouthEastLng { get; set; }
}
