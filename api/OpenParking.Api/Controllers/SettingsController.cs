using Microsoft.AspNetCore.Mvc;
using OpenParking.Core.Interfaces;
using OpenParking.Core.Entities;
using OpenParking.Core.Models;

namespace OpenParking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController(ISettingsService settingsService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<SystemSetting>>>>> GetAll()
    {
        var result = await settingsService.GetAllGroupedAsync();
        return Ok(ApiResponse<Dictionary<string, List<SystemSetting>>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{key}")]
    public async Task<ActionResult<ApiResponse<object>>> GetByKey(string key)
    {
        var val = await settingsService.GetStringAsync(key);
        if (string.IsNullOrEmpty(val))
            throw new AppException(ErrorCodes.NotFound, $"Setting '{key}' not found", 404);

        return Ok(ApiResponse<object>.Ok(new { key, value = val }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{key}")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateSetting(string key, [FromBody] UpdateSettingDto dto)
    {
        await settingsService.SetAsync(key, dto.Value, updatedBy: dto.UpdatedBy ?? "Admin");
        return Ok(ApiResponse<object>.Ok(new { key, value = dto.Value, message = "Setting updated successfully and cache invalidated" }, HttpContext.TraceIdentifier));
    }
}

public class UpdateSettingDto
{
    public string Value { get; set; } = string.Empty;
    public string? UpdatedBy { get; set; }
}
