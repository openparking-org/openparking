namespace OpenParking.Core.Interfaces;

/// <summary>
/// Marker interface for the SignalR SlotHub.
/// Lives in Core so Infrastructure can reference it without a circular dependency on the Api project.
/// The actual Hub class in OpenParking.Api inherits from Hub and this interface.
/// </summary>
public interface ISlotHub
{
    Task SlotUpdated(object payload);
    Task PenaltyIssued(object payload);
}
