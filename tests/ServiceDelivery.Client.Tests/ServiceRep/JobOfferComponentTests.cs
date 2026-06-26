using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.Services;
using ServiceDelivery.Client.Core.ViewModels;
using ServiceDelivery.Client.UI.Features.ServiceRep.Pages;

namespace ServiceDelivery.Client.Tests.ServiceRep;

public class JobOfferComponentTests : BunitContext
{
    private readonly Mock<IPersonaNavigator> _navigator = new();
    private readonly Mock<IJobOfferService> _jobOfferService = new();
    private readonly Mock<IDeclineOfferService> _declineOfferService = new();
    private readonly InMemoryJobOfferStore _store = new();

    // ShellViewModel collaborators — the page now drives the shared app-bar chrome (BUG-036), so the
    // page test registers a real ShellViewModel with a loaded menu (mirrors RepIdleComponentTests).
    private readonly Mock<ITokenStore> _tokenStore = new();
    private readonly Mock<ILogoutSideEffect> _sideEffect = new();
    private readonly Mock<IReleaseVehicleAction> _releaseAction = new();
    private readonly Mock<IShellPresentation> _presentation = new();
    private ShellViewModel _shell = default!;

    private static JobOfferPayload Offer(
        string requesterName = "Marcus",
        ServiceTier tier = ServiceTier.Gold,
        string dtcTitle = "P0700 · Transmission Control Fault",
        double distanceMiles = 12.4,
        int etaMinutes = 13,
        double lat = 41.6,
        double lng = -93.6) =>
        new(Guid.NewGuid(), requesterName, tier, dtcTitle, distanceMiles, etaMinutes, lat, lng);

    private void RegisterPage(JobOfferPayload offer, string? vehicleContext = null)
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<IJobOfferStore>(_store);
        Services.AddSingleton(_navigator.Object);
        Services.AddSingleton(_jobOfferService.Object);
        Services.AddSingleton(_declineOfferService.Object);

        _presentation.SetupGet(p => p.MenuStyle).Returns(ShellMenuStyle.Drawer);
        _shell = new ShellViewModel(
            _tokenStore.Object, _navigator.Object, _sideEffect.Object,
            _releaseAction.Object, _presentation.Object, new PersonaMenuFactory());
        _shell.Load(new UserProfile(
            Guid.NewGuid(), "Rosa Alvarez", UserRole.ServiceRep, ServiceTier.None, Guid.NewGuid()));
        // Simulate the subtitle RepIdle leaves behind when the rep is on shift, so the offer screen's
        // "drop · On shift" behaviour (AC-2) can be exercised.
        if (vehicleContext is not null)
        {
            _shell.SetVehicleContext(vehicleContext);
        }
        Services.AddSingleton(_shell);

        // Default decline outcome so the Decline button is clickable in tests that do not exercise
        // the decline path (FE-010). Decline-path tests override this per scenario.
        _declineOfferService
            .Setup(s => s.DeclineAsync(It.IsAny<Guid>()))
            .ReturnsAsync(DeclineOfferResult.Success);

