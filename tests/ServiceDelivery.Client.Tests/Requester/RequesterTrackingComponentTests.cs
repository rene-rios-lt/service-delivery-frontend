using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.ViewModels;
using ServiceDelivery.Client.UI.Features.Requester.Pages;

namespace ServiceDelivery.Client.Tests.Requester;

/// <summary>
/// bUnit component tests for <see cref="RequesterTracking"/> (FE-017). Drives the mockup
/// (requester-tracking__web-1280x800 / __mobile-390x844): the rep name above the map (AC-2), the vehicle
/// subtitle with a registration and its pre-BE-031 fallback (AC-2), the status pill label per rep state
/// (AC-4), the ETA chip and its OnSite hide + arrived message (AC-3/AC-5), and the full-bleed map + bottom
/// sheet structure (AC-6). The map itself is the real FE-024 GoogleMap component (mirroring ActiveJob); the
/// overlay-interop calls are asserted separately in <see cref="RequesterTrackingMapInteropTests"/>.
/// </summary>
public class RequesterTrackingComponentTests : BunitContext
{
    private readonly Mock<IRepAssignedStore> _store = new();
    private readonly Mock<IRequesterHubService> _hub = new();
    private readonly Mock<IMapsLoader> _mapsLoader = new();
    private RequesterTrackingViewModel _viewModel = null!;

    // ShellViewModel collaborators — the tracking page sets the shared app-bar title on init (mockup: "Your
    // technician is on the way"), so the page test registers a real ShellViewModel, mirroring ActiveJob.
    private readonly Mock<ITokenStore> _tokenStore = new();
    private readonly Mock<IPersonaNavigator> _navigatorForShell = new();
    private readonly Mock<ILogoutSideEffect> _sideEffect = new();
    private readonly Mock<IReleaseVehicleAction> _releaseAction = new();
    private readonly Mock<IShellPresentation> _presentation = new();

    private static RepAssignedPayload Payload(
        string repName = "Jordan Tran",
        double etaMinutes = 9,
        double latitude = 41.601,
        double longitude = -93.609,
        string vehicleRegistration = "IA-4471") =>
        new(Guid.NewGuid(), repName, etaMinutes, latitude, longitude, vehicleRegistration);

    private IRenderedComponent<RequesterTracking> RenderPage(RepAssignedPayload payload)
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        _mapsLoader.Setup(l => l.LoadAsync()).ReturnsAsync(new MapsAvailability(true, null));
        Services.AddSingleton(_mapsLoader.Object);
        _store.SetupGet(s => s.CurrentPayload).Returns(payload);
        Services.AddSingleton(_store.Object);
        Services.AddSingleton(_hub.Object);
        _viewModel = new RequesterTrackingViewModel(_store.Object, _hub.Object);
        Services.AddSingleton(_viewModel);

        _presentation.SetupGet(p => p.MenuStyle).Returns(ShellMenuStyle.Drawer);
        var shell = new ShellViewModel(
            _tokenStore.Object, _navigatorForShell.Object, _sideEffect.Object,
            _releaseAction.Object, _presentation.Object, new PersonaMenuFactory());
        shell.Load(new UserProfile(
            Guid.NewGuid(), "Marcus Webb", UserRole.Requester, ServiceTier.Gold, Guid.NewGuid()));
        Services.AddSingleton(shell);

