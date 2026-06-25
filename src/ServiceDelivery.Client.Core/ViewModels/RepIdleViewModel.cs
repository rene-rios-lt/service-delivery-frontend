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

    // Rendered when the store is empty (direct navigation with no prior take-over): a neutral,
    // blank-but-safe vehicle so the card and app-bar subtitle render without a NullReferenceException.
    private static readonly ClaimedVehicle EmptyVehicle =
        new(Guid.Empty, string.Empty, string.Empty, []);

    public RepIdleViewModel(
        IClaimedVehicleStore claimedVehicleStore,
        IRepHubService repHub,
        IPersonaNavigator navigator)
    {
        // Consume the hand-off once: read the vehicle the rep took over, then clear the store so a
        // later re-navigation does not resurrect a stale claim (mirrors IJobOfferStore consumption).
        Vehicle = claimedVehicleStore.CurrentVehicle ?? EmptyVehicle;
        claimedVehicleStore.ClearVehicle();
        _repHub = repHub;
        _navigator = navigator;
    }

    public ClaimedVehicle Vehicle { get; }

    // A rep only reaches this screen after a successful take-over, so the resting state is always
    // Available. The screen has no other visual state — the only change is navigating away to the
    // offer (AC-3), which is handled by the navigator, not by mutating this property.
    public RepIdleState State => RepIdleState.Available;

    public int JobsCompletedToday { get; }

    public async Task StartAsync()
    {
        _repHub.OnJobOfferReceived(OnJobOfferReceivedAsync);
        await _repHub.StartAsync();
    }

    public Task StopAsync() => _repHub.StopAsync();

    private Task OnJobOfferReceivedAsync(JobOfferPayload offer)
    {
        _navigator.NavigateToJobOffer(offer);
        return Task.CompletedTask;
    }
}
