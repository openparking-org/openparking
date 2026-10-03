using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenParking.Api.Dtos;
using OpenParking.Api.Security;
using OpenParking.Core.Entities;
using OpenParking.Core.Interfaces;
using OpenParking.Core.Models;
using OpenParking.Infrastructure.Data;

namespace OpenParking.Api.Controllers;

/// <summary>
/// Booking &amp; Payment slice. Owns the reservation half of the lifecycle;
/// SessionsController takes over once the driver physically arrives.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BookingsController(
    AppDbContext db,
    IFeeCalculationService fees,
    ILogger<BookingsController> logger) : ControllerBase
{
    /// <summary>Statuses that still hold the slot and therefore block an overlap.</summary>
    private static readonly BookingStatus[] BlockingStatuses =
        [BookingStatus.Pending, BookingStatus.Confirmed, BookingStatus.Active];

    [HttpGet]
    public async Task<IActionResult> GetBookings([FromQuery] PaginatedQuery query, [FromQuery] Guid? userId = null, [FromQuery] BookingStatus? status = null)
    {
        var bookings = db.Bookings
            .Include(b => b.Slot)!.ThenInclude(s => s!.Zone)
            .AsNoTracking();

        // A driver may only ever see their own reservations; the userId filter
        // is an administrative convenience, not something a driver can widen.
        if (!User.IsStaff())
            bookings = bookings.Where(b => b.UserId == User.UserId());
        else if (userId.HasValue)
            bookings = bookings.Where(b => b.UserId == userId.Value);

        if (status.HasValue)
            bookings = bookings.Where(b => b.Status == status.Value);

        var totalCount = await bookings.CountAsync();

        var page = await bookings
            .OrderByDescending(b => b.StartTime)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return Ok(PagedResult<BookingResponse>.Create(
            page.Select(b => BookingResponse.From(b)).ToList(),
            totalCount,
            query.Page,
            query.PageSize));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetBooking(Guid id)
    {
        var booking = await db.Bookings
            .Include(b => b.Slot)!.ThenInclude(s => s!.Zone)
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking == null)
            return NotFound(new { error = $"Booking '{id}' not found" });

        // Report another driver's booking as missing rather than forbidden, so
        // the response cannot be used to probe which ids exist.
        if (!User.IsStaff() && booking.UserId != User.UserId())
            return NotFound(new { error = $"Booking '{id}' not found" });

        return Ok(BookingResponse.From(booking));
    }

    /// <summary>
    /// Prices a window without reserving anything, so the mobile app can show a
    /// fee before the driver commits.
    /// </summary>
    [HttpPost("quote")]
    public async Task<IActionResult> Quote([FromBody] CreateBookingRequest request)
    {
        if (request.EndTime <= request.StartTime)
            return BadRequest(new { error = "End time must be after start time" });

        var slot = await db.Slots.Include(s => s.Zone).AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.SlotId);

        if (slot == null)
            return NotFound(new { error = $"Slot '{request.SlotId}' not found" });

        var quote = await fees.EstimateAsync(
            request.StartTime,
            request.EndTime,
            slot.Zone?.BaseHourlyRate,
            request.SurgeMultiplier);

        return Ok(FeeBreakdownResponse.From(quote));
    }

    [HttpPost]
    public async Task<IActionResult> CreateBooking([FromBody] CreateBookingRequest request)
    {
        if (request.EndTime <= request.StartTime)
            return BadRequest(new { error = "End time must be after start time" });

        if (request.EndTime <= DateTime.UtcNow)
            return BadRequest(new { error = "Cannot book a window that has already ended" });

        var slot = await db.Slots.Include(s => s.Zone)
            .FirstOrDefaultAsync(s => s.Id == request.SlotId);

        if (slot == null)
            return NotFound(new { error = $"Slot '{request.SlotId}' not found" });

        if (slot.Status == SlotStatus.Maintenance)
            return Conflict(new { error = $"Slot '{slot.SlotNumber}' is out of service" });

        var userId = User.UserId();
        if (userId is null)
            return Unauthorized(new { error = "Token does not identify a user" });

        // Two reservations clash when each starts before the other ends. Held in
        // the booking table rather than on Slot.Status, because a slot can carry
        // many non-overlapping future reservations at once.
        var clashes = await db.Bookings.AnyAsync(b =>
            b.SlotId == request.SlotId &&
            BlockingStatuses.Contains(b.Status) &&
            b.StartTime < request.EndTime &&
            request.StartTime < b.EndTime);

        if (clashes)
            return Conflict(new { error = $"Slot '{slot.SlotNumber}' is already reserved for part of that window" });

        var quote = await fees.EstimateAsync(
            request.StartTime,
            request.EndTime,
            slot.Zone?.BaseHourlyRate,
            request.SurgeMultiplier);

        var booking = new Booking
        {
            UserId = userId.Value,
            SlotId = request.SlotId,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Status = BookingStatus.Confirmed,
            QrCodeContent = DigitalPass.NewCode(),
            EstimatedFee = quote.Amount
        };

        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Booking {BookingId} confirmed for slot {SlotNumber}: {Hours}h at {Rate}/h = {Amount}",
            booking.Id, slot.SlotNumber, quote.BillableHours, quote.EffectiveHourlyRate, quote.Amount);

        return CreatedAtAction(nameof(GetBooking), new { id = booking.Id }, new
        {
            booking = BookingResponse.From(booking, slot),
            fee = FeeBreakdownResponse.From(quote)
        });
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelBooking(Guid id)
    {
        var booking = await db.Bookings.FirstOrDefaultAsync(b => b.Id == id);

        if (booking == null)
            return NotFound(new { error = $"Booking '{id}' not found" });

        if (!User.IsStaff() && booking.UserId != User.UserId())
            return NotFound(new { error = $"Booking '{id}' not found" });

        if (booking.Status == BookingStatus.Cancelled)
            return Ok(BookingResponse.From(booking));

        // Once the driver has checked in the session owns the lifecycle, so the
        // reservation can no longer be withdrawn.
        if (booking.Status is BookingStatus.Active or BookingStatus.Completed)
            return Conflict(new { error = $"Cannot cancel a booking that is {booking.Status}" });

        booking.Status = BookingStatus.Cancelled;
        await db.SaveChangesAsync();

        logger.LogInformation("Booking {BookingId} cancelled", booking.Id);
        return Ok(BookingResponse.From(booking));
    }
}
