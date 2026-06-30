using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Services;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.ViewModels;
using ServiceDelivery.Client.UI.Features.Requester.Pages;

namespace ServiceDelivery.Client.Tests.Requester;

/// <summary>
/// bUnit component tests for <see cref="RequesterPending"/> (FE-016). Drives the mockup
/// (requester-finding__mobile-390x844): the indeterminate spinner (AC-1/AC-2), the "Finding your
/// technician…" heading + sub-message (AC-2), the tier badge card reflecting the requester's REAL tier
/// (AC-2/AC-5 — the BUG-034 masking guard: a non-Gold profile must show its own tier, not GOLD), the
/// absence of any refresh control (AC-4, push-driven), and the responsive root data-testid (AC-5).
/// </summary>
public class RequesterPendingComponentTests : BunitContext
{
    private readonly Mock<IRequesterHubService> _hub = new();
    private readonly Mock<IPersonaNavigator> _navigator = new();
    private readonly Mock<IAuthService> _authService = new();
    private readonly Mock<ITokenStore> _tokenStore = new();
    private readonly Mock<ILogoutSideEffect> _sideEffect = new();
    private readonly Mock<IReleaseVehicleAction> _releaseAction = new();
    private readonly Mock<IShellPresentation> _presentation = new();

    private void RegisterPage(ServiceTier tier = ServiceTier.Gold, bool hubConnected = true)
    {
        _hub.SetupGet(h => h.IsConnected).Returns(hubConnected);
        _authService.Setup(a => a.GetCurrentUserAsync())
            .ReturnsAsync(new UserProfile(Guid.NewGuid(), "Marcus Wright", UserRole.Requester, tier, Guid.NewGuid()));

        var viewModel = new RequesterPendingViewModel(
            _hub.Object, _navigator.Object, _authService.Object,
            NullLogger<RequesterPendingViewModel>.Instance);

        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(viewModel);
        Services.AddSingleton<ILogger<RequesterPending>>(NullLogger<RequesterPending>.Instance);

        _presentation.SetupGet(p => p.MenuStyle).Returns(ShellMenuStyle.Drawer);
        var shell = new ShellViewModel(
            _tokenStore.Object, _navigator.Object, _sideEffect.Object,
            _releaseAction.Object, _presentation.Object, new PersonaMenuFactory());
        Services.AddSingleton(shell);
    }

    [Fact]
    public void GivenTheRequesterPendingPage_WhenRendered_ThenSpinnerIsVisible()
    {
        // Arrange
        RegisterPage();

        // Act
        var cut = Render<RequesterPending>();

        // Assert
        Assert.NotNull(cut.Find("[data-testid='pending-spinner']"));
    }

    [Fact]
    public void GivenTheRequesterPendingPage_WhenRendered_ThenFindingTechnicianMessageIsPresent()
    {
        // Arrange
        RegisterPage();

        // Act
        var cut = Render<RequesterPending>();

        // Assert
        var heading = cut.Find("[data-testid='pending-heading']");
        Assert.Contains("Finding your technician", heading.TextContent);
    }

    [Fact]
    public void GivenTheRequesterPendingPage_WhenRendered_ThenSubMessageIsPresent()
    {
        // Arrange
        RegisterPage();

        // Act
        var cut = Render<RequesterPending>();

        // Assert
        var sub = cut.Find("[data-testid='pending-submessage']");
        Assert.Contains("locating the nearest qualified rep", sub.TextContent);
    }

    [Fact]
    public void GivenAGoldRequester_WhenRendered_ThenTierBadgeCardIsVisible()
    {
        // Arrange
        RegisterPage(ServiceTier.Gold);

        // Act
        var cut = Render<RequesterPending>();

        // Assert
        var card = cut.Find("[data-testid='tier-card']");
        var badge = cut.Find("[data-testid='tier-badge']");
        Assert.Contains("GOLD", badge.TextContent);
        Assert.Contains("Priority service", card.TextContent);
    }

    [Fact]
    public void GivenASilverRequester_WhenRendered_ThenTierBadgeShowsSilverNotGold()
    {
        // Arrange — BUG-034 masking guard: the badge MUST reflect the authenticated requester's REAL tier.
        // A test that would pass with a hardcoded GOLD is not acceptable, so render a non-Gold (Silver)
        // profile and assert the badge shows SILVER and NOT GOLD.
        RegisterPage(ServiceTier.Silver);

        // Act
        var cut = Render<RequesterPending>();

        // Assert
        var badge = cut.Find("[data-testid='tier-badge']");
        Assert.Contains("SILVER", badge.TextContent);
        Assert.DoesNotContain("GOLD", badge.TextContent);
        Assert.Contains("sd-badge--silver", badge.GetAttribute("class"));
    }

    [Fact]
    public void GivenABronzeRequester_WhenRendered_ThenTierBadgeShowsBronze()
    {
        // Arrange — BUG-034 masking guard continued: bronze1 → BRONZE, never GOLD.
        RegisterPage(ServiceTier.Bronze);

        // Act
        var cut = Render<RequesterPending>();

        // Assert
        var badge = cut.Find("[data-testid='tier-badge']");
        Assert.Contains("BRONZE", badge.TextContent);
        Assert.DoesNotContain("GOLD", badge.TextContent);
    }

    [Fact]
    public void GivenTheRequesterPendingPage_WhenRendered_ThenNoRefreshButtonIsPresent()
    {
        // Arrange — AC-4: the transition is push-driven (RepAssigned over RequesterHub), so the screen
        // exposes no manual refresh control. There are no buttons on the pending view at all.
        RegisterPage();

        // Act
        var cut = Render<RequesterPending>();

        // Assert
        Assert.Empty(cut.FindAll("[data-testid='refresh-button']"));
        Assert.Empty(cut.FindAll("button"));
    }

    [Fact]
    public void GivenTheRequesterPendingPage_WhenRendered_ThenRootElementHasDataTestId()
    {
        // Arrange — AC-5: the responsive root container carries a stable data-testid so the E2E suite can
        // assert the requester landed on the pending view across mobile/web/desktop.
        RegisterPage();

        // Act
        var cut = Render<RequesterPending>();

        // Assert
        Assert.NotNull(cut.Find("[data-testid='requester-pending']"));
    }

    [Fact]
    public void GivenTheHubIsReconnecting_WhenRendered_ThenReconnectingIndicatorIsVisible()
    {
        // Arrange — BUG-038: while the hub is reconnecting (IsConnected false) the screen shows an
        // unobtrusive "Reconnecting…" indicator rather than crashing or showing nothing.
        RegisterPage(hubConnected: false);

        // Act
        var cut = Render<RequesterPending>();

        // Assert
        var status = cut.Find("[data-testid='hub-status']");
        Assert.Contains("Reconnecting", status.TextContent);
    }

    [Fact]
    public void GivenTheHubIsConnected_WhenRendered_ThenReconnectingIndicatorIsNotShown()
    {
        // Arrange — when the hub is connected the reconnecting indicator stays hidden.
        RegisterPage(hubConnected: true);

        // Act
        var cut = Render<RequesterPending>();

        // Assert
        Assert.Empty(cut.FindAll("[data-testid='hub-status']"));
    }
}
