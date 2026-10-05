using Microsoft.AspNetCore.SignalR;
using OpenParking.Core.Interfaces;

namespace OpenParking.Api.Hubs;

/// <summary>
/// Implements IRealtimeNotifier using SignalR SlotHub.
/// Registered in Api's DI so Infrastructure never references the Hub class directly.
/// </summary>
public class SignalRNotifier(IHubContext<SlotHub, ISlotHub> hub) : IRealtimeNotifier
{
    public async Task NotifySlotUpdatedAsync(string zoneId, string slotId, string status)
    {
        await hub.Clients
            .Group($"zone:{zoneId}")
            .SlotUpdated(new { slotId, status });
    }

    public async Task NotifyPenaltyIssuedAsync(string userId, object payload)
    {
        await hub.Clients
            .User(userId)
            .PenaltyIssued(payload);
    }
}
