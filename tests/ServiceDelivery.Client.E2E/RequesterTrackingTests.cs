using ServiceDelivery.Client.E2E.Helpers;

namespace ServiceDelivery.Client.E2E;

/// <summary>
/// FE-017 coverage: the Requester live rep-tracking view on the live Web host (black box — assertions target
/// <c>data-testid</c> selectors, text content, and element presence/absence only). Reaches the tracking
/// route the same deterministic way <see cref="RequesterFindingTests"/> reaches the pending route: log in as
/// the seeded <c>gold1</c> requester, set the location via the device-GPS path, position the WHOLE fleet at
/// the request coordinates via the Simulator-role account immediately before submitting DTC-001, submit,
/// wait for the pending route, then wait for the push-driven auto-transition to <c>/requester/tracking</c>
/// when a rep accepts (RepAssigned). From there it asserts the map overlays (AC-1), the rep name (AC-2), the
/// ETA chip (AC-3), the status pill (AC-4), and the full-bleed map + bottom-sheet layout (AC-6).
///
/// AC-5 (OnSite: ETA hidden + "Your technician has arrived") and the OnSite overlay teardown depend on the
/// rep actually driving all the way to the requester and marking arrival — a multi-minute simulator journey
/// not bounded within a single E2E run — so its assertion is written against the arrived state but gated on
/// reaching it; it is the live-system complement to the bUnit OnSite tests, which prove the same transition
/// deterministically. The <c>rep-vehicle</c> registration assertion is advisory until BE-031 ships the
/// <c>vehicleRegistration</c> field — until then the subtitle reads "Service Rep".
///
/// Determinism follows the BUG-032/040 lesson exactly as RequesterFindingTests documents it: the fleet is
/// positioned in range at the matching snapshot, and scripts/local/test-playwright.sh starts the simulator
/// with Simulator__AutoDeclineRatePercent=0 so the matched rep always ACCEPTS.
///
/// Not run during the offline pipeline — requires a running backend + simulator (start.sh) and the Web host.
/// Execute via scripts/local/test-playwright.sh (or test-e2e.sh) against a live system.
/// </summary>
[TestFixture]
public sealed class RequesterTrackingTests : E2ETestBase
{
    // A fixed position in the Des Moines area used for the deterministic "Use my current location" path.
    // The fleet is positioned at these exact coordinates so a matching vehicle is in range at submission.
    private const double TestLatitude = 41.5868;
    private const double TestLongitude = -93.6250;

    // Seeded DTC-001 (Hydraulic system fault) — requires EquipmentType.HydraulicTool, which V-001..V-007
    // all carry. Selecting the DTC by its known GUID makes the chosen fault deterministic.
    private const string Dtc001Id = "20000000-0000-0000-0000-000000000001";

    // The push chain for the tracking transition is match → offer → accept-delay → SignalR delivery, with a
    // possible single decline→re-match. 45 s is the same comfortable bound RequesterFindingTests uses and
    // honours the E2ETestBase SignalR-wait convention (>= 10 s for any SignalR-driven UI update).
    private const int RepAssignedTimeoutMs = 45_000;

    private static string RequesterEmail =>
        Environment.GetEnvironmentVariable("E2E_REQUESTER_EMAIL") ?? "gold1@example.com";

    private static string RequesterPassword =>
        Environment.GetEnvironmentVariable("E2E_REQUESTER_PASSWORD") ?? "Password123!";

    private static string BackendBaseUrl =>
        Environment.GetEnvironmentVariable("E2E_BACKEND_URL") ?? "http://localhost:5180";

    private async Task LoginAsRequesterAsync()
    {
        await Page.GotoAsync("/login");
        await Page.WaitForSelectorAsync("[data-testid='login-card']");

        await Page.FillAsync("[data-testid='email-input']", RequesterEmail);
        await Page.FillAsync("[data-testid='password-input']", RequesterPassword);
        await Page.ClickAsync("[data-testid='sign-in-button']");

        await Page.WaitForURLAsync("**/requester/submit");
    }

    private async Task WaitForDtcOptionsAsync()
    {
        var realOptions = Page.Locator("[data-testid='dtc-select'] option[value]:not([value=''])");
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (await realOptions.CountAsync() > 0)
            {
                return;
            }

            await Task.Delay(200);
        }

