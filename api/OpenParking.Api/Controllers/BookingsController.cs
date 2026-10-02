using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenParking.Core.Entities;
using OpenParking.Core.Interfaces;
using OpenParking.Core.Models;

namespace OpenParking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BookingsController(IBookingService bookingService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ApiResponse<Booking>>> CreateBooking([FromBody] CreateBookingRequest req)
    {
        var userId = GetCurrentUserId() ?? throw new AppException(ErrorCodes.Unauthorized, "User context not found", 401);
        var booking = await bookingService.CreateBookingAsync(req, userId);
        return Ok(ApiResponse<Booking>.Ok(booking, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<Booking>>> GetBooking(Guid id)
    {
        var booking = await bookingService.GetBookingAsync(id);
        // Authorization check - only owner or admin should see this
        var userId = GetCurrentUserId() ?? throw new AppException(ErrorCodes.Unauthorized, "User context not found", 401);
        if (booking.UserId != userId && !User.IsInRole("SystemAdmin") && !User.IsInRole("ParkingAdmin"))
        {
            return Forbid();
        }
        return Ok(ApiResponse<Booking>.Ok(booking, HttpContext.TraceIdentifier));
    }

    [HttpGet("user")]
    public async Task<ActionResult<ApiResponse<PagedResult<Booking>>>> GetUserBookings([FromQuery] PaginatedQuery query)
    {
        var userId = GetCurrentUserId() ?? throw new AppException(ErrorCodes.Unauthorized, "User context not found", 401);
        var result = await bookingService.GetUserBookingsAsync(userId, query);
        return Ok(ApiResponse<PagedResult<Booking>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<ApiResponse<Booking>>> CancelBooking(Guid id)
    {
        var userId = GetCurrentUserId() ?? throw new AppException(ErrorCodes.Unauthorized, "User context not found", 401);
        var booking = await bookingService.CancelBookingAsync(id, userId);
        return Ok(ApiResponse<Booking>.Ok(booking, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:guid}/route")]
    public ActionResult<ApiResponse<object>> GetBookingRoute(Guid id)
    {
        // Fallback route generation stub
        var dummyRoute = new
        {
            Waypoints = new[] { new { X = 0, Y = 0 }, new { X = 10, Y = 10 } }
        };
        return Ok(ApiResponse<object>.Ok(dummyRoute, HttpContext.TraceIdentifier));
    }

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (Guid.TryParse(sub, out var guid)) return guid;
        return null;
    }
}
