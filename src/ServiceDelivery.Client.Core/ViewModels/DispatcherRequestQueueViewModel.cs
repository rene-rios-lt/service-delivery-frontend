using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.ViewModels;

/// <summary>
/// Orchestrates the dispatcher's active request queue (FE-004): the initial REST snapshot load, real-time
/// merging of the three DispatchHub request-lifecycle events, and the priority sort (Gold → Silver → Bronze,
/// then oldest-first within a tier). These are cohesive — all change for the same reason (keeping the
/// dispatcher's live queue correct). Transport concerns live behind <see cref="IActiveRequestQueueService"/>
/// and <see cref="IDispatchHubService"/>; this ViewModel never touches HttpClient or a HubConnection directly
/// (Dependency Inversion), mirroring <see cref="DispatcherFleetViewModel"/>.
/// </summary>
public class DispatcherRequestQueueViewModel
{
    private readonly IActiveRequestQueueService _queueService;
    private readonly IDispatchHubService _hub;
    private readonly IRedirectEligibilityService _eligibility;
    private readonly IDispatcherRedirectService _redirectService;

    // Keyed by request id so an Assigned/Completed event resolves its card in O(1); Queue applies the sort.
    private readonly Dictionary<Guid, ActiveRequestEntry> _entries = new();

    // FE-005: the latest fleet snapshot bridged in from DispatcherFleetViewModel by RequestQueueRail, and the
    // per-request redirect eligibility recomputed from it. Kept independent of the fleet ViewModel (SRP) — the
    // component is the composition bridge, so eligibility rules and queue-display rules change independently.
    private IReadOnlyList<FleetVehicleEntry> _fleet = [];
    private readonly Dictionary<Guid, RedirectInfo> _redirectEligibility = new();

    // FE-005 (cycle 3): the real-time active-request tier per rep, learned from the DispatchHub
    // ServiceRequestAssigned event (whose target request's tier the queue already holds), keyed by rep id. The
    // GET /dispatcher/fleet snapshot only carries a rep's active-request tier if the rep was already assigned
    // when the dispatcher opened the board, and the ~3s VehiclePositionUpdated pings carry position/state only —
    // so without this overlay a rep assigned AFTER load never surfaces a redirect. Applied non-destructively
    // over the fleet in RecomputeEligibility so a subsequent position-update UpdateFleetData cannot clobber it;
    // cleared on ServiceRequestCompleted (the inverse).
    private readonly Dictionary<Guid, (Guid RequestId, ServiceTier Tier)> _realtimeAssignedTier = new();

    // Redirect confirmation dialog state (FE-005 AC-2/AC-3/AC-4).
    private RedirectInfo? _activeRedirectInfo;
    private bool _isRedirecting;
    private string? _redirectError;

    public DispatcherRequestQueueViewModel(
        IActiveRequestQueueService queueService,
        IDispatchHubService hub,
        IRedirectEligibilityService eligibility,
        IDispatcherRedirectService redirectService)
    {
        _queueService = queueService;
        _hub = hub;
        _eligibility = eligibility;
        _redirectService = redirectService;
    }

    /// <summary>Raised whenever the queue contents or an entry's state change.</summary>
    public event Action? StateChanged;

    /// <summary>
    /// Whether the DispatchHub connection is currently established (and therefore joined to its
    /// <c>dealer:{id}</c> group on the backend, so real-time request-lifecycle events will be delivered).
    /// Mirrors <see cref="RequesterPendingViewModel.IsHubConnected"/>. The rail exposes this as an
    /// E2E-detectable readiness hook: a one-shot <c>ServiceRequestPending</c>/<c>ServiceRequestCompleted</c>
    /// emitted before the connection joins its group is lost with no retry, so a live scenario must wait for
    /// this to be true before triggering the backend event it expects the rail to receive.
    /// </summary>
    public bool IsHubConnected => _hub.IsConnected;

    /// <summary>
    /// The queue as the rail renders it — sorted Gold → Silver → Bronze (tier descending, since the
    /// <see cref="ServiceTier"/> ordinal runs None &lt; Bronze &lt; Silver &lt; Gold), then by
    /// <see cref="ActiveRequestEntry.CreatedAt"/> ascending (oldest first) within a tier.
    /// </summary>
    public IReadOnlyList<ActiveRequestEntry> Queue =>
        _entries.Values
            .OrderByDescending(e => e.Tier)
            .ThenBy(e => e.CreatedAt)
            .ToList();

    public async Task LoadAsync()
    {
        var requests = await _queueService.GetActiveRequestsAsync();
        _entries.Clear();
        foreach (var entry in requests)
        {
            _entries[entry.RequestId] = entry;
        }

        RaiseStateChanged();
    }

