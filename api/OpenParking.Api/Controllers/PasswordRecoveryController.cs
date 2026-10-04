using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using OpenParking.Core.Models;
using OpenParking.Infrastructure.Services;

namespace OpenParking.Api.Controllers;

[ApiController, Route("api/users")]
public class PasswordRecoveryController(PasswordRecoveryService recovery, IMemoryCache cache) : ControllerBase
{
    [HttpPost("forgot-password")]
    public async Task<IActionResult> RequestReset(RecoveryRequest request)
    {
        var key = $"recovery:{HttpContext.Connection.RemoteIpAddress}:{request.Email.Trim().ToLowerInvariant()}";
        if (cache.TryGetValue(key, out _))
            throw new AppException(ErrorCodes.ValidationFailed, "Please wait a minute before requesting another reset email.", 429);
        cache.Set(key, true, TimeSpan.FromMinutes(1));
        await recovery.RequestAsync(request.Email);
        return Ok(ApiResponse<object>.Ok(new { message = "If this email belongs to an account, a reset code has been sent. It expires in 20 minutes." }));
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> Reset(ResetPasswordRequest request)
    {
        await recovery.ResetAsync(request.Token, request.Password);
        return Ok(ApiResponse<object>.Ok(new { message = "Password changed. Sign in with your new password." }));
    }
}
public class RecoveryRequest { [Required, EmailAddress] public string Email { get; set; } = string.Empty; }
public class ResetPasswordRequest
{
    [Required, StringLength(300)] public string Token { get; set; } = string.Empty;
    [Required, StringLength(128, MinimumLength = 8)] public string Password { get; set; } = string.Empty;
}