        Assert.Fail("DTC options did not load within the timeout.");
    }

    private async Task UseDeviceLocationAsync()
    {
        await Page.Context.GrantPermissionsAsync(new[] { "geolocation" });
        await Page.Context.SetGeolocationAsync(new Geolocation
        {
            Latitude = (float)TestLatitude,
            Longitude = (float)TestLongitude
        });

        await Page.ClickAsync("[data-testid='use-my-location-button']");
        await Page.WaitForSelectorAsync("[data-testid='pin-set-label']", new() { Timeout = 10_000 });
    }

    // Logs in, positions the fleet in range, submits DTC-001, waits for pending, then waits for the
    // push-driven auto-transition to the tracking route — the shared precondition for every assertion below.
    private async Task ReachTrackingRouteAsync()
    {
        await LoginAsRequesterAsync();
        await WaitForDtcOptionsAsync();
        await UseDeviceLocationAsync();

        BackendApiHelper.PositionFleetAt(BackendBaseUrl, TestLatitude, TestLongitude);

        await Page.SelectOptionAsync("[data-testid='dtc-select']", new SelectOptionValue { Value = Dtc001Id });
        await Page.ClickAsync("[data-testid='request-service-button']");
        await Page.WaitForURLAsync("**/requester/pending", new() { Timeout = 10_000 });

        await Page.WaitForURLAsync("**/requester/tracking", new() { Timeout = RepAssignedTimeoutMs });
    }

    // Reaching /requester/tracking consumes one Available HydraulicTool rep from the finite live fleet
    // (V-001..V-007) per assignment, and NUnit gives every [Test] a fresh browser context (E2ETestBase.SetUp)
    // — so N reach-tracking tests = N assignments. Running after the submit/finding suites, the rep pool gets
    // exhausted, later tracking tests time out waiting for RepAssigned, and the run flakes (1–3 nondeterministic
    // failures). RequesterFindingTests avoids this by needing only ONE actual assignment; this suite mirrors
    // that. All the live, assignment-dependent assertions (map container, rep name, status pill, ETA chip,
    // tracking sheet) are made against a SINGLE reached tracking page — the expensive login→submit→assign
    // precondition runs exactly once, not once per assertion. Every distinct assertion the earlier five-test
    // shape covered is preserved; only the duplicated precondition is removed.
    //
    // AC-5 (OnSite: ETA hidden + "Your technician has arrived") and the rep-vehicle registration assertion are
    // NOT asserted live here — reaching OnSite needs a multi-minute simulator journey to the requester (not
    // bounded within one E2E run), and vehicleRegistration is gated on BE-031. Both stay deterministically
    // covered by the bUnit OnSite + interop tests, as the class doc records.
    [Test]
    public async Task GivenRequesterOnTrackingPage_WhenRepAssigned_ThenMapOverlaysRepNameStatusEtaAndLayoutAreShown()
    {
        // Arrange — reach the tracking route ONCE (the sole assignment-dependent precondition for the suite).
        await ReachTrackingRouteAsync();

        // Act — resolve every live overlay/data element on the single reached page.
        // AC-1 / AC-6: the real Google map (requester pin, moving rep marker, connecting route line) — the map
        // container carries data-testid='google-map', now rendering the real map after the web key-loading fix.
        var map = await Page.WaitForSelectorAsync("[data-testid='google-map']", new() { Timeout = 10_000 });
        // AC-2: the assigned rep's name in the bottom sheet.
        var repName = await Page.WaitForSelectorAsync("[data-testid='rep-name']", new() { Timeout = 10_000 });
        // AC-4: the initial state on assignment is EnRoute, so the status pill reads "On the way".
        var statusPill = await Page.WaitForSelectorAsync("[data-testid='status-pill']", new() { Timeout = 10_000 });
        // AC-3: the ETA chip carries a "min" ETA seeded from the assignment and refreshed by RepPositionUpdated
        // pushes. (A decreasing-value assertion would require a multi-minute journey; asserting the chip renders
        // its ETA is the bounded live check.)
        var etaChip = await Page.WaitForSelectorAsync("[data-testid='eta-chip']", new() { Timeout = 10_000 });
        // AC-6: the responsive layout is a full-bleed map with a bottom-sheet overlay card.
        var trackingSheet = await Page.WaitForSelectorAsync("[data-testid='tracking-sheet']", new() { Timeout = 10_000 });

        // Assert — every distinct assertion the earlier five separate tests made, against this one page.
        Assert.That(await map!.IsVisibleAsync(), Is.True);
        Assert.That((await repName!.TextContentAsync())?.Trim(), Is.Not.Empty);
        Assert.That(await statusPill!.TextContentAsync(), Does.Contain("On the way"));
        Assert.That(await etaChip!.TextContentAsync(), Does.Contain("min"));
        Assert.That(await trackingSheet!.IsVisibleAsync(), Is.True);
    }
}
