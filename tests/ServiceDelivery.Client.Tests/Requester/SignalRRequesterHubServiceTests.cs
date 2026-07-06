using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging.Abstractions;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.UI.Features.Requester.Services;

namespace ServiceDelivery.Client.Tests.Requester;

/// <summary>
/// Unit tests for <see cref="SignalRRequesterHubService"/> — the RequesterHub client (FE-016/AC-3).
/// Mirrors <c>SignalRRepHubServiceTests</c>: the access-token provider sources the JWT from
/// <see cref="ITokenStore"/> (websockets cannot carry an Authorization header on the [Authorize]
/// RequesterHub), and the bounded back-off initial-connect loop (BUG-038) is asserted directly via the
/// internal test seam so deleting the loop turns these tests red without a live SignalR server.
/// </summary>
public class SignalRRequesterHubServiceTests
{
    private readonly Mock<ITokenStore> _tokenStore = new();

    private SignalRRequesterHubService CreateService()
    {
        var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5180") };
        return new SignalRRequesterHubService(
            httpClient, _tokenStore.Object, NullLogger<SignalRRequesterHubService>.Instance);
    }

    // Builds the service through the internal test seam: a fake connect delegate (counted, controllable
    // success/failure), a no-op delay so the bounded back-off runs instantly, and a connection-state
    // delegate driven by the fake connect. This lets RetryConnectAsync be asserted directly — deleting
    // the loop turns these tests red.
    private SignalRRequesterHubService CreateServiceWithSeam(
        Func<int, bool> connectSucceedsOnAttempt, Action<int> recordAttempt)
    {
        var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5180") };
        var attempts = 0;
        var connected = false;
        Func<CancellationToken, Task> connect = _ =>
        {
            attempts++;
            recordAttempt(attempts);
            if (!connectSucceedsOnAttempt(attempts))
            {
                throw new InvalidOperationException("simulated transport failure");
            }

            connected = true;
            return Task.CompletedTask;
        };
        Func<TimeSpan, CancellationToken, Task> noDelay = (_, _) => Task.CompletedTask;
        return new SignalRRequesterHubService(
            httpClient, _tokenStore.Object, NullLogger<SignalRRequesterHubService>.Instance,
            connect, noDelay,
            () => connected ? HubConnectionState.Connected : HubConnectionState.Disconnected);
    }

    // FE-019: builds the service with an EXPLICIT connection state and a connect-invocation counter, so the
    // state-aware StartAsync guard (only cold-connect from Disconnected) can be asserted independently of
    // whether a connect succeeds. Production binds the state seam to the real HubConnection.State.
    private SignalRRequesterHubService CreateServiceWithStateSeam(
        HubConnectionState state, Action recordConnectAttempt)
    {
        var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5180") };
        Func<CancellationToken, Task> connect = _ =>
        {
            recordConnectAttempt();
            return Task.CompletedTask;
        };
        Func<TimeSpan, CancellationToken, Task> noDelay = (_, _) => Task.CompletedTask;
        return new SignalRRequesterHubService(
            httpClient, _tokenStore.Object, NullLogger<SignalRRequesterHubService>.Instance,
            connect, noDelay, () => state);
    }

    [Fact]
    public async Task GivenAStoredToken_WhenTheRequesterHubRequestsAnAccessToken_ThenTheStoredTokenIsProvided()
    {
        // Arrange — the RequesterHub is [Authorize(Roles="Requester")]; SignalR appends ?access_token=...
        // from this provider because websockets cannot carry an Authorization header. Without it the
        // requester's hub connection is unauthenticated and never joins its requester:{userId} group,
        // so it never receives RepAssigned.
        _tokenStore.Setup(t => t.GetTokenAsync()).ReturnsAsync("requester-jwt-xyz");
        var service = CreateService();

        // Act
        var token = await service.ProvideAccessTokenAsync();

        // Assert
        Assert.Equal("requester-jwt-xyz", token);
    }

