using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.ViewModels;
using ServiceDelivery.Client.UI.Features.Requester.Pages;

namespace ServiceDelivery.Client.Tests.Requester;

/// <summary>
/// bUnit component tests for <see cref="RequesterTracking"/> in the redirected state (FE-018). Drives both
/// redirect mockups (requester-redirect__web-1280x800 / __mobile-390x844) by invoking the two captured hub
/// handlers in the production event order — RepAssigned first (new rep's position/ETA/name/vehicle), then
/// RepRedirected (banner only): the redirect apology banner and its exact text (AC-1), the app-bar title swap
/// to "A new technician is on the way" (AC-1), the "NEW" chip beside the rep name (AC-3), and the rep name /
/// vehicle / ETA replacement driven by the redirect's RepAssigned (AC-2/AC-3). The map itself is the real
/// FE-024 GoogleMap component; the overlay-interop calls are asserted in RequesterTrackingMapInteropTests.
/// </summary>
public class RequesterRedirectComponentTests : BunitContext
{
    private readonly Mock<IRepAssignedStore> _store = new();
    private readonly Mock<IRequesterHubService> _hub = new();
    private readonly Mock<IMapsLoader> _mapsLoader = new();
    private RequesterTrackingViewModel _viewModel = null!;

    private readonly Mock<ITokenStore> _tokenStore = new();
    private readonly Mock<IPersonaNavigator> _navigatorForShell = new();
    private readonly Mock<ILogoutSideEffect> _sideEffect = new();
    private readonly Mock<IReleaseVehicleAction> _releaseAction = new();
    private readonly Mock<IShellPresentation> _presentation = new();

    // The captured hub handlers, armed before RenderPage and resolved lazily (the ViewModel is constructed —
    // and the handlers registered — inside RenderPage). Fired in production order to simulate a redirect.
    private Func<RepAssignedPayload, Task>? _capturedAssigned;
    private Func<RepRedirectedPayload, Task>? _capturedRedirected;

    private static RepAssignedPayload SeedPayload(
        string repName = "Jordan Tran",
        double etaMinutes = 9,
        double latitude = 41.601,
        double longitude = -93.609,
        string vehicleRegistration = "IA-4471") =>
        new(Guid.NewGuid(), repName, etaMinutes, latitude, longitude, vehicleRegistration);

    private static RepAssignedPayload NewRepAssignedPayload(
        string repName = "Alex Rivera",
        double etaMinutes = 14,
        double latitude = 41.820,
        double longitude = -93.410,
        string vehicleRegistration = "IA-3382") =>
        new(Guid.NewGuid(), repName, etaMinutes, latitude, longitude, vehicleRegistration);

    private static RepRedirectedPayload RedirectedPayload(
        string oldRepName = "Jordan Tran",
        string newRepName = "Alex Rivera",
        double newEtaMinutes = 14) =>
        new(oldRepName, newRepName, newEtaMinutes);

    private IRenderedComponent<RequesterTracking> RenderPage(RepAssignedPayload seed)
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        _mapsLoader.Setup(l => l.LoadAsync()).ReturnsAsync(new MapsAvailability(true, null));
        Services.AddSingleton(_mapsLoader.Object);

        _hub.Setup(h => h.OnRepAssigned(It.IsAny<Func<RepAssignedPayload, Task>>()))
            .Callback<Func<RepAssignedPayload, Task>>(h => _capturedAssigned = h);
        _hub.Setup(h => h.OnRepRedirected(It.IsAny<Func<RepRedirectedPayload, Task>>()))
            .Callback<Func<RepRedirectedPayload, Task>>(h => _capturedRedirected = h);

        _store.SetupGet(s => s.CurrentPayload).Returns(seed);
        Services.AddSingleton(_store.Object);
        Services.AddSingleton(_hub.Object);
        _viewModel = new RequesterTrackingViewModel(_store.Object, _hub.Object);
        Services.AddSingleton(_viewModel);

        _presentation.SetupGet(p => p.MenuStyle).Returns(ShellMenuStyle.Drawer);
        _shell = new ShellViewModel(
            _tokenStore.Object, _navigatorForShell.Object, _sideEffect.Object,
            _releaseAction.Object, _presentation.Object, new PersonaMenuFactory());
        _shell.Load(new UserProfile(
            Guid.NewGuid(), "Marcus Webb", UserRole.Requester, ServiceTier.Gold, Guid.NewGuid()));
        Services.AddSingleton(_shell);

