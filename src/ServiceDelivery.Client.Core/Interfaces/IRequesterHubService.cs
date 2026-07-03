using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.Interfaces;

/// <summary>
/// Client for the SignalR RequesterHub (<c>/hubs/requester</c>). Narrow per the requester pending /
/// tracking views' needs (Interface Segregation): start and stop the connection, surface the connection
/// state for a "reconnecting" indicator, register a callback for the <c>RepAssigned</c> event
/// (FE-016/AC-3) so the pending view transitions to tracking the instant a rep is assigned, and register a
/// callback for the <c>RepPositionUpdated</c> event (FE-017/AC-3) so the tracking view moves the rep marker
/// and refreshes the ETA/status as the rep travels. Connection lifecycle and transport details live in the
/// host-shared implementation, never in the ViewModel.
/// </summary>
public interface IRequesterHubService
{
    /// <summary>
    /// True when the underlying hub connection is in the Connected state. False during the
    /// initial-connect retry loop (BUG-038) or after a disconnect. Lets the pending screen surface a
    /// "reconnecting" indicator without coupling to SignalR connection-state types.
    /// </summary>
    bool IsConnected { get; }

    Task StartAsync();

    Task StopAsync();

    void OnRepAssigned(Func<RepAssignedPayload, Task> handler);

    /// <summary>
    /// Registers a callback for the RequesterHub <c>RepPositionUpdated</c> event (FE-017/AC-3). Each push
    /// carries the rep's new position, updated ETA, and current state; the tracking ViewModel uses it to
    /// move the rep marker, refresh the ETA chip, and swap the status message as the rep travels.
    /// </summary>
    void OnRepPositionUpdated(Func<RepPositionUpdatedPayload, Task> handler);
}