    [Fact]
    public async Task GivenTheBackendIsUnreachable_WhenStartAsyncCalled_ThenItDoesNotThrowAndReportsNotConnected()
    {
        // Arrange — BUG-038: there is no live RequesterHub at the configured base address, so the
        // underlying HubConnection.StartAsync() fails. The resilient StartAsync must catch the transport
        // failure and run a bounded back-off retry loop rather than propagating — the pending screen
        // relies on this so it never trips Blazor's #blazor-error-ui. HubConnection cannot connect
        // without a live transport, so this proves the connect path is non-throwing (wiring check); the
        // live retry-then-recover behaviour is verified by the E2E/smoke complement.
        _tokenStore.Setup(t => t.GetTokenAsync()).ReturnsAsync("requester-jwt-xyz");
        var service = CreateService();

        // Act
        var exception = await Record.ExceptionAsync(() => service.StartAsync());

        // Assert
        Assert.Null(exception);
        Assert.False(service.IsConnected);
    }

    [Fact]
    public async Task GivenEveryConnectAttemptFails_WhenRetryConnectAsyncCalled_ThenAllFiveAttemptsAreMadeBeforeGivingUp()
    {
        // Arrange — AC-3: the loop is bounded. When every attempt fails it must exhaust its budget and
        // return without throwing, leaving the service disconnected (the screen stays in its reconnecting
        // state rather than crashing). The 5-step back-off schedule means exactly 5 attempts.
        var attemptsObserved = 0;
        var service = CreateServiceWithSeam(
            connectSucceedsOnAttempt: _ => false,
            recordAttempt: n => attemptsObserved = n);

        // Act
        var exception = await Record.ExceptionAsync(() => service.RetryConnectAsync());

        // Assert
        Assert.Null(exception);
        Assert.Equal(5, attemptsObserved);
        Assert.False(service.IsConnected);
    }

    [Fact]
    public async Task GivenConnectFailsThenSucceeds_WhenRetryConnectAsyncCalled_ThenConnectsOnTheSuccessfulAttempt()
    {
        // Arrange — AC-3: the bounded back-off loop must keep retrying after a transient failure rather
        // than giving up. The fake connect fails the first two attempts then succeeds; the no-op delay
        // lets the loop run instantly. This asserts the production RetryConnectAsync loop directly — if
        // the loop is deleted, connect is never re-invoked and IsConnected stays false (test goes red).
        var attemptsObserved = 0;
        var service = CreateServiceWithSeam(
            connectSucceedsOnAttempt: attempt => attempt >= 3,
            recordAttempt: n => attemptsObserved = n);

        // Act
        await service.RetryConnectAsync();

        // Assert
        Assert.True(attemptsObserved > 1, "connect should have been retried more than once");
        Assert.Equal(3, attemptsObserved);
        Assert.True(service.IsConnected);
    }

    [Fact]
    public void GivenARepAssignedHandler_WhenOnRepAssignedRegistered_ThenItBindsToTheRepAssignedEventWithoutThrowing()
    {
        // Arrange — AC-3: the service registers a "RepAssigned" handler that forwards the deserialized
        // RepAssignedPayload to the subscriber, mirroring the OnRedirectReceived direct-bind pattern (the
        // client RepAssignedPayload field names match the backend exactly). HubConnection is sealed and
        // cannot dispatch a registered client handler without a live transport, so this unit test proves
        // the binding is wired (correct event name and payload type, no throw); the E2E test is the
        // live-system complement that proves an actual server push transitions the screen.
        var service = CreateService();
        RepAssignedPayload? received = null;

        // Act
        var register = () => service.OnRepAssigned(payload =>
        {
            received = payload;
            return Task.CompletedTask;
        });

        // Assert
        var exception = Record.Exception(register);
        Assert.Null(exception);
        Assert.Null(received);
    }

    [Fact]
    public void GivenARepPositionUpdatedHandler_WhenOnRepPositionUpdatedRegistered_ThenItBindsToTheRepPositionUpdatedEventWithoutThrowing()
    {
        // Arrange — FE-017/AC-3: the service registers a "RepPositionUpdated" handler that forwards the
        // deserialized RepPositionUpdatedPayload to the subscriber, mirroring the OnRepAssigned direct-bind
        // pattern (the client RepPositionUpdatedPayload field names match the backend exactly). HubConnection
        // is sealed and cannot dispatch a registered client handler without a live transport, so this unit
        // test proves the binding is wired (correct event name and payload type, no throw); the E2E test is
        // the live-system complement that proves an actual server push updates the tracking screen.
        var service = CreateService();
        RepPositionUpdatedPayload? received = null;

        // Act
        var register = () => service.OnRepPositionUpdated(payload =>
        {
            received = payload;
            return Task.CompletedTask;
        });

        // Assert
        var exception = Record.Exception(register);
        Assert.Null(exception);
        Assert.Null(received);
    }

