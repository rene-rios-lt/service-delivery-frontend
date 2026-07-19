using ServiceDelivery.Client.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ServiceDelivery.Client.UI.Features.Dispatcher.Services;

namespace ServiceDelivery.Client.Tests.Dispatcher;

/// <summary>
/// Deterministic tests for <see cref="SignalRVehiclePositionHubService"/> (FE-003). The real SignalR
/// transport (negotiate / websocket) needs a live hub and is left to the E2E suites, but the seams that
/// carry the bug-prone contract are exercised here without a server: the JWT the hub appends as
/// <c>?access_token=</c> comes from <see cref="ITokenStore"/> (the SignalR analogue of the REST auth
/// handler — a wrong wire silently leaves the connection unauthenticated), a freshly-built connection
/// reports <see cref="IVehiclePositionHubService.IsConnected"/> false, and — via the internal connect/delay
/// seam — an unreachable backend at mount never throws into the dispatcher dashboard (BUG-038 bounded
/// back-off), matching the sibling <c>SignalRRepHubService</c> / <c>SignalRRequesterHubService</c>.
/// </summary>
public class SignalRVehiclePositionHubServiceTests
{
    private readonly Mock<ITokenStore> _tokenStore = new();

    private SignalRVehiclePositionHubService CreateService() =>
        new(new HttpClient { BaseAddress = new Uri("http://localhost:5180") },
            _tokenStore.Object,
            NullLogger<SignalRVehiclePositionHubService>.Instance,
            NullLoggerFactory.Instance);

    // Builds the service through the internal seam: a counted fake connect (controllable success/failure),
    // a no-op delay so the bounded back-off runs instantly, and a connection-state delegate driven by it.
    private SignalRVehiclePositionHubService CreateServiceWithSeam(
        Func<int, bool> connectSucceedsOnAttempt, Action<int>? recordAttempt = null)
    {
        var attempts = 0;
        var connected = false;
        Func<CancellationToken, Task> connect = _ =>
        {
            attempts++;
            recordAttempt?.Invoke(attempts);
            if (!connectSucceedsOnAttempt(attempts))
            {
                throw new InvalidOperationException("simulated transport failure");
            }

            connected = true;
            return Task.CompletedTask;
        };
        Func<TimeSpan, CancellationToken, Task> noDelay = (_, _) => Task.CompletedTask;
        return new SignalRVehiclePositionHubService(
            new HttpClient { BaseAddress = new Uri("http://localhost:5180") },
            _tokenStore.Object,
            NullLogger<SignalRVehiclePositionHubService>.Instance,
            NullLoggerFactory.Instance,
            connect, noDelay, () => connected);
    }

    [Fact]
    public void GivenServiceBuiltAgainstTheBackendBaseAddress_WhenTheHubUrlIsResolved_ThenItTargetsTheBackendVehiclePositionHubContractPath()
    {
        // Arrange — the backend maps VehiclePositionHub at "/hubs/position" (Api/Program.cs:150). Every OTHER
        // test in this file seams the transport away (the injected connect delegate never sees the URL), so a
        // wrong hub path stays green there — which is exactly how the FE-003 cycle-1 defect
        // ("hubs/vehicleposition") shipped behind a green suite. This guard pins the REAL resolved URL the
        // service hands to the connection builder. The expected path is HARD-CODED to the backend contract
        // literal — it does NOT reference the production const — so a drift in the const fails here
        // (anti-masking rule, test-quality skill: a guard that mirrors the code's own constant proves nothing).
        var service = new SignalRVehiclePositionHubService(
            new HttpClient { BaseAddress = new Uri("http://localhost:5180") },
            _tokenStore.Object,
            NullLogger<SignalRVehiclePositionHubService>.Instance,
            NullLoggerFactory.Instance);

        // Act
        var resolved = service.HubUri;

        // Assert
        Assert.Equal("/hubs/position", resolved.AbsolutePath);
        Assert.Equal("http://localhost:5180/hubs/position", resolved.ToString());
    }

    [Fact]
    public void GivenAnInjectedLoggerFactory_WhenTheServiceIsConstructed_ThenTheHubConnectionLoggingIsRoutedToTheFactory()
    {
        // Arrange — FE-003 cycle 9: a client-side SignalR transport failure was invisible TWICE in this story
        // because the HubConnection was built with no logger routing (its internal transport/dispatch logs
        // went nowhere). The service must now route that logging into the host's ILoggerFactory. The
        // HubConnection resolves ILoggerFactory when the builder constructs it, so a factory routed via
        // ConfigureLogging is asked for at least one logger during construction; a spy factory that is NEVER
        // asked proves the routing is absent. NullLoggerFactory would silently pass either way, so a real spy
        // is used (anti-masking: the assertion must fail when the routing is not wired).
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(NullLogger.Instance);

        // Act — constructing the service builds the HubConnection (which pulls a logger from the routed factory).
        _ = new SignalRVehiclePositionHubService(
            new HttpClient { BaseAddress = new Uri("http://localhost:5180") },
            _tokenStore.Object,
            NullLogger<SignalRVehiclePositionHubService>.Instance,
            loggerFactory.Object);

        // Assert — the connection's logging pipeline was fed the injected factory (it created at least one
        // logger from it); with no routing the spy is never touched.
        loggerFactory.Verify(f => f.CreateLogger(It.IsAny<string>()), Times.AtLeastOnce);
    }

    [Fact]
    public void GivenAFreshlyBuiltConnection_WhenIsConnectedChecked_ThenReturnsFalse()
    {
        // Arrange
        var service = CreateService();

        // Act
        var connected = service.IsConnected;

        // Assert
        Assert.False(connected);
    }

    [Fact]
    public async Task GivenATokenInStore_WhenAccessTokenProvided_ThenReturnsStoredToken()
    {
        // Arrange
        _tokenStore.Setup(t => t.GetTokenAsync()).ReturnsAsync("jwt-abc-123");
        var service = CreateService();

        // Act
        var token = await service.ProvideAccessTokenAsync();

        // Assert
        Assert.Equal("jwt-abc-123", token);
    }

    [Fact]
    public async Task GivenBackendUnreachable_WhenStartAsync_ThenDoesNotThrow()
    {
        // Arrange — the dispatcher dashboard calls StartHubAsync on mount; an unreachable backend must not
        // crash it (BUG-038). Connect always fails.
        var service = CreateServiceWithSeam(connectSucceedsOnAttempt: _ => false);

        // Act
        var start = async () => await service.StartAsync();

        // Assert
        var ex = await Record.ExceptionAsync(start);
        Assert.Null(ex);
    }

    [Fact]
    public async Task GivenInitialConnectFailsThenSucceeds_WhenRetryConnectAsync_ThenReconnects()
    {
        // Arrange — connect throws on the first attempt, succeeds on the second; the bounded back-off loop
        // (with a no-op delay) must retry and end connected.
        var attempts = 0;
        var service = CreateServiceWithSeam(
            connectSucceedsOnAttempt: attempt => attempt >= 2,
            recordAttempt: n => attempts = n);

        // Act
        await service.RetryConnectAsync();

        // Assert
        Assert.True(attempts >= 2);
        Assert.True(service.IsConnected);
    }
}