        return Render<RequesterTracking>();
    }

    // Arms the mock to capture the RepPositionUpdated handler the ViewModel registers during RenderPage,
    // and returns a func that invokes it. The returned func reads the captured delegate lazily (at call
    // time), because the ViewModel is constructed — and the handler registered — inside RenderPage, which
    // runs after this setup. Must be called BEFORE RenderPage.
    private Func<RepPositionUpdatedPayload, Task> Handler()
    {
        Func<RepPositionUpdatedPayload, Task>? captured = null;
        _hub.Setup(h => h.OnRepPositionUpdated(It.IsAny<Func<RepPositionUpdatedPayload, Task>>()))
            .Callback<Func<RepPositionUpdatedPayload, Task>>(h => captured = h);
        return payload => captured!(payload);
    }

    [Fact]
    public void GivenTrackingPage_WhenRendered_ThenRepNameElementShowsAssignedRepName()
    {
        // Arrange — AC-2: the rep name from the RepAssigned payload is shown in the bottom sheet.
        var cut = RenderPage(Payload(repName: "Jordan Tran"));

        // Act
        var repName = cut.Find("[data-testid='rep-name']");

        // Assert
        Assert.Equal("Jordan Tran", repName.TextContent.Trim());
    }

    [Fact]
    public void GivenTrackingPageWithRegistration_WhenRendered_ThenRepVehicleShowsVehicleRegistration()
    {
        // Arrange — AC-2: with a registration present the subtitle reads "Vehicle IA-4471 · Service Rep".
        var cut = RenderPage(Payload(vehicleRegistration: "IA-4471"));

        // Act
        var repVehicle = cut.Find("[data-testid='rep-vehicle']");

        // Assert
        Assert.Equal("Vehicle IA-4471 · Service Rep", repVehicle.TextContent.Trim());
    }

    [Fact]
    public void GivenTrackingPageWithEmptyRegistration_WhenRendered_ThenRepVehicleShowsServiceRepOnly()
    {
        // Arrange — AC-2 pre-BE-031 fallback: with no registration the subtitle degrades to "Service Rep"
        // with no dangling "Vehicle ·".
        var cut = RenderPage(Payload(vehicleRegistration: string.Empty));

        // Act
        var repVehicle = cut.Find("[data-testid='rep-vehicle']");

        // Assert
        Assert.Equal("Service Rep", repVehicle.TextContent.Trim());
    }

    [Theory]
    [InlineData("EnRoute", "On the way")]
    [InlineData("Within15Miles", "Almost there")]
    [InlineData("OnSite", "Your technician has arrived")]
    public async Task GivenTrackingPage_WhenRepStateIs_ThenStatusPillShowsCorrectLabel(
        string state, string expectedLabel)
    {
        // Arrange — AC-4/AC-5: the status pill reflects the rep state pushed over RepPositionUpdated.
        var handler = Handler();
        var cut = RenderPage(Payload());

        // Act
        await cut.InvokeAsync(() => handler(new RepPositionUpdatedPayload(41.601, -93.609, 4, state)));

        // Assert
        var pill = cut.Find("[data-testid='status-pill']");
        Assert.Contains(expectedLabel, pill.TextContent);
    }

    [Fact]
    public void GivenTrackingPage_WhenRendered_ThenStatusPillShowsOnTheWayInitially()
    {
        // Arrange — AC-4: the initial state on assignment is EnRoute, so the pill reads "On the way" before
        // any position push arrives.
        var cut = RenderPage(Payload());

        // Act
        var pill = cut.Find("[data-testid='status-pill']");

        // Assert
        Assert.Contains("On the way", pill.TextContent);
    }

    [Fact]
    public void GivenTrackingPage_WhenRendered_ThenEtaChipShowsSeededMinutes()
    {
        // Arrange — AC-3: the ETA chip shows the ETA seeded from the RepAssigned payload (9 min).
        var cut = RenderPage(Payload(etaMinutes: 9));

        // Act
        var eta = cut.Find("[data-testid='eta-chip']");

        // Assert
        Assert.Contains("9", eta.TextContent);
    }

    [Fact]
    public async Task GivenTrackingPage_WhenRepPositionUpdated_ThenEtaChipUpdatesToNewMinutes()
    {
        // Arrange — AC-3: a RepPositionUpdated push refreshes the ETA chip without a screen reload. Seed 9,
        // push 4.
        var handler = Handler();
        var cut = RenderPage(Payload(etaMinutes: 9));

        // Act
        await cut.InvokeAsync(() => handler(new RepPositionUpdatedPayload(41.601, -93.609, 4, "EnRoute")));

        // Assert
        var eta = cut.Find("[data-testid='eta-chip']");
        Assert.Contains("4", eta.TextContent);
    }

    [Fact]
    public async Task GivenTrackingPage_WhenRepStateBecomesOnSite_ThenEtaElementAbsentAndArrivedMessagePresent()
    {
        // Arrange — AC-5: once the rep is OnSite the ETA chip is removed from the DOM and the pill reads the
        // arrival message.
        var handler = Handler();
        var cut = RenderPage(Payload());

        // Act
        await cut.InvokeAsync(() => handler(new RepPositionUpdatedPayload(41.601, -93.609, 0, "OnSite")));

        // Assert
        Assert.Empty(cut.FindAll("[data-testid='eta-chip']"));
        Assert.Contains("Your technician has arrived", cut.Find("[data-testid='status-pill']").TextContent);
    }

    [Fact]
    public void GivenTrackingPage_WhenRendered_ThenRootContainerHasSdMapClassAndBottomSheetChild()
    {
        // Arrange — AC-6: the responsive layout is a full-bleed map region (.sd-map) with a bottom-sheet
        // overlay card (data-testid='tracking-sheet'), mirroring ActiveJob's .sd-map / .sd-sheet structure.
        // The pixel-level responsive fidelity is verified by the AI-review render-and-screenshot gate; this
        // asserts the structural contract bUnit can see.
        var cut = RenderPage(Payload());

        // Act
        var map = cut.Find(".sd-map");
        var sheet = cut.Find("[data-testid='tracking-sheet']");

        // Assert
        Assert.NotNull(map);
        Assert.NotNull(sheet);
    }

    [Fact]
    public void GivenTrackingPage_WhenRendered_ThenGoogleMapComponentIsPresent()
    {
        // Arrange — AC-1: the page renders the real FE-024 GoogleMap component (its available-SDK container
        // carries data-testid='google-map'), not a CSS/SVG placeholder.
        var cut = RenderPage(Payload());

        // Act / Assert
        Assert.NotNull(cut.Find("[data-testid='google-map']"));
    }
}
