using Microsoft.AspNetCore.SignalR;

namespace OpenParking.Api.Hubs;

public class SlotHub : Hub
{
    public async Task JoinZoneGroup(string zoneId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"zone:{zoneId}");
    }

    public async Task LeaveZoneGroup(string zoneId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"zone:{zoneId}");
    }
}
