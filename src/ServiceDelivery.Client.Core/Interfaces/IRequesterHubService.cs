using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.Interfaces;

/// <summary>
/// Client for the SignalR RequesterHub (<c>/hubs/requester</c>). Narrow per the requester pending /
/// tracking views' needs (Interface Segregation): start and stop the connection, surface the connection
/// state for a "reconnecting" indicator, and register a callback for the <c>RepAssigned</c> event
/// (FE-016/AC-3) so the pending view transitions to tracking the instant a rep is assigned. Connection
/// lifecycle and transport details live in the host-shared implementation, never in the ViewModel.
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
}
