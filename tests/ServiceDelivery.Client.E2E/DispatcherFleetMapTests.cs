using ServiceDelivery.Client.E2E.Helpers;

namespace ServiceDelivery.Client.E2E;

/// <summary>
/// FE-003 Playwright coverage (ACs 1 web-path, 2, 4, 5, 6, 9) against the live Web host. Drives the dispatcher
/// fleet map as a black box, asserting only on <c>data-testid</c> selectors, the invisible
/// <c>data-fleet-count</c> hook (the google.maps markers are JS-rendered and not reliably CSS-queryable, so
/// the count attribute is the deterministic "fleet rendered" signal), and each marker's <c>data-lat</c>/
/// <c>data-lng</c> (the AC-4 real-time scenario captures a marker's position and polls for a hub-delivered
/// change). The fleet is positioned via the backend before the marker/count assertions so at least one vehicle
/// reports a live position; the running simulator then keeps positions flowing. Authored but NOT executed in
/// the /master pipeline — run via
/// <c>scripts/local/test-playwright.sh</c> (or <c>test-e2e.sh</c>) against a live system with a real Google
/// Maps key configured.
/// </summary>
[TestFixture]
public sealed class DispatcherFleetMapTests : E2ETestBase
{
    // Iowa map centre (matches FleetMap's centre) — positioning the fleet here puts every vehicle inside
    // the map's initial statewide view.
    private const double IowaLat = 41.60;
    private const double IowaLng = -93.60;

    private static string BackendBaseUrl =>
        Environment.GetEnvironmentVariable("E2E_BACKEND_URL") ?? "http://localhost:5180";

    [Test]
    public async Task GivenValidDispatcherCredentials_WhenLoginSubmittedOnWebHost_ThenDispatcherPageLoadsWithoutError()
    {
        // Arrange & Act — AC-1 (web path): dispatcher login must complete to /dispatcher with no crash.
        await LoginAsDispatcherAsync();

        // Assert
        await Page.WaitForSelectorAsync("[data-testid='dispatcher-dashboard']");
        Assert.That(Page.Url, Does.Contain("/dispatcher"));
        // The web host ships the stock #blazor-error-ui banner permanently in the DOM (index.html), hidden via
        // display:none until Blazor flips it to display:block on an unhandled exception. A plain count/text match
        // would always match the hidden markup and never pass — assert it is not VISIBLE so the guard still fails
        // when the banner is actually shown (AC-1 crash-free-login guard).
        Assert.That(
            await Page.Locator("#blazor-error-ui").IsVisibleAsync(), Is.False,
            "The Blazor unhandled-error banner should not be visible after a crash-free dispatcher login.");
    }

    [Test]
    public async Task GivenAuthenticatedDispatcher_WhenFleetMapLoads_ThenFleetMarkersAreVisible()
    {
        // Arrange — AC-2: position the fleet so vehicles report a live position, then log in.
        BackendApiHelper.PositionFleetAt(BackendBaseUrl, IowaLat, IowaLng);
        await LoginAsDispatcherAsync();

        // Act — wait for the SignalR-driven fleet count to climb above zero (markers are JS-rendered, so the
        // invisible count hook is the reliable signal that the fleet rendered).
        await Page.WaitForFunctionAsync(
            "() => { const el = document.querySelector(\"[data-testid='fleet-map-panel']\");" +
            " return el && parseInt(el.getAttribute('data-fleet-count') || '0') > 0; }",
            null,
            new PageWaitForFunctionOptions { Timeout = 15_000 });

        // Assert
        var panel = await Page.WaitForSelectorAsync("[data-testid='fleet-map-panel']");
        var count = int.Parse(await panel!.GetAttributeAsync("data-fleet-count") ?? "0");
        Assert.That(count, Is.GreaterThan(0));
    }

    // A distinctly different Iowa coordinate to re-position the fleet to, forcing an unambiguous
    // position change. Still inside the statewide zoom-7 view and a valid (non-Offline) state, so the
    // marker stays rendered — it just moves.
    private const double MovedLat = 42.02;
    private const double MovedLng = -92.90;

