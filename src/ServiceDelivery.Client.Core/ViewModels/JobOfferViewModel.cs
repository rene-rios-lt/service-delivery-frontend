using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.ViewModels;

public class JobOfferViewModel
{
    private const int InitialSeconds = 60;
    private const int UrgentThresholdSeconds = 10;

    private readonly JobOfferPayload _offer;
    private readonly IPersonaNavigator _navigator;

    public JobOfferViewModel(JobOfferPayload offer, IPersonaNavigator navigator)
    {
        _offer = offer;
        _navigator = navigator;
    }

    // Raised after each countdown tick so the Razor page can re-render (StateHasChanged). Keeps the
    // timer-driven re-render out of the page — the page only subscribes.
    public event Action? StateChanged;

    public int SecondsRemaining { get; private set; } = InitialSeconds;

    public bool IsUrgent => SecondsRemaining <= UrgentThresholdSeconds;

    public string RequesterName => _offer.RequesterName;

    public ServiceTier Tier => _offer.Tier;

    public string DtcTitle => _offer.DtcTitle;

    public double DistanceMiles => _offer.DistanceMiles;

    public int EtaMinutes => _offer.EtaMinutes;

    public double Lat => _offer.Lat;

    public double Lng => _offer.Lng;

    // Accept and Decline are wired to their buttons in FE-008 but do nothing yet — FE-009 completes
    // the accept response and FE-010 the decline response. They return a completed Task rather than
    // throwing NotImplementedException so the buttons are honestly interactive (Liskov-safe).
    public Task AcceptAsync() => Task.CompletedTask;

    public Task DeclineAsync() => Task.CompletedTask;

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
