using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.Services;
using ServiceDelivery.Client.UI.Features.ServiceRep.Services;

namespace ServiceDelivery.Client.Tests.ServiceRep;

public class HttpHeartbeatServiceTests
{
    private readonly Mock<IClaimedVehicleStore> _store = new();

    private static ClaimedVehicle Vehicle() =>
        new(Guid.NewGuid(), "IA-4471", "Transit 350", new[] { "Hydraulics" });

    // Records every request that reaches the transport and lets the test control the status returned.
    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;

        public List<HttpRequestMessage> Requests { get; } = new();

        public RecordingHandler(HttpStatusCode status = HttpStatusCode.OK)
        {
            _status = status;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(_status));
        }
    }

    // Controllable delay seam (mirrors SignalRRepHubService's _delayAsync test seam): each loop
    // continuation awaits a TaskCompletionSource the test completes by calling ReleaseTick(), so a
    // tick fires only when the test decides — no real wall-clock waiting.
    private sealed class TickGate
    {
        private TaskCompletionSource<bool> _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int DelayInvocations { get; private set; }

        public Task DelayAsync(TimeSpan _, CancellationToken cancellationToken)
        {
            DelayInvocations++;
            cancellationToken.Register(() => _gate.TrySetCanceled(cancellationToken));
            return _gate.Task;
        }

        // Release the current delay (fire one tick) and arm the gate for the next continuation.
        public void ReleaseTick()
        {
            var current = _gate;
            _gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            current.TrySetResult(true);
        }
    }

    private HttpHeartbeatService CreateService(
        RecordingHandler handler, TickGate gate)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5180") };
        return new HttpHeartbeatService(
            httpClient, _store.Object, NullLogger<HttpHeartbeatService>.Instance, gate.DelayAsync);
    }

    // Spins until a predicate is satisfied or a short budget elapses, so the test observes the
    // background loop's progress without sleeping a fixed amount. No real heartbeat interval elapses —
    // only the in-memory loop scheduling.
    private static async Task WaitUntil(Func<bool> predicate, int timeoutMs = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!predicate() && DateTime.UtcNow < deadline)
        {
            await Task.Yield();
            await Task.Delay(1);
        }
    }

    [Fact]
    public async Task GivenAVehicleIsClaimed_WhenHeartbeatServiceStarted_ThenHeartbeatIsPostedOnEachTick()
    {
        // Arrange
        _store.SetupGet(s => s.CurrentVehicle).Returns(Vehicle());
        var handler = new RecordingHandler();
        var gate = new TickGate();
        await using var service = CreateService(handler, gate);

        // Act
        await service.StartAsync();
        await WaitUntil(() => gate.DelayInvocations >= 1);
        gate.ReleaseTick();
        await WaitUntil(() => handler.Requests.Count >= 1);

        // Assert
        Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.EndsWith("rep/heartbeat", handler.Requests[0].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GivenARunningHeartbeat_WhenDelayCompletesThreeTimes_ThenThreePostsAreSent()
    {
        // Arrange
        _store.SetupGet(s => s.CurrentVehicle).Returns(Vehicle());
        var handler = new RecordingHandler();
        var gate = new TickGate();
        await using var service = CreateService(handler, gate);
        await service.StartAsync();

        // Act — fire three ticks; each released delay drives exactly one POST.
        for (var i = 1; i <= 3; i++)
        {
            await WaitUntil(() => gate.DelayInvocations >= i);
            gate.ReleaseTick();
            await WaitUntil(() => handler.Requests.Count >= i);
        }

        // Assert
        Assert.Equal(3, handler.Requests.Count);
        Assert.All(handler.Requests, r =>
        {
            Assert.Equal(HttpMethod.Post, r.Method);
            Assert.EndsWith("rep/heartbeat", r.RequestUri!.AbsolutePath);
        });
    }

    [Fact]
    public async Task GivenARunningHeartbeat_WhenStopCalled_ThenNoFurtherHeartbeatsAreSent()
    {
        // Arrange — one tick fires, then the loop is stopped; releasing the (now-cancelled) gate again
        // must not produce another POST. This is the frontend half of AC-2: the loop respects
        // cancellation so the rep stops being marked human-controlled the moment it stops.
        _store.SetupGet(s => s.CurrentVehicle).Returns(Vehicle());
        var handler = new RecordingHandler();
        var gate = new TickGate();
        await using var service = CreateService(handler, gate);
        await service.StartAsync();
        await WaitUntil(() => gate.DelayInvocations >= 1);
        gate.ReleaseTick();
        await WaitUntil(() => handler.Requests.Count >= 1);
        var countAfterFirstTick = handler.Requests.Count;

        // Act
        await service.StopAsync();
        gate.ReleaseTick();
        await Task.Delay(20);

        // Assert
        Assert.Equal(1, countAfterFirstTick);
        Assert.Equal(countAfterFirstTick, handler.Requests.Count);
        Assert.False(service.IsRunning);
    }

    [Fact]
    public async Task GivenARunningHeartbeat_WhenCurrentVehicleClearedFromStore_ThenLoopExits()
    {
        // Arrange — release path (AC-2 / AC-3 observe-the-store). ReleaseVehicleAction clears the store
        // on a successful release; the loop checks CurrentVehicle each cycle and must self-terminate
        // within one interval when it becomes null, without ReleaseVehicleAction knowing about the
        // heartbeat (Open/Closed). A real in-memory store lets the test flip the claim mid-loop.
        var store = new InMemoryClaimedVehicleStore();
        store.SetVehicle(Vehicle());
        var handler = new RecordingHandler();
        var gate = new TickGate();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5180") };
        await using var service = new HttpHeartbeatService(
            httpClient, store, NullLogger<HttpHeartbeatService>.Instance, gate.DelayAsync);
        await service.StartAsync();
        await WaitUntil(() => gate.DelayInvocations >= 1);

        // Act — the vehicle is released, then the in-flight tick completes; the post-delay continuation
        // check sees a null vehicle and the loop exits without posting.
        store.ClearVehicle();
        gate.ReleaseTick();
        await WaitUntil(() => !service.IsRunning);

        // Assert
        Assert.False(service.IsRunning);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GivenHeartbeatEndpointReturns404_WhenTickFires_ThenLoopContinuesWithoutThrowing()
    {
        // Arrange — a 404 (no rep state record) must be swallowed and logged, never crash the loop:
        // the next tick must still fire. The backend sweeper (BE-028) is the real safety net.
        _store.SetupGet(s => s.CurrentVehicle).Returns(Vehicle());
        var handler = new RecordingHandler(HttpStatusCode.NotFound);
        var gate = new TickGate();
        await using var service = CreateService(handler, gate);
        await service.StartAsync();

        // Act — fire two ticks; the first returns 404, the loop must keep running and fire the second.
        await WaitUntil(() => gate.DelayInvocations >= 1);
        gate.ReleaseTick();
        await WaitUntil(() => handler.Requests.Count >= 1);
        await WaitUntil(() => gate.DelayInvocations >= 2);
        gate.ReleaseTick();
        await WaitUntil(() => handler.Requests.Count >= 2);

        // Assert
        Assert.Equal(2, handler.Requests.Count);
        Assert.True(service.IsRunning);
    }

    [Fact]
    public async Task GivenHeartbeatServiceNeverStarted_WhenStopAsyncCalled_ThenItCompletesWithoutThrowing()
    {
        // Arrange — idempotence: StopAsync is honoured even before StartAsync (e.g. a logout side-effect
        // runs when the rep never went on duty). It must complete without throwing and stay not-running.
        _store.SetupGet(s => s.CurrentVehicle).Returns(Vehicle());
        var handler = new RecordingHandler();
        var gate = new TickGate();
        await using var service = CreateService(handler, gate);

        // Act
        var exception = await Record.ExceptionAsync(() => service.StopAsync());

        // Assert
        Assert.Null(exception);
        Assert.False(service.IsRunning);
    }

    [Fact]
    public async Task GivenHeartbeatAlreadyRunning_WhenStartAsyncCalledAgain_ThenOnlyOneLoopIsActive()
    {
        // Arrange — idempotence: re-entering /rep/idle after completing a job calls StartAsync again on
        // an already-running loop. A second call must NOT spawn a second loop (which would double every
        // heartbeat). Proof: after two StartAsync calls, one released tick produces exactly one POST.
        _store.SetupGet(s => s.CurrentVehicle).Returns(Vehicle());
        var handler = new RecordingHandler();
        var gate = new TickGate();
        await using var service = CreateService(handler, gate);

        // Act
        await service.StartAsync();
        await WaitUntil(() => gate.DelayInvocations >= 1);
        await service.StartAsync();
        gate.ReleaseTick();
        await WaitUntil(() => handler.Requests.Count >= 1);
        await Task.Delay(20);

        // Assert
        Assert.True(service.IsRunning);
        Assert.Single(handler.Requests);
    }
}