        _store.SetOffer(offer);
    }

    [Fact]
    public void GivenAStoredOffer_WhenJobOfferPageRendered_ThenRequesterNameIsDisplayed()
    {
        // Arrange
        RegisterPage(Offer(requesterName: "Marcus"));

        // Act
        var cut = Render<JobOffer>();

        // Assert
        var name = cut.Find("[data-testid='requester-name']");
        Assert.Equal("Marcus", name.TextContent.Trim());
    }

    [Fact]
    public void GivenAGoldTierOffer_WhenJobOfferPageRendered_ThenGoldTierBadgeIsVisible()
    {
        // Arrange
        RegisterPage(Offer(tier: ServiceTier.Gold));

        // Act
        var cut = Render<JobOffer>();

        // Assert
        var badge = cut.Find("[data-testid='tier-badge']");
        Assert.Contains("GOLD", badge.TextContent);
        Assert.Contains("sd-badge--gold", badge.ClassList);
    }

    [Fact]
    public void GivenASilverTierOffer_WhenJobOfferPageRendered_ThenSilverTierBadgeIsVisible()
    {
        // Arrange
        RegisterPage(Offer(tier: ServiceTier.Silver));

        // Act
        var cut = Render<JobOffer>();

        // Assert
        var badge = cut.Find("[data-testid='tier-badge']");
        Assert.Contains("SILVER", badge.TextContent);
        Assert.Contains("sd-badge--silver", badge.ClassList);
    }

    [Fact]
    public void GivenABronzeTierOffer_WhenJobOfferPageRendered_ThenBronzeTierBadgeIsVisible()
    {
        // Arrange
        RegisterPage(Offer(tier: ServiceTier.Bronze));

        // Act
        var cut = Render<JobOffer>();

        // Assert
        var badge = cut.Find("[data-testid='tier-badge']");
        Assert.Contains("BRONZE", badge.TextContent);
        Assert.Contains("sd-badge--bronze", badge.ClassList);
    }

    [Fact]
    public void GivenAJobOffer_WhenJobOfferPageRendered_ThenContentIsGroupedInAnElevatedCard()
    {
        // Arrange
        // AC-3: the requester / tier / fault / metrics block sits inside a single elevated card.
        RegisterPage(Offer());

        // Act
        var cut = Render<JobOffer>();

        // Assert
        var card = cut.Find("[data-testid='offer-card']");
        Assert.Contains("sd-card", card.ClassList);
        Assert.NotNull(card.QuerySelector("[data-testid='requester-name']"));
        Assert.NotNull(card.QuerySelector("[data-testid='tier-badge']"));
        Assert.NotNull(card.QuerySelector("[data-testid='eta-minutes']"));
    }

    [Fact]
    public void GivenAJobOfferPage_WhenInitialized_ThenShellTitleIsIncomingJobOffer()
    {
        // Arrange
        // AC-4: the offer route overrides the app-bar title to match the mockup.
        RegisterPage(Offer());

        // Act
        Render<JobOffer>();

        // Assert
        Assert.Equal("Incoming Job Offer", _shell.Title);
    }

    [Fact]
    public void GivenAJobOfferPage_WhenInitialized_ThenShellMenuAffordanceIsHidden()
    {
        // Arrange
        // AC-4: the mockup shows no hamburger on the offer screen.
        RegisterPage(Offer());

        // Act
        Render<JobOffer>();

        // Assert
        Assert.False(_shell.IsMenuAffordanceVisible);
    }

    [Fact]
    public void GivenAnOnShiftSubtitle_WhenJobOfferPageInitialized_ThenSubtitleDropsTheOnShiftSuffix()
    {
        // Arrange
        // AC-2: the offer screen must not show the stale "· On shift" subtitle left by the idle view.
        RegisterPage(Offer(), vehicleContext: "Vehicle V-001 · On shift");

        // Act
        Render<JobOffer>();

        // Assert
        Assert.Equal("Vehicle V-001", _shell.Menu!.VehicleContext);
        Assert.DoesNotContain("On shift", _shell.Menu!.VehicleContext);
    }

    [Fact]
    public void GivenAJobOfferPage_WhenDisposed_ThenShellChromeIsRestored()
    {
        // Arrange
        // Leaving the offer screen restores the default chrome so later routes are unaffected.
        RegisterPage(Offer());
        var cut = Render<JobOffer>();

        // Act
        cut.Instance.Dispose();

        // Assert
        Assert.Equal("Service Delivery", _shell.Title);
        Assert.True(_shell.IsMenuAffordanceVisible);
    }

    [Fact]
    public void GivenAJobOfferPayload_WhenJobOfferPageRendered_ThenDtcTitleIsDisplayed()
    {
        // Arrange
        RegisterPage(Offer(dtcTitle: "P0700 · Transmission Control Fault"));

        // Act
        var cut = Render<JobOffer>();

        // Assert
        var dtc = cut.Find("[data-testid='dtc-title']");
        Assert.Equal("P0700 · Transmission Control Fault", dtc.TextContent.Trim());
    }

    [Fact]
    public void GivenAJobOfferPayload_WhenJobOfferPageRendered_ThenDistanceMilesIsDisplayed()
    {
        // Arrange
        RegisterPage(Offer(distanceMiles: 12.4));

        // Act
        var cut = Render<JobOffer>();

        // Assert
        var distance = cut.Find("[data-testid='distance-miles']");
        Assert.Contains("12.4", distance.TextContent);
        Assert.Contains("MILES", distance.TextContent);
    }

    [Fact]
    public void GivenAJobOfferPayload_WhenJobOfferPageRendered_ThenEtaMinutesIsDisplayed()
    {
        // Arrange
        RegisterPage(Offer(etaMinutes: 13));

        // Act
        var cut = Render<JobOffer>();

        // Assert
        var eta = cut.Find("[data-testid='eta-minutes']");
        Assert.Contains("13", eta.TextContent);
        Assert.Contains("MIN ETA", eta.TextContent);
    }

    [Fact]
    public void GivenAJobOfferPayload_WhenJobOfferPageRendered_ThenMapPinIsVisible()
    {
        // Arrange
        RegisterPage(Offer(lat: 41.6, lng: -93.6));

        // Act
        var cut = Render<JobOffer>();

        // Assert
        var pin = cut.Find("[data-testid='map-pin']");
        Assert.Contains("sd-pin-dest", pin.ClassList);
    }

    [Fact]
    public void GivenANewOffer_WhenJobOfferPageRendered_ThenCountdownShows60()
    {
        // Arrange
        RegisterPage(Offer());

        // Act
        var cut = Render<JobOffer>();

        // Assert
        var countdown = cut.Find("[data-testid='countdown-number']");
        Assert.Equal("60", countdown.TextContent.Trim());
    }

    [Fact]
    public void GivenCountdownAbove10_WhenRendered_ThenCountdownRingIsNotUrgent()
    {
        // Arrange
        // A freshly rendered offer rests at 60 seconds — well above the 10-second urgent threshold,
        // so the ring keeps the normal (blue) styling and the urgency label is hidden (AC-3).
        RegisterPage(Offer());

        // Act
        var cut = Render<JobOffer>();

        // Assert
        var ring = cut.Find("[data-testid='countdown-ring']");
        Assert.DoesNotContain("sd-countdown--urgent", ring.ClassList);
        Assert.Empty(cut.FindAll("[data-testid='urgency-label']"));
    }

    [Fact]
    public async Task GivenCountdownAt10OrBelow_WhenRendered_ThenCountdownRingIsUrgent()
    {
        // Arrange
        // Drive the countdown into the final 10 seconds via the component's tick entry point — the
        // same method the production timer calls. The ring turns red (urgent) and the urgency label
        // appears (AC-3).
        RegisterPage(Offer());
        var cut = Render<JobOffer>();

        // Act
        for (var i = 0; i < 50; i++)
        {
            await cut.InvokeAsync(() => cut.Instance.TickAsync());
        }

        // Assert
        var ring = cut.Find("[data-testid='countdown-ring']");
        Assert.Contains("sd-countdown--urgent", ring.ClassList);
        Assert.Equal("10", cut.Find("[data-testid='countdown-number']").TextContent.Trim());
        Assert.NotNull(cut.Find("[data-testid='urgency-label']"));
    }

    [Fact]
    public void GivenAJobOffer_WhenJobOfferPageRendered_ThenAcceptButtonIsVisible()
    {
        // Arrange
        RegisterPage(Offer());

        // Act
        var cut = Render<JobOffer>();

        // Assert
        var accept = cut.Find("[data-testid='accept-button']");
        Assert.Contains("Accept", accept.TextContent);
    }

    [Fact]
    public void GivenAJobOffer_WhenJobOfferPageRendered_ThenDeclineButtonIsVisible()
    {
        // Arrange
        RegisterPage(Offer());

        // Act
        var cut = Render<JobOffer>();

        // Assert
        var decline = cut.Find("[data-testid='decline-button']");
        Assert.Contains("Decline", decline.TextContent);
    }

    [Fact]
    public void GivenDeclineSuccess_WhenDeclineButtonClicked_ThenNavigateToRepIdleViewIsCalled()
    {
        // Arrange
        // AC-2: tapping Decline dismisses the offer screen and returns the rep to the idle /
        // waiting-for-offers view.
        _declineOfferService
            .Setup(s => s.DeclineAsync(It.IsAny<Guid>()))
            .ReturnsAsync(DeclineOfferResult.Success);
        RegisterPage(Offer());
        var cut = Render<JobOffer>();

        // Act
        cut.Find("[data-testid='decline-button']").Click();

        // Assert
        _navigator.Verify(n => n.NavigateToRepIdleView(), Times.Once);
    }

    [Fact]
    public async Task GivenAJobOfferAtZeroSeconds_WhenCountdownExpires_ThenNavigationToIdleIsInvoked()
    {
        // Arrange
        // Tick the rendered page all the way to zero — the offer has expired server-side, so the
        // page dismisses by navigating back to the idle / waiting-for-offers view (AC-5).
        RegisterPage(Offer());
        var cut = Render<JobOffer>();

        // Act
        for (var i = 0; i < 60; i++)
        {
            await cut.InvokeAsync(() => cut.Instance.TickAsync());
        }

        // Assert
        _navigator.Verify(n => n.NavigateToRepIdleView(), Times.Once);
    }

    [Fact]
    public void GivenAcceptSuccess_WhenAcceptButtonClicked_ThenNavigateToActiveJobIsCalled()
    {
        // Arrange
        // AC-2: a successful accept transitions away from the offer screen to the active-job view.
        _jobOfferService
            .Setup(s => s.AcceptAsync(It.IsAny<Guid>()))
            .ReturnsAsync(AcceptOfferResult.Success);
        RegisterPage(Offer());
        var cut = Render<JobOffer>();

        // Act
        cut.Find("[data-testid='accept-button']").Click();

        // Assert
        _navigator.Verify(n => n.NavigateToActiveJob(), Times.Once);
    }

    [Fact]
    public void GivenAcceptConflict_WhenAcceptButtonClicked_ThenOfferExpiredMessageIsDisplayed()
    {
        // Arrange
        // AC-3: a 409 means the offer expired between the tap and the API call — the page shows the
        // "Offer expired" message.
        _jobOfferService
            .Setup(s => s.AcceptAsync(It.IsAny<Guid>()))
            .ReturnsAsync(AcceptOfferResult.Conflict);
        RegisterPage(Offer());
        var cut = Render<JobOffer>();

        // Act
        cut.Find("[data-testid='accept-button']").Click();

        // Assert
        var message = cut.Find("[data-testid='offer-expired-message']");
        Assert.Contains("Offer expired", message.TextContent);
    }

    [Fact]
    public void GivenAStoredOffer_WhenJobOfferPageRendered_ThenStoreIsClearedSoTheOfferIsNotReshownOnReEntry()
    {
        // Arrange
        // The page consumes the in-flight offer on init; clearing the store prevents a stale offer
        // from being re-shown if the rep navigates back to /rep/offer after the screen dismisses.
        RegisterPage(Offer());

        // Act
        Render<JobOffer>();

        // Assert
        Assert.Null(_store.CurrentOffer);
    }
}
