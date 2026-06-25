using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.ViewModels;
using ServiceDelivery.Client.UI.Features.ServiceRep.Pages;

namespace ServiceDelivery.Client.Tests.ServiceRep;

public class ActiveJobComponentTests : BunitContext
{
    private readonly Mock<IActiveJobService> _activeJobService = new();
    private readonly Mock<IRepHubService> _repHub = new();
    private readonly Mock<IArriveService> _arriveService = new();
    private ActiveJobViewModel _viewModel = null!;

    private static RedirectPayload Redirect(
        string requesterTier = "Gold",
        string dtcTitle = "P0420 · Catalyst Efficiency Low",
        double distanceMiles = 11.2,
        double etaMinutes = 14,
        double latitude = 41.85,
        double longitude = -93.35) =>
        new(Guid.NewGuid(), requesterTier, dtcTitle, distanceMiles, etaMinutes, latitude, longitude);

    private static ActiveJobContext Context(
        string requesterName = "Marcus Webb",
        string dtcTitle = "P0700 · Transmission Control Fault",
        double requesterLat = 41.60,
        double requesterLng = -93.60,
        double repLat = 41.70,
        double repLng = -93.50,
        int etaMinutes = 9,
        string repState = "EnRoute",
        string tier = "Gold") =>
        new(Guid.NewGuid(), requesterName, dtcTitle, requesterLat, requesterLng,
            repLat, repLng, etaMinutes, repState, tier);

