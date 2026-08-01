using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.ViewModels;

/// <summary>
/// Orchestrates the dispatcher fleet map's situational awareness (FE-003): the initial REST snapshot load,
/// real-time <c>VehiclePositionUpdated</c> merging, marker selection, and — for a claimed vehicle — the
/// force-release action (FE-022). These are cohesive — all change for the same reason (keeping the dispatcher's
/// live picture of the fleet correct and letting them act on it). Transport concerns (connection lifecycle,
/// retry/back-off, the force-release POST) live behind <see cref="IVehiclePositionHubService"/> and
/// <see cref="IForceReleaseService"/>; this ViewModel never touches HttpClient or a HubConnection directly
/// (Dependency Inversion).
/// </summary>
public class DispatcherFleetViewModel
{
    private readonly IDispatcherFleetService _fleetService;
    private readonly IVehiclePositionHubService _hub;
    private readonly IForceReleaseService _forceReleaseService;

    // Keyed by the string vehicle id (the marker key). Holds every vehicle from the snapshot; VisibleVehicles
    // applies the display filter so an Offline vehicle can be updated in place yet drop off the map.
    private readonly Dictionary<string, FleetVehicleEntry> _fleet = new();

    private string? _selectedVehicleId;

    // Force-release confirmation dialog state (FE-022 AC-1/AC-3/AC-5).
    private ForceReleaseInfo? _activeForceReleaseInfo;
    private bool _isForceReleasing;
    private string? _forceReleaseError;

    public DispatcherFleetViewModel(
        IDispatcherFleetService fleetService,
        IVehiclePositionHubService hub,
        IForceReleaseService forceReleaseService)
    {
        _fleetService = fleetService;
        _hub = hub;
        _forceReleaseService = forceReleaseService;
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

    // ---- FE-022: force-release confirmation dialog --------------------------------------------------------

    /// <summary>The force-release action currently shown in the confirmation dialog, or null when closed.</summary>
    public ForceReleaseInfo? ActiveForceReleaseInfo => _activeForceReleaseInfo;

    /// <summary>True from the moment a force-release is confirmed until the API round-trip resolves.</summary>
    public bool IsForceReleasing => _isForceReleasing;

    /// <summary>The message from the last failed force-release, or null when the last attempt did not error.</summary>
    public string? ForceReleaseError => _forceReleaseError;

    /// <summary>
    /// Opens the force-release confirmation dialog for the given vehicle, building
    /// <see cref="ActiveForceReleaseInfo"/> from its fleet entry. No-op for an unknown vehicle or an UNCLAIMED
    /// one (<see cref="FleetVehicleEntry.RepId"/> null) — an unclaimed vehicle has no rep session to revoke, so
    /// there is nothing to force-release. Returns a completed task (the open itself does no I/O) so the FleetMap
    /// can wire it as an async EventCallback handler.
    /// </summary>
    public Task OpenForceReleaseAsync(string vehicleId)
    {
        if (_fleet.TryGetValue(vehicleId, out var entry) && entry.RepId is not null)
        {
            _activeForceReleaseInfo = new ForceReleaseInfo(
                Guid.Parse(entry.VehicleId),
                entry.RepName ?? string.Empty,
                entry.Registration,
                entry.ActiveRequestTitle);
            _forceReleaseError = null;
            RaiseStateChanged();
        }

        return Task.CompletedTask;
    }

    /// <summary>Dismisses the force-release confirmation dialog without releasing.</summary>
    public void CancelForceRelease()
    {
        _activeForceReleaseInfo = null;
        RaiseStateChanged();
    }

    /// <summary>
    /// Confirms the force-release currently in the dialog. The dialog stays open with
    /// <see cref="IsForceReleasing"/> true for the duration of the <c>POST /vehicles/{id}/force-release</c>
    /// round-trip (so the confirm button disables and double-submit is prevented). On success the dialog is
    /// dismissed; on error (AC-5) <see cref="ForceReleaseError"/> carries the message and the dialog REMAINS open
    /// so the dispatcher sees why the release failed (the rep greying to Unclaimed reaches the map separately via
    /// the existing VehiclePositionHub Offline update — AC-4).
    /// </summary>
    public async Task ConfirmForceReleaseAsync()
    {
        if (_activeForceReleaseInfo is not { } info)
        {
            return;
        }

        _isForceReleasing = true;
        _forceReleaseError = null;
        RaiseStateChanged();

        try
        {
            await _forceReleaseService.ForceReleaseAsync(info.VehicleId);
            _activeForceReleaseInfo = null;
        }
        catch (Exception ex)
        {
            _forceReleaseError = ex.Message;
        }
        finally
        {
            _isForceReleasing = false;
            RaiseStateChanged();
        }
    }

    private static bool IsVisible(FleetVehicleEntry entry) =>
        entry.RepState is not null && entry.RepState != "Offline";

    private void RaiseStateChanged() => StateChanged?.Invoke();
}
