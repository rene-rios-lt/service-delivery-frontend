using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.ViewModels;

public class JobOfferViewModel
{
    private const int InitialSeconds = 60;
    private const int UrgentThresholdSeconds = 10;
    private const string OfferExpiredMessage = "Offer expired";

    private readonly JobOfferPayload _offer;
    private readonly IPersonaNavigator _navigator;
    private readonly IJobOfferService _jobOfferService;
    private readonly IDeclineOfferService _declineOfferService;

    public JobOfferViewModel(
        JobOfferPayload offer,
        IPersonaNavigator navigator,
        IJobOfferService jobOfferService,
        IDeclineOfferService declineOfferService)
    {
        _offer = offer;
        _navigator = navigator;
        _jobOfferService = jobOfferService;
        _declineOfferService = declineOfferService;
    }

    // Raised after each countdown tick so the Razor page can re-render (StateHasChanged). Keeps the
    // timer-driven re-render out of the page — the page only subscribes.
    public event Action? StateChanged;

    public int SecondsRemaining { get; private set; } = InitialSeconds;

    // Non-null after a 409 conflict on accept (AC-3): the offer expired between the tap and the API
    // call. The page renders this as the "Offer expired" message before dismissing to the idle view.
    public string? ErrorMessage { get; private set; }

    public bool IsUrgent => SecondsRemaining <= UrgentThresholdSeconds;

    public string RequesterName => _offer.RequesterName;

    public ServiceTier Tier => _offer.Tier;

    public string DtcTitle => _offer.DtcTitle;

    public double DistanceMiles => _offer.DistanceMiles;

    public int EtaMinutes => _offer.EtaMinutes;

    public double Lat => _offer.Lat;

    public double Lng => _offer.Lng;

    // Accept calls POST /job-offers/{id}/accept via the service (FE-009).
    public async Task AcceptAsync()
    {
        var result = await _jobOfferService.AcceptAsync(_offer.OfferId);

        if (result == AcceptOfferResult.Success)
        {
            // AC-2: accepted — transition to the active-job view (FE-011) to navigate to the requester.
            _navigator.NavigateToActiveJob();
            return;
        }

        // AC-3: 409 conflict — the offer expired between the tap and the API call. Surface the message
        // for the page to show, then dismiss back to the idle / waiting-for-offers view.
        ErrorMessage = OfferExpiredMessage;
        _navigator.NavigateToRepIdleView();
    }

    // Decline calls POST /job-offers/{id}/decline via the service (AC-1). On success the offer screen
    // dismisses back to the idle / waiting-for-offers view (AC-2).
    public async Task DeclineAsync()
    {
        await _declineOfferService.DeclineAsync(_offer.OfferId);
        _navigator.NavigateToRepIdleView();
    }

    // BUG-037: handle the server-pushed JobOfferExpired event. When it targets the on-screen offer the
    // server has expired it before the local countdown reached zero, so dismiss the screen immediately
    // rather than waiting out the timer. A payload for any other offer (stale or out-of-order push) is
    // ignored so it cannot dismiss the offer the rep is currently looking at (AC-3). Server-driven
    // expiry joins the local countdown and the 409-on-accept path as a third "this offer is no longer
    // actionable" route — all cohesive in this ViewModel.
    public Task HandleJobOfferExpiredAsync(JobOfferExpiredPayload payload)
    {
        if (payload.OfferId != _offer.OfferId)
        {
            return Task.CompletedTask;
        }

        // Park the countdown at zero so a stray tick cannot fire a second navigation — TickAsync's
        // zero-guard then short-circuits any further decrement.
        SecondsRemaining = 0;
        _navigator.NavigateToRepIdleView();

        return Task.CompletedTask;
    }

    public Task TickAsync()
    {
        if (SecondsRemaining == 0)
        {
            return Task.CompletedTask;
        }

        SecondsRemaining--;
        StateChanged?.Invoke();

        if (SecondsRemaining == 0)
        {
            // Countdown reached zero — the offer has expired server-side, so dismiss the screen by
            // navigating back to the idle / waiting-for-offers view (AC-5).
            _navigator.NavigateToRepIdleView();
        }

        return Task.CompletedTask;
    }
}
