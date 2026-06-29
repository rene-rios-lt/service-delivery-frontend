using Bunit;
using Bunit.JSInterop;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.ViewModels;
using ServiceDelivery.Client.UI.Features.ServiceRep.Pages;

namespace ServiceDelivery.Client.Tests.ServiceRep;

/// <summary>
/// FE-026 map-interop tests: asserts the imperative GoogleMap (FE-024) API calls the ActiveJob page issues
/// on first render and on each poll / state transition (AC-2 rep marker, AC-3 requester pin + route
/// polyline, AC-4 fitBounds / panTo / setZoom and marker colours). The real google.maps.Map cannot render
/// under bUnit, so — exactly as GoogleMapComponentTests does — the googleMap.js module is mocked and these
/// tests assert the exact module function calls (name + argument values) that flow through the embedded
/// GoogleMap. Kept separate from ActiveJobComponentTests so that class stays focused on chip / button /
/// bottom-sheet behaviour (Single Responsibility at the test level).
/// </summary>
public class ActiveJobMapInteropTests : BunitContext
{
    private const string ModulePath =
        "./_content/ServiceDelivery.Client.UI/Features/Maps/googleMap.js";

    private readonly Mock<IActiveJobService> _activeJobService = new();
    private readonly Mock<IRepHubService> _repHub = new();
    private readonly Mock<IArriveService> _arriveService = new();
    private readonly Mock<ICompleteJobService> _completeJobService = new();
    private readonly Mock<IPersonaNavigator> _navigator = new();
    private readonly Mock<ISnackbar> _snackbar = new();
    private readonly Mock<IMapsLoader> _mapsLoader = new();
    private ActiveJobViewModel _viewModel = null!;
    private readonly BunitJSModuleInterop _module;

    private readonly Mock<ITokenStore> _tokenStore = new();
    private readonly Mock<ILogoutSideEffect> _sideEffect = new();
    private readonly Mock<IReleaseVehicleAction> _releaseAction = new();
    private readonly Mock<IShellPresentation> _presentation = new();
    private readonly Mock<IPersonaNavigator> _navigatorForShell = new();