    public async Task StartHubAsync()
    {
        _hub.OnServiceRequestPending(HandleServiceRequestPendingAsync);
        _hub.OnServiceRequestAssigned(HandleServiceRequestAssignedAsync);
        _hub.OnServiceRequestCompleted(HandleServiceRequestCompletedAsync);
        await _hub.StartAsync();

        // The connect is now resolved (on the happy path the connection is established and joined to its
        // dealer group; on failure the service's bounded back-off is retrying in the background). Either way
        // the connectivity state has settled, so raise StateChanged to re-render the rail — this is what flips
        // its data-dispatch-hub-connected readiness hook once the hub is live.
        RaiseStateChanged();
    }

    public Task StopHubAsync() => _hub.StopAsync();

    /// <summary>
    /// A new unassigned request arrived. The event carries no requester name, so fetch the full entry before
    /// adding the card; if the follow-up fetch returns null (already completed server-side), skip silently.
    /// </summary>
    public async Task HandleServiceRequestPendingAsync(ServiceRequestPendingPayload payload)
    {
        var entry = await _queueService.GetRequestAsync(payload.RequestId);
        if (entry is null)
        {
            return;
        }

        _entries[entry.RequestId] = entry;

        // FE-005 (cycle 3): a newly-arrived request must have its redirect eligibility computed against the
        // current fleet immediately — a dispatcher already on the board should see the Redirect button as soon
        // as the request is known and an eligible EnRoute rep exists, not only after the next ~3s fleet poll.
        // Consistent with LoadAsync / UpdateFleetData / the Assigned + Completed handlers, which all recompute.
        RecomputeEligibility();

        RaiseStateChanged();
    }

    /// <summary>
    /// A rep accepted the request: flip the card to "Assigned" and fill the rep name. If the request is not yet
    /// in the queue this is an <em>upsert</em>: the backend only emits <c>ServiceRequestPending</c> to the
    /// dispatcher when NO rep matches (there is no matching-radius cap), so a request that matches an available
    /// rep is assigned without the dispatcher ever seeing it Pending — the assigned event is then the first
    /// real-time signal for it, and it must ADD the card, not be ignored (otherwise a newly-assigned request
    /// only appears after a page reload). The event carries no requester name/tier/DTC, so fetch the full entry
    /// (as the Pending handler does); a null fetch (already completed server-side) is skipped silently.
    /// </summary>
    public async Task HandleServiceRequestAssignedAsync(ServiceRequestAssignedPayload payload)
    {
        ActiveRequestEntry assigned;
        if (_entries.TryGetValue(payload.RequestId, out var existing))
        {
            assigned = existing with
            {
                Status = "Assigned",
                AssignedRepName = payload.RepName,
            };
        }
        else
        {
            var fetched = await _queueService.GetRequestAsync(payload.RequestId);
            if (fetched is null)
            {
                return;
            }

            assigned = fetched with
            {
                Status = "Assigned",
                AssignedRepName = payload.RepName,
            };
        }

        _entries[payload.RequestId] = assigned;

        // FE-005 (cycle 3): record the rep's real-time active-request tier so redirect eligibility surfaces for a
        // rep assigned AFTER the dispatcher's fleet snapshot loaded, WITHOUT a snapshot reload. We already know
        // the tier here (it is on the assigned queue entry). The overlay is applied over the fleet inside
        // RecomputeEligibility, so this is robust to the tier-less VehiclePositionUpdated merges that follow.
        _realtimeAssignedTier[payload.RepId] = (payload.RequestId, assigned.Tier);
        RecomputeEligibility();

        RaiseStateChanged();
    }

    /// <summary>
    /// The request completed: remove its card from the queue and drop any real-time tier learned for the rep on
    /// that request (the inverse of the assignment overlay — a rep whose lower-tier job cleared is no longer a
    /// redirect target), then recompute eligibility.
    /// </summary>
    public Task HandleServiceRequestCompletedAsync(ServiceRequestCompletedPayload payload)
    {
        ClearRealtimeTierForRequest(payload.RequestId);

        if (_entries.Remove(payload.RequestId))
        {
            RecomputeEligibility();
            RaiseStateChanged();
        }

        return Task.CompletedTask;
    }

    // ---- FE-005: redirect eligibility + confirmation dialog ---------------------------------------------

    /// <summary>The redirect opportunity currently shown in the confirmation dialog, or null when closed.</summary>
    public RedirectInfo? ActiveRedirectInfo => _activeRedirectInfo;

    /// <summary>True from the moment a redirect is confirmed until the API round-trip resolves.</summary>
    public bool IsRedirecting => _isRedirecting;

