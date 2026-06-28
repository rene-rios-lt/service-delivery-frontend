using Microsoft.Extensions.Logging;
using ServiceDelivery.Client.Core.Interfaces;

namespace ServiceDelivery.Client.UI.Features.ServiceRep.Services;

/// <summary>
/// On-duty heartbeat loop for the ServiceRep persona (FE-023). Owns both the fixed-interval timer
/// and the <c>POST /rep/heartbeat</c> call (no body — the rep is identified by the JWT
/// <c>AuthTokenHttpHandler</c> attaches). The loop runs while a vehicle is claimed
/// (<see cref="IClaimedVehicleStore.CurrentVehicle"/> is non-null) and self-terminates within one
/// interval once the store is cleared (the release path — observe-the-store), so
/// <c>ReleaseVehicleAction</c> needs no change (Open/Closed). Explicit logout stops it immediately
/// via <c>ServiceRepLogoutSideEffect</c>.
///
/// The interval delay is hidden behind an injectable <see cref="Func{T1, T2, TResult}"/> seam —
/// identical to <c>SignalRRepHubService</c>'s <c>_delayAsync</c> — so the loop is unit-testable
/// without waiting wall-clock time. The public constructor binds it to <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.
/// </summary>
public sealed class HttpHeartbeatService : IHeartbeatService, IAsyncDisposable
{
    private const string HeartbeatPath = "rep/heartbeat";
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);

    private readonly HttpClient _httpClient;
    private readonly IClaimedVehicleStore _store;
    private readonly ILogger<HttpHeartbeatService> _logger;

    // Test seam (mirrors SignalRRepHubService._delayAsync): the interval delay is injectable so the
    // loop can be ticked deterministically in tests without real time. Production binds Task.Delay.
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;

    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    public HttpHeartbeatService(
        HttpClient httpClient,
        IClaimedVehicleStore store,
        ILogger<HttpHeartbeatService> logger)
        : this(httpClient, store, logger, Task.Delay)
    {
    }

    internal HttpHeartbeatService(
        HttpClient httpClient,
        IClaimedVehicleStore store,
        ILogger<HttpHeartbeatService> logger,
        Func<TimeSpan, CancellationToken, Task> delayAsync)
    {
        _httpClient = httpClient;
        _store = store;
        _logger = logger;
        _delayAsync = delayAsync;
    }

    public bool IsRunning { get; private set; }

    // Idempotent: a second StartAsync on an already-running loop is a no-op. The rep re-enters
    // /rep/idle after completing a job, which calls StartAsync again — it must not spawn a second loop.
    public Task StartAsync()
    {
        if (IsRunning)
        {
            return Task.CompletedTask;
        }

        _cts = new CancellationTokenSource();
        IsRunning = true;
        _loopTask = RunLoopAsync(_cts.Token);
        return Task.CompletedTask;
    }

    // Idempotent: stopping a loop that never started (or already stopped) completes without throwing.
    public async Task StopAsync()
    {
        if (_cts is null)
        {
            return;
        }

        await _cts.CancelAsync();
        if (_loopTask is not null)
        {
            try
            {
                await _loopTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when the in-flight delay is cancelled by StopAsync.
            }
        }

        _cts.Dispose();
        _cts = null;
        _loopTask = null;
        IsRunning = false;
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _store.CurrentVehicle is not null)
            {
                await _delayAsync(HeartbeatInterval, cancellationToken);
                if (cancellationToken.IsCancellationRequested || _store.CurrentVehicle is null)
                {
                    break;
                }

                await PostHeartbeatAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Cancellation (StopAsync) is a clean exit, not an error.
        }
        finally
        {
            IsRunning = false;
        }
    }

    // A 404 (rep has no state record) or any other non-success / transport error is swallowed and
    // logged — the loop must keep ticking. The backend stale-heartbeat sweeper (BE-028) is the safety
    // net for a genuinely-gone rep; a single failed POST must never crash the on-duty loop.
    private async Task PostHeartbeatAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.PostAsync(HeartbeatPath, content: null, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Heartbeat POST returned {StatusCode}; continuing the loop.", response.StatusCode);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Heartbeat POST failed; continuing the loop.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}
