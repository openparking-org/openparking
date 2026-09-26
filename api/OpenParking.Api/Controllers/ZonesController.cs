using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenParking.Core.Entities;
using OpenParking.Core.Models;
using OpenParking.Infrastructure.Data;

namespace OpenParking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ZonesController(AppDbContext db) : ControllerBase
{
    // GET /api/zones — public, used by Flutter to list nearby lots
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<ZoneDto>>>> GetZones()
    {
        var zones = await db.Zones
            .Include(z => z.Slots)
            .AsNoTracking()
            .Select(z => new ZoneDto
            {
                Id          = z.Id,
                Name        = z.Name,
                Code        = z.Code,
                Latitude    = z.Latitude,
                Longitude   = z.Longitude,
                BaseHourlyRate = z.BaseHourlyRate,
                TotalCapacity  = z.TotalCapacity,
                AvailableCount = z.Slots.Count(s => s.Status == SlotStatus.Available)
            })
            .ToListAsync();

        return Ok(ApiResponse<List<ZoneDto>>.Ok(zones, HttpContext.TraceIdentifier));
    }

    // GET /api/zones/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ZoneDetailDto>>> GetZone(Guid id)
    {
        var zone = await db.Zones
            .Include(z => z.Slots)
            .Include(z => z.FloorPlans)
            .AsNoTracking()
            .FirstOrDefaultAsync(z => z.Id == id);

        if (zone is null)
            throw new AppException(ErrorCodes.NotFound, $"Zone {id} not found.", 404);

        return Ok(ApiResponse<ZoneDetailDto>.Ok(ZoneDetailDto.From(zone), HttpContext.TraceIdentifier));
    }

    // POST /api/zones — ParkingAdmin or SystemAdmin only
    [Authorize(Roles = "ParkingAdmin,SystemAdmin")]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<ZoneDto>>> CreateZone([FromBody] CreateZoneRequest req)
    {
        if (!ModelState.IsValid)
            throw new AppException(ErrorCodes.ValidationFailed,
                string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

        var codeExists = await db.Zones.AnyAsync(z => z.Code == req.Code.ToUpper());
        if (codeExists)
            throw new AppException(ErrorCodes.Conflict, $"Zone code '{req.Code}' is already in use.");

        var zone = new Zone
        {
            Name           = req.Name,
            Code           = req.Code.ToUpper(),
            Latitude       = req.Latitude,
            Longitude      = req.Longitude,
            BaseHourlyRate = req.BaseHourlyRate,
            TotalCapacity  = req.TotalCapacity
        };

        db.Zones.Add(zone);
        await db.SaveChangesAsync();

        var dto = new ZoneDto
        {
            Id = zone.Id, Name = zone.Name, Code = zone.Code,
            Latitude = zone.Latitude, Longitude = zone.Longitude,
            BaseHourlyRate = zone.BaseHourlyRate, TotalCapacity = zone.TotalCapacity,
            AvailableCount = 0
        };

        return CreatedAtAction(nameof(GetZone), new { id = zone.Id },
            ApiResponse<ZoneDto>.Ok(dto, HttpContext.TraceIdentifier));
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
}

public class ZoneDetailDto : ZoneDto
{
    public List<SlotSummaryDto> Slots { get; set; } = [];
    public List<FloorPlanSummaryDto> FloorPlans { get; set; } = [];

    public static ZoneDetailDto From(Zone zone) => new()
    {
        Id             = zone.Id,
        Name           = zone.Name,
        Code           = zone.Code,
        Latitude       = zone.Latitude,
        Longitude      = zone.Longitude,
        BaseHourlyRate = zone.BaseHourlyRate,
        TotalCapacity  = zone.TotalCapacity,
        AvailableCount = zone.Slots.Count(s => s.Status == SlotStatus.Available),
        Slots = zone.Slots.Select(s => new SlotSummaryDto
        {
            Id = s.Id, SlotNumber = s.SlotNumber, Type = s.Type.ToString(), Status = s.Status.ToString()
        }).ToList(),
        FloorPlans = zone.FloorPlans.Select(f => new FloorPlanSummaryDto
        {
            Id = f.Id, FloorName = f.FloorName, FloorOrder = f.FloorOrder, ImageUrl = f.ImageUrl
        }).ToList()
    };
}

public class SlotSummaryDto
{
    public Guid Id { get; set; }
    public string SlotNumber { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class FloorPlanSummaryDto
{
    public Guid Id { get; set; }
    public string FloorName { get; set; } = string.Empty;
    public int FloorOrder { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
}