    [Test]
    public async Task GivenAuthenticatedDispatcher_WhenAVehiclePositionIsBroadcastOverTheHub_ThenTheRenderedMarkerMoves()
    {
        // Arrange — AC-4 real-time delivery. This is the ONE scenario that fails on a dead VehiclePositionHub:
        // the initial marker position comes from the REST snapshot (LoadAsync), but the ONLY path that moves a
        // marker afterwards is a VehiclePositionUpdated event over the hub — no REST re-fetch happens. So a
        // changed marker position proves the hub delivered. The other scenarios assert data-fleet-count, which
        // the REST snapshot alone populates, so they stay green even with the wrong hub path (cycle-1 defect).
        BackendApiHelper.PositionFleetAt(BackendBaseUrl, IowaLat, IowaLng);
        await LoginAsDispatcherAsync();

        // Capture a specific rendered marker's live position (bounded wait — never a bare lookup, per BUG-048).
        var marker = await Page.WaitForSelectorAsync(
            "[data-testid^='fleet-marker-']", new() { Timeout = 15_000 });
        var markerTestId = await marker!.GetAttributeAsync("data-testid");
        var markerSelector = $"[data-testid='{markerTestId}']";
        var initialLat = await marker.GetAttributeAsync("data-lat");
        var initialLng = await marker.GetAttributeAsync("data-lng");
        Assert.That(initialLat, Is.Not.Null.And.Not.Empty, "The rendered marker must expose its live position.");

        // Act — post a new distinct position for the whole fleet as the Simulator account. The backend
        // broadcasts VehiclePositionUpdated over VehiclePositionHub for each vehicle; nothing re-runs the REST
        // snapshot, so the map can only learn the new position through the hub.
        BackendApiHelper.PositionFleetAt(BackendBaseUrl, MovedLat, MovedLng);

        // Assert — bounded poll (the live simulator also ticks positions every ~3s, so the 15 s budget is
        // ample) for THIS marker's data-lat/data-lng to change from the captured values. A dead hub never
        // delivers the update, so this times out and fails — exactly the cycle-1 defect this scenario guards.
        await Page.WaitForFunctionAsync(
            "arg => { const el = document.querySelector(arg.selector); if (!el) return false;" +
            " return el.getAttribute('data-lat') !== arg.lat || el.getAttribute('data-lng') !== arg.lng; }",
            new { selector = markerSelector, lat = initialLat, lng = initialLng },
            new PageWaitForFunctionOptions { Timeout = 15_000 });

        var movedMarker = await Page.WaitForSelectorAsync(markerSelector);
        var movedLat = await movedMarker!.GetAttributeAsync("data-lat");
        var movedLng = await movedMarker.GetAttributeAsync("data-lng");
        Assert.That(
            movedLat != initialLat || movedLng != initialLng, Is.True,
            "A hub-delivered VehiclePositionUpdated should have moved the rendered marker.");
    }

    [Test]
    public async Task GivenAuthenticatedDispatcher_WhenFleetMarkerClicked_ThenPopoverShowsRepDetails()
    {
        // Arrange — AC-5: position the fleet and log in so at least one marker renders.
        BackendApiHelper.PositionFleetAt(BackendBaseUrl, IowaLat, IowaLng);
        await LoginAsDispatcherAsync();

        // Act — click the first rendered fleet marker using an auto-retrying locator so the click
        // re-resolves the element on each attempt and rides out the 3 s Maps-SDK marker re-render (BUG-056).
        // IElementHandle (WaitForSelectorAsync) binds to a single DOM node and throws "Element is not
        // attached to the DOM" when the node is replaced; Locator re-resolves before every action.
        await Page.Locator("[data-testid^='fleet-marker-']").First.ClickAsync(
            new LocatorClickOptions { Timeout = 15_000 });

        // Assert — the rep popover opens with the rep name.
        var popover = await Page.WaitForSelectorAsync("[data-testid='rep-popover']", new() { Timeout = 10_000 });
        Assert.That(await popover!.IsVisibleAsync(), Is.True);
        var name = await Page.WaitForSelectorAsync("[data-testid='popover-rep-name']");
        Assert.That((await name!.TextContentAsync())?.Trim(), Is.Not.Empty);
    }

    [Test]
    public async Task GivenAuthenticatedDispatcher_WhenFleetMapLoads_ThenLegendIsVisible()
    {
        // Arrange & Act — AC-6.
        await LoginAsDispatcherAsync();

        // Assert — the legend is present on page load.
        var legend = await Page.WaitForSelectorAsync("[data-testid='fleet-legend']");
        Assert.That(await legend!.IsVisibleAsync(), Is.True);
        Assert.That(await Page.Locator("[data-testid='fleet-legend'] .sd-legend__row").CountAsync(), Is.EqualTo(5));
    }

    [Test]
    public async Task GivenAuthenticatedDispatcher_WhenPageLoadedAt1440And1280Widths_ThenMapAndQueueRailAreBothVisible()
    {
        // Arrange — AC-9: both the map column and the ACTIVE REQUESTS rail stay visible at Desktop (1440)
        // and Web (1280) widths.
        await LoginAsDispatcherAsync();
        await Page.WaitForSelectorAsync("[data-testid='dispatcher-dashboard']");

        foreach (var width in new[] { 1440, 1280 })
        {
            // Act
            await Page.SetViewportSizeAsync(width, 900);

            // Assert
            Assert.That(
                await Page.Locator("[data-testid='dispatcher-map-column']").IsVisibleAsync(), Is.True,
                $"map column should be visible at {width}px");
            Assert.That(
                await Page.Locator("[data-testid='dispatcher-queue-rail']").IsVisibleAsync(), Is.True,
                $"queue rail should be visible at {width}px");
        }
    }
}