        return Render<RequesterTracking>();
    }

    private ShellViewModel _shell = null!;

    // Fires the two redirect events in the production order (RepAssigned then RepRedirected) inside the
    // renderer's dispatcher so the component re-renders. Mirrors a live redirect exactly.
    private async Task FireRedirectAsync(
        IRenderedComponent<RequesterTracking> cut,
        RepAssignedPayload newRep,
        RepRedirectedPayload redirect)
    {
        await cut.InvokeAsync(() => _capturedAssigned!(newRep));
        await cut.InvokeAsync(() => _capturedRedirected!(redirect));
    }

    [Fact]
    public async Task GivenTrackingPage_WhenRepRedirectedEventFires_ThenRedirectBannerShowsCorrectMessage()
    {
        // Arrange — AC-1: after the redirect, a banner (data-testid='redirect-banner') shows the exact apology
        // text naming the old and new reps. Absent before the redirect.
        var cut = RenderPage(SeedPayload());

        // Act
        await FireRedirectAsync(cut, NewRepAssignedPayload(), RedirectedPayload(
            oldRepName: "Jordan Tran", newRepName: "Alex Rivera"));

        // Assert
        var banner = cut.Find("[data-testid='redirect-banner']");
        Assert.Contains("Our apologies, we needed to redirect", banner.TextContent);
        Assert.Contains("Jordan Tran", banner.TextContent);
        Assert.Contains("Alex Rivera", banner.TextContent);
        Assert.Contains("is heading your way now.", banner.TextContent);
    }

    [Fact]
    public async Task GivenTrackingPage_WhenRepRedirectedEventFires_ThenBannerRendersBothRepNamesInBold()
    {
        // Arrange — AC-1 (mockup fidelity): both mockups render the old and new rep names in bold within the
        // banner. Assert each name sits inside a <strong> element (not merely present as plain text).
        var cut = RenderPage(SeedPayload());

        // Act
        await FireRedirectAsync(cut, NewRepAssignedPayload(), RedirectedPayload(
            oldRepName: "Jordan Tran", newRepName: "Alex Rivera"));

        // Assert
        var bolded = cut.FindAll("[data-testid='redirect-banner'] strong").Select(e => e.TextContent).ToList();
        Assert.Contains("Jordan Tran", bolded);
        Assert.Contains("Alex Rivera", bolded);
    }

    [Fact]
    public void GivenTrackingPage_WhenNotRedirected_ThenRedirectBannerIsAbsent()
    {
        // Arrange — AC-1 baseline (must not regress): before any redirect the banner is absent and the app-bar
        // title is the FE-017 "Your technician is on the way".
        var cut = RenderPage(SeedPayload());

        // Act / Assert
        Assert.Empty(cut.FindAll("[data-testid='redirect-banner']"));
        Assert.Equal("Your technician is on the way", _shell.Title);
    }

    [Fact]
    public async Task GivenTrackingPage_WhenRepRedirectedEventFires_ThenAppBarTitleChangesToNewTechnicianIsOnTheWay()
    {
        // Arrange — AC-1: after the redirect the app-bar title (owned by the page via Shell.SetTitle) changes
        // to "A new technician is on the way" (both mockups), replacing the FE-017 baseline title.
        var cut = RenderPage(SeedPayload());

        // Act
        await FireRedirectAsync(cut, NewRepAssignedPayload(), RedirectedPayload());

        // Assert
        Assert.Equal("A new technician is on the way", _shell.Title);
    }

    [Fact]
    public async Task GivenTrackingPage_WhenRepRedirectedEventFires_ThenNewChipIsPresent()
    {
        // Arrange — AC-3: after the redirect a "NEW" chip (data-testid='new-rep-chip') appears beside the rep
        // name in the bottom sheet (both mockups). Absent before the redirect.
        var cut = RenderPage(SeedPayload());

        // Act
        await FireRedirectAsync(cut, NewRepAssignedPayload(), RedirectedPayload());

        // Assert
        var chip = cut.Find("[data-testid='new-rep-chip']");
        Assert.Contains("NEW", chip.TextContent);
    }

    [Fact]
    public void GivenTrackingPage_WhenNotRedirected_ThenNewChipIsAbsent()
    {
        // Arrange — AC-3 baseline (must not regress): the "NEW" chip is shown only after a redirect.
        var cut = RenderPage(SeedPayload());

        // Act / Assert
        Assert.Empty(cut.FindAll("[data-testid='new-rep-chip']"));
    }

    [Fact]
    public async Task GivenTrackingPage_WhenRepAssignedEventFires_ThenRepNameElementShowsNewRepName()
    {
        // Arrange — AC-3: the new rep's name (from the redirect's RepAssigned) replaces the old one in the
        // bottom sheet. Seed "Jordan Tran", redirect assigns "Alex Rivera".
        var cut = RenderPage(SeedPayload(repName: "Jordan Tran"));

        // Act
        await FireRedirectAsync(cut, NewRepAssignedPayload(repName: "Alex Rivera"), RedirectedPayload());

        // Assert
        Assert.Equal("Alex Rivera", cut.Find("[data-testid='rep-name']").TextContent.Trim());
    }

    [Fact]
    public async Task GivenTrackingPage_WhenRepAssignedEventFires_ThenEtaChipShowsNewEtaMinutes()
    {
        // Arrange — AC-2: the ETA chip updates to the new rep's ETA (from the redirect's RepAssigned). Seed 9,
        // redirect assigns 14 (the mockup value).
        var cut = RenderPage(SeedPayload(etaMinutes: 9));

        // Act
        await FireRedirectAsync(cut, NewRepAssignedPayload(etaMinutes: 14), RedirectedPayload());

        // Assert
        Assert.Contains("14", cut.Find("[data-testid='eta-chip']").TextContent);
    }

    [Fact]
    public async Task GivenTrackingPage_WhenRepAssignedEventFires_ThenRepVehicleShowsNewRegistration()
    {
        // Arrange — AC-3 (BE-031 guard): the vehicle subtitle updates to the new rep's registration (from the
        // redirect's RepAssigned). Seed IA-4471, redirect assigns IA-3382 (the mockup value).
        var cut = RenderPage(SeedPayload(vehicleRegistration: "IA-4471"));

        // Act
        await FireRedirectAsync(cut, NewRepAssignedPayload(vehicleRegistration: "IA-3382"), RedirectedPayload());

        // Assert
        Assert.Equal("Vehicle IA-3382 · Service Rep", cut.Find("[data-testid='rep-vehicle']").TextContent.Trim());
    }
}
