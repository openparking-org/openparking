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
[Authorize]
public class BookingsController(IBookingService bookingService, NavigationService navigation, ISettingsService settings) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ApiResponse<BookingSummary>>> CreateBooking([FromBody] CreateBookingRequest req)
    {
        var userId = GetCurrentUserId() ?? throw new AppException(ErrorCodes.Unauthorized, "User context not found", 401);
        var booking = await bookingService.CreateBookingAsync(req, userId);
        return Ok(ApiResponse<BookingSummary>.Ok(BookingSummary.From(booking, await settings.GetStringAsync("pricing.default_currency", "USD")), HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<BookingSummary>>> GetBooking(Guid id)
    {
        var booking = await bookingService.GetBookingAsync(id);
        // Authorization check - only owner or admin should see this
        var userId = GetCurrentUserId() ?? throw new AppException(ErrorCodes.Unauthorized, "User context not found", 401);
        if (booking.UserId != userId && !User.IsInRole("SystemAdmin") && !User.IsInRole("ParkingAdmin"))
        {
            return Forbid();
        }
        return Ok(ApiResponse<BookingSummary>.Ok(BookingSummary.From(booking, await settings.GetStringAsync("pricing.default_currency", "USD")), HttpContext.TraceIdentifier));
    }

    [HttpGet("user")]
    public async Task<ActionResult<ApiResponse<PagedResult<BookingSummary>>>> GetUserBookings([FromQuery] PaginatedQuery query)
    {
        var userId = GetCurrentUserId() ?? throw new AppException(ErrorCodes.Unauthorized, "User context not found", 401);
        var result = await bookingService.GetUserBookingsAsync(userId, query);
        var currency = await settings.GetStringAsync("pricing.default_currency", "USD");
        return Ok(ApiResponse<PagedResult<BookingSummary>>.Ok(new PagedResult<BookingSummary> { Items = result.Items.Select(b => BookingSummary.From(b, currency)).ToList(), TotalCount = result.TotalCount, Page = result.Page, PageSize = result.PageSize }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<ApiResponse<BookingSummary>>> CancelBooking(Guid id)
    {
        var userId = GetCurrentUserId() ?? throw new AppException(ErrorCodes.Unauthorized, "User context not found", 401);
        var booking = await bookingService.CancelBookingAsync(id, userId);
        return Ok(ApiResponse<BookingSummary>.Ok(BookingSummary.From(booking, await settings.GetStringAsync("pricing.default_currency", "USD")), HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:guid}/route")]
    public async Task<ActionResult<ApiResponse<NavigationResult>>> GetBookingRoute(Guid id)
    {
        var booking = await bookingService.GetBookingAsync(id);
        var userId = GetCurrentUserId() ?? throw new AppException(ErrorCodes.Unauthorized, "Sign in again.", 401);
        if (booking.UserId != userId && !User.IsInRole("SystemAdmin") && !User.IsInRole("ParkingAdmin")) return Forbid();
        var route = await navigation.RouteAsync(booking.Slot!.ZoneId, booking.SlotId);
        return Ok(ApiResponse<NavigationResult>.Ok(route, HttpContext.TraceIdentifier));
    }

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (Guid.TryParse(sub, out var guid)) return guid;
        return null;
    }
}

public record BookingSummary(Guid Id, Guid UserId, Guid SlotId, DateTime StartTime, DateTime EndTime,
    string? VehiclePlate, string Status, decimal EstimatedFee, Guid? ZoneId, string? ZoneName, string Currency,
    SlotSummaryDto? Slot)
{
    public static BookingSummary From(Booking b, string currency) => new(b.Id, b.UserId, b.SlotId, b.StartTime,
        b.EndTime, b.VehiclePlate, b.Status.ToString(), b.EstimatedFee, b.Slot?.ZoneId, b.Slot?.Zone?.Name, currency,
        b.Slot == null ? null : new SlotSummaryDto { Id = b.Slot.Id, SlotNumber = b.Slot.SlotNumber,
            Type = b.Slot.Type.ToString(), Status = b.Slot.Status.ToString(), Floor = b.Slot.Floor });
}
