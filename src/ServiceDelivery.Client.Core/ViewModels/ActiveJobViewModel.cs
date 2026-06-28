using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.ViewModels;

public class ActiveJobViewModel
{
    // The backend rep state at which the rep is close enough to arrive (BE-008). At this state the
    // "I've Arrived" button (AC-4) becomes enabled.
    private const string Within15MilesState = "Within15Miles";

    // The backend rep state once the rep has marked arrival (BE-019). On load in this state the view
    // is already on-site: arrive stays enabled (AC-3) and the on-site presentation applies.
    private const string OnSiteState = "OnSite";

    private readonly IActiveJobService _activeJobService;
    private readonly IRepHubService _repHub;
    private readonly IArriveService _arriveService;
    private readonly ICompleteJobService _completeJobService;
    private readonly IPersonaNavigator _navigator;

    public ActiveJobViewModel(
        IActiveJobService activeJobService,
        IRepHubService repHub,
        IArriveService arriveService,
        ICompleteJobService completeJobService,
        IPersonaNavigator navigator)
    {
        _activeJobService = activeJobService;
        _repHub = repHub;
        _arriveService = arriveService;
        _completeJobService = completeJobService;
        _navigator = navigator;
    }

    // Raised after each position poll and after a redirect so the Razor page can re-render
    // (StateHasChanged). Keeps the poll/redirect-driven re-render out of the page — the page only
    // subscribes.
    public event Action? StateChanged;

    public double RepLat { get; private set; }

    public double RepLng { get; private set; }

    public double RequesterLat { get; private set; }

    public double RequesterLng { get; private set; }

    public int EtaMinutes { get; private set; }

    public double DistanceMiles { get; private set; }

    public bool IsArrivedEnabled { get; private set; }

    public bool IsOnSite { get; private set; }

    public string RequesterName { get; private set; } = string.Empty;

    public string DtcTitle { get; private set; } = string.Empty;

    public string Tier { get; private set; } = string.Empty;

    public async Task LoadAsync()
    {
        var context = await _activeJobService.GetActiveJobAsync();
        ApplyContext(context);
    }

    public async Task StartAsync()
    {
        _repHub.OnRedirectReceived(OnRedirectReceivedAsync);
        await _repHub.StartAsync();
    }

    public Task StopAsync() => _repHub.StopAsync();

    public async Task ArriveAsync()
    {
        await _arriveService.ArriveAsync();
        IsOnSite = true;
        StateChanged?.Invoke();
    }

    public async Task CompleteAsync()
    {
        await _completeJobService.CompleteAsync();
        _navigator.NavigateToRepIdleView();
    }

    public async Task PollPositionAsync()
    {
        var context = await _activeJobService.GetActiveJobAsync();
        ApplyContext(context);
        StateChanged?.Invoke();
    }

    public Task OnRedirectReceivedAsync(RedirectPayload redirect)
    {
        RequesterLat = redirect.Latitude;
        RequesterLng = redirect.Longitude;
        DtcTitle = redirect.DtcTitle;
        EtaMinutes = (int)Math.Round(redirect.EtaMinutes);
        DistanceMiles = redirect.DistanceMiles;
        StateChanged?.Invoke();
        return Task.CompletedTask;
    }

    private void ApplyContext(ActiveJobContext? context)
    {
        if (context is null)
        {
            return;
        }

        RepLat = context.RepLat;
        RepLng = context.RepLng;
        RequesterLat = context.RequesterLat;
        RequesterLng = context.RequesterLng;
        RequesterName = context.RequesterName;
        DtcTitle = context.DtcTitle;
        Tier = context.Tier;
        EtaMinutes = context.EtaMinutes;
        DistanceMiles = context.DistanceMiles;
        IsOnSite = IsOnSite || context.RepState == OnSiteState;
        IsArrivedEnabled = IsOnSite || context.RepState == Within15MilesState;
    }
}
