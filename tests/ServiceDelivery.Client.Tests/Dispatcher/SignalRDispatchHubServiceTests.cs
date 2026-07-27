using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.UI.Features.Dispatcher.Services;

namespace ServiceDelivery.Client.Tests.Dispatcher;

/// <summary>
/// Deterministic tests for <see cref="SignalRDispatchHubService"/> (FE-004). The real SignalR transport needs
/// a live hub and is left to the E2E suites, but the bug-prone contract seams are exercised here without a
/// server: the resolved hub URL is pinned to the backend <c>/hubs/dispatch</c> contract path (a wrong path
/// would otherwise ship green — the FE-003 cycle-1 defect), the JWT comes from <see cref="ITokenStore"/>, a
/// fresh connection reports <c>IsConnected</c> false, HubConnection logging is routed to the injected factory,
/// and — via the internal connect/delay seam — an unreachable backend at mount never throws into the
/// dispatcher dashboard (BUG-038 bounded back-off). Mirrors <c>SignalRVehiclePositionHubService</c>.
/// </summary>
public class SignalRDispatchHubServiceTests
{
    private readonly Mock<ITokenStore> _tokenStore = new();

    private SignalRDispatchHubService CreateService() =>
        new(new HttpClient { BaseAddress = new Uri("http://localhost:5180") },
            _tokenStore.Object,
            NullLogger<SignalRDispatchHubService>.Instance,
            NullLoggerFactory.Instance);

    private SignalRDispatchHubService CreateServiceWithSeam(
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
        return new SignalRDispatchHubService(
            new HttpClient { BaseAddress = new Uri("http://localhost:5180") },
            _tokenStore.Object,
            NullLogger<SignalRDispatchHubService>.Instance,
            NullLoggerFactory.Instance,
            connect, noDelay, () => connected);
    }

    [Fact]
    public void GivenServiceBuiltAgainstTheBackendBaseAddress_WhenTheHubUrlIsResolved_ThenItTargetsTheBackendDispatchHubContractPath()
    {
        // Arrange — the backend maps DispatchHub at "/hubs/dispatch" (api-design.md). The expected path is
        // HARD-CODED to the backend contract literal — it does NOT reference the production const — so a drift
        // in the const fails here (anti-masking rule: a guard that mirrors the code's own constant proves
        // nothing).
        var service = CreateService();

        // Act
        var resolved = service.HubUri;

        // Assert
        Assert.Equal("/hubs/dispatch", resolved.AbsolutePath);
        Assert.Equal("http://localhost:5180/hubs/dispatch", resolved.ToString());
    }

    [Fact]
    public void GivenAnInjectedLoggerFactory_WhenTheServiceIsConstructed_ThenTheHubConnectionLoggingIsRoutedToTheFactory()
    {
        // Arrange — a spy factory that is never asked for a logger proves the routing is absent (NullLoggerFactory
        // would pass either way, so a real spy is used).
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(NullLogger.Instance);

        // Act
        _ = new SignalRDispatchHubService(
            new HttpClient { BaseAddress = new Uri("http://localhost:5180") },
            _tokenStore.Object,
            NullLogger<SignalRDispatchHubService>.Instance,
            loggerFactory.Object);

        // Assert
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
