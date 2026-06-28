using Microsoft.Extensions.Logging;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.ViewModels;

/// <summary>
/// Orchestrates the idle / waiting-for-offers view: holds the claimed-vehicle info and the current
/// Available state, and ends the idle state when a <c>JobOfferReceived</c> event arrives over
/// RepHub by navigating to the job-offer screen (FE-008). Depends only on Core abstractions.
/// </summary>
public class RepIdleViewModel
{
    private readonly IClaimedVehicleStore _claimedVehicleStore;
    private readonly IRepHubService _repHub;
    private readonly IPersonaNavigator _navigator;
    private readonly ILogger<RepIdleViewModel> _logger;
    private readonly IHeartbeatService _heartbeatService;

    // Rendered when the store is empty (direct navigation with no prior take-over): a neutral,
    // blank-but-safe vehicle so the card and app-bar subtitle render without a NullReferenceException.
    private static readonly ClaimedVehicle EmptyVehicle =
        new(Guid.Empty, string.Empty, string.Empty, []);

    public RepIdleViewModel(
        IClaimedVehicleStore claimedVehicleStore,
        IRepHubService repHub,
        IPersonaNavigator navigator,
        ILogger<RepIdleViewModel> logger,
        IHeartbeatService heartbeatService)
    {
        _claimedVehicleStore = claimedVehicleStore;
        _repHub = repHub;
        _navigator = navigator;
        _logger = logger;
        _heartbeatService = heartbeatService;

        // BUG-042 (double-subscribe): register the job-offer handler exactly once per VM lifetime. The
        // VM is scoped (≈ singleton for the BlazorWebView session) and the page calls StartAsync on every
        // re-entry to /rep/idle; registering inside StartAsync accumulated handlers on the same
        // HubConnection and fired NavigateToJobOffer once per past navigation. The registration belongs
        // here — it is independent of how many times the page is mounted. The handler survives the
        // stop/start cycles StartAsync/DisposeAsync run on each page entry/teardown.
        _repHub.OnJobOfferReceived(OnJobOfferReceivedAsync);
    }

    // BUG-042 (stale display): read the claimed vehicle from the store on every access — do NOT cache it
    // at construction. The VM is reused across navigations, so a release-then-take-over of a different
    // vehicle in the same session must be reflected on the idle card and app-bar subtitle. The store is
    // the durable source of truth (BUG-041): this read never clears it.
    public ClaimedVehicle Vehicle => _claimedVehicleStore.CurrentVehicle ?? EmptyVehicle;

    // A rep only reaches this screen after a successful take-over, so the resting state is always
    // Available. The screen has no other visual state — the only change is navigating away to the
    // offer (AC-3), which is handled by the navigator, not by mutating this property.
    public RepIdleState State => RepIdleState.Available;

    public int JobsCompletedToday { get; }

    // Reflects whether the RepHub connection is currently established. False while the service's
    // initial-connect retry loop is still running (or after a connect failure), so the screen can
    // show an unobtrusive "Reconnecting…" indicator instead of crashing (BUG-038).
    public bool IsHubConnected => _repHub.IsConnected;

    public async Task StartAsync()
    {
        // BUG-038: the hub's StartAsync retries internally, but if it still throws (backend
        // unreachable for the whole retry budget) we swallow-and-log here so the idle screen never
        // raises an unhandled-error banner. The reconnecting state is surfaced via IsHubConnected.
        // The job-offer handler is registered once in the constructor (BUG-042), so StartAsync only
        // (re)connects the hub on each page entry.
        try
        {
            await _repHub.StartAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex, "RepHub connection could not be established; idle screen will show reconnecting.");
        }

        // FE-023: entering the idle view (post take-over) is the single "go on duty" moment, so the
        // heartbeat loop is started here. StartAsync is idempotent, so re-entering /rep/idle after
        // completing a job is safe — it never spawns a second loop. The heartbeat is started AFTER the
        // hub-connect attempt and outside its try/catch: it is an independent on-duty concern that must
        // run even if the hub is momentarily unreachable. It is deliberately NOT stopped in StopAsync —
        // the loop spans every rep page (idle → offer → job) and is torn down only on explicit logout
        // (ServiceRepLogoutSideEffect) or when the claimed-vehicle store is cleared (release path).
        await _heartbeatService.StartAsync();
    }

    public Task StopAsync() => _repHub.StopAsync();

    private Task OnJobOfferReceivedAsync(JobOfferPayload offer)
    {
        _navigator.NavigateToJobOffer(offer);
        return Task.CompletedTask;
    }
}
