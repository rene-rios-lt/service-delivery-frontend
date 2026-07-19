using ServiceDelivery.Client.Appium.Mac.Helpers;

namespace ServiceDelivery.Client.Appium.Mac;

/// <summary>
/// FE-003 Phase 3 — the live Desktop (Mac Catalyst) gate the Playwright/Web suite cannot exercise: the
/// SecureStorage→Preferences token-store swap (AC-1) is a Mac Catalyst-only crash, the fleet map render
/// (AC-2) must be proven on the Desktop host too, and the AC-4 real-time scenario proves a hub-delivered
/// position update reaches the rendered client UI. All scenarios share one Mac2Driver session
/// (<see cref="MacDesktopTestBase"/>) because launching the Desktop app is slow.
/// <para>
/// These are NATIVE mac2 tests (no WebView context — see <see cref="MacDesktopTestBase"/>): every assertion
/// is made against the macOS accessibility (AX) tree, not the DOM. The fleet markers themselves are
/// AX-INVISIBLE (google.maps marks its marker panes aria-hidden — see <see cref="MacDesktopTestBase"/>), so the
/// marker-presence and marker-move assertions anchor on FleetMap's visually-hidden-but-AX-exposed fleet SUMMARY
/// (one static-text entry per visible vehicle carrying its identity, state, and live coordinates), NOT on the
/// markers. Authored but NOT executed in the /master pipeline — run via <c>scripts/local/test-appium-mac.sh</c>
/// after the one-time local prerequisites (mac2 driver install, an Accessibility grant for the Appium/terminal
/// process, a Debug Desktop build). No Safari web-developer / WebView-inspector grant is needed — mac2 never
/// enters a WebView context.
/// </para>
/// </summary>
[TestFixture]
public sealed class DispatcherFleetMapDesktopTests : MacDesktopTestBase
{
    // Iowa map centre (matches FleetMap's centre) — positioning the fleet here puts every vehicle inside
    // the map's initial statewide view so its markers render.
    private const double IowaLat = 41.60;
    private const double IowaLng = -93.60;

    // A distinctly different Iowa coordinate the real-time scenario re-positions the fleet to, forcing an
    // unambiguous position change. Still inside the statewide view and a valid (non-Offline) state, so the
    // vehicle stays visible. It differs from Iowa at the 4-dp precision FleetMap stamps into each summary
    // entry's coordinate text, so the accessible entry text observably changes when the hub delivers the new
    // position.
    private const double MovedLat = 42.02;
    private const double MovedLng = -92.90;

    [OneTimeSetUp]
    public void ClaimPositionAndLogIn()
    {
        try
        {
            // SNAPSHOT-path arrange (cycle 9). The AC-10 "login → fleet map renders with markers" scenario
            // does NOT need live hub delivery — and hub delivery is exactly what fails under XCTest launch. A
            // vehicle that is CLAIMED (non-Offline rep-state) AND has a posted position comes back in the
            // GET /dispatcher/fleet SNAPSHOT, so LoadAsync renders its marker on dashboard mount with no hub
            // event at all. The snapshot render path (map, legend, dashboard) is proven healthy under XCTest,
            // so seeding a visible vehicle into the snapshot BEFORE login makes the markers scenario
            // deterministic. (Contrast the marker-MOVE scenario below, which is left as the instrumented probe
            // for the hub-delivery anomaly.)
            //
            // In this BACKEND-ONLY run (test-appium-mac.sh sets SD_SKIP_SIMULATOR=1) nothing claims or
            // positions a vehicle on its own: with no simulator every vehicle is Offline/hidden in the
            // snapshot (cycle-1 advisory). So claim V-001 as rep1 (→ Available) and position the fleet BEFORE
            // logging in — both are persisted server-side, so the fresh dashboard's LoadAsync GET reflects
            // them. Login happens once for the shared session (launching the Desktop app is slow).
            BackendApiHelper.ClaimVehicleAsRep(MacDesktopConfig.BackendBaseUrl);
            BackendApiHelper.PositionFleetAt(MacDesktopConfig.BackendBaseUrl, IowaLat, IowaLng);
            LoginAsDispatcher();
        }
        catch
        {
            DumpAxTreeIfRequested("ClaimPositionAndLogIn");
            throw;
        }
    }

    [Test]
    public void GivenDesktopDispatcher_WhenLoginSubmitted_ThenFleetMapRendersWithoutError()
    {
        // Arrange — login happened in the fixture OneTimeSetUp (crash-free login is the assertion here: the
        // old SecureStorageTokenStore threw a Keychain error on unsigned Mac Catalyst before any UI).

        // Act — the dashboard's "ACTIVE REQUESTS" rail head is the native dashboard anchor; the Blazor error
        // banner ships display:none and only reaches the AX tree if Blazor flips it to visible on an
        // unhandled exception (so the absence check is inherently visibility-aware). Not a bare wait on async
        // state: LoginAsDispatcher already synchronised on the dashboard anchor via WaitForSignalR, so both
        // native lookups resolve at once.
        var dashboardShown = ExistsNow(DashboardAnchor);
        var errorBannerShown = ExistsNow(ErrorBanner);

        // Assert — the dashboard routed in (login completed) and no Blazor error banner is visible.
        Assert.That(dashboardShown, Is.True, "Dispatcher dashboard should be present after Desktop login.");
        Assert.That(errorBannerShown, Is.False, "No Blazor unhandled-error banner should be visible.");
    }

