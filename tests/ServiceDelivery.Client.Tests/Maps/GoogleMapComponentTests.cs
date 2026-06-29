using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Bunit;
using Microsoft.AspNetCore.Components;
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

    [Fact]
    public void GivenGoogleMapRendered_WhenAvailable_ThenContainerCarriesSdGoogleMapSizingClass()
    {
        // Arrange — cycle-3 BLOCKED fix (FE-026): the map div must carry the sd-google-map class, which the
        // component's own scoped CSS (GoogleMap.razor.css) sizes (width/height/min-height). FE-026 moved the
        // map div into this child component, so it no longer inherits the consuming page's scoped .sd-map
        // height rule — without sd-google-map sizing the container collapses to 0px and google.maps paints
        // into a flat grey box (map.Displayed == False, no tiles).
        MapsAvailable();

        // Act
        var cut = Render<GoogleMap>(p => p
            .Add(c => c.Lat, 41.6)
            .Add(c => c.Lng, -93.6)
            .Add(c => c.Zoom, 12));

        // Assert
        Assert.Contains("sd-google-map", cut.Find("[data-testid='google-map']").GetAttribute("class"));
    }

    [Fact]
    public void GivenMapsUnavailable_WhenGoogleMapRendered_ThenPlaceholderCarriesSdGoogleMapSizingClass()
    {
        // Arrange — cycle-3 BLOCKED fix (FE-026): the FE-025 'map unavailable' placeholder must be sized
        // too (it shares the sd-google-map sizing hook) so it stays visible rather than collapsing to 0px.
        MapsUnavailable();

        // Act
        var cut = Render<GoogleMap>(p => p
            .Add(c => c.Lat, 41.6)
            .Add(c => c.Lng, -93.6)
            .Add(c => c.Zoom, 12));

        // Assert
        Assert.Contains("sd-google-map", cut.Find("[data-testid='map-unavailable']").GetAttribute("class"));
    }

    [Fact]
    public void GivenTheGoogleMapComponent_WhenItsScopedCssIsRead_ThenSdGoogleMapHasAHeightRule()
    {
        // Arrange — cycle-3 BLOCKED fix (FE-026): GoogleMap must carry its OWN height (GoogleMap.razor.css)
        // independent of any host page's scoped CSS, so FE-024's map is self-sizing for every consumer. The
        // bUnit renderer ignores scoped CSS, so this source-read guard asserts the rule exists and sizes the
        // sd-google-map div with a real height (the live Appium render is the ultimate proof).
        var cssPath = RepoRoot.Combine(
            "src", "ServiceDelivery.Client.UI", "Features", "Maps", "Components", "GoogleMap.razor.css");

        // Act
        Assert.True(File.Exists(cssPath), $"Expected GoogleMap.razor.css at '{cssPath}'.");
        var css = File.ReadAllText(cssPath);

        // Assert
        Assert.Contains(".sd-google-map", css);
        Assert.Contains("height", css);
        Assert.Contains("min-height", css);
    }

    [Fact]
    public void GivenInteractiveFalse_WhenGoogleMapInitialised_ThenInitMapReceivesGestureHandlingNone()
    {
        // Arrange — FE-027 (checkpoint #1 decision A): a read-only map (the job-offer screen) must lock out
        // panning/zooming so the rep cannot drag away from the requester pin during the countdown. The
        // component threads Interactive=false into initMap as the gestureHandling option 'none'.
        MapsAvailable();

        // Act
        Render<GoogleMap>(p => p
            .Add(c => c.Lat, 41.6)
            .Add(c => c.Lng, -93.6)
            .Add(c => c.Zoom, 12)
            .Add(c => c.Interactive, false));

        // Assert
        var invocation = _module.VerifyInvoke("initMap");
        Assert.Equal("none", invocation.Arguments[4]);
    }

    [Fact]
    public void GivenInteractiveDefault_WhenGoogleMapInitialised_ThenInitMapDoesNotReceiveGestureHandlingNone()
    {
        // Arrange — FE-027: the default map (ActiveJob / Dispatcher) stays interactive (backwards-compatible).
        // With Interactive unset (defaulting to true) the gestureHandling option must NOT be 'none'.
        MapsAvailable();

        // Act
        Render<GoogleMap>(p => p
            .Add(c => c.Lat, 41.6)
            .Add(c => c.Lng, -93.6)
            .Add(c => c.Zoom, 12));

        // Assert
        var invocation = _module.VerifyInvoke("initMap");
        Assert.NotEqual("none", invocation.Arguments[4]);
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
    public void GivenAnOnMapReadyCallback_WhenMapInitialised_ThenCallbackIsInvokedAfterInitMap()
    {
        // Arrange — FE-026 needs a deterministic signal that the map is ready before it places overlays
        // (the parent's OnAfterRenderAsync runs before this child's async module import completes). The
        // OnMapReady callback fires once initMap has been invoked, so the consumer can place its
        // markers/polyline knowing the JS module is live.
        MapsAvailable();
        var initialised = false;

        // Act
        Render<GoogleMap>(p => p
            .Add(c => c.Lat, 41.6)
            .Add(c => c.Lng, -93.6)
            .Add(c => c.Zoom, 12)
            .Add(c => c.OnMapReady, EventCallback.Factory.Create(this, () => initialised = true)));

        // Assert
        _module.VerifyInvoke("initMap");
        Assert.True(initialised);
    }

    [Fact]
    public void GivenMapsUnavailable_WhenOnMapReadyCallbackSupplied_ThenCallbackIsNotInvoked()
    {
        // Arrange — with no SDK the module never imports, so there is no ready signal. The callback must
        // not fire (a consumer must not place overlays against a map that was never initialised).
        MapsUnavailable();
        var initialised = false;

        // Act
        Render<GoogleMap>(p => p
            .Add(c => c.Lat, 41.6)
            .Add(c => c.Lng, -93.6)
            .Add(c => c.Zoom, 12)
            .Add(c => c.OnMapReady, EventCallback.Factory.Create(this, () => initialised = true)));

        // Assert
        Assert.False(initialised);
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

        // Assert — initMap is exported async (it awaits importLibrary before constructing the map), so the
        // export is matched without pinning the sync/async modifier; the rest are synchronous exports.
        Assert.Contains("export async function initMap", module);
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

    [Fact]
    public void GivenTheGoogleMapModule_WhenItsSourceIsRead_ThenPolylineTestIdIsADomAttribute()
    {
        // Arrange — FE-026 AC-6: a google.maps.Polyline cannot host a DOM element the way an
        // AdvancedMarkerElement can, so its testId was only stored as a Maps property
        // (polyline.set("testId", ...)) — invisible to the CSS selectors Appium/Playwright use. The fix
        // attaches an invisible <div> carrying the data-testid attribute to the map container when the
        // polyline is created, and removes it on removePolyline / disposeMap. This guards the committed
        // source: the polyline path must stamp the testId as a real DOM data-testid attribute, not only as
        // a Maps property.
        var modulePath = RepoRoot.Combine(
            "src", "ServiceDelivery.Client.UI", "wwwroot", "Features", "Maps", "googleMap.js");

        // Act
        var module = File.ReadAllText(modulePath);
        var polylineBody = module[module.IndexOf("export function addOrUpdatePolyline")..module.IndexOf("export function removePolyline")];

        // Assert
        Assert.Contains("setAttribute(\"data-testid\"", polylineBody);
        Assert.DoesNotContain("polyline.set(\"testId\"", polylineBody);
    }

    [Fact]
    public void GivenTheGoogleMapModule_WhenItsSourceIsRead_ThenInitMapAwaitsImportLibraryBeforeConstructingTheMap()
    {
        // Arrange — BLOCKED finding (FE-026): with loading=async the SDK bootstrap script's onload does not
        // guarantee the `maps` / `marker` classes are usable yet; only `google.maps.importLibrary(...)`
        // resolving does. initMap must await importLibrary for both libraries BEFORE `new google.maps.Map`,
        // so the map is never constructed against an undefined class (which threw the unhandled Blazor
        // error / collapsed grey box). This guards the committed source: the importLibrary awaits must
        // appear ahead of the Map construction.
        var modulePath = RepoRoot.Combine(
            "src", "ServiceDelivery.Client.UI", "wwwroot", "Features", "Maps", "googleMap.js");

        // Act
        var module = File.ReadAllText(modulePath);
        var initMapBody = module[module.IndexOf("function initMap")..module.IndexOf("export function disposeMap")];

        // Assert
        Assert.Contains("await google.maps.importLibrary(\"maps\")", initMapBody);
        Assert.Contains("await google.maps.importLibrary(\"marker\")", initMapBody);
        Assert.True(
            initMapBody.IndexOf("importLibrary") < initMapBody.IndexOf("new google.maps.Map"),
            "initMap must await importLibrary before constructing the google.maps.Map.");
    }
}
