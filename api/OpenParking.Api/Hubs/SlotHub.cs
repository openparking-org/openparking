using Microsoft.AspNetCore.SignalR;
using OpenParking.Core.Interfaces;

namespace OpenParking.Api.Hubs;

/// <summary>
/// Real-time hub for slot occupancy updates and penalty notifications.
/// Implements ISlotHub so EnforcementService can use IHubContext&lt;ISlotHub&gt;
/// without creating a circular dependency on OpenParking.Api.
/// Design reference: design.md §17
/// </summary>
public class SlotHub : Hub<ISlotHub>
{
    public async Task JoinZoneGroup(string zoneId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"zone:{zoneId}");

    public async Task LeaveZoneGroup(string zoneId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"zone:{zoneId}");

    // ISlotHub methods are called by the server via IHubContext — not by clients directly
    public Task SlotUpdated(object payload) => throw new NotImplementedException();
    public Task PenaltyIssued(object payload) => throw new NotImplementedException();
}
