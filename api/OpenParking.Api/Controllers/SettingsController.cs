using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Globalization;
using System.Security.Claims;
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
    [Authorize(Roles = "SystemAdmin")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateSetting(string key, [FromBody] UpdateSettingDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Value))
            throw new AppException(ErrorCodes.ValidationFailed, "A setting value is required.");
        var grouped = await settingsService.GetAllGroupedAsync();
        var setting = grouped.Values.SelectMany(x => x).FirstOrDefault(x => x.Key == key)
            ?? throw new AppException(ErrorCodes.NotFound, "Setting not found.", 404);
        if (key == "pricing.default_currency")
        {
            if (!new[] { "USD", "EUR", "GBP", "LKR" }.Contains(dto.Value))
                throw new AppException(ErrorCodes.ValidationFailed, "Choose a supported currency.");
        }
        else if (bool.TryParse(setting.Value, out _))
        {
            if (!bool.TryParse(dto.Value, out _)) throw new AppException(ErrorCodes.ValidationFailed, "Enter true or false.");
        }
        else if (decimal.TryParse(setting.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
        {
            if (!decimal.TryParse(dto.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) || value < 0 ||
                (key.Contains("confidence") && value > 1))
                throw new AppException(ErrorCodes.ValidationFailed, "Enter a nonnegative number; confidence must be between 0 and 1.");
        }
        await settingsService.SetAsync(key, dto.Value, updatedBy: User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "Admin");
        return Ok(ApiResponse<object>.Ok(new { key, value = dto.Value, message = "Setting updated successfully and cache invalidated" }, HttpContext.TraceIdentifier));
    }
}

public class UpdateSettingDto
{
    public string Value { get; set; } = string.Empty;
    public string? UpdatedBy { get; set; }
}
