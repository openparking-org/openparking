using Microsoft.AspNetCore.SignalR;
using OpenParking.Core.Interfaces;

namespace OpenParking.Api.Hubs;

/// <summary>
/// Implements IRealtimeNotifier using SignalR SlotHub.
/// Registered in Api's DI so Infrastructure never references the Hub class directly.
/// </summary>
public class SignalRNotifier(IHubContext<SlotHub, ISlotHub> hub) : IRealtimeNotifier
{
    public async Task NotifySlotUpdatedAsync(string floorPlanId, string slotId, bool isOccupied)
    {
        await hub.Clients
            .Group($"floor-{floorPlanId}")
            .SlotUpdated(new { slotId, isOccupied });
    }

    public async Task NotifyPenaltyIssuedAsync(string userId, object payload)
    {
        await hub.Clients
            .User(userId)
            .PenaltyIssued(payload);
    }
}
