using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenParking.Core.Entities;
using OpenParking.Core.Interfaces;
using OpenParking.Core.Models;
using OpenParking.Infrastructure.Data;

namespace OpenParking.Api.Controllers;

[ApiController, Authorize]
[Route("api/customer/bookings")]
public class CustomerBookingsController(AppDbContext db, ISettingsService settings) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PaginatedQuery query)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var userId))
            throw new AppException(ErrorCodes.Unauthorized, "Sign in again.", 401);
        var source = db.Bookings.AsNoTracking().Where(b => b.UserId == userId);
        var total = await source.CountAsync();
        var currency = await settings.GetStringAsync("pricing.default_currency", "USD");
        var rows = await source.OrderByDescending(b => b.StartTime).ThenBy(b => b.Id)
            .Skip(Math.Max(0, query.Skip)).Take(query.Take).Select(b => new
            {
                b.Id, b.UserId, b.SlotId, b.StartTime, b.EndTime, b.VehiclePlate,
                Status = b.Status.ToString(), b.EstimatedFee,
                ZoneId = b.Slot!.ZoneId, ZoneName = b.Slot.Zone!.Name, SlotNumber = b.Slot.SlotNumber,
                Slot = new { b.Slot.Id, b.Slot.SlotNumber, Type = b.Slot.Type.ToString(), Status = b.Slot.Status.ToString(), b.Slot.Floor },
                Session = db.ParkingSessions.Where(s => s.BookingId == b.Id).OrderByDescending(s => s.CheckInTime)
                    .Select(s => new { s.Id, s.BookingId, SlotNumber = b.Slot.SlotNumber, ZoneName = b.Slot.Zone.Name,
                        ZoneId = b.Slot.ZoneId, Status = s.Status.ToString(), s.CheckInTime, s.CheckOutTime,
                        s.TotalFee, s.PenaltyFee, s.OverstayMinutes, HourlyRate = b.Slot.Zone.BaseHourlyRate, Currency = currency }).FirstOrDefault(),
                IsPaid = db.AuditLogs.Any(a => a.EntityType == "Booking" && a.EntityId == b.Id &&
                    a.Action == "PAYMENT_RECEIVED"),
                Currency = currency
            }).ToListAsync();
        return Ok(ApiResponse<object>.Ok(new { items = rows, totalCount = total, page = query.Page, pageSize = query.PageSize }));
    }
}
