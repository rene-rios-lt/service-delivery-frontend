using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.Services;
using ServiceDelivery.Client.Core.ViewModels;
using ServiceDelivery.Client.UI.Features.ServiceRep.Pages;
using ServiceDelivery.Client.UI.Shared.Components;

namespace ServiceDelivery.Client.Tests.ServiceRep;

public class JobOfferComponentTests : BunitContext
{
    private readonly Mock<IPersonaNavigator> _navigator = new();
    private readonly Mock<IJobOfferService> _jobOfferService = new();
    private readonly Mock<IDeclineOfferService> _declineOfferService = new();
    private readonly InMemoryJobOfferStore _store = new();

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

        // A real ShellViewModel (concrete singleton, consistent with the app's wiring) so the page's
        // OnInitialized SetFocusedMode call and Dispose ClearFocusedMode call are exercised end-to-end.
        _presentation.SetupGet(p => p.MenuStyle).Returns(ShellMenuStyle.Drawer);
        _shell = new ShellViewModel(
            _tokenStore.Object,
            _navigator.Object,
            _sideEffect.Object,
            _releaseAction.Object,
            _presentation.Object,
            new PersonaMenuFactory());
        _shell.Load(new UserProfile(Guid.NewGuid(), "Rosa Alvarez", UserRole.ServiceRep, ServiceTier.None, Guid.NewGuid()));
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

    // Renders the shared PersonaShell bound to the same ShellViewModel the page mutated, so the
    // app-bar chrome (title override, suppressed menu/avatar, subtitle) can be asserted as rendered.
    // PersonaShell pulls MudBlazor's PointerEventsNoneService (async-dispose only), so it renders in a
    // dedicated `await using` context whose async disposal handles it cleanly — mirroring
    // PersonaShellComponentTests. The page itself renders in the inherited context and mutates _shell.
    private static IRenderedComponent<PersonaShell> RenderShell(BunitContext ctx, ShellViewModel shell)
    {
        ctx.Services.AddMudServices();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        RenderFragment body = builder => builder.AddMarkupContent(0, "<div data-testid='page-body'></div>");
        return ctx.Render<PersonaShell>(p => p
            .Add(c => c.ViewModel, shell)
            .Add(c => c.Body, body));
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
    public void GivenASilverTierOffer_WhenJobOfferPageRendered_ThenSilverBadgeHasModifierClassAndText()
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
    public void GivenABronzeTierOffer_WhenJobOfferPageRendered_ThenBronzeBadgeHasModifierClassAndText()
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
    public void GivenAStoredOffer_WhenJobOfferPageRendered_ThenRequesterContentIsWrappedInSdCard()
    {
        // Arrange
        // AC-3: the requester name, tier badge, DTC title and metric tiles must sit inside an elevated
        // .sd-card surface (per the mockup) rather than floating flat on the page background.
        RegisterPage(Offer());

        // Act
        var cut = Render<JobOffer>();

        // Assert
        var card = cut.Find(".sd-card");
        Assert.NotNull(card.QuerySelector("[data-testid='requester-name']"));
        Assert.NotNull(card.QuerySelector("[data-testid='tier-badge']"));
        Assert.NotNull(card.QuerySelector("[data-testid='dtc-title']"));
        Assert.NotNull(card.QuerySelector("[data-testid='distance-miles']"));
        Assert.NotNull(card.QuerySelector("[data-testid='eta-minutes']"));
    }

    [Fact]
    public async Task GivenJobOfferPageInitializes_WhenRendered_ThenAppBarTitleIsIncomingJobOffer()
    {
        // Arrange
        // AC-4: the offer screen enters focused mode on init, overriding the app-bar title.
        RegisterPage(Offer(), vehicleContext: "Vehicle IA-4471 · On shift");

        // Act
        Render<JobOffer>();
        await using var shellCtx = new BunitContext();
        var shell = RenderShell(shellCtx, _shell);

        // Assert
        Assert.Contains("Incoming Job Offer", shell.Find("[data-testid='appbar-title']").TextContent);
    }

    [Fact]
    public async Task GivenRepIdleSetVehicleContextWithOnShift_WhenJobOfferPageInitializes_ThenSubtitleIsVehicleRegOnly()
    {
        // Arrange
        // AC-2: the stale "· On shift" suffix from the idle screen's context is stripped — the offer
        // subtitle reads the vehicle registration only.
        RegisterPage(Offer(), vehicleContext: "Vehicle IA-4471 · On shift");

        // Act
        Render<JobOffer>();
        await using var shellCtx = new BunitContext();
        var shell = RenderShell(shellCtx, _shell);

        // Assert
        var subtitle = shell.Find("[data-testid='appbar-context']").TextContent;
        Assert.Contains("Vehicle IA-4471", subtitle);
        Assert.DoesNotContain("On shift", subtitle);
    }

    [Fact]
    public async Task GivenJobOfferPageInitializes_WhenRendered_ThenMenuAffordanceIsNotPresent()
    {
        // Arrange
        RegisterPage(Offer(), vehicleContext: "Vehicle IA-4471 · On shift");

        // Act
        Render<JobOffer>();
        await using var shellCtx = new BunitContext();
        var shell = RenderShell(shellCtx, _shell);

        // Assert
        Assert.Empty(shell.FindAll("[data-testid='appbar-menu-affordance']"));
    }

    [Fact]
    public async Task GivenJobOfferPageInitializes_WhenRendered_ThenAvatarRemainsPresent()
    {
        // Arrange
        // AC-4 (corrected): the persona avatar stays on the offer screen to match the mockup exactly —
        // only the hamburger is suppressed in focused mode.
        RegisterPage(Offer(), vehicleContext: "Vehicle IA-4471 · On shift");

        // Act
        Render<JobOffer>();
        await using var shellCtx = new BunitContext();
        var shell = RenderShell(shellCtx, _shell);

        // Assert
        Assert.NotNull(shell.Find("[data-testid='appbar-avatar']"));
    }

    [Fact]
    public async Task GivenJobOfferPageDisposed_WhenLeavingOfferRoute_ThenShellChromeIsRestored()
    {
        // Arrange
        // Leaving the offer screen (accept, decline, or expiry disposes the page) must restore the
        // normal shell: default title and the menu affordance + avatar reappear. This proves
        // ClearFocusedMode runs on dispose and the shell is not left stuck in focused mode (AC-4).
        RegisterPage(Offer(), vehicleContext: "Vehicle IA-4471 · On shift");
        var cut = Render<JobOffer>();

        // Act
        // Disposing the page is what the framework does when the rep navigates away from /rep/offer;
        // the page's Dispose() must call Shell.ClearFocusedMode().
        cut.Instance.Dispose();
        await using var shellCtx = new BunitContext();
        var shell = RenderShell(shellCtx, _shell);

        // Assert
        Assert.Contains("Service Delivery", shell.Find("[data-testid='appbar-title']").TextContent);
        Assert.NotNull(shell.Find("[data-testid='appbar-menu-affordance']"));
        Assert.NotNull(shell.Find("[data-testid='appbar-avatar']"));
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
