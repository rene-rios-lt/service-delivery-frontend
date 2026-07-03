using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.ViewModels;

/// <summary>
/// Orchestrates the requester live rep-tracking view (FE-017): seeds itself from the
/// <see cref="IRepAssignedStore"/> payload the pending view deposited (rep name, vehicle registration, ETA,
/// initial rep + requester coordinates), registers the RequesterHub <c>RepPositionUpdated</c> handler, and
/// on each position push moves the rep marker coordinates, refreshes the ETA, maps the rep state to a status
/// message, hides the ETA once on-site, and raises <see cref="StateChanged"/> so the page re-renders and
/// redraws the map overlays. Depends only on Core abstractions — no map, HTTP, or SignalR types leak in.
/// </summary>
public class RequesterTrackingViewModel
{
    // Backend rep state strings (BE-008 / BE-019), matching RepStateColour.ForState and ActiveJob's
    // MapRepState. EnRoute is the initial state on assignment (the rep is on the way); the position stream
    // moves it to Within15Miles then OnSite.
    private const string EnRouteState = "EnRoute";
    private const string Within15MilesState = "Within15Miles";
    private const string OnSiteState = "OnSite";

    // State-to-message map (AC-4/AC-5). An unrecognised state falls back to the en-route message so the pill
    // never renders empty (mirrors RepStateColour's offline-grey fallback for an unknown state).
    private const string EnRouteMessage = "On the way";
    private const string Within15MilesMessage = "Almost there";
    private const string OnSiteMessage = "Your technician has arrived";

    public RequesterTrackingViewModel(IRepAssignedStore repAssignedStore, IRequesterHubService requesterHub)
    {
        var payload = repAssignedStore.CurrentPayload;
        if (payload is not null)
        {
            RepName = payload.RepName;
            VehicleRegistration = payload.VehicleRegistration;
            EtaMinutes = payload.EtaMinutes;
            RepLat = payload.Latitude;
            RepLng = payload.Longitude;
            // The requester's own location is the assignment destination. The rep starts navigating from its
            // own position (payload lat/lng) toward the requester; the requester pin is fixed here until the
            // first RepPositionUpdated push, then the route polyline connects the moving rep to it.
            RequesterLat = payload.Latitude;
            RequesterLng = payload.Longitude;
        }

        requesterHub.OnRepPositionUpdated(HandlePositionUpdatedAsync);
    }

    // Raised after each RepPositionUpdated push so the Razor page can re-render (StateHasChanged) and redraw
    // the map overlays. Keeps the push-driven re-render out of the page — the page only subscribes.
    public event Action? StateChanged;

    public string RepName { get; private set; } = string.Empty;

    // The rep's vehicle registration from the RepAssigned payload (BE-031 contract). Null/empty until BE-031
    // ships — VehicleSubtitle degrades gracefully in that case.
    public string VehicleRegistration { get; private set; } = string.Empty;

    public double EtaMinutes { get; private set; }

    public double RepLat { get; private set; }

    public double RepLng { get; private set; }

    public double RequesterLat { get; private set; }

    public double RequesterLng { get; private set; }

    // The rep's current state, driving the marker colour, status message, and ETA visibility. Starts EnRoute
    // (the rep is on the way the instant it is assigned) and advances via the position stream.
    public string RepState { get; private set; } = EnRouteState;

    // AC-2: the bottom-sheet subtitle. Degrades to "Service Rep" when the registration is null/empty
    // (pre-BE-031) — no dangling "Vehicle ·" — otherwise reads "Vehicle {registration} · Service Rep".
    public string VehicleSubtitle =>
        string.IsNullOrWhiteSpace(VehicleRegistration)
            ? "Service Rep"
            : $"Vehicle {VehicleRegistration} · Service Rep";

    // AC-4/AC-5: the status-pill message for the current rep state.
    public string StatusMessage => RepState switch
    {
        Within15MilesState => Within15MilesMessage,
        OnSiteState => OnSiteMessage,
        _ => EnRouteMessage,
    };

    // AC-5: the ETA chip is shown while the rep is still travelling and hidden once on-site (the trip is
    // over — there is no ETA to a rep already here).
    public bool IsEtaVisible => RepState != OnSiteState;

    // AC-1/AC-3/AC-4/AC-5: a RepPositionUpdated push moves the rep marker, refreshes the ETA, advances the
    // state (which reshapes the message, marker colour, and ETA visibility), then raises StateChanged so the
    // page re-renders and redraws the overlays.
    public Task HandlePositionUpdatedAsync(RepPositionUpdatedPayload payload)
    {
        RepLat = payload.Latitude;
        RepLng = payload.Longitude;
        EtaMinutes = payload.EtaMinutes;
        RepState = payload.State;
        StateChanged?.Invoke();
        return Task.CompletedTask;
    }
}
