using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.Interfaces;

/// <summary>
/// Real-time transport for the dispatcher fleet map (FE-003): manages the <c>VehiclePositionHub</c>
/// connection (<c>/hubs/position</c>) and forwards each <c>VehiclePositionUpdated</c> event to a
/// registered handler. Narrow by design (Interface Segregation): three lifecycle members plus one event
/// registration — the initial snapshot load is <see cref="IDispatcherFleetService"/>'s responsibility, not
/// combined here.
/// </summary>
public interface IVehiclePositionHubService
{
    bool IsConnected { get; }

    Task StartAsync();

    Task StopAsync();

    void OnVehiclePositionUpdated(Func<VehiclePositionUpdatedPayload, Task> handler);
}
