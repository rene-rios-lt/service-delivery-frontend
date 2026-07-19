using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.ViewModels;

/// <summary>
/// Orchestrates the dispatcher fleet map's situational awareness (FE-003): the initial REST snapshot load,
/// real-time <c>VehiclePositionUpdated</c> merging, and marker selection. These are cohesive — all three
/// change for the same reason (keeping the dispatcher's live picture of the fleet correct). Transport
/// concerns (connection lifecycle, retry/back-off) live behind <see cref="IVehiclePositionHubService"/>;
/// this ViewModel never touches HttpClient or a HubConnection directly (Dependency Inversion).
/// </summary>
public class DispatcherFleetViewModel
{
    private readonly IDispatcherFleetService _fleetService;
    private readonly IVehiclePositionHubService _hub;

    // Keyed by the string vehicle id (the marker key). Holds every vehicle from the snapshot; VisibleVehicles
    // applies the display filter so an Offline vehicle can be updated in place yet drop off the map.
    private readonly Dictionary<string, FleetVehicleEntry> _fleet = new();

    private string? _selectedVehicleId;

    public DispatcherFleetViewModel(IDispatcherFleetService fleetService, IVehiclePositionHubService hub)
    {
        _fleetService = fleetService;
        _hub = hub;
    }

    /// <summary>Raised whenever the fleet, a vehicle position, or the selection changes.</summary>
    public event Action? StateChanged;

    /// <summary>
    /// The vehicles rendered as markers — every fleet vehicle whose rep-state is known and not "Offline"
    /// (Offline is a legend-only state; its marker is removed from the map — AC-7).
    /// </summary>
    public IReadOnlyList<FleetVehicleEntry> VisibleVehicles =>
        _fleet.Values.Where(IsVisible).ToList();

    /// <summary>The vehicle whose popover is open, resolved live so it survives position-update merges.</summary>
    public FleetVehicleEntry? SelectedVehicle =>
        _selectedVehicleId is not null && _fleet.TryGetValue(_selectedVehicleId, out var entry) ? entry : null;

    public async Task LoadAsync()
    {
        var fleet = await _fleetService.GetFleetAsync();
        _fleet.Clear();
        foreach (var entry in fleet)
        {
            _fleet[entry.VehicleId] = entry;
        }

        RaiseStateChanged();
    }

    public Task StartHubAsync()
    {
        _hub.OnVehiclePositionUpdated(HandleVehiclePositionUpdatedAsync);
        return _hub.StartAsync();
    }

    public Task StopHubAsync() => _hub.StopAsync();

    /// <summary>
    /// Merges a live position event into the fleet: for a vehicle already in the snapshot, only its live
    /// lat/lng and rep-state change (the snapshot-only metadata is preserved); a vehicle not yet seen is
    /// added from the event's fields. A rep-state of "Offline" flows through the same path and simply drops
    /// the vehicle out of <see cref="VisibleVehicles"/>.
    /// </summary>
    public Task HandleVehiclePositionUpdatedAsync(VehiclePositionUpdatedPayload payload)
    {
        var vehicleId = payload.VehicleId.ToString();

        _fleet[vehicleId] = _fleet.TryGetValue(vehicleId, out var existing)
            ? existing with
            {
                Latitude = payload.Latitude,
                Longitude = payload.Longitude,
                RepState = payload.State,
            }
            : payload.ToFleetVehicleEntry();

        RaiseStateChanged();
        return Task.CompletedTask;
    }

    public void SelectVehicle(string vehicleId)
    {
        _selectedVehicleId = vehicleId;
        RaiseStateChanged();
    }

    public void ClearSelection()
    {
        _selectedVehicleId = null;
        RaiseStateChanged();
    }

    private static bool IsVisible(FleetVehicleEntry entry) =>
        entry.RepState is not null && entry.RepState != "Offline";

    private void RaiseStateChanged() => StateChanged?.Invoke();
}
