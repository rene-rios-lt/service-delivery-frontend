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

    private readonly IRequesterHubService _requesterHub;

    public RequesterTrackingViewModel(IRepAssignedStore repAssignedStore, IRequesterHubService requesterHub)
    {
        _requesterHub = requesterHub;

        var payload = repAssignedStore.CurrentPayload;
        if (payload is not null)
        {
            ApplyRepState(payload);
            // The requester's own location is the assignment destination. The rep starts navigating from its
            // own position (payload lat/lng) toward the requester; the requester pin is fixed here until the
            // first RepPositionUpdated push, then the route polyline connects the moving rep to it. Only the
            // INITIAL assignment sets this — a redirect's RepAssigned (HandleRepAssignedAsync) moves the rep
            // but leaves the fixed requester destination in place.
            RequesterLat = payload.Latitude;
            RequesterLng = payload.Longitude;
        }

        requesterHub.OnRepPositionUpdated(HandlePositionUpdatedAsync);
        requesterHub.OnRepAssigned(HandleRepAssignedAsync);
        requesterHub.OnRepRedirected(HandleRepRedirectedAsync);
    }

    // FE-018 (hub handoff): the requester's RequesterHub connection is SHARED (scoped) between the pending
    // and tracking views, and the pending page STOPS it when it navigates here (RequesterPending.DisposeAsync
    // → StopAsync). An explicit StopAsync leaves the connection Disconnected — WithAutomaticReconnect only
    // recovers unexpected drops, not a deliberate stop — so without a restart the tracking view would join no
    // group and silently miss every subsequent push: the initial map would show the seeded rep and freeze,
    // and the redirect's RepAssigned + RepRedirected (fired seconds later) would never arrive (the live E2E
    // caught exactly this — a stable listener on the same group received all three events while the browser
    // received only the first). The tracking page calls this on entry to re-establish the connection; the
    // backend's RequesterHub.OnConnectedAsync re-adds it to requester:{userId}. StartAsync on an already-live
    // connection is a benign no-op, so this is safe whether the pending stop has landed yet or not.
    public Task StartAsync() => _requesterHub.StartAsync();

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

    // FE-018/AC-1: banner state, set only by HandleRepRedirectedAsync. A one-way transition — once true it
    // stays true (a second RepRedirected merely refreshes the names/message). Drives the redirect banner's
    // visibility, the app-bar title swap, and the "NEW" chip in the bottom sheet.
    public bool IsRedirected { get; private set; }

    // The displaced (old) rep's name, used to build the apology message and shown in bold in the banner.
    // Empty until a redirect arrives.
    public string OldRepName { get; private set; } = string.Empty;

    // The replacement (new) rep's name from the RepRedirected payload, shown in bold in the banner alongside
    // OldRepName. This is the banner's copy of the name; the rep name shown in the bottom sheet comes from the
    // concurrent RepAssigned (RepName). Empty until a redirect arrives.
    public string NewRepName { get; private set; } = string.Empty;

    // AC-1: the requester-facing apology text — "Our apologies, we needed to redirect {old}. {new} is
    // heading your way now." Empty until a redirect arrives.
    public string RedirectMessage { get; private set; } = string.Empty;

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

    // FE-018/AC-2/AC-3: a RepAssigned push (the FIRST of the two redirect events) is the authority for the
    // rep-state picture — it moves the map, refreshes the ETA, and swaps the rep name and vehicle
    // registration to the new rep, then raises StateChanged so the page re-renders and redraws the overlays.
    public Task HandleRepAssignedAsync(RepAssignedPayload payload)
    {
        ApplyRepState(payload);
        StateChanged?.Invoke();
        return Task.CompletedTask;
    }

    // The rep-state assignment shared by the constructor seed and HandleRepAssignedAsync so the two are
    // identical in shape (Implementation Note 2): rep name, vehicle registration, ETA, and the rep marker
    // coordinates all come from the RepAssigned payload. Does NOT set RequesterLat/RequesterLng — the fixed
    // requester destination is seeded once by the constructor and is not the rep's moving position.
    private void ApplyRepState(RepAssignedPayload payload)
    {
        RepName = payload.RepName;
        VehicleRegistration = payload.VehicleRegistration;
        EtaMinutes = payload.EtaMinutes;
        RepLat = payload.Latitude;
        RepLng = payload.Longitude;
    }

    // FE-018/AC-1: a RepRedirected push (the SECOND of the two redirect events) is a pure banner concern —
    // it sets ONLY IsRedirected, OldRepName, and the apology message, then raises StateChanged so the banner
    // renders. It must NOT touch RepName, EtaMinutes, RepLat, RepLng, or VehicleRegistration — those come
    // exclusively from the concurrent RepAssigned handled above.
    public Task HandleRepRedirectedAsync(RepRedirectedPayload payload)
    {
        IsRedirected = true;
        OldRepName = payload.OldRepName;
        NewRepName = payload.NewRepName;
        RedirectMessage = BuildRedirectMessage(payload.OldRepName, payload.NewRepName);
        StateChanged?.Invoke();
        return Task.CompletedTask;
    }

    // AC-1: the exact apology text shown in the banner (matching both mockups, which render the two names in
    // bold). The names are the only variable slots. Kept as a single builder so the wording lives in one place.
    private static string BuildRedirectMessage(string oldRepName, string newRepName) =>
        $"Our apologies, we needed to redirect {oldRepName}. {newRepName} is heading your way now.";

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
