using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.UI.Features.Maps.Components;

namespace ServiceDelivery.Client.Tests.Maps;

/// <summary>
/// bUnit interop tests for <see cref="GoogleMap"/> (FE-024). The real Google map cannot render under
/// bUnit (ADR-0010 / AC-7), so these tests assert the exact JS-module calls the component issues —
/// function name AND argument values (id/lat/lng/colour/testId/points) — plus the unavailable
/// placeholder branch. <see cref="IMapsLoader"/> is mocked to drive available/unavailable; the JS module
/// is mocked via <see cref="BunitJSModuleInterop"/> exactly as <c>MapsLoaderTests</c> does.
/// </summary>
public class GoogleMapComponentTests : BunitContext
{
    private const string ModulePath =
        "./_content/ServiceDelivery.Client.UI/Features/Maps/googleMap.js";

    private readonly Mock<IMapsLoader> _mapsLoader = new();
    private readonly BunitJSModuleInterop _module;

    public GoogleMapComponentTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _module = JSInterop.SetupModule(ModulePath);
        _module.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(_mapsLoader.Object);
    }

    private void MapsAvailable() =>
        _mapsLoader.Setup(l => l.LoadAsync()).ReturnsAsync(new MapsAvailability(true, null));

    private void MapsUnavailable() =>
        _mapsLoader.Setup(l => l.LoadAsync())
            .ReturnsAsync(new MapsAvailability(false, "Google Maps API key is missing."));

    [Fact]
    public void GivenValidCentreAndZoom_WhenGoogleMapInitialised_ThenInitMapCalledWithCorrectLatLngZoom()
    {
        // Arrange
        MapsAvailable();

        // Act
        Render<GoogleMap>(p => p
            .Add(c => c.Lat, 41.6005)
            .Add(c => c.Lng, -93.6091)
            .Add(c => c.Zoom, 12));

        // Assert
        var invocation = _module.VerifyInvoke("initMap");
        Assert.Equal(41.6005, invocation.Arguments[1]);
        Assert.Equal(-93.6091, invocation.Arguments[2]);
        Assert.Equal(12, invocation.Arguments[3]);
    }

    [Fact]
    public void GivenGoogleMapRendered_WhenAvailable_ThenContainerHasGoogleMapTestId()
    {
        // Arrange
        MapsAvailable();

        // Act
        var cut = Render<GoogleMap>(p => p
            .Add(c => c.Lat, 41.6)
            .Add(c => c.Lng, -93.6)
            .Add(c => c.Zoom, 12));

        // Assert
        Assert.NotNull(cut.Find("[data-testid='google-map']"));
    }

    private IRenderedComponent<GoogleMap> RenderAvailableMap()
    {
        MapsAvailable();
        return Render<GoogleMap>(p => p
            .Add(c => c.Lat, 41.6)
            .Add(c => c.Lng, -93.6)
            .Add(c => c.Zoom, 12));
    }

    [Fact]
    public async Task GivenAnInitialisedMap_WhenAddOrUpdateMarkerCalled_ThenJsAddOrUpdateMarkerInvokedWithCorrectArgs()
    {
        // Arrange
        var cut = RenderAvailableMap();

        // Act
        await cut.Instance.AddOrUpdateMarkerAsync("rep-7", 41.62, -93.71, "#1E88E5", "rep-marker");

        // Assert
        var invocation = _module.VerifyInvoke("addOrUpdateMarker");
        Assert.Equal("rep-7", invocation.Arguments[1]);
        Assert.Equal(41.62, invocation.Arguments[2]);
        Assert.Equal(-93.71, invocation.Arguments[3]);
        Assert.Equal("#1E88E5", invocation.Arguments[4]);
        Assert.Equal("rep-marker", invocation.Arguments[5]);
    }

    [Fact]
    public async Task GivenAMapWithMarker_WhenRemoveMarkerCalled_ThenJsRemoveMarkerInvokedWithId()
    {
        // Arrange
        var cut = RenderAvailableMap();

        // Act
        await cut.Instance.RemoveMarkerAsync("rep-7");

        // Assert
        var invocation = _module.VerifyInvoke("removeMarker");
        Assert.Equal("rep-7", invocation.Arguments[1]);
    }

    [Fact]
    public async Task GivenAnInitialisedMap_WhenAddOrUpdatePolylineCalled_ThenJsAddOrUpdatePolylineInvokedWithCorrectArgs()
    {
        // Arrange
        var cut = RenderAvailableMap();
        var points = new[] { new GpsPoint(41.60, -93.60), new GpsPoint(41.70, -93.50) };

        // Act
        await cut.Instance.AddOrUpdatePolylineAsync("route-1", points, "route-line");

        // Assert
        var invocation = _module.VerifyInvoke("addOrUpdatePolyline");
        Assert.Equal("route-1", invocation.Arguments[1]);
        var passedPoints = Assert.IsAssignableFrom<IEnumerable<GpsPoint>>(invocation.Arguments[2]).ToArray();
        Assert.Equal(2, passedPoints.Length);
        Assert.Equal(new GpsPoint(41.60, -93.60), passedPoints[0]);
        Assert.Equal(new GpsPoint(41.70, -93.50), passedPoints[1]);
        Assert.Equal("route-line", invocation.Arguments[3]);
    }

    [Fact]
    public async Task GivenAMapWithPolyline_WhenRemovePolylineCalled_ThenJsRemovePolylineInvokedWithId()
    {
        // Arrange
        var cut = RenderAvailableMap();

        // Act
        await cut.Instance.RemovePolylineAsync("route-1");

        // Assert
        var invocation = _module.VerifyInvoke("removePolyline");
        Assert.Equal("route-1", invocation.Arguments[1]);
    }

    [Fact]
    public async Task GivenAnInitialisedMap_WhenPanToCalled_ThenJsPanToInvokedWithCorrectLatLng()
    {
        // Arrange
        var cut = RenderAvailableMap();

        // Act
        await cut.Instance.PanToAsync(41.88, -93.62);

        // Assert
        var invocation = _module.VerifyInvoke("panTo");
        Assert.Equal(41.88, invocation.Arguments[1]);
        Assert.Equal(-93.62, invocation.Arguments[2]);
    }

    [Fact]
    public async Task GivenAnInitialisedMap_WhenSetZoomCalled_ThenJsSetZoomInvokedWithCorrectZoom()
    {
        // Arrange
        var cut = RenderAvailableMap();

        // Act
        await cut.Instance.SetZoomAsync(15);

        // Assert
        var invocation = _module.VerifyInvoke("setZoom");
        Assert.Equal(15, invocation.Arguments[1]);
    }

    [Fact]
    public async Task GivenAnInitialisedMap_WhenFitBoundsCalled_ThenJsFitBoundsInvokedWithCorrectPoints()
    {
        // Arrange
        var cut = RenderAvailableMap();
        var points = new[] { new GpsPoint(41.50, -93.80), new GpsPoint(41.90, -93.40) };

        // Act
        await cut.Instance.FitBoundsAsync(points);

        // Assert
        var invocation = _module.VerifyInvoke("fitBounds");
        var passedPoints = Assert.IsAssignableFrom<IEnumerable<GpsPoint>>(invocation.Arguments[1]).ToArray();
        Assert.Equal(2, passedPoints.Length);
        Assert.Equal(new GpsPoint(41.50, -93.80), passedPoints[0]);
        Assert.Equal(new GpsPoint(41.90, -93.40), passedPoints[1]);
    }

    [Fact]
    public async Task GivenEnRouteState_WhenAddOrUpdateMarkerCalledWithEnRouteColour_ThenJsReceivesBlueHex()
    {
        // Arrange — the colour the caller resolves from RepStateColour for an en-route rep must reach the
        // JS marker call unchanged. This exercises the real component→JS plumbing for AC-3, not the
        // RepStateColour lookup (covered separately) — so the test resolves the token then asserts the
        // exact hex landed at the interop boundary.
        var cut = RenderAvailableMap();
        var enRouteColour = RepStateColour.ForState("EnRoute");

        // Act
        await cut.Instance.AddOrUpdateMarkerAsync("rep-3", 41.6, -93.6, enRouteColour, "rep-marker");

        // Assert
        var invocation = _module.VerifyInvoke("addOrUpdateMarker");
        Assert.Equal("#1E88E5", invocation.Arguments[4]);
    }

    [Fact]
    public async Task GivenAnInitialisedMap_WhenDisposeAsyncCalled_ThenJsDisposeMapInvoked()
    {
        // Arrange
        var cut = RenderAvailableMap();

        // Act
        await cut.Instance.DisposeAsync();

        // Assert — disposeMap targets the SAME container that initMap created (no leaked map / handlers).
        var initContainerId = _module.VerifyInvoke("initMap").Arguments[0];
        var disposeContainerId = _module.VerifyInvoke("disposeMap").Arguments[0];
        Assert.Equal(initContainerId, disposeContainerId);
    }

    [Fact]
    public async Task GivenGoogleMapNeverInitialised_WhenDisposeAsyncCalled_ThenNoJsDisposeCall()
    {
        // Arrange — the SDK is unavailable, so OnAfterRenderAsync never imports the module. Disposing the
        // component must not attempt a disposeMap call (no module to tear down) and must not throw.
        MapsUnavailable();
        var cut = Render<GoogleMap>(p => p
            .Add(c => c.Lat, 41.6)
            .Add(c => c.Lng, -93.6)
            .Add(c => c.Zoom, 12));

        // Act
        await cut.Instance.DisposeAsync();

        // Assert
        _module.VerifyNotInvoke("disposeMap");
    }

    [Fact]
    public async Task GivenATestId_WhenAddOrUpdateMarkerCalled_ThenJsReceivesTestId()
    {
        // Arrange — AC-5: the data-testid hook the caller supplies for a marker (e.g. "requester-pin")
        // must reach the JS module so it can stamp it onto the marker element for Playwright/Appium.
        var cut = RenderAvailableMap();

        // Act
        await cut.Instance.AddOrUpdateMarkerAsync("req-1", 41.6, -93.6, "#2E9E5B", "requester-pin");

        // Assert
        var invocation = _module.VerifyInvoke("addOrUpdateMarker");
        Assert.Equal("requester-pin", invocation.Arguments[5]);
    }

    [Fact]
    public async Task GivenATestId_WhenAddOrUpdatePolylineCalled_ThenJsReceivesTestId()
    {
        // Arrange — AC-5: the polyline's data-testid hook (e.g. "route-line") must reach the JS module.
        var cut = RenderAvailableMap();
        var points = new[] { new GpsPoint(41.6, -93.6), new GpsPoint(41.7, -93.5) };

        // Act
        await cut.Instance.AddOrUpdatePolylineAsync("route-9", points, "route-line");

        // Assert
        var invocation = _module.VerifyInvoke("addOrUpdatePolyline");
        Assert.Equal("route-line", invocation.Arguments[3]);
    }

    [Fact]
    public void GivenMapsUnavailable_WhenGoogleMapRendered_ThenPlaceholderIsRendered()
    {
        // Arrange — AC-6: the SDK is unavailable (no key). The component must degrade to a labelled
        // placeholder instead of the live map container, so the user sees a clear message, not a blank box.
        MapsUnavailable();

        // Act
        var cut = Render<GoogleMap>(p => p
            .Add(c => c.Lat, 41.6)
            .Add(c => c.Lng, -93.6)
            .Add(c => c.Zoom, 12));

        // Assert
        Assert.NotNull(cut.Find("[data-testid='map-unavailable']"));
        Assert.Empty(cut.FindAll("[data-testid='google-map']"));
    }

    [Fact]
    public void GivenMapsUnavailable_WhenGoogleMapRendered_ThenInitMapIsNotCalled()
    {
        // Arrange — AC-6: with no SDK, the component must never reach the JS module (no unhandled JS
        // error from calling google.maps with no SDK loaded). initMap must not be invoked.
        MapsUnavailable();

        // Act
        Render<GoogleMap>(p => p
            .Add(c => c.Lat, 41.6)
            .Add(c => c.Lng, -93.6)
            .Add(c => c.Zoom, 12));

        // Assert
        _module.VerifyNotInvoke("initMap");
    }

    [Fact]
    public void GivenMapsUnavailable_WhenGoogleMapRendered_ThenPlaceholderHasMapUnavailableTestId()
    {
        // Arrange — AC-6: the placeholder carries a data-testid so E2E (QUAL-003/004) can assert the
        // graceful-degradation path without a live map.
        MapsUnavailable();

        // Act
        var cut = Render<GoogleMap>(p => p
            .Add(c => c.Lat, 41.6)
            .Add(c => c.Lng, -93.6)
            .Add(c => c.Zoom, 12));

        // Assert
        var placeholder = cut.Find("[data-testid='map-unavailable']");
        Assert.Contains("Map unavailable", placeholder.TextContent);
    }

    [Fact]
    public void GivenTheGoogleMapModule_WhenItsSourceIsRead_ThenItExportsEveryInteropFunction()
    {
        // Arrange — the bUnit tests mock the JS module, so they cannot exercise the real googleMap.js. As
        // FE-025 does for mapsLoader.js, this asserts the committed module's source actually exports each
        // function the C# component invokes — guarding against a renamed/missing export the mocks hide.
        var modulePath = RepoRoot.Combine(
            "src", "ServiceDelivery.Client.UI", "wwwroot", "Features", "Maps", "googleMap.js");

        // Act
        var module = File.ReadAllText(modulePath);

        // Assert
        Assert.Contains("export function initMap", module);
        Assert.Contains("export function disposeMap", module);
        Assert.Contains("export function addOrUpdateMarker", module);
        Assert.Contains("export function removeMarker", module);
        Assert.Contains("export function addOrUpdatePolyline", module);
        Assert.Contains("export function removePolyline", module);
        Assert.Contains("export function panTo", module);
        Assert.Contains("export function setZoom", module);
        Assert.Contains("export function fitBounds", module);
    }

    [Fact]
    public void GivenTheGoogleMapModule_WhenItsSourceIsRead_ThenMarkersUseAdvancedMarkerElementAndCarryDataTestId()
    {
        // Arrange — AC-5 + constraint #4: markers must use the marker library's AdvancedMarkerElement (the
        // classic Marker cannot host a DOM element), and the marker's element must carry the data-testid so
        // Playwright/Appium can locate it. fitBounds must use LatLngBounds to frame the supplied points.
        var modulePath = RepoRoot.Combine(
            "src", "ServiceDelivery.Client.UI", "wwwroot", "Features", "Maps", "googleMap.js");

        // Act
        var module = File.ReadAllText(modulePath);

        // Assert
        Assert.Contains("AdvancedMarkerElement", module);
        Assert.Contains("data-testid", module);
        Assert.Contains("LatLngBounds", module);
    }
}
