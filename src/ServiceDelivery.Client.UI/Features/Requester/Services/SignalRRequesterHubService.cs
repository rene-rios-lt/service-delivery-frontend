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
    private readonly Func<CancellationToken, Task> _connectAsync;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
    private readonly Func<bool> _isConnected;

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
        _isConnected = () => _connection.State == HubConnectionState.Connected;
    }

    /// <summary>
    /// Test seam: injects the connect / delay / connection-state operations so the back-off retry loop
    /// can be exercised deterministically without a live transport. Not used in production wiring.
    /// </summary>
    internal SignalRRequesterHubService(
        HttpClient httpClient,
        ITokenStore tokenStore,
        ILogger<SignalRRequesterHubService> logger,
        Func<CancellationToken, Task> connectAsync,
        Func<TimeSpan, CancellationToken, Task> delayAsync,
        Func<bool> isConnected)
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
        _isConnected = isConnected;
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

    public bool IsConnected => _isConnected();

    // BUG-038: never let an unreachable backend propagate an exception to the caller (the pending
    // screen). Try once; if that fails, hand off to a bounded back-off retry loop running on its own task
    // so the caller returns immediately and the screen renders in its reconnecting state instead of
    // crashing.
    public async Task StartAsync()
    {
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
