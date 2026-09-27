namespace OpenParking.Core.Interfaces;

/// <summary>
/// Abstraction over real-time push notifications (SignalR).
/// Infrastructure depends on this interface, not on the concrete Hub class,
/// breaking the circular dependency between Infrastructure and Api.
/// </summary>
public interface IRealtimeNotifier
{
    /// <summary>Notify a specific user's connected clients of a slot status change.</summary>
    Task NotifySlotUpdatedAsync(string floorPlanId, string slotId, bool isOccupied);

    /// <summary>Notify a specific user that a penalty has been issued against them.</summary>
    Task NotifyPenaltyIssuedAsync(string userId, object payload);
}
