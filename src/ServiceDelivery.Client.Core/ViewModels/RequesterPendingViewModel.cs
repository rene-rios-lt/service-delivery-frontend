using Microsoft.Extensions.Logging;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.ViewModels;

/// <summary>
/// Orchestrates the requester pending / "finding your technician" view (FE-016): starts the
/// <see cref="IRequesterHubService"/>, registers the <c>RepAssigned</c> handler once (BUG-042) so an
/// assignment navigates straight to the tracking view (AC-3), swallows-and-logs hub connect failures and
/// surfaces <see cref="IsHubConnected"/> (BUG-038), and sources the authenticated requester's REAL
/// service <see cref="Tier"/> from <see cref="IAuthService"/> (never a hardcoded GOLD — the BUG-034
/// masking guard). Depends only on Core abstractions.
/// </summary>
public class RequesterPendingViewModel
{
    private readonly IRequesterHubService _requesterHub;
    private readonly IPersonaNavigator _navigator;
    private readonly IAuthService _authService;
    private readonly ILogger<RequesterPendingViewModel> _logger;

    public RequesterPendingViewModel(
        IRequesterHubService requesterHub,
        IPersonaNavigator navigator,
        IAuthService authService,
        ILogger<RequesterPendingViewModel> logger)
    {
        _requesterHub = requesterHub;
        _navigator = navigator;
        _authService = authService;
        _logger = logger;

        // BUG-042 (double-subscribe): register the RepAssigned handler exactly once per VM lifetime. The
        // VM is scoped (≈ singleton for the BlazorWebView session) and the page calls StartAsync on every
        // entry to /requester/pending; registering inside StartAsync would accumulate handlers on the same
        // HubConnection and fire NavigateToRequesterTracking once per past navigation. The registration
        // belongs here — it is independent of how many times the page is mounted.
        _requesterHub.OnRepAssigned(OnRepAssignedAsync);
    }

    // Reflects whether the RequesterHub connection is currently established. False while the service's
    // initial-connect retry loop is still running (or after a connect failure), so the screen can show an
    // unobtrusive "Reconnecting…" indicator instead of crashing (BUG-038).
    public bool IsHubConnected => _requesterHub.IsConnected;

    // The authenticated requester's REAL service tier, sourced from UserProfile.Tier (GET /users/me) on
    // StartAsync — never a hardcoded GOLD (the BUG-034 masking guard). Defaults to None until the profile
    // loads (or if the fetch fails), so the tier badge renders the user's actual tier (gold1 → Gold,
    // silver1 → Silver, bronze1 → Bronze) and simply hides when the tier is unresolved.
    public ServiceTier Tier { get; private set; } = ServiceTier.None;

    public async Task StartAsync()
    {
        // Source the authenticated requester's tier first so the badge renders the real tier on init.
        // A failed fetch (e.g. 401 at the route) must not crash the screen — swallow-and-log, leaving the
        // tier unresolved (None) so the badge is simply omitted rather than tripping the error banner.
        try
        {
            var profile = await _authService.GetCurrentUserAsync();
            Tier = profile.Tier;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex, "Could not load the requester profile for the pending screen; tier badge will be hidden.");
        }

        // BUG-038: the hub's StartAsync retries internally, but if it still throws (backend unreachable
        // for the whole retry budget) we swallow-and-log here so the pending screen never raises an
        // unhandled-error banner. The reconnecting state is surfaced via IsHubConnected. The RepAssigned
        // handler is registered once in the constructor (BUG-042), so StartAsync only (re)connects the
        // hub on each page entry.
        try
        {
            await _requesterHub.StartAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex, "RequesterHub connection could not be established; pending screen will show reconnecting.");
        }
    }

    public Task StopAsync() => _requesterHub.StopAsync();

    // AC-3: a RepAssigned push transitions the requester from "finding your technician" to the
    // rep-tracking view (FE-017's route). The navigator carries no payload yet — FE-017 owns the
    // tracking screen and will fetch / receive the rep detail; FE-016 only needs the transition.
    private Task OnRepAssignedAsync(RepAssignedPayload payload)
    {
        _navigator.NavigateToRequesterTracking();
        return Task.CompletedTask;
    }
}