    [Test]
    public void GivenDesktopDispatcher_WhenLoginSubmitted_ThenFleetMapRendersWithVehicleMarkers()
    {
        // Arrange — the fixture OneTimeSetUp claimed V-001 as rep1 and positioned the fleet BEFORE logging in,
        // so the dashboard's LoadAsync GET /dispatcher/fleet snapshot already carries a visible (claimed,
        // positioned, non-Offline) vehicle. Its marker renders straight from the snapshot — the render path
        // proven healthy under XCTest — WITHOUT depending on live hub delivery. The marker itself is
        // AX-invisible (google.maps aria-hidden panes — see MacDesktopTestBase), so this asserts on the
        // fleet SUMMARY entry FleetMap renders for that visible vehicle, which IS AX-exposed.

        // Act — bounded poll (≈500 ms laps, the 15 s SignalR budget) for the snapshot-rendered summary entry.
        // Each lap also re-POSTs a fleet position as the Simulator account as BELT-AND-BRACES: it is idempotent
        // and keeps the vehicle's position fresh should the snapshot arrange race the dashboard mount, but the
        // entry's PRIMARY source is the snapshot, not this broadcast. The condition returns bool because
        // Selenium's DefaultWait.Until only accepts a bool or a reference type; a nullable value type (e.g.
        // int?) throws an ArgumentException immediately.
        WaitForSignalR(d =>
        {
            BackendApiHelper.PositionFleetAt(MacDesktopConfig.BackendBaseUrl, IowaLat, IowaLng);
            return d.FindElements(FleetSummaryEntryAny).Count > 0;
        });

        // Assert — re-count fresh now the wait has confirmed at least one visible-vehicle entry is present.
        var entryCount = Driver.FindElements(FleetSummaryEntryAny).Count;
        Assert.That(
            entryCount, Is.GreaterThan(0),
            "The fleet map should render at least one visible vehicle (asserted via its AX-exposed summary entry).");
    }

    [Test]
    public void GivenDesktopDispatcher_WhenAVehiclePositionIsBroadcastOverTheHub_ThenTheRenderedMarkerMoves()
    {
        // Arrange — AC-4 real-time delivery on the Desktop host, observed via the AX-exposed fleet summary
        // (the markers are AX-invisible — see MacDesktopTestBase). First materialize at least one visible
        // vehicle POST-connect and capture its FULL summary-entry text. Same backend-only rationale as the
        // markers scenario: with SD_SKIP_SIMULATOR=1 nothing ticks positions on its own, so each lap re-POSTs
        // the fleet at Iowa (idempotent) until a live "Unassigned" VehiclePositionUpdated broadcast lands after
        // the hub connection is up and the entry renders. FleetMap re-renders the summary on the same
        // StateChanged that syncs markers, so the entry's coordinate text is recomputed on every hub-delivered
        // position update.
        //
        // WHAT THIS PROVES (documented honestly): this proves hub delivery reaches the RENDERED client UI
        // (VehiclePositionUpdated → ViewModel merge → Blazor DOM re-render → AX-exposed summary text change).
        // The JS google.maps marker-MOVEMENT path (the same shared component) is live-proven by the Playwright
        // marker-move scenario and visually by this gate's teardown screenshots; the AX tree cannot observe
        // inside the Maps SDK's aria-hidden marker panes — which is exactly why VoiceOver (and this test) needs
        // the out-of-pane summary in the first place.
        var initialText = WaitForSignalR(d =>
        {
            BackendApiHelper.PositionFleetAt(MacDesktopConfig.BackendBaseUrl, IowaLat, IowaLng);
            var entries = d.FindElements(FleetSummaryEntryAny);
            return entries.Count == 0 ? null : AccessibleName(entries[0]);
        })!; // WaitForSignalR only returns a non-null value; the null branch above is the "not ready" sentinel.

        // Re-resolve the SAME entry on every poll by its STABLE "Vehicle <registration>" identity prefix —
        // never a cached element (a Blazor re-render REPLACES the entry element, and a held mac2 element's
        // reads can go stale). Only the changing coordinate suffix can flip the movement signal below; the
        // identity prefix excludes the state and coordinate portions so it survives both a state and a move.
        var entryLocator = FleetSummaryEntryWithTextPrefix(StableEntryPrefix(initialText));

        // Act + Assert — bounded post-connect move loop (≈500 ms laps, the 15 s SignalR budget). Each lap
        // re-POSTs a DISTINCT fleet position (MovedLat/MovedLng) as the Simulator account so a fresh
        // VehiclePositionUpdated broadcast is delivered post-connect, then re-queries THIS entry FRESH and
        // checks whether its coordinate text has changed from the captured initial text. In backend-only mode
        // these POSTs are the ONLY position source, so a text change is necessarily hub-delivered end-to-end
        // (post-connect event → ViewModel merge → DOM re-render → AX text change). A dead hub never changes the
        // text, so the budget exhausts and the scenario fails; no REST re-fetch path moves a vehicle post-mount.
        var movedText = WaitForSignalR(d =>
        {
            BackendApiHelper.PositionFleetAt(MacDesktopConfig.BackendBaseUrl, MovedLat, MovedLng);
            var els = d.FindElements(entryLocator);
            if (els.Count == 0)
            {
                return null;
            }

            var current = AccessibleName(els[0]);
            return current is not null && current != initialText ? current : null;
        });

        Assert.That(
            movedText, Is.Not.Null,
            "A hub-delivered VehiclePositionUpdated should have changed the rendered fleet-summary entry's coordinate text on Desktop.");
    }

    // The stable identity is the "Vehicle <registration>" portion — everything before the first " — "
    // separator. The trailing " — <state> — <lat>,<lng>" changes on every position render (coords) and could
    // change on a state transition, so anchoring on identity alone keeps re-resolving the SAME entry across a
    // move without going stale.
    private static string StableEntryPrefix(string fullText)
    {
        const string separator = " — ";
        var index = fullText.IndexOf(separator, StringComparison.Ordinal);
        return index >= 0 ? fullText[..index] : fullText;
    }
}