    private void RegisterPage(ActiveJobContext context)
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        _activeJobService.Setup(s => s.GetActiveJobAsync()).ReturnsAsync(context);
        Services.AddSingleton(_activeJobService.Object);
        Services.AddSingleton(_repHub.Object);
        Services.AddSingleton(_arriveService.Object);
        _viewModel = new ActiveJobViewModel(_activeJobService.Object, _repHub.Object, _arriveService.Object);
        Services.AddSingleton(_viewModel);
    }

    [Fact]
    public void GivenAnActiveJob_WhenInitialized_ThenRepMarkerAndRequesterPinAreRendered()
    {
        // Arrange
        // AC-1: the map shows the rep's moving marker and the requester's fixed pin.
        RegisterPage(Context());

        // Act
        var cut = Render<ActiveJob>();

        // Assert
        Assert.NotNull(cut.Find("[data-testid='rep-marker']"));
        Assert.NotNull(cut.Find("[data-testid='requester-pin']"));
    }

    [Fact]
    public async Task GivenAnUpdatedEta_WhenComponentRendered_ThenEtaCardShowsNewMinutes()
    {
        // Arrange
        // AC-2: as the rep's position changes, a poll returns a new ETA and the floating ETA card
        // re-renders with the new minutes without a screen reload.
        RegisterPage(Context(etaMinutes: 9));
        var cut = Render<ActiveJob>();
        _activeJobService.Setup(s => s.GetActiveJobAsync()).ReturnsAsync(Context(etaMinutes: 6));

        // Act
        await cut.InvokeAsync(() => cut.Instance.PollOnceAsync());

        // Assert
        var etaCard = cut.Find("[data-testid='eta-minutes']");
        Assert.Contains("6", etaCard.TextContent);
    }

    [Fact]
    public void GivenAGoldTierJob_WhenComponentRendered_ThenTierBadgeShowsGoldStyledPerDesignSystem()
    {
        // Arrange
        // FE-012 fidelity: the bottom sheet shows the service tier as a gold badge with a star icon
        // beside the requester name (matches rep-on-site mockup; .sd-badge .sd-badge--gold).
        RegisterPage(Context(tier: "Gold"));

        // Act
        var cut = Render<ActiveJob>();

        // Assert
        var badge = cut.Find("[data-testid='tier-badge']");
        Assert.Contains("GOLD", badge.TextContent.ToUpperInvariant());
        Assert.Contains("sd-badge", badge.GetAttribute("class"));
        Assert.Contains("sd-badge--gold", badge.GetAttribute("class"));
        Assert.NotNull(badge.QuerySelector(".sd-badge__icon"));
    }

    [Theory]
    [InlineData("Gold", "sd-badge--gold")]
    [InlineData("Silver", "sd-badge--silver")]
    [InlineData("Bronze", "sd-badge--bronze")]
    public void GivenAnyTierJob_WhenComponentRendered_ThenTierBadgeRendersGenericallyForThatTier(
        string tier, string expectedModifierClass)
    {
        // Arrange
        // FE-012 fidelity: the badge is rendered generically for the request's tier, not hardcoded to
        // GOLD — the tier text and the tier-specific design-system modifier class both follow the data.
        RegisterPage(Context(tier: tier));

        // Act
        var cut = Render<ActiveJob>();

        // Assert
        var badge = cut.Find("[data-testid='tier-badge']");
        Assert.Contains(tier.ToUpperInvariant(), badge.TextContent.ToUpperInvariant());
        Assert.Contains(expectedModifierClass, badge.GetAttribute("class"));
    }

    [Fact]
    public void GivenAnActiveJob_WhenComponentRendered_ThenBottomSheetShowsDtcTitleAndRequesterName()
    {
        // Arrange
        // AC-3: the bottom sheet shows the requester name and the DTC title for the active job.
        RegisterPage(Context(
            requesterName: "Marcus Webb",
            dtcTitle: "P0700 · Transmission Control Fault"));

        // Act
        var cut = Render<ActiveJob>();

        // Assert
        var sheet = cut.Find("[data-testid='bottom-sheet']");
        Assert.Equal("Marcus Webb", cut.Find("[data-testid='requester-name']").TextContent.Trim());
        Assert.Equal("P0700 · Transmission Control Fault", cut.Find("[data-testid='dtc-title']").TextContent.Trim());
        Assert.NotNull(sheet);
    }

    [Fact]
    public void GivenIsArrivedDisabled_WhenComponentRendered_ThenButtonHasDisabledAttribute()
    {
        // Arrange
        // AC-4: while the rep is still en route the "I've Arrived" button is disabled.
        RegisterPage(Context(repState: "EnRoute"));

        // Act
        var cut = Render<ActiveJob>();

        // Assert
        var button = cut.Find("[data-testid='arrived-button']");
        Assert.True(button.HasAttribute("disabled"));
    }

    [Fact]
    public async Task GivenARedirect_WhenComponentReceivesUpdate_ThenMapPinMovesToNewCoordinatesWithoutNavigation()
    {
        // Arrange
        // AC-6: a RedirectReceived event moves the requester pin to the new destination in-place — the
        // page re-renders the new coordinates and never navigates away.
        RegisterPage(Context(requesterLat: 41.60, requesterLng: -93.60));
        var cut = Render<ActiveJob>();
        var nav = Services.GetRequiredService<NavigationManager>();
        var uriBeforeRedirect = nav.Uri;

        // Act
        await cut.InvokeAsync(() => _viewModel.OnRedirectReceivedAsync(Redirect(latitude: 41.85, longitude: -93.35)));

        // Assert
        var pin = cut.Find("[data-testid='requester-pin']");
        Assert.Equal("41.85", pin.GetAttribute("data-lat"));
        Assert.Equal("-93.35", pin.GetAttribute("data-lng"));
        Assert.Equal(uriBeforeRedirect, nav.Uri);
    }

    [Fact]
    public void GivenAnActiveJob_WhenInitialized_ThenStraightRouteLineIsRendered()
    {
        // Arrange
        // AC-1: the map draws a straight route line between the rep marker and the requester pin.
        RegisterPage(Context(
            requesterLat: 41.60, requesterLng: -93.60,
            repLat: 41.70, repLng: -93.50));

        // Act
        var cut = Render<ActiveJob>();

        // Assert
        Assert.NotNull(cut.Find("[data-testid='route-line']"));
    }

    [Fact]
    public void GivenAnActiveJob_WhenRepStateIsEnRoute_ThenEnRouteChipIsRendered()
    {
        // Arrange
        // AC-4: while the rep is still en route the state chip reads "En Route".
        RegisterPage(Context(repState: "EnRoute"));

        // Act
        var cut = Render<ActiveJob>();

        // Assert
        var chip = cut.Find("[data-testid='state-chip']");
        Assert.Contains("En Route", chip.TextContent);
    }

    [Fact]
    public void GivenArrivedButtonClicked_WhenComponentHandlesClick_ThenViewModelArriveAsyncIsCalled()
    {
        // Arrange
        // AC-1: tapping the enabled "I've Arrived" button invokes the ViewModel's ArriveAsync, which
        // calls POST /rep/arrive via IArriveService.
        RegisterPage(Context(repState: "Within15Miles"));
        var cut = Render<ActiveJob>();

        // Act
        cut.Find("[data-testid='arrived-button']").Click();

        // Assert
        _arriveService.Verify(s => s.ArriveAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenIsOnSiteTrue_WhenComponentRendered_ThenRouteLineIsHidden()
    {
        // Arrange
        // AC-2: once the rep arrives the highlighted navigation route polyline is removed (only the
        // faint background road grid remains). The route-line SVG must no longer be in the DOM.
        RegisterPage(Context(repState: "Within15Miles"));
        var cut = Render<ActiveJob>();

        // Act
        await cut.InvokeAsync(() => _viewModel.ArriveAsync());

        // Assert
        Assert.Empty(cut.FindAll("[data-testid='route-line']"));
    }

    [Fact]
    public async Task GivenIsOnSiteTrue_WhenComponentRendered_ThenMarkCompleteButtonIsVisibleAsPrimaryAction()
    {
        // Arrange
        // AC-2: on arrival "Mark Complete" becomes the primary action in the bottom sheet.
        RegisterPage(Context(repState: "Within15Miles"));
        var cut = Render<ActiveJob>();

        // Act
        await cut.InvokeAsync(() => _viewModel.ArriveAsync());

        // Assert
        var completeButton = cut.Find("[data-testid='complete-button']");
        Assert.Contains("Mark Complete", completeButton.TextContent);
        Assert.Contains("sd-btn--primary", completeButton.GetAttribute("class"));
    }

    [Fact]
    public async Task GivenIsOnSiteTrue_WhenComponentRendered_ThenArrivedButtonIsNoLongerPrimary()
    {
        // Arrange
        // AC-2: once on-site "I've Arrived" is no longer the primary action — it is removed in favour
        // of "Mark Complete".
        RegisterPage(Context(repState: "Within15Miles"));
        var cut = Render<ActiveJob>();

        // Act
        await cut.InvokeAsync(() => _viewModel.ArriveAsync());

        // Assert
        Assert.Empty(cut.FindAll("[data-testid='arrived-button']"));
    }

    [Fact]
    public async Task GivenIsOnSiteTrue_WhenComponentRendered_ThenStateChipShowsOnSite()
    {
        // Arrange
        // AC-2: the state chip switches to the red "On Site" chip once the rep arrives.
        RegisterPage(Context(repState: "Within15Miles"));
        var cut = Render<ActiveJob>();

        // Act
        await cut.InvokeAsync(() => _viewModel.ArriveAsync());

        // Assert
        var chip = cut.Find("[data-testid='state-chip']");
        Assert.Contains("On Site", chip.TextContent);
        Assert.Contains("sd-chip--onsite", chip.GetAttribute("class"));
    }

    [Fact]
    public void GivenAnActiveJob_WhenRepStateIsWithin15Miles_ThenWithin15ChipIsRendered()
    {
        // Arrange
        // AC-4: once the rep is within 15 miles the state chip switches to "Within 15 mi".
        RegisterPage(Context(repState: "Within15Miles"));

        // Act
        var cut = Render<ActiveJob>();

        // Assert
        var chip = cut.Find("[data-testid='state-chip']");
        Assert.Contains("Within 15 mi", chip.TextContent);
    }
}
