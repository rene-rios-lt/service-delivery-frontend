using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.UI.Features.Requester.Services;

/// <summary>
/// Blazor-generic <see cref="IRequesterHubService"/> backed by a real SignalR <see cref="HubConnection"/>.
/// Shared by every host because the hub contract is platform-agnostic. The hub URL is resolved from the
/// same <see cref="HttpClient"/> base address the rest of the app uses (so it always targets the
/// configured backend) plus the RequesterHub path <c>/hubs/requester</c>. This adapter only manages the
/// connection lifecycle and forwards the <c>RepAssigned</c> event (pending view, FE-016) to the
/// registered handler — all screen logic lives in the ViewModel. Mirrors <c>SignalRRepHubService</c>.
/// </summary>
public sealed class SignalRRequesterHubService : IRequesterHubService, IAsyncDisposable
{
    private const string RequesterHubPath = "hubs/requester";
    private const string RepAssignedEvent = "RepAssigned";
    private const string RepPositionUpdatedEvent = "RepPositionUpdated";
    private const string RepRedirectedEvent = "RepRedirected";
    private const string ServiceCompletedEvent = "ServiceCompleted";

    // BUG-038: bounded exponential back-off for the *initial* connect (1s → 2s → 4s → 8s → 16s, capped
    // at 30s). WithAutomaticReconnect only recovers a connection that was once established; it does
    // nothing for a backend that is unreachable at the instant the pending screen mounts, which is the
    // case this loop covers.
    private static readonly TimeSpan[] InitialConnectBackoff =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4),
        TimeSpan.FromSeconds(8),
        TimeSpan.FromSeconds(16),
    ];

    private readonly HubConnection _connection;
    private readonly ITokenStore _tokenStore;
    private readonly ILogger<SignalRRequesterHubService> _logger;
    private readonly CancellationTokenSource _disposeCts = new();

    // BUG-038: the connect, delay, and connection-state operations are seams so the bounded back-off
    // retry loop is unit-testable without a live SignalR server. Production binds them to the real
    // HubConnection and Task.Delay; tests inject a connect delegate that throws-then-succeeds plus a
    // no-op delay, which lets RetryConnectAsync be asserted directly (deleting the loop turns those
    // tests red — they no longer rely on the harness calling StartAsync twice).
    // FE-019: the connection-state seam surfaces the FULL HubConnectionState (not just a connected bool) so
    // StartAsync can distinguish Disconnected (cold-connect) from Connected/Connecting/Reconnecting (no-op).
    private readonly Func<CancellationToken, Task> _connectAsync;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
    private readonly Func<HubConnectionState> _connectionState;

    public SignalRRequesterHubService(
        HttpClient httpClient, ITokenStore tokenStore, ILogger<SignalRRequesterHubService> logger)
    {
        _tokenStore = tokenStore;
        _logger = logger;
        var hubUrl = new Uri(httpClient.BaseAddress!, RequesterHubPath);
        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options => options.AccessTokenProvider = ProvideAccessTokenAsync)
            .WithAutomaticReconnect()
            .Build();
        _connectAsync = ct => _connection.StartAsync(ct);
        _delayAsync = Task.Delay;
        _connectionState = () => _connection.State;
    }

    /// <summary>
    /// Test seam: injects the connect / delay / connection-state operations so the back-off retry loop
    /// and the state-aware StartAsync guard can be exercised deterministically without a live transport.
    /// Not used in production wiring.
    /// </summary>
    internal SignalRRequesterHubService(
        HttpClient httpClient,
        ITokenStore tokenStore,
        ILogger<SignalRRequesterHubService> logger,
        Func<CancellationToken, Task> connectAsync,
        Func<TimeSpan, CancellationToken, Task> delayAsync,
        Func<HubConnectionState> connectionState)
    {
        _tokenStore = tokenStore;
        _logger = logger;
        var hubUrl = new Uri(httpClient.BaseAddress!, RequesterHubPath);
        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options => options.AccessTokenProvider = ProvideAccessTokenAsync)
            .WithAutomaticReconnect()
            .Build();
        _connectAsync = connectAsync;
        _delayAsync = delayAsync;
        _connectionState = connectionState;
    }

    /// <summary>
    /// Supplies the JWT that SignalR appends as <c>?access_token=...</c> when negotiating the
    /// RequesterHub connection. The RequesterHub is <c>[Authorize(Roles="Requester")]</c> and websockets
    /// cannot send an Authorization header, so this is the SignalR equivalent of
    /// <c>AuthTokenHttpHandler</c> for REST calls — without it the connection is unauthenticated and
    /// never joins its <c>requester:{userId}</c> group.
    /// </summary>
    public Task<string?> ProvideAccessTokenAsync() => _tokenStore.GetTokenAsync();

    // The backend RepAssignedPayload field names (RepId/RepName/EtaMinutes/Latitude/Longitude) match the
    // client RepAssignedPayload exactly, so — like OnRedirectReceived on the RepHub — we bind directly to
    // the payload with no wire-DTO mapping step. The captured-payload deserialization test guards the
    // field-name match (ADR-0011).
    public void OnRepAssigned(Func<RepAssignedPayload, Task> handler) =>
        _connection.On(RepAssignedEvent, handler);

    // FE-017: the backend RepPositionUpdatedPayload field names (Latitude/Longitude/EtaMinutes/State) match
    // the client RepPositionUpdatedPayload exactly, so — like OnRepAssigned above — we bind directly to the
    // payload with no wire-DTO mapping step. The captured-payload deserialization test guards the field-name
    // match (ADR-0011).
    public void OnRepPositionUpdated(Func<RepPositionUpdatedPayload, Task> handler) =>
        _connection.On(RepPositionUpdatedEvent, handler);

    // FE-018: the backend RepRedirectedPayload field names (OldRepName/NewRepName/NewEtaMinutes) match the
    // client RepRedirectedPayload exactly, so — like OnRepAssigned / OnRepPositionUpdated above — we bind
    // directly to the payload with no wire-DTO mapping step. The captured-payload deserialization test guards
    // the field-name match (ADR-0011).
    public void OnRepRedirected(Func<RepRedirectedPayload, Task> handler) =>
        _connection.On(RepRedirectedEvent, handler);

    // FE-019: the backend ServiceCompletedPayload field name (RequestId) matches the client
    // ServiceCompletedPayload exactly, so — like the three On* handlers above — we bind directly to the
    // payload with no wire-DTO mapping step. The captured-payload deserialization test guards the field-name
    // match (ADR-0011).
    public void OnServiceCompleted(Func<ServiceCompletedPayload, Task> handler) =>
        _connection.On(ServiceCompletedEvent, handler);

    public bool IsConnected => _connectionState() == HubConnectionState.Connected;

    // BUG-038: never let an unreachable backend propagate an exception to the caller (the pending
    // screen). Try once; if that fails, hand off to a bounded back-off retry loop running on its own task
    // so the caller returns immediately and the screen renders in its reconnecting state instead of
    // crashing.
    public async Task StartAsync()
    {
        // FE-019: only cold-connect from Disconnected. The scoped RequesterHub connection is SHARED and now
        // PERSISTS across the requester's pending→tracking→complete navigations, so a view re-entering on a
        // Connected (or Connecting/Reconnecting) connection is a genuine no-op — NOT a failure to back off
        // from. HubConnection.StartAsync throws "cannot be started if it is not in the Disconnected state"
        // on a non-Disconnected connection; previously that throw was swallowed into the back-off below,
        // opening a multi-second window where the requester was not joined to its group and the one-shot
        // ServiceCompleted push was lost (SignalR does not buffer group messages for absent clients). This
        // guard makes StartAsync genuinely idempotent so the re-entry is a true no-op, while a real
        // cold/unreachable start (Disconnected) still connects and, on failure, backs off.
        if (_connectionState() != HubConnectionState.Disconnected)
        {
            return;
        }

        try
        {
            await _connectAsync(_disposeCts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex, "Initial RequesterHub connect failed; starting bounded back-off retry in the background.");
            _ = RetryConnectAsync();
        }
    }

    // Internal (not private) so the bounded back-off sequencing is asserted directly by a unit test
    // that injects a fake connect delegate (throws-then-succeeds) and a no-op delay — the retry
    // evidence lives here, on the production loop, not in a test that calls StartAsync twice.
    internal async Task RetryConnectAsync()
    {
        foreach (var delay in InitialConnectBackoff)
        {
            if (_disposeCts.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await _delayAsync(delay, _disposeCts.Token);
                await _connectAsync(_disposeCts.Token);
                _logger.LogInformation("RequesterHub connection re-established after retry.");
                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RequesterHub connect retry failed; will retry with longer back-off.");
            }
        }

        _logger.LogWarning("RequesterHub connect retry budget exhausted; remaining disconnected.");
    }

    public Task StopAsync() => _connection.StopAsync();

    public async ValueTask DisposeAsync()
    {
        await _disposeCts.CancelAsync();
        _disposeCts.Dispose();
        await _connection.DisposeAsync();
    }
}
