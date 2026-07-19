using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.UI.Features.Dispatcher.Services;

/// <summary>
/// Blazor-generic <see cref="IVehiclePositionHubService"/> backed by a real SignalR
/// <see cref="HubConnection"/> to the <c>VehiclePositionHub</c> (<c>/hubs/position</c>, FE-003 — the path
/// the backend maps in <c>Api/Program.cs</c>; pinned by a resolved-URL contract guard so it cannot drift).
/// Shared by every host serving the Dispatcher persona because the hub contract is platform-agnostic. This
/// adapter owns only the connection lifecycle and forwards the <c>VehiclePositionUpdated</c> event to the
/// registered handler — all fleet logic lives in <c>DispatcherFleetViewModel</c>. Mirrors
/// <c>SignalRRepHubService</c> / <c>SignalRRequesterHubService</c>, including the BUG-038 bounded back-off
/// so an unreachable backend at dashboard mount never crashes the dispatcher view.
/// </summary>
public sealed class SignalRVehiclePositionHubService : IVehiclePositionHubService, IAsyncDisposable
{
    private const string VehiclePositionHubPath = "hubs/position";
    private const string VehiclePositionUpdatedEvent = "VehiclePositionUpdated";

    // BUG-038: bounded exponential back-off for the *initial* connect (1s → 2s → 4s → 8s → 16s).
    // WithAutomaticReconnect only recovers a connection that was once established; it does nothing for a
    // backend unreachable at the instant the dashboard mounts, which is the case this loop covers.
    private static readonly TimeSpan[] InitialConnectBackoff =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4),
        TimeSpan.FromSeconds(8),
        TimeSpan.FromSeconds(16),
    ];

    private readonly HubConnection _connection;
    private readonly Uri _hubUri;
    private readonly ITokenStore _tokenStore;
    private readonly ILogger<SignalRVehiclePositionHubService> _logger;
    private readonly CancellationTokenSource _disposeCts = new();

    // BUG-038: the connect / delay / connection-state operations are seams so the bounded back-off retry
    // loop is unit-testable without a live SignalR server. Production binds them to the real HubConnection
    // and Task.Delay; tests inject a connect delegate that throws-then-succeeds plus a no-op delay.
    private readonly Func<CancellationToken, Task> _connectAsync;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
    private readonly Func<bool> _isConnected;

    public SignalRVehiclePositionHubService(
        HttpClient httpClient,
        ITokenStore tokenStore,
        ILogger<SignalRVehiclePositionHubService> logger,
        ILoggerFactory loggerFactory)
    {
        _tokenStore = tokenStore;
        _logger = logger;
        _hubUri = new Uri(httpClient.BaseAddress!, VehiclePositionHubPath);
        _connection = BuildConnection(_hubUri, loggerFactory);
        _connectAsync = ct => _connection.StartAsync(ct);
        _delayAsync = Task.Delay;
        _isConnected = () => _connection.State == HubConnectionState.Connected;
    }

    /// <summary>
    /// Test seam: injects the connect / delay / connection-state operations so the back-off retry loop can
    /// be exercised deterministically without a live transport. Not used in production wiring.
    /// </summary>
    internal SignalRVehiclePositionHubService(
        HttpClient httpClient,
        ITokenStore tokenStore,
        ILogger<SignalRVehiclePositionHubService> logger,
        ILoggerFactory loggerFactory,
        Func<CancellationToken, Task> connectAsync,
        Func<TimeSpan, CancellationToken, Task> delayAsync,
        Func<bool> isConnected)
    {
        _tokenStore = tokenStore;
        _logger = logger;
        _hubUri = new Uri(httpClient.BaseAddress!, VehiclePositionHubPath);
        _connection = BuildConnection(_hubUri, loggerFactory);
        _connectAsync = connectAsync;
        _delayAsync = delayAsync;
        _isConnected = isConnected;
    }

    // Both constructors build the VehiclePositionHub connection identically (the only difference is the
    // connect/delay/state seam), so the builder lives here to avoid duplication.
    //
    // FE-003 cycle 9: route the HubConnection's own internal transport/dispatch logging into the host's
    // ILoggerFactory. A client-side SignalR transport failure was invisible TWICE in this story because the
    // connection was built with no logger — its diagnostics went nowhere. AddSingleton(loggerFactory) makes
    // the connection resolve the host factory instead of its default, so its logs reach the host log; on
    // Desktop the OsLogLoggerProvider forwards them to the macOS unified log, where they survive the
    // XCTest/Appium launcher (which swallows stdout) during the live Mac2 gate.
    private HubConnection BuildConnection(Uri hubUri, ILoggerFactory loggerFactory) =>
        new HubConnectionBuilder()
            .WithUrl(hubUri, options => options.AccessTokenProvider = ProvideAccessTokenAsync)
            .WithAutomaticReconnect()
            .ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Debug);
                logging.Services.AddSingleton(loggerFactory);
            })
            .Build();

    /// <summary>
    /// The fully-resolved hub URL this service hands to the connection builder (the exact <see cref="Uri"/>
    /// passed to <c>WithUrl</c>). Exposed as a test seam so a contract guard can pin the resolved path
    /// against the backend's <c>MapHub</c> route — the transport-seam tests never observe the URL, so a
    /// wrong path would otherwise ship green (FE-003 cycle-1 defect).
    /// </summary>
    internal Uri HubUri => _hubUri;

    /// <summary>
    /// Supplies the JWT that SignalR appends as <c>?access_token=...</c> when negotiating the
    /// VehiclePositionHub connection. The hub is <c>[Authorize(Roles="Dispatcher")]</c> and websockets
    /// cannot send an Authorization header, so this is the SignalR equivalent of <c>AuthTokenHttpHandler</c>
    /// for REST calls — without it the connection is unauthenticated and never joins its <c>dealer:{id}</c>
    /// group, so no VehiclePositionUpdated events arrive.
    /// </summary>
    public Task<string?> ProvideAccessTokenAsync() => _tokenStore.GetTokenAsync();

    // The backend VehiclePositionUpdatedPayload field names (RepId/VehicleId/Latitude/Longitude/State)
    // match the client wire DTO exactly, so we bind directly to it; the ViewModel does the merge into the
    // fleet dictionary. The captured-payload deserialization test guards the field-name match (ADR-0011).
    public void OnVehiclePositionUpdated(Func<VehiclePositionUpdatedPayload, Task> handler) =>
        _connection.On(VehiclePositionUpdatedEvent, handler);

    public bool IsConnected => _isConnected();

    // BUG-038: never let an unreachable backend propagate an exception to the caller (the dispatcher
    // dashboard). Try once; if that fails, hand off to a bounded back-off retry loop on its own task so the
    // caller returns immediately and the map renders in its reconnecting state instead of crashing.
    public async Task StartAsync()
    {
        try
        {
            await _connectAsync(_disposeCts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex, "Initial VehiclePositionHub connect failed; starting bounded back-off retry in the background.");
            _ = RetryConnectAsync();
        }
    }

    // Internal (not private) so the bounded back-off sequencing is asserted directly by a unit test that
    // injects a fake connect delegate (throws-then-succeeds) and a no-op delay.
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
                _logger.LogInformation("VehiclePositionHub connection re-established after retry.");
                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "VehiclePositionHub connect retry failed; will retry with longer back-off.");
            }
        }

        _logger.LogWarning("VehiclePositionHub connect retry budget exhausted; remaining disconnected.");
    }

    public Task StopAsync() => _connection.StopAsync();

    public async ValueTask DisposeAsync()
    {
        await _disposeCts.CancelAsync();
        _disposeCts.Dispose();
        await _connection.DisposeAsync();
    }
}
