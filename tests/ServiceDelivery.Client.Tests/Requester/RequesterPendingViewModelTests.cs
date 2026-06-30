using Microsoft.Extensions.Logging.Abstractions;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.ViewModels;

namespace ServiceDelivery.Client.Tests.Requester;

/// <summary>
/// Unit tests for <see cref="RequesterPendingViewModel"/> (FE-016). The pending view orchestrates the
/// RequesterHub connection (start/stop with the BUG-038 swallow-and-log pattern), registers the
/// RepAssigned handler once in the constructor (BUG-042), navigates to the tracking view on assignment
/// (AC-3), surfaces the hub connection state (BUG-038 reconnecting indicator), and sources the
/// authenticated requester's REAL service tier from <see cref="IAuthService.GetCurrentUserAsync"/>
/// (never a hardcoded GOLD — the BUG-034 masking guard).
/// </summary>
public class RequesterPendingViewModelTests
{
    private readonly Mock<IRequesterHubService> _hub = new();
    private readonly Mock<IPersonaNavigator> _navigator = new();
    private readonly Mock<IAuthService> _authService = new();

    private RequesterPendingViewModel CreateViewModel()
    {
        _authService.Setup(a => a.GetCurrentUserAsync())
            .ReturnsAsync(new UserProfile(Guid.NewGuid(), "Marcus Wright", UserRole.Requester, ServiceTier.Gold, Guid.NewGuid()));
        return new RequesterPendingViewModel(
            _hub.Object, _navigator.Object, _authService.Object,
            NullLogger<RequesterPendingViewModel>.Instance);
    }

    [Fact]
    public async Task GivenRequesterPendingViewModel_WhenStartAsyncCalled_ThenRequesterHubStartAsyncIsInvoked()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        await viewModel.StartAsync();

        // Assert
        _hub.Verify(h => h.StartAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenARepAssignedEvent_WhenHandledByViewModel_ThenNavigatesToRequesterTracking()
    {
        // Arrange — BUG-042: the RepAssigned handler is registered once in the constructor. Capture it,
        // then invoke it as the hub would on an actual server push (AC-3: push-driven transition).
        Func<RepAssignedPayload, Task>? capturedHandler = null;
        _hub.Setup(h => h.OnRepAssigned(It.IsAny<Func<RepAssignedPayload, Task>>()))
            .Callback<Func<RepAssignedPayload, Task>>(h => capturedHandler = h);
        CreateViewModel();
        var payload = new RepAssignedPayload(
            Guid.NewGuid(), "Marcus Wright", 7.5, 41.601, -93.609);

        // Act
        await capturedHandler!.Invoke(payload);

        // Assert
        _navigator.Verify(n => n.NavigateToRequesterTracking(), Times.Once);
    }

    [Fact]
    public async Task GivenHubThrowsOnStart_WhenViewModelStartAsyncCalled_ThenExceptionIsSwallowedAndLogged()
    {
        // Arrange — BUG-038: the hub's StartAsync retries internally, but if it still throws (backend
        // unreachable for the whole retry budget) the ViewModel must swallow-and-log so the pending
        // screen never raises an unhandled-error banner. The reconnecting state is surfaced via
        // IsHubConnected.
        _hub.Setup(h => h.StartAsync()).ThrowsAsync(new InvalidOperationException("hub unreachable"));
        var viewModel = CreateViewModel();

        // Act
        var exception = await Record.ExceptionAsync(() => viewModel.StartAsync());

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task GivenRequesterPendingViewModel_WhenStopAsyncCalled_ThenRequesterHubStopAsyncIsInvoked()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        await viewModel.StopAsync();

        // Assert
        _hub.Verify(h => h.StopAsync(), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GivenTheHubConnectionState_WhenIsHubConnectedRead_ThenItReflectsTheHubState(bool connected)
    {
        // Arrange — BUG-038: the pending screen shows a "Reconnecting…" indicator while the hub is not
        // connected, so the VM surfaces the hub's connection state without coupling to SignalR types.
        _hub.SetupGet(h => h.IsConnected).Returns(connected);
        var viewModel = CreateViewModel();

        // Act
        var isConnected = viewModel.IsHubConnected;

        // Assert
        Assert.Equal(connected, isConnected);
    }

    [Theory]
    [InlineData(ServiceTier.Gold)]
    [InlineData(ServiceTier.Silver)]
    [InlineData(ServiceTier.Bronze)]
    public async Task GivenAnAuthenticatedRequester_WhenStartAsyncCalled_ThenTierReflectsTheUsersRealTier(ServiceTier tier)
    {
        // Arrange — BUG-034 masking guard: the tier badge must reflect the authenticated requester's REAL
        // tier from UserProfile.Tier (GET /users/me), never a hardcoded GOLD. A non-Gold profile must
        // surface its own tier — a test that would pass with a hardcoded GOLD is not acceptable.
        _hub.Reset();
        _navigator.Reset();
        _authService.Setup(a => a.GetCurrentUserAsync())
            .ReturnsAsync(new UserProfile(Guid.NewGuid(), "Marcus Wright", UserRole.Requester, tier, Guid.NewGuid()));
        var viewModel = new RequesterPendingViewModel(
            _hub.Object, _navigator.Object, _authService.Object,
            NullLogger<RequesterPendingViewModel>.Instance);

        // Act
        await viewModel.StartAsync();

        // Assert
        Assert.Equal(tier, viewModel.Tier);
    }

    [Fact]
    public async Task GivenTheProfileFetchFails_WhenStartAsyncCalled_ThenItDoesNotThrowAndTierIsNone()
    {
        // Arrange — sourcing the profile must never crash the pending screen. If GET /users/me fails the
        // VM swallows-and-logs (consistent with the hub-connect path) and leaves the tier unresolved
        // (None) so the badge simply does not render rather than tripping the error banner.
        _hub.Reset();
        _navigator.Reset();
        _authService.Setup(a => a.GetCurrentUserAsync())
            .ThrowsAsync(new HttpRequestException("Response status code does not indicate success: 401 (Unauthorized)."));
        var viewModel = new RequesterPendingViewModel(
            _hub.Object, _navigator.Object, _authService.Object,
            NullLogger<RequesterPendingViewModel>.Instance);

        // Act
        var exception = await Record.ExceptionAsync(() => viewModel.StartAsync());

        // Assert
        Assert.Null(exception);
        Assert.Equal(ServiceTier.None, viewModel.Tier);
    }
}