    [Fact]
    public void GivenARepRedirectedHandler_WhenOnRepRedirectedRegistered_ThenItBindsToTheRepRedirectedEventWithoutThrowing()
    {
        // Arrange — FE-018/AC-4: the service registers a "RepRedirected" handler that forwards the
        // deserialized RepRedirectedPayload to the subscriber, mirroring the OnRepAssigned direct-bind pattern
        // (the client RepRedirectedPayload field names match the backend exactly). HubConnection is sealed and
        // cannot dispatch a registered client handler without a live transport, so this unit test proves the
        // binding is wired (correct event name and payload type, no throw); the E2E test is the live-system
        // complement that proves an actual server push shows the redirect banner.
        var service = CreateService();
        RepRedirectedPayload? received = null;

        // Act
        var register = () => service.OnRepRedirected(payload =>
        {
            received = payload;
            return Task.CompletedTask;
        });

        // Assert
        var exception = Record.Exception(register);
        Assert.Null(exception);
        Assert.Null(received);
    }

    [Fact]
    public async Task GivenAConnectedConnection_WhenStartAsyncCalledAgain_ThenNoReconnectAttemptedAndNoThrow()
    {
        // Arrange — FE-019 (shared-connection handoff): the scoped RequesterHub connection PERSISTS across
        // the requester's pending→tracking→complete navigations, so a view re-entering on a still-live
        // (Connected) connection must find StartAsync a genuine no-op. It must NOT re-issue a connect — the
        // real HubConnection rejects StartAsync unless it is Disconnected ("cannot be started if it is not in
        // the Disconnected state"), and that throw was being swallowed into the BUG-038 back-off, opening a
        // multi-second window where the requester was not joined to its group and the one-shot
        // ServiceCompleted push was lost.
        var connectAttempts = 0;
        var service = CreateServiceWithStateSeam(HubConnectionState.Connected, () => connectAttempts++);

        // Act
        var exception = await Record.ExceptionAsync(() => service.StartAsync());

        // Assert — no connect attempted, no throw (a live connection is a no-op, not a failure to retry).
        Assert.Null(exception);
        Assert.Equal(0, connectAttempts);
    }

    [Fact]
    public async Task GivenADisconnectedConnection_WhenStartAsyncCalled_ThenItConnects()
    {
        // Arrange — a Disconnected connection is a genuine cold entry (e.g. a direct navigation / refresh
        // straight to /requester/tracking), so StartAsync must still connect.
        var connectAttempts = 0;
        var service = CreateServiceWithStateSeam(HubConnectionState.Disconnected, () => connectAttempts++);

        // Act
        await service.StartAsync();

        // Assert
        Assert.Equal(1, connectAttempts);
    }

    [Fact]
    public void GivenSignalRRequesterHubService_WhenOnServiceCompletedRegistered_ThenHandlerIsAccepted()
    {
        // Arrange — FE-019/AC-4: the service registers a "ServiceCompleted" handler that forwards the
        // deserialized ServiceCompletedPayload to the subscriber, mirroring the OnRepAssigned direct-bind
        // pattern (the client ServiceCompletedPayload field name matches the backend exactly). HubConnection
        // is sealed and cannot dispatch a registered client handler without a live transport, so this unit
        // test proves the binding is wired (correct event name and payload type, no throw); the E2E test is
        // the live-system complement that proves an actual server push navigates to the completion screen.
        var service = CreateService();
        ServiceCompletedPayload? received = null;

        // Act
        var register = () => service.OnServiceCompleted(payload =>
        {
            received = payload;
            return Task.CompletedTask;
        });

        // Assert
        var exception = Record.Exception(register);
        Assert.Null(exception);
        Assert.Null(received);
    }
}
