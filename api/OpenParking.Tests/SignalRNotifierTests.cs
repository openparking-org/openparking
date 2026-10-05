using Microsoft.AspNetCore.SignalR;
using Moq;
using OpenParking.Api.Hubs;
using OpenParking.Core.Interfaces;
using Xunit;

namespace OpenParking.Tests;

public class SignalRNotifierTests
{
    [Fact]
    public async Task NotifySlotUpdatedAsync_SendsMessageToZoneGroup()
    {
        var mockHubContext = new Mock<IHubContext<SlotHub, ISlotHub>>();
        var mockClients = new Mock<IHubClients<ISlotHub>>();
        var mockClientProxy = new Mock<ISlotHub>();

        mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);
        mockClients.Setup(c => c.Group("zone:zone-123")).Returns(mockClientProxy.Object);

        var notifier = new SignalRNotifier(mockHubContext.Object);

        await notifier.NotifySlotUpdatedAsync("zone-123", "slot-1", "Occupied");

        mockClientProxy.Verify(
            c => c.SlotUpdated(It.Is<object>(obj => 
                obj.GetType().GetProperty("slotId")!.GetValue(obj)!.ToString() == "slot-1" &&
                obj.GetType().GetProperty("status")!.GetValue(obj)!.ToString() == "Occupied")),
            Times.Once);
    }

    [Fact]
    public async Task NotifyPenaltyIssuedAsync_SendsMessageToSpecificUser()
    {
        var mockHubContext = new Mock<IHubContext<SlotHub, ISlotHub>>();
        var mockClients = new Mock<IHubClients<ISlotHub>>();
        var mockClientProxy = new Mock<ISlotHub>();

        var userId = Guid.NewGuid().ToString();

        mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);
        mockClients.Setup(c => c.User(userId)).Returns(mockClientProxy.Object);

        var notifier = new SignalRNotifier(mockHubContext.Object);
        var payload = new { amount = 500, reason = "Overstay" };

        await notifier.NotifyPenaltyIssuedAsync(userId, payload);

        mockClientProxy.Verify(c => c.PenaltyIssued(payload), Times.Once);
    }
}
