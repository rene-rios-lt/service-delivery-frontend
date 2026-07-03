using Bunit;
using Bunit.JSInterop;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.ViewModels;
using ServiceDelivery.Client.UI.Features.Requester.Pages;

namespace ServiceDelivery.Client.Tests.Requester;

/// <summary>
/// FE-017 map-interop tests: asserts the imperative GoogleMap (FE-024) API calls the RequesterTracking page
/// issues on first render and on each RepPositionUpdated push (AC-1 fixed requester pin + moving rep marker
/// + connecting route polyline, AC-4 rep marker recolour per state, AC-5 polyline + requester pin removed
/// once on-site). The real google.maps.Map cannot render under bUnit, so — exactly as ActiveJobMapInteropTests
/// does — the googleMap.js module is mocked and these tests assert the exact module function calls (name +
/// argument values) that flow through the embedded GoogleMap. Kept separate from RequesterTrackingComponentTests
/// so that class stays focused on the DOM chrome (Single Responsibility at the test level).
/// </summary>
public class RequesterTrackingMapInteropTests : BunitContext
{
    private const string ModulePath =
        "./_content/ServiceDelivery.Client.UI/Features/Maps/googleMap.js";

    private readonly Mock<IRepAssignedStore> _store = new();
    private readonly Mock<IRequesterHubService> _hub = new();
    private readonly Mock<IMapsLoader> _mapsLoader = new();
    private RequesterTrackingViewModel _viewModel = null!;
    private readonly BunitJSModuleInterop _module;

    // ShellViewModel collaborators — the tracking page sets the shared app-bar title on init, so the page
    // test registers a real ShellViewModel (mirrors ActiveJobMapInteropTests).
    private readonly Mock<ITokenStore> _tokenStore = new();
    private readonly Mock<IPersonaNavigator> _navigatorForShell = new();
    private readonly Mock<ILogoutSideEffect> _sideEffect = new();
    private readonly Mock<IReleaseVehicleAction> _releaseAction = new();
    private readonly Mock<IShellPresentation> _presentation = new();

    public RequesterTrackingMapInteropTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _module = JSInterop.SetupModule(ModulePath);
        _module.Mode = JSRuntimeMode.Loose;
    }

    private static RepAssignedPayload Payload(
        double latitude = 41.601,
        double longitude = -93.609) =>
        new(Guid.NewGuid(), "Jordan Tran", 9, latitude, longitude, "IA-4471");

    private IRenderedComponent<RequesterTracking> RenderPage(RepAssignedPayload payload)
    {
        Services.AddMudServices();
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
    public void GivenTrackingLoad_WhenMapInitialised_ThenRequesterMarkerPlacedAtRequesterCoordsAsFixedPin()
    {
        // Arrange — AC-1: the requester's own location is a fixed dark pin (data-testid='requester-pin')
        // placed on first render.
        var cut = RenderPage(Payload(latitude: 41.601, longitude: -93.609));

        // Act / Assert (initial render places the overlays)
        var invocation = LastMarkerCall("requester");
        Assert.Equal(41.601, invocation.Arguments[2]);
        Assert.Equal(-93.609, invocation.Arguments[3]);
        Assert.Equal("#2B2F3A", invocation.Arguments[4]);
        Assert.Equal("requester-pin", invocation.Arguments[5]);
    }

    [Fact]
    public async Task GivenRepPositionUpdated_WhenPushed_ThenRepMarkerMovesToNewCoords()
    {
        // Arrange — AC-1: the rep marker (data-testid='rep-marker') tracks each RepPositionUpdated push.
        var handler = Handler();
        var cut = RenderPage(Payload());

        // Act
        await cut.InvokeAsync(() => handler(new RepPositionUpdatedPayload(41.820, -93.410, 4, "EnRoute")));

        // Assert
        var invocation = LastMarkerCall("rep");
        Assert.Equal(41.820, invocation.Arguments[2]);
        Assert.Equal(-93.410, invocation.Arguments[3]);
        Assert.Equal("rep-marker", invocation.Arguments[5]);
    }

    [Fact]
    public async Task GivenEnRouteState_WhenRepPositionUpdated_ThenRepMarkerColourIsEnRouteBlue()
    {
        // Arrange — AC-4: an en-route rep's marker is the design-system EnRoute blue.
        var handler = Handler();
        var cut = RenderPage(Payload());

        // Act
        await cut.InvokeAsync(() => handler(new RepPositionUpdatedPayload(41.601, -93.609, 4, "EnRoute")));

        // Assert
        var invocation = LastMarkerCall("rep");
        Assert.Equal("#1E88E5", invocation.Arguments[4]);
    }

    [Fact]
    public async Task GivenWithin15MilesState_WhenRepPositionUpdated_ThenRepMarkerColourIsWithin15Yellow()
    {
        // Arrange — AC-4: within 15 miles the rep marker recolours to the design-system yellow.
        var handler = Handler();
        var cut = RenderPage(Payload());

        // Act
        await cut.InvokeAsync(() => handler(new RepPositionUpdatedPayload(41.601, -93.609, 4, "Within15Miles")));

        // Assert
        var invocation = LastMarkerCall("rep");
        Assert.Equal("#F4A100", invocation.Arguments[4]);
    }

    [Fact]
    public async Task GivenEnRouteState_WhenRepPositionUpdated_ThenRoutePolylineConnectsRepAndRequester()
    {
        // Arrange — AC-1: while en route the route polyline (data-testid='route-line') connects the moving
        // rep position to the fixed requester position.
        var handler = Handler();
        var cut = RenderPage(Payload(latitude: 41.601, longitude: -93.609));

        // Act
        await cut.InvokeAsync(() => handler(new RepPositionUpdatedPayload(41.820, -93.410, 4, "EnRoute")));

        // Assert
        var invocation = LastPolylineCall("route");
        var points = ((IEnumerable<GpsPoint>)invocation.Arguments[2]!).ToArray();
        Assert.Equal(new GpsPoint(41.820, -93.410), points[0]);
        Assert.Equal(new GpsPoint(41.601, -93.609), points[1]);
        Assert.Equal("route-line", invocation.Arguments[3]);
    }

    [Fact]
    public async Task GivenRepArrivesOnSite_WhenStateChanges_ThenRemovePolylineAndRemoveRequesterMarkerCalled()
    {
        // Arrange — AC-5: once the rep is OnSite the trip is over — the route polyline and the fixed
        // requester pin are removed so only the rep marker remains over the work site.
        var handler = Handler();
        var cut = RenderPage(Payload());

        // Act
        await cut.InvokeAsync(() => handler(new RepPositionUpdatedPayload(41.601, -93.609, 0, "OnSite")));

        // Assert
        Assert.Contains(_module.Invocations, i =>
            i.Identifier == "removePolyline" && (string)i.Arguments[1]! == "route");
        Assert.Contains(_module.Invocations, i =>
            i.Identifier == "removeMarker" && (string)i.Arguments[1]! == "requester");
    }

    [Fact]
    public async Task GivenOnSiteState_WhenRepArrives_ThenRepMarkerColourIsOnSiteRed()
    {
        // Arrange — AC-4/AC-5: when the rep is on-site the rep marker recolours to the design-system red.
        var handler = Handler();
        var cut = RenderPage(Payload());

        // Act
        await cut.InvokeAsync(() => handler(new RepPositionUpdatedPayload(41.601, -93.609, 0, "OnSite")));

        // Assert
        var invocation = LastMarkerCall("rep");
        Assert.Equal("#E5392F", invocation.Arguments[4]);
    }

    // Returns the most recent addOrUpdateMarker module call for the given marker id (e.g. "rep").
    private JSRuntimeInvocation LastMarkerCall(string markerId) =>
        _module.Invocations.Last(i =>
            i.Identifier == "addOrUpdateMarker" && (string)i.Arguments[1]! == markerId);

    // Returns the most recent addOrUpdatePolyline module call for the given polyline id (e.g. "route").
    private JSRuntimeInvocation LastPolylineCall(string polylineId) =>
        _module.Invocations.Last(i =>
            i.Identifier == "addOrUpdatePolyline" && (string)i.Arguments[1]! == polylineId);
}
