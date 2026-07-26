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

    // Keyed by request id so an Assigned/Completed event resolves its card in O(1); Queue applies the sort.
    private readonly Dictionary<Guid, ActiveRequestEntry> _entries = new();

    public DispatcherRequestQueueViewModel(IActiveRequestQueueService queueService, IDispatchHubService hub)
    {
        _queueService = queueService;
        _hub = hub;
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
        if (_entries.TryGetValue(payload.RequestId, out var existing))
        {
            _entries[payload.RequestId] = existing with
            {
                Status = "Assigned",
                AssignedRepName = payload.RepName,
            };
            RaiseStateChanged();
            return;
        }

        var fetched = await _queueService.GetRequestAsync(payload.RequestId);
        if (fetched is null)
        {
            return;
        }

        _entries[payload.RequestId] = fetched with
        {
            Status = "Assigned",
            AssignedRepName = payload.RepName,
        };
        RaiseStateChanged();
    }

    /// <summary>The request completed: remove its card from the queue.</summary>
    public Task HandleServiceRequestCompletedAsync(ServiceRequestCompletedPayload payload)
    {
        if (_entries.Remove(payload.RequestId))
        {
            RaiseStateChanged();
        }

        return Task.CompletedTask;
    }

    private void RaiseStateChanged() => StateChanged?.Invoke();
}
