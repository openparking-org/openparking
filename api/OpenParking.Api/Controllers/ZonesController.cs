using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenParking.Core.Entities;
using OpenParking.Infrastructure.Data;

namespace OpenParking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ZonesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetZones()
    {
        var zones = await db.Zones
            .Include(z => z.Slots)
            .Include(z => z.FloorPlans)
            .AsNoTracking()
            .ToListAsync();
        return Ok(zones);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetZone(Guid id)
    {
        var zone = await db.Zones
            .Include(z => z.Slots)
            .Include(z => z.FloorPlans)
            .AsNoTracking()
            .FirstOrDefaultAsync(z => z.Id == id);

        if (zone == null)
            return NotFound();

        return Ok(zone);
    }

    [HttpPost]
    public async Task<IActionResult> CreateZone([FromBody] Zone zone)
    {
        db.Zones.Add(zone);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetZone), new { id = zone.Id }, zone);
    }
}
