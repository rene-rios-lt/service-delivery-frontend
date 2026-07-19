using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.Interfaces;

/// <summary>
/// Reads the dispatcher's fleet snapshot from the backend (FE-003). One focused capability — the initial
/// <c>GET /dispatcher/fleet</c> load — kept separate from the real-time <c>IVehiclePositionHubService</c>
/// (Interface Segregation): a caller that only needs the snapshot does not depend on hub lifecycle methods.
/// </summary>
public interface IDispatcherFleetService
{
    Task<IReadOnlyList<FleetVehicleEntry>> GetFleetAsync();
}
