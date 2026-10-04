using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenParking.Core.Entities;
using OpenParking.Core.Interfaces;
using OpenParking.Core.Models;
using OpenParking.Infrastructure.Data;

namespace OpenParking.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "ParkingAdmin,SystemAdmin")]
public class AdminController(AppDbContext db, IBookingService bookings, ISettingsService settings,
    IHttpClientFactory clients, IConfiguration configuration) : ControllerBase
{
    [HttpGet("zones")]
    public async Task<IActionResult> Zones([FromQuery] PaginatedQuery query)
    {
        var source = db.Zones.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            source = source.Where(z => z.Name.ToLower().Contains(search) || z.Code.ToLower().Contains(search));
        }
        var total = await source.CountAsync();
        var currency = await settings.GetStringAsync("pricing.default_currency", "USD");
        var rows = await source.OrderBy(z => z.Name).Skip(Math.Max(0, query.Skip)).Take(query.Take)
            .Select(z => new { z.Id, z.Name, z.Code, z.Latitude, z.Longitude, z.BaseHourlyRate, z.TotalCapacity,
                AvailableCount = z.Slots.Count(s => s.Status == SlotStatus.Available),
                OccupiedCount = z.Slots.Count(s => s.Status == SlotStatus.Occupied),
                ReservedCount = z.Slots.Count(s => s.Status == SlotStatus.Reserved),
                MappedCount = z.Slots.Count, Currency = currency }).ToListAsync();
        return Ok(ApiResponse<object>.Ok(new { items = rows, totalCount = total, page = query.Page, pageSize = query.PageSize }));
    }

    [HttpGet("zones/{id:guid}/floor-plans")]
    public async Task<IActionResult> FloorPlans(Guid id)
    {
        var rows = await db.FloorPlans.AsNoTracking().Where(f => f.ZoneId == id).OrderBy(f => f.FloorOrder)
            .Select(f => new { f.Id, f.FloorName, f.FloorOrder, f.ImageUrl, f.ImageWidthPx, f.ImageHeightPx,
                f.WaypointGraphJson, f.AnchorNorthWestLat, f.AnchorNorthWestLng, f.AnchorSouthEastLat, f.AnchorSouthEastLng }).ToListAsync();
        return Ok(ApiResponse<object>.Ok(rows));
    }

    [HttpPut("zones/{id:guid}/floors/{floor:int}/slots")]
    public async Task<IActionResult> SaveSlots(Guid id, int floor, [FromBody] AdminSlotLayoutRequest request)
    {
        if (!await db.Zones.AnyAsync(z => z.Id == id)) throw new AppException(ErrorCodes.NotFound, "Zone not found.", 404);
        if (request.Slots.Count > 1000) throw new AppException(ErrorCodes.ValidationFailed, "Save at most 1000 bays per floor.");
        var numbers = request.Slots.Select(s => s.SlotNumber.Trim().ToUpperInvariant()).ToList();
        if (numbers.Any(string.IsNullOrWhiteSpace) || numbers.Distinct().Count() != numbers.Count)
            throw new AppException(ErrorCodes.ValidationFailed, "Bay numbers must be nonempty and unique.");
        if (request.Slots.Any(s => !Enum.IsDefined(s.Type)))
            throw new AppException(ErrorCodes.ValidationFailed, "Invalid bay type.");
        if (request.FloorPlanId.HasValue && !await db.FloorPlans.AnyAsync(f => f.Id == request.FloorPlanId && f.ZoneId == id && f.FloorOrder == floor))
            throw new AppException(ErrorCodes.ValidationFailed, "Floor plan does not belong to this floor.");
        var existing = await db.Slots.Where(s => s.ZoneId == id && s.Floor == floor).ToListAsync();
        var ids = request.Slots.Where(s => s.Id.HasValue).Select(s => s.Id!.Value).ToList();
        if (ids.Distinct().Count() != ids.Count || ids.Any(slotId => existing.All(s => s.Id != slotId)))
            throw new AppException(ErrorCodes.ValidationFailed, "Bay identifiers do not belong to this floor.");
        if (await db.Slots.AnyAsync(s => s.ZoneId == id && s.Floor != floor && numbers.Contains(s.SlotNumber)))
            throw new AppException(ErrorCodes.ValidationFailed, "A bay number is already used on another floor.");
        var removed = existing.Where(s => !ids.Contains(s.Id)).ToList();
        var removedIds = removed.Select(s => s.Id).ToList();
        if (removed.Any(s => s.Status != SlotStatus.Available) || await db.Bookings.AnyAsync(b => removedIds.Contains(b.SlotId)))
            throw new AppException(ErrorCodes.ValidationFailed, "Bays with reservations or booking history cannot be removed. Set them to Maintenance instead.", 409);
        db.Slots.RemoveRange(removed);
        var saved = new List<Slot>();
        foreach (var item in request.Slots)
        {
            if (item.CanvasX is < 0 or > 1000 || item.CanvasY is < 0 or > 600 || item.CanvasWidth is <= 0 or > 1000 || item.CanvasHeight is <= 0 or > 600)
                throw new AppException(ErrorCodes.ValidationFailed, "Bay geometry is outside the editor bounds.");
            if (item.BoundingBoxJson != null)
            {
                try { using var geometry = JsonDocument.Parse(item.BoundingBoxJson); }
                catch (JsonException) { throw new AppException(ErrorCodes.ValidationFailed, "Bay geometry must be valid JSON."); }
            }
            var slot = item.Id.HasValue ? existing.Single(s => s.Id == item.Id.Value) : new Slot { ZoneId = id, Floor = floor, CreatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "Admin" };
            if (!item.Id.HasValue) db.Slots.Add(slot);
            slot.SlotNumber = item.SlotNumber.Trim().ToUpperInvariant(); slot.Type = item.Type;
            slot.CanvasX = item.CanvasX; slot.CanvasY = item.CanvasY; slot.CanvasWidth = item.CanvasWidth; slot.CanvasHeight = item.CanvasHeight;
            slot.BoundingBoxJson = item.BoundingBoxJson; slot.FloorPlanId = request.FloorPlanId;
            slot.AssignedSensorId = item.AssignedSensorId; slot.AssignedCameraId = item.AssignedCameraId;
            slot.NearestWaypointId = item.NearestWaypointId; slot.UpdatedAt = DateTime.UtcNow;
            saved.Add(slot);
        }
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(saved.Select(s => new { s.Id, s.SlotNumber })));
    }

    [HttpGet("bookings")]
    public async Task<IActionResult> ListBookings([FromQuery] PaginatedQuery query, [FromQuery] BookingStatus? status)
    {
        var source = db.Bookings.AsNoTracking().AsQueryable();
        if (status.HasValue) source = source.Where(b => b.Status == status);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            source = source.Where(b => (b.VehiclePlate != null && b.VehiclePlate.ToLower().Contains(search)) ||
                (b.User != null && (b.User.FullName.ToLower().Contains(search) || b.User.Email.ToLower().Contains(search))) ||
                (b.Slot != null && b.Slot.SlotNumber.ToLower().Contains(search)));
        }
        var total = await source.CountAsync();
        var currency = await settings.GetStringAsync("pricing.default_currency", "USD");
        var rows = await source.OrderByDescending(b => b.CreatedAt).Skip(Math.Max(0, query.Skip)).Take(query.Take)
            .Select(b => new
            {
                b.Id, b.UserId, DriverName = b.User == null ? "Unassigned" : b.User.FullName,
                DriverEmail = b.User == null ? "" : b.User.Email, b.VehiclePlate,
                ZoneName = b.Slot == null || b.Slot.Zone == null ? "" : b.Slot.Zone.Name,
                SlotNumber = b.Slot == null ? "" : b.Slot.SlotNumber,
                b.StartTime, b.EndTime, Status = b.Status.ToString(), b.EstimatedFee,
                Session = db.ParkingSessions.Where(s => s.BookingId == b.Id).OrderByDescending(s => s.CheckInTime)
                    .Select(s => new { s.Id, Status = s.Status.ToString(), s.CheckInTime, s.CheckOutTime, s.TotalFee, s.PenaltyFee }).FirstOrDefault(),
                IsPaid = db.AuditLogs.Any(a => a.EntityType == "Booking" && a.EntityId == b.Id && a.Action == "PAYMENT_RECEIVED"),
                Currency = currency
            }).ToListAsync();
        return Ok(ApiResponse<object>.Ok(new { items = rows, totalCount = total, page = query.Page, pageSize = query.PageSize }, HttpContext.TraceIdentifier));
    }

    [HttpGet("drivers")]
    public async Task<IActionResult> Drivers([FromQuery] PaginatedQuery query)
    {
        var source = db.Users.AsNoTracking().Where(u => u.Role == UserRole.Driver && u.IsActive);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            source = source.Where(u => u.FullName.ToLower().Contains(search) || u.Email.ToLower().Contains(search));
        }
        var total = await source.CountAsync();
        var rows = await source.OrderBy(u => u.FullName).Skip(Math.Max(0, query.Skip)).Take(query.Take)
            .Select(u => new { u.Id, u.FullName, u.Email, u.HasDisabilityPermit }).ToListAsync();
        return Ok(ApiResponse<object>.Ok(new { items = rows, totalCount = total, page = query.Page, pageSize = query.PageSize }));
    }

    [HttpPost("bookings")]
    public async Task<IActionResult> CreateBooking([FromBody] AdminBookingRequest request)
    {
        if (!await db.Users.AnyAsync(u => u.Id == request.DriverId && u.IsActive && u.Role == UserRole.Driver))
            throw new AppException(ErrorCodes.ValidationFailed, "Select an active driver.");
        if (string.IsNullOrWhiteSpace(request.VehiclePlate))
            throw new AppException(ErrorCodes.ValidationFailed, "Vehicle plate is required.");
        var booking = await bookings.CreateBookingAsync(request, request.DriverId);
        return Ok(ApiResponse<object>.Ok(new { booking.Id, booking.SlotId, booking.EstimatedFee, Status = booking.Status.ToString(), booking.StartTime, booking.EndTime }));
    }

    [HttpPost("bookings/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var booking = await bookings.GetBookingAsync(id);
        await bookings.CancelBookingAsync(id, booking.UserId);
        return Ok(ApiResponse<object>.Ok(new { id, status = "Cancelled" }));
    }

    // Attendant confirms payment collected at the gate; no online charge is made.
    // Record the collection in the audit trail so payment status survives reloads.
    [HttpPost("bookings/{id:guid}/pay")]
    public async Task<IActionResult> Pay(Guid id)
    {
        var booking = await bookings.GetBookingAsync(id);
        if (booking.Status != BookingStatus.Completed)
            throw new AppException(ErrorCodes.ValidationFailed, "Check out the booking before marking it paid.", 409);
        if (!await db.AuditLogs.AnyAsync(a => a.EntityType == "Booking" && a.EntityId == id && a.Action == "PAYMENT_RECEIVED"))
        {
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "Booking", EntityId = id, Action = "PAYMENT_RECEIVED",
                ActorUserId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor) ? actor : null,
                ActorEmail = User.FindFirstValue(ClaimTypes.Email) ?? "Admin",
                PayloadJson = "{\"method\":\"gate\"}", IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? ""
            });
            await db.SaveChangesAsync();
        }
        return Ok(ApiResponse<object>.Ok(new { id, isPaid = true, message = "Payment received at gate" }));
    }

    [HttpGet("permits")]
    public async Task<IActionResult> Permits([FromQuery] PaginatedQuery query, [FromQuery] PermitStatus? status)
    {
        var source = db.DisabilityPermits.AsNoTracking().AsQueryable();
        if (status.HasValue) source = source.Where(p => p.Status == status);
        var total = await source.CountAsync();
        var rows = await source.OrderByDescending(p => p.CreatedAt).Skip(Math.Max(0, query.Skip)).Take(query.Take)
            .Select(p => new { p.Id, p.UserId, FullName = p.User == null ? "Unknown user" : p.User.FullName,
                p.PermitNumber, p.Jurisdiction, p.ExpiryDate, p.DocumentImageUrl, Status = p.Status.ToString(), p.ReviewNotes,
                p.RejectionReason, p.CreatedAt, p.ReviewedAt }).ToListAsync();
        return Ok(ApiResponse<object>.Ok(new { items = rows, totalCount = total, page = query.Page, pageSize = query.PageSize }));
    }

    [HttpGet("workflows")]
    public async Task<IActionResult> Workflows([FromQuery] PaginatedQuery query, [FromQuery] WorkflowStatus? status)
    {
        var source = db.AgentWorkflowRuns.AsNoTracking().AsQueryable();
        if (status.HasValue) source = source.Where(w => w.Status == status);
        var total = await source.CountAsync();
        var rows = await source.OrderByDescending(w => w.CreatedAt).Skip(Math.Max(0, query.Skip)).Take(query.Take)
            .Select(w => new { w.Id, w.Objective, w.WorkflowType, Status = w.Status.ToString(), w.CreatedAt, w.DecisionReason }).ToListAsync();
        return Ok(ApiResponse<object>.Ok(new { items = rows, totalCount = total, page = query.Page, pageSize = query.PageSize }));
    }

    [HttpGet("hardware")]
    public async Task<IActionResult> Hardware([FromQuery] PaginatedQuery query)
    {
        var source = db.AuditLogs.AsNoTracking().Where(a => a.EntityType == "Hardware");
        if (!string.IsNullOrWhiteSpace(query.Search))
            source = source.Where(a => a.PayloadJson != null && a.PayloadJson.Contains(query.Search));
        var total = await source.CountAsync();
        var rows = await source.OrderByDescending(a => a.CreatedAt).Skip(Math.Max(0, query.Skip)).Take(query.Take)
            .Select(a => new { a.Id, a.Action, a.PayloadJson, a.CreatedAt }).ToListAsync();
        return Ok(ApiResponse<object>.Ok(new { items = rows, totalCount = total, page = query.Page, pageSize = query.PageSize }));
    }

    [HttpPost("hardware")]
    public async Task<IActionResult> RecordHardware([FromBody] HardwareEventRequest request)
    {
        if (!await db.Zones.AnyAsync(z => z.Id == request.ZoneId))
            throw new AppException(ErrorCodes.NotFound, "Zone not found.", 404);
        var entry = new AuditLog
        {
            EntityType = "Hardware", EntityId = request.ZoneId, Action = request.Type,
            PayloadJson = JsonSerializer.Serialize(new { deviceId = request.DeviceId, zoneId = request.ZoneId, message = request.Message, severity = request.Severity }),
            ActorEmail = User.FindFirstValue(ClaimTypes.Email) ?? "Admin"
        };
        db.AuditLogs.Add(entry);
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { entry.Id, entry.CreatedAt }));
    }

    [HttpPost("ai/{capability}")]
    public async Task<IActionResult> Ai(string capability, [FromBody] JsonElement payload, CancellationToken cancellationToken)
    {
        var path = capability switch { "permit-validation" => "/ai/permits/validate", "slot-detection" => "/ai/cartographer/detect", _ => null };
        if (path == null) return NotFound();
        var aiUrl = configuration["AI_SERVICE_URL"] ?? configuration["AiService:BaseUrl"] ?? "http://localhost:8000";
        var client = clients.CreateClient("AdminAi");
        client.Timeout = TimeSpan.FromSeconds(30);
        try
        {
            using var response = await client.PostAsJsonAsync(aiUrl.TrimEnd('/') + path, payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new AppException("AI_UNAVAILABLE", "AI service could not complete the request. Try again or use manual review.", 502);
            var result = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            return Ok(ApiResponse<JsonElement>.Ok(result));
        }
        catch (HttpRequestException) { throw new AppException("AI_UNAVAILABLE", "AI service is unavailable. Manual operations remain available.", 503); }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { throw new AppException("AI_TIMEOUT", "AI service took too long. Please retry.", 504); }
    }
}

public class AdminBookingRequest : CreateBookingRequest { public Guid DriverId { get; set; } }
public class AdminSlotLayoutRequest
{
    public Guid? FloorPlanId { get; set; }
    public List<AdminSlotRequest> Slots { get; set; } = [];
}
public class AdminSlotRequest : CreateSlotRequest { public Guid? Id { get; set; } }
public class HardwareEventRequest
{
    public Guid ZoneId { get; set; }
    [Required, StringLength(128)] public string DeviceId { get; set; } = "";
    [Required, StringLength(40)] public string Type { get; set; } = "Heartbeat";
    [Required, StringLength(1000)] public string Message { get; set; } = "";
    [RegularExpression("^(Info|Warning|Error)$")] public string Severity { get; set; } = "Info";
}
