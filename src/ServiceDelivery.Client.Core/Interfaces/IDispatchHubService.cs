using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.Interfaces;

/// <summary>
/// Client for the DispatchHub (<c>/hubs/dispatch</c>) scoped to the three request-lifecycle events the
/// dispatcher queue consumes (FE-004): <c>ServiceRequestPending</c>, <c>ServiceRequestAssigned</c>, and
/// <c>ServiceRequestCompleted</c>.
/// <para>
/// ISP: this interface intentionally omits <c>RepStateChanged</c>, <c>RepOfflineMidJob</c>, and
/// <c>FleetPositionUpdate</c> — those are reserved for FE-005/FE-006. A future story needing a different
/// subset should define a new, similarly focused interface rather than widening this one.
/// </para>
/// </summary>
public interface IDispatchHubService
{
    bool IsConnected { get; }

    Task StartAsync();

    Task StopAsync();

    void OnServiceRequestPending(Func<ServiceRequestPendingPayload, Task> handler);

    void OnServiceRequestAssigned(Func<ServiceRequestAssignedPayload, Task> handler);

    void OnServiceRequestCompleted(Func<ServiceRequestCompletedPayload, Task> handler);
}