    /// <summary>The message from the last failed redirect, or null when the last attempt did not error.</summary>
    public string? RedirectError => _redirectError;

    /// <summary>The eligible redirect for this request's card, or null when no rep can be redirected to it.</summary>
    public RedirectInfo? GetRedirectInfo(Guid requestId) =>
        _redirectEligibility.GetValueOrDefault(requestId);

    /// <summary>
    /// Called by the rail whenever the fleet ViewModel's state changes: stores the latest fleet snapshot,
    /// recomputes per-request redirect eligibility, and re-renders. This is the composition bridge between the
    /// two independent dispatcher ViewModels (SRP) — the rail owns the wiring, not either ViewModel.
    /// </summary>
    public void UpdateFleetData(IReadOnlyList<FleetVehicleEntry> fleet)
    {
        _fleet = fleet;
        RecomputeEligibility();
        RaiseStateChanged();
    }

    private void RecomputeEligibility()
    {
        var effectiveFleet = ApplyRealtimeTiers(_fleet);
        _redirectEligibility.Clear();
        foreach (var entry in _entries.Values)
        {
            var info = _eligibility.FindEligibleRedirect(entry, effectiveFleet);
            if (info is not null)
            {
                _redirectEligibility[entry.RequestId] = info;
            }
        }
    }

    /// <summary>
    /// Overlays the real-time active-request tiers (learned from ServiceRequestAssigned) onto the fleet snapshot
    /// before it is scanned for eligibility. Non-destructive: when no assignment overlay is active the original
    /// list is returned unchanged (same reference, no allocation), so the common case is untouched and a stale
    /// tier can never leak in. A rep with an overlay has its <see cref="FleetVehicleEntry.ActiveRequestTier"/>
    /// replaced by the tier it was most recently assigned to.
    /// </summary>
    private IReadOnlyList<FleetVehicleEntry> ApplyRealtimeTiers(IReadOnlyList<FleetVehicleEntry> fleet)
    {
        if (_realtimeAssignedTier.Count == 0)
        {
            return fleet;
        }

        return fleet
            .Select(v => v.RepId is { } repId && _realtimeAssignedTier.TryGetValue(repId, out var assignment)
                ? v with { ActiveRequestTier = assignment.Tier.ToString() }
                : v)
            .ToList();
    }

    private void ClearRealtimeTierForRequest(Guid requestId)
    {
        foreach (var (repId, assignment) in _realtimeAssignedTier)
        {
            if (assignment.RequestId == requestId)
            {
                _realtimeAssignedTier.Remove(repId);
                break;
            }
        }
    }

    /// <summary>Opens the redirect confirmation dialog for the given request (no-op when not eligible).</summary>
    public void ShowRedirectDialog(Guid requestId)
    {
        if (_redirectEligibility.TryGetValue(requestId, out var info))
        {
            _activeRedirectInfo = info;
            RaiseStateChanged();
        }
    }

    /// <summary>Dismisses the redirect confirmation dialog without redirecting.</summary>
    public void CancelRedirect()
    {
        _activeRedirectInfo = null;
        RaiseStateChanged();
    }

    /// <summary>
    /// Confirms the redirect currently in the dialog. Optimistic (AC-3): the dialog is dismissed and
    /// <see cref="IsRedirecting"/> flips true BEFORE the <c>POST /dispatcher/redirect</c> round-trip completes.
    /// On success the in-flight flag clears, the dialog stays closed, and the entry's eligibility is dropped (a
    /// subsequent fleet poll recomputes it once the rep's new state lands). On error (AC-4) the flag clears,
    /// <see cref="RedirectError"/> carries the message, the entry's eligibility is dropped (so the underlying
    /// card can no longer initiate a redirect), and the dialog RE-SURFACES carrying the error so the dispatcher
    /// sees why the redirect failed (the error banner is rendered inside the dialog per the composition map).
    /// </summary>
    public async Task ConfirmRedirectAsync()
    {
        if (_activeRedirectInfo is not { } info)
        {
            return;
        }

        _activeRedirectInfo = null;
        _isRedirecting = true;
        _redirectError = null;
        RaiseStateChanged();

        try
        {
            await _redirectService.RedirectAsync(info.RepId, info.ToRequestId);
        }
        catch (Exception ex)
        {
            _redirectError = ex.Message;
            _activeRedirectInfo = info;
        }
        finally
        {
            _isRedirecting = false;
            _redirectEligibility.Remove(info.ToRequestId);
            RaiseStateChanged();
        }
    }

    private void RaiseStateChanged() => StateChanged?.Invoke();
}