    public ActiveJobMapInteropTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _module = JSInterop.SetupModule(ModulePath);
        _module.Mode = JSRuntimeMode.Loose;
    }

    private static ActiveJobContext Context(
        string requesterName = "Marcus Webb",
        string dtcTitle = "P0700 · Transmission Control Fault",
        double requesterLat = 41.60,
        double requesterLng = -93.60,
        double repLat = 41.70,
        double repLng = -93.50,
        int etaMinutes = 9,
        double distanceMiles = 8.1,
        string repState = "EnRoute",
        string tier = "Gold") =>
        new(Guid.NewGuid(), requesterName, dtcTitle, requesterLat, requesterLng,
            repLat, repLng, etaMinutes, distanceMiles, repState, tier);

    private IRenderedComponent<ActiveJob> RenderPage(ActiveJobContext context)
    {
        Services.AddMudServices();
        Services.AddSingleton(_snackbar.Object);
        _mapsLoader.Setup(l => l.LoadAsync()).ReturnsAsync(new MapsAvailability(true, null));
        Services.AddSingleton(_mapsLoader.Object);
        _activeJobService.Setup(s => s.GetActiveJobAsync()).ReturnsAsync(context);
        Services.AddSingleton(_activeJobService.Object);
        Services.AddSingleton(_repHub.Object);
        Services.AddSingleton(_arriveService.Object);
        Services.AddSingleton(_completeJobService.Object);
        _viewModel = new ActiveJobViewModel(
            _activeJobService.Object, _repHub.Object, _arriveService.Object,
            _completeJobService.Object, _navigator.Object);
        Services.AddSingleton(_viewModel);

        _presentation.SetupGet(p => p.MenuStyle).Returns(ShellMenuStyle.Drawer);
        var shell = new ShellViewModel(
            _tokenStore.Object, _navigatorForShell.Object, _sideEffect.Object,
            _releaseAction.Object, _presentation.Object, new PersonaMenuFactory());
        shell.Load(new UserProfile(
            Guid.NewGuid(), "Rosa Alvarez", UserRole.ServiceRep, ServiceTier.None, Guid.NewGuid()));
        Services.AddSingleton(shell);

        return Render<ActiveJob>();
    }

    [Fact]
    public async Task GivenAPositionPoll_WhenViewModelUpdates_ThenAddOrUpdateMarkerCalledWithRepCoords()
    {
        // Arrange
        // AC-2: the rep marker is placed at the rep's live position and moves on each poll. The poll returns
        // new rep coordinates; the page must re-place the "rep" marker at those coordinates.
        var cut = RenderPage(Context(repLat: 41.70, repLng: -93.50));
        _activeJobService.Setup(s => s.GetActiveJobAsync())
            .ReturnsAsync(Context(repLat: 41.82, repLng: -93.41));

        // Act
        await cut.InvokeAsync(() => cut.Instance.PollOnceAsync());

        // Assert
        var invocation = LastMarkerCall("rep");
        Assert.Equal(41.82, invocation.Arguments[2]);
        Assert.Equal(-93.41, invocation.Arguments[3]);
    }

    [Fact]
    public void GivenEnRouteState_WhenMapInitialised_ThenRepMarkerColourIsEnRouteBlue()
    {
        // Arrange
        // AC-2: on initial render an en-route rep's marker is placed in the design-system EnRoute blue.
        var cut = RenderPage(Context(repState: "EnRoute"));

        // Act
        // (initial render places the overlays)

        // Assert
        var invocation = LastMarkerCall("rep");
        Assert.Equal("#1E88E5", invocation.Arguments[4]);
    }

    [Fact]
    public void GivenActiveJobLoad_WhenMapInitialised_ThenRequesterMarkerPlacedAtRequesterCoords()
    {
        // Arrange
        // AC-3: the requester pin is placed at the requester's location on initial render while en route.
        var cut = RenderPage(Context(repState: "EnRoute", requesterLat: 41.60, requesterLng: -93.60));

        // Act
        // (initial render places the overlays)

        // Assert
        var invocation = LastMarkerCall("requester");
        Assert.Equal(41.60, invocation.Arguments[2]);
        Assert.Equal(-93.60, invocation.Arguments[3]);
        Assert.Equal("requester-pin", invocation.Arguments[5]);
    }

    [Fact]
    public void GivenEnRouteState_WhenMapInitialised_ThenRoutePolylineAddedWithRepAndRequesterPoints()
    {
        // Arrange
        // AC-3: while en route the route polyline connects the rep position to the requester position.
        var cut = RenderPage(Context(
            repState: "EnRoute",
            repLat: 41.70, repLng: -93.50,
            requesterLat: 41.60, requesterLng: -93.60));

        // Act
        // (initial render places the overlays)

        // Assert
        var invocation = LastPolylineCall("route");
        var points = ((IEnumerable<GpsPoint>)invocation.Arguments[2]!).ToArray();
        Assert.Equal(new GpsPoint(41.70, -93.50), points[0]);
        Assert.Equal(new GpsPoint(41.60, -93.60), points[1]);
        Assert.Equal("route-line", invocation.Arguments[3]);
    }

    [Fact]
    public async Task GivenRepArrivesOnSite_WhenStateChanges_ThenRemovePolylineCalledForRoute()
    {
        // Arrange
        // AC-3: once the rep arrives on-site the route polyline is removed from the map.
        var cut = RenderPage(Context(repState: "Within15Miles"));

        // Act
        await cut.InvokeAsync(() => _viewModel.ArriveAsync());

        // Assert
        Assert.Contains(_module.Invocations, i =>
            i.Identifier == "removePolyline" && (string)i.Arguments[1]! == "route");
    }

    [Fact]
    public void GivenEnRouteState_WhenMapInitialised_ThenFitBoundsCalledWithRepAndRequesterPoints()
    {
        // Arrange
        // AC-4: while en route the map frames both the rep and the requester so the whole trip is visible.
        var cut = RenderPage(Context(
            repState: "EnRoute",
            repLat: 41.70, repLng: -93.50,
            requesterLat: 41.60, requesterLng: -93.60));

        // Act
        // (initial render frames the map)

        // Assert
        var invocation = LastFitBoundsCall();
        var points = ((IEnumerable<GpsPoint>)invocation.Arguments[1]!).ToArray();
        Assert.Equal(new GpsPoint(41.70, -93.50), points[0]);
        Assert.Equal(new GpsPoint(41.60, -93.60), points[1]);
    }

    [Fact]
    public async Task GivenWithin15MilesState_WhenMapInitialised_ThenFitBoundsCalledWithRepAndRequesterPoints()
    {
        // Arrange
        // AC-4: within 15 miles the rep and requester are still both shown (fitBounds), as en route.
        var cut = RenderPage(Context(repState: "EnRoute",
            repLat: 41.70, repLng: -93.50, requesterLat: 41.60, requesterLng: -93.60));

        // Act
        await cut.InvokeAsync(() => _viewModel.PollPositionAsync());
        _activeJobService.Setup(s => s.GetActiveJobAsync()).ReturnsAsync(Context(
            repState: "Within15Miles", repLat: 41.70, repLng: -93.50,
            requesterLat: 41.60, requesterLng: -93.60));
        await cut.InvokeAsync(() => cut.Instance.PollOnceAsync());

        // Assert
        var invocation = LastFitBoundsCall();
        var points = ((IEnumerable<GpsPoint>)invocation.Arguments[1]!).ToArray();
        Assert.Equal(new GpsPoint(41.70, -93.50), points[0]);
        Assert.Equal(new GpsPoint(41.60, -93.60), points[1]);
    }

    [Fact]
    public async Task GivenOnSiteState_WhenRepArrives_ThenPanToRepCoordsAndSetZoom15Called()
    {
        // Arrange
        // AC-4: on-site the map recentres on the rep at a street-level zoom (15), since the requester pin
        // is gone and the rep is doing close-up work.
        var cut = RenderPage(Context(repState: "Within15Miles", repLat: 41.65, repLng: -93.55));

        // Act
        await cut.InvokeAsync(() => _viewModel.ArriveAsync());

        // Assert
        var panTo = _module.Invocations.Last(i => i.Identifier == "panTo");
        Assert.Equal(41.65, panTo.Arguments[1]);
        Assert.Equal(-93.55, panTo.Arguments[2]);
        var setZoom = _module.Invocations.Last(i => i.Identifier == "setZoom");
        Assert.Equal(15, setZoom.Arguments[1]);
    }

    [Fact]
    public async Task GivenWithin15MilesState_WhenPollUpdates_ThenRepMarkerColourIsWithin15Yellow()
    {
        // Arrange
        // AC-4: once the poll reports Within15Miles the rep marker recolours to the design-system yellow.
        var cut = RenderPage(Context(repState: "EnRoute"));
        _activeJobService.Setup(s => s.GetActiveJobAsync())
            .ReturnsAsync(Context(repState: "Within15Miles"));

        // Act
        await cut.InvokeAsync(() => cut.Instance.PollOnceAsync());

        // Assert
        var invocation = LastMarkerCall("rep");
        Assert.Equal("#F4A100", invocation.Arguments[4]);
    }

    [Fact]
    public async Task GivenOnSiteState_WhenRepArrives_ThenRepMarkerColourIsOnSiteRed()
    {
        // Arrange
        // AC-4: when the rep marks arrival the rep marker recolours to the design-system on-site red.
        var cut = RenderPage(Context(repState: "Within15Miles"));

        // Act
        await cut.InvokeAsync(() => _viewModel.ArriveAsync());

        // Assert
        var invocation = LastMarkerCall("rep");
        Assert.Equal("#E5392F", invocation.Arguments[4]);
    }

    // Returns the most recent fitBounds module call.
    private JSRuntimeInvocation LastFitBoundsCall() =>
        _module.Invocations.Last(i => i.Identifier == "fitBounds");

    // Returns the most recent addOrUpdateMarker module call for the given marker id (e.g. "rep").
    private JSRuntimeInvocation LastMarkerCall(string markerId) =>
        _module.Invocations.Last(i =>
            i.Identifier == "addOrUpdateMarker" && (string)i.Arguments[1]! == markerId);

    // Returns the most recent addOrUpdatePolyline module call for the given polyline id (e.g. "route").
    private JSRuntimeInvocation LastPolylineCall(string polylineId) =>
        _module.Invocations.Last(i =>
            i.Identifier == "addOrUpdatePolyline" && (string)i.Arguments[1]! == polylineId);
}
