using System.Globalization;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.UI.Features.Maps.Components;

namespace ServiceDelivery.Client.UI.Features.Dispatcher.Components;

/// <summary>
/// Code-behind for the dispatcher fleet map (FE-003). Owns the embedded <see cref="GoogleMap"/> ref and the
/// marker delta-sync: on the map's ready signal and on every <c>DispatcherFleetViewModel.StateChanged</c> it
/// reconciles the live markers with the ViewModel's visible vehicles (add/update per vehicle, remove any
/// that dropped out — e.g. went Offline). A marker tap flows back through <see cref="OnMarkerClickedAsync"/>
/// into the ViewModel's selection. One clear purpose — keep the map's overlays in step with the ViewModel.
/// </summary>
public partial class FleetMap : IDisposable
{
    // Iowa centre / statewide zoom (mockup + plan): the fleet map opens framing the whole operating area.
    private const double IowaCentreLat = 41.60;
    private const double IowaCentreLng = -93.60;
    private const int IowaZoom = 7;

    private GoogleMap? _mapRef;

    // The map is not usable for overlay calls until its JS module has imported and initMap has run; the
    // OnMapReady signal flips this so a StateChanged that arrives during the initial load does not push
    // markers into a map that does not exist yet (the initial placement happens in OnMapReadyAsync).
    private bool _mapReady;

    // The marker ids currently on the map, so a delta sync can remove markers whose vehicle is no longer
    // visible (Offline / gone) rather than leaving a stale pin behind.
    private readonly HashSet<string> _renderedMarkerIds = new();

    protected override void OnInitialized()
    {
        ViewModel.StateChanged += OnViewModelStateChanged;
    }

    private async Task OnMapReadyAsync()
    {
        _mapReady = true;
        await SyncMarkersAsync();
    }

    /// <summary>Raised by the embedded map when a marker is tapped; selects that vehicle (opens its popover).</summary>
    public Task OnMarkerClickedAsync(string vehicleId)
    {
        ViewModel.SelectVehicle(vehicleId);
        return Task.CompletedTask;
    }

    private void OnViewModelStateChanged()
    {
        // A fleet load, a live position update, or a selection change can move/recolour/remove markers, so
        // resync the overlays alongside the Razor re-render.
        InvokeAsync(async () =>
        {
            await SyncMarkersAsync();
            StateHasChanged();
        });
    }

    private async Task SyncMarkersAsync()
    {
        if (!_mapReady || _mapRef is null)
        {
            return;
        }

        var visibleIds = new HashSet<string>();
        foreach (var vehicle in ViewModel.VisibleVehicles)
        {
            visibleIds.Add(vehicle.VehicleId);
            await _mapRef.AddOrUpdateMarkerAsync(
                vehicle.VehicleId,
                vehicle.Latitude,
                vehicle.Longitude,
                RepStateColour.ForState(vehicle.RepState ?? "Offline"),
                $"fleet-marker-{vehicle.VehicleId}");
        }

        foreach (var staleId in _renderedMarkerIds.Where(id => !visibleIds.Contains(id)).ToList())
        {
            await _mapRef.RemoveMarkerAsync(staleId);
        }

        _renderedMarkerIds.Clear();
        _renderedMarkerIds.UnionWith(visibleIds);
    }

    /// <summary>
    /// The accessible-summary line for one visible vehicle: identity, state, and its live position at the same
    /// 4-decimal precision googleMap.js stamps into the marker aria-label. This is the text an sr-only summary
    /// entry carries so a VoiceOver user (and the native Desktop E2E AX tree) can read the fleet — the
    /// google.maps markers themselves are pruned from the accessibility tree by the Maps SDK's aria-hidden
    /// overlay panes, so this out-of-pane mirror is the only accessible readout of a vehicle's live position.
    /// </summary>
    private static string FleetSummaryLine(FleetVehicleEntry vehicle) =>
        $"Vehicle {vehicle.Registration} — {vehicle.RepState} — " +
        $"{vehicle.Latitude.ToString("F4", CultureInfo.InvariantCulture)}," +
        $"{vehicle.Longitude.ToString("F4", CultureInfo.InvariantCulture)}";

    public void Dispose()
    {
        ViewModel.StateChanged -= OnViewModelStateChanged;
    }
}
