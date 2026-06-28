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
    private readonly IRepHubService _repHub;
    private readonly IPersonaNavigator _navigator;
    private readonly ILogger<RepIdleViewModel> _logger;

    // Rendered when the store is empty (direct navigation with no prior take-over): a neutral,
    // blank-but-safe vehicle so the card and app-bar subtitle render without a NullReferenceException.
    private static readonly ClaimedVehicle EmptyVehicle =
        new(Guid.Empty, string.Empty, string.Empty, []);

    public RepIdleViewModel(
        IClaimedVehicleStore claimedVehicleStore,
        IRepHubService repHub,
        IPersonaNavigator navigator,
        ILogger<RepIdleViewModel> logger)
    {
        // Read the vehicle the rep took over WITHOUT clearing the store: IClaimedVehicleStore is the
        // durable source of truth for the session, shared with ReleaseVehicleAction (BUG-041). Clearing
        // here wiped the claim before the release action could read it, so the confirmation dialog never
        // opened. The store is durably cleared on the real exit path — ReleaseVehicleAction.ReleaseAsync
        // calls ClearVehicle() on a successful release; a fresh take-over overwrites it via SetVehicle.
        Vehicle = claimedVehicleStore.CurrentVehicle ?? EmptyVehicle;
        _repHub = repHub;
        _navigator = navigator;
        _logger = logger;
    }

    public ClaimedVehicle Vehicle { get; }

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
        _repHub.OnJobOfferReceived(OnJobOfferReceivedAsync);

        // BUG-038: the hub's StartAsync retries internally, but if it still throws (backend
        // unreachable for the whole retry budget) we swallow-and-log here so the idle screen never
        // raises an unhandled-error banner. The reconnecting state is surfaced via IsHubConnected.
        try
        {
            await _repHub.StartAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex, "RepHub connection could not be established; idle screen will show reconnecting.");
        }
    }

    public Task StopAsync() => _repHub.StopAsync();

    private Task OnJobOfferReceivedAsync(JobOfferPayload offer)
    {
        _navigator.NavigateToJobOffer(offer);
        return Task.CompletedTask;
    }
}
