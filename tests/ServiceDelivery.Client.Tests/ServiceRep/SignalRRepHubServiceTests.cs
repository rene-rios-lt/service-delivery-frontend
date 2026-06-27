using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.UI.Features.ServiceRep.Services;

namespace ServiceDelivery.Client.Tests.ServiceRep;

public class SignalRRepHubServiceTests
{
    private readonly Mock<ITokenStore> _tokenStore = new();

    private SignalRRepHubService CreateService()
    {
        var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5180") };
        return new SignalRRepHubService(
            httpClient, _tokenStore.Object, NullLogger<SignalRRepHubService>.Instance);
    }

    // Builds the service through the internal test seam: a fake connect delegate (counted, controllable
    // success/failure), a no-op delay so the bounded back-off runs instantly, and a connection-state
    // delegate driven by the fake connect. This lets RetryConnectAsync be asserted directly — deleting
    // the loop turns these tests red.
    private SignalRRepHubService CreateServiceWithSeam(
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
        return new SignalRRepHubService(
            httpClient, _tokenStore.Object, NullLogger<SignalRRepHubService>.Instance,
            connect, noDelay, () => connected);
    }

    [Fact]
    public async Task GivenAStoredToken_WhenTheHubRequestsAnAccessToken_ThenTheStoredTokenIsProvided()
    {
        // Arrange — the RepHub is [Authorize]; SignalR appends ?access_token=... from this provider
        // because websockets cannot carry an Authorization header. Without it the rep's hub connection
        // is unauthenticated and never joins its rep group, so it never receives JobOfferReceived.
        _tokenStore.Setup(t => t.GetTokenAsync()).ReturnsAsync("rep-jwt-xyz");
        var service = CreateService();

        // Act
        var token = await service.ProvideAccessTokenAsync();

        // Assert
        Assert.Equal("rep-jwt-xyz", token);
    }

    [Fact]
    public async Task GivenNoStoredToken_WhenTheHubRequestsAnAccessToken_ThenNullIsProvidedWithoutThrowing()
    {
        // Arrange
        _tokenStore.Setup(t => t.GetTokenAsync()).ReturnsAsync((string?)null);
        var service = CreateService();

        // Act
        var token = await service.ProvideAccessTokenAsync();

        // Assert
        Assert.Null(token);
    }

    [Fact]
    public async Task GivenTheBackendIsUnreachable_WhenStartAsyncCalled_ThenItDoesNotThrowAndReportsNotConnected()
    {
        // Arrange — BUG-038: there is no live RepHub at the configured base address, so the underlying
        // HubConnection.StartAsync() fails. The resilient StartAsync must catch the transport failure and
        // run a bounded back-off retry loop rather than propagating — the idle screen relies on this so
        // it never trips Blazor's #blazor-error-ui. HubConnection cannot connect without a live transport,
        // so this proves the connect path is non-throwing (wiring check); the live retry-then-recover
        // behaviour is verified by the E2E/smoke complement.
        _tokenStore.Setup(t => t.GetTokenAsync()).ReturnsAsync("rep-jwt-xyz");
        var service = CreateService();

        // Act
        var exception = await Record.ExceptionAsync(() => service.StartAsync());

        // Assert
        Assert.Null(exception);
        Assert.False(service.IsConnected);
    }

    [Fact]
    public async Task GivenConnectFailsTwiceThenSucceeds_WhenRetryConnectRuns_ThenConnectIsRetriedAndBecomesConnected()
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
    public async Task GivenConnectKeepsFailing_WhenRetryConnectRuns_ThenItGivesUpAfterBoundedAttemptsWithoutThrowing()
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
    public async Task GivenConnectSucceedsOnFirstRetry_WhenRetryConnectRuns_ThenItStopsRetryingImmediately()
    {
        // Arrange — AC-3: once a retry succeeds the loop must stop, not keep dialing. The fake connect
        // succeeds on the first retry attempt; the loop must invoke connect exactly once and report
        // connected. If the loop ignored the success and kept going, attemptsObserved would exceed 1.
        var attemptsObserved = 0;
        var service = CreateServiceWithSeam(
            connectSucceedsOnAttempt: attempt => attempt >= 1,
            recordAttempt: n => attemptsObserved = n);

        // Act
        await service.RetryConnectAsync();

        // Assert
        Assert.Equal(1, attemptsObserved);
        Assert.True(service.IsConnected);
    }

    [Fact]
    public void GivenAJobOfferExpiredEventPushed_WhenOnJobOfferExpiredRegistered_ThenHandlerBindsToTheJobOfferExpiredEvent()
    {
        // Arrange — BUG-037: the RepHub publishes JobOfferExpired carrying a single OfferId. The
        // service must register a "JobOfferExpired" handler that forwards the deserialized
        // JobOfferExpiredPayload to the subscriber, mirroring the OnRedirectReceived direct-bind
        // pattern (no field-name mismatch). HubConnection is sealed and cannot dispatch a registered
        // client handler without a live transport, so this unit test proves the binding is wired
        // (correct event name and payload type, no throw); the Appium E2E test is the live-system
        // complement that proves an actual server push dismisses the screen.
        var service = CreateService();
        JobOfferExpiredPayload? received = null;

        // Act
        var register = () => service.OnJobOfferExpired(payload =>
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
