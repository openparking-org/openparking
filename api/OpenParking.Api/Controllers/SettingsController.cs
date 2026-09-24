using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenParking.Core.Interfaces;
using OpenParking.Infrastructure.Data;

namespace OpenParking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController(ISettingsService settingsService, AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var settings = await db.SystemSettings.AsNoTracking().ToListAsync();
        return Ok(settings.GroupBy(s => s.Category).ToDictionary(g => g.Key, g => g.ToList()));
    }

    [HttpGet("{key}")]
    public async Task<IActionResult> GetByKey(string key)
    {
        var val = await settingsService.GetStringAsync(key);
        if (string.IsNullOrEmpty(val))
            return NotFound(new { error = $"Setting '{key}' not found" });

        return Ok(new { key, value = val });
    }

    [HttpPut("{key}")]
    public async Task<IActionResult> UpdateSetting(string key, [FromBody] UpdateSettingDto dto)
    {
        await settingsService.SetAsync(key, dto.Value, updatedBy: dto.UpdatedBy ?? "Admin");
        return Ok(new { key, value = dto.Value, message = "Setting updated successfully and cache invalidated" });
    }
}

public class UpdateSettingDto
{
    public string Value { get; set; } = string.Empty;
    public string? UpdatedBy { get; set; }
}
