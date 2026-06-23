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

    public RepIdleViewModel(
        ClaimedVehicle vehicle,
        IRepHubService repHub,
        IPersonaNavigator navigator)
    {
        Vehicle = vehicle;
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
