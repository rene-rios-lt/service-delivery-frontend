using ServiceDelivery.Client.E2E.Helpers;

namespace ServiceDelivery.Client.E2E;

/// <summary>
/// FE-022 Playwright coverage (ACs 3, 5, 7) against the live Web host. Drives the dispatcher force-release flow
/// as a black box, asserting only on <c>data-testid</c> selectors: a claimed vehicle's marker opens the
/// rep-marker popover, whose "Force-release vehicle" button opens the confirmation dialog; confirming dismisses
/// it optimistically; the dialog is visible at the desktop width and still fits a narrow web viewport.
/// <para>
/// The Dispatcher persona runs on BOTH Web and Desktop from the same shared Client.UI/Client.Core code, so each
/// meaningful scenario here (except the browser-only narrow-width reflow) has a mirrored Mac2 Desktop scenario
/// in <c>DispatcherForceReleaseMacTests</c> (Dispatcher Web/Desktop E2E parity rule). Authored but NOT executed
/// in the /master pipeline — run via <c>scripts/local/test-playwright.sh</c> (or <c>test-e2e.sh</c>) against a
/// live system with a real Google Maps key configured (the marker is a google.maps DOM element on Web).
/// </para>
/// <para>
/// <b>Arrange.</b> Force-release only needs a CLAIMED vehicle rendered on the map — no matching/EnRoute dance.
/// A dedicated rep+vehicle (rep8 / V-008) is used so the fixture does not contend with the fleet-map (rep1),
/// queue (rep2/3) or redirect (rep5/6/7) fixtures (QUAL-030 isolation). V-008 does NOT carry HydraulicTool, so
/// it is never matched to a DTC-001 request and stays idle-claimed — keeping its marker deterministically
/// present and clickable throughout the scenario.
/// </para>
/// </summary>
[TestFixture]
public sealed class DispatcherForceReleaseTests : E2ETestBase
{
    // Iowa map centre (matches FleetMap's centre) — positioning the dedicated vehicle here puts its marker
    // inside the map's initial statewide view.
    private const double IowaLat = 41.60;
    private const double IowaLng = -93.60;

    // Dedicated force-release rep+vehicle (unused by sibling fixtures — QUAL-030 isolation).
    private const string ForceReleaseRepEmail = "rep8@dealer.com";
    private const string ForceReleaseVehicleId = "30000000-0000-0000-0000-000000000008";

    private const string DispatcherEmail = "alex@dealer.com";

    private static string MarkerSelector => $"[data-testid='fleet-marker-{ForceReleaseVehicleId}']";
    private const string ForceReleaseButton = "[data-testid='popover-force-release']";
    private const string DialogSelector = "[data-testid='force-release-dialog']";
    private const string ConfirmButton = "[data-testid='force-release-confirm']";

    private static string BackendBaseUrl =>
        Environment.GetEnvironmentVariable("E2E_BACKEND_URL") ?? "http://localhost:5180";

    // Claims + positions the dedicated vehicle so its marker renders CLAIMED, logs the dispatcher in, then opens
    // the force-release confirmation dialog by clicking the marker (auto-retrying Locator — the Maps SDK
    // re-renders markers every ~3 s, BUG-056) and the popover's force-release button.
    private async Task ArrangeClaimedVehicleAndOpenDialogAsync()
    {
        BackendApiHelper.ClaimAndPositionVehicle(
            BackendBaseUrl, ForceReleaseRepEmail, ForceReleaseVehicleId, IowaLat, IowaLng);
        await LoginAsDispatcherAsync();

        // Click the dedicated vehicle's marker (bounded, auto-retrying so a Maps-SDK marker re-render can't
        // detach a cached handle — BUG-048/BUG-056), then the popover's "Force-release vehicle" button.
        await Page.Locator(MarkerSelector).First.ClickAsync(new LocatorClickOptions { Timeout = 15_000 });
        var releaseButton = await Page.WaitForSelectorAsync(ForceReleaseButton, new() { Timeout = 10_000 });
        await releaseButton!.ClickAsync();
        await Page.WaitForSelectorAsync(DialogSelector, new() { Timeout = 10_000 });
    }

    [Test]
    public async Task GivenDispatcherOnWebWithClaimedVehicle_WhenForceReleaseConfirmed_ThenDialogDismissed()
    {
        // Arrange — AC-3: open the confirmation dialog on a claimed vehicle.
        await ArrangeClaimedVehicleAndOpenDialogAsync();
        Assert.That(
            await Page.Locator(DialogSelector).IsVisibleAsync(), Is.True,
            "the force-release confirmation dialog should open");

        // Act — confirm the force-release.
        await Page.ClickAsync(ConfirmButton);

        // Assert — the dialog is dismissed (the backend returns 200 and the ViewModel clears it).
        await Page.WaitForSelectorAsync(
            DialogSelector, new() { State = WaitForSelectorState.Detached, Timeout = 10_000 });
        Assert.That(await Page.Locator(DialogSelector).CountAsync(), Is.EqualTo(0));
    }

    [Test]
    public async Task GivenDispatcherOnWeb_WhenForceReleaseDialogOpened_ThenDialogVisibleAtDesktopWidth()
    {
        // Arrange — AC-7: at the desktop width the dialog is visible and centred over the map.
        await Page.SetViewportSizeAsync(1440, 900);

        // Act
        await ArrangeClaimedVehicleAndOpenDialogAsync();

        // Assert
        Assert.That(
            await Page.Locator(DialogSelector).IsVisibleAsync(), Is.True,
            "the dialog should be visible at 1440px");
    }

    [Test]
    public async Task GivenDispatcherOnWeb_WhenForceReleaseDialogOpened_ThenDialogFitsNarrowWebWidth()
    {
        // Arrange — AC-7 (web-only): the .sd-dialog max-width:90% keeps the dialog accessible when the browser
        // viewport is narrowed. No Desktop mirror — a native window has no browser viewport to resize.
        await ArrangeClaimedVehicleAndOpenDialogAsync();

        // Act — narrow the viewport below the dashboard's single-column breakpoint.
        await Page.SetViewportSizeAsync(768, 900);

        // Assert — the dialog is still visible and does not overflow the viewport.
        var dialog = Page.Locator(DialogSelector);
        Assert.That(await dialog.IsVisibleAsync(), Is.True, "the dialog should remain visible at 768px");
        var box = await dialog.BoundingBoxAsync();
        Assert.That(box, Is.Not.Null);
        Assert.That(box!.Width, Is.LessThanOrEqualTo(768), "the dialog should fit within the narrow viewport");
    }

    // QUARANTINED [Explicit] — the AC-5 error banner is NOT reachable via the live backend, and this is a
    // property of the backend contract, not a product gap in FE-022's dispatcher UI. The force-release endpoint
    // (POST /vehicles/{id}/force-release) is IDEMPOTENT: releasing an already-released vehicle returns 200 (the
    // handler simply skips the session/state block when the vehicle is already unclaimed — see
    // ForceReleaseVehicleCommandHandler), and the only non-2xx it produces is 404 for a MISSING vehicle. So the
    // planned "force-release server-side, then confirm in the UI" arrange yields a 200 on the UI confirm too —
    // the dialog dismisses (success), the error banner never surfaces. There is no live vehicle state that makes
    // a valid, existing, claimed vehicle un-force-releasable (unlike redirect's 422). AC-5's on-error behaviour
    // is therefore covered DETERMINISTICALLY offline: the ViewModel error test
    // (DispatcherFleetViewModelForceReleaseTests.GivenAForceReleaseThatErrors_...KeepsDialogOpen) and the dialog
    // bUnit tests (ForceReleaseConfirmDialogTests error-banner-visible + confirm-disabled). Kept authored so the
    // arrange (BackendApiHelper.ForceReleaseVehicleAs) is ready should the backend later add a rejecting state.
    [Test, Explicit(
        "Backend force-release is idempotent (re-release returns 200, only 404 for a missing vehicle), so the " +
        "AC-5 error banner cannot be triggered live against a seeded vehicle — the error behaviour is covered " +
        "deterministically by the offline ViewModel + dialog tests.")]
    public async Task GivenDispatcherOnWeb_WhenForceReleaseApiErrors_ThenErrorBannerVisibleAndConfirmDisabled()
    {
        // Arrange — AC-5: open the dialog, then release the vehicle server-side before the UI confirm.
        await ArrangeClaimedVehicleAndOpenDialogAsync();
        BackendApiHelper.ForceReleaseVehicleAs(BackendBaseUrl, ForceReleaseVehicleId, DispatcherEmail);

        // Act — confirm; the backend would need to reject this for the error path to run.
        await Page.ClickAsync(ConfirmButton);

        // Assert — the error banner surfaces (would require a non-2xx the idempotent endpoint does not return).
        var error = await Page.WaitForSelectorAsync(
            "[data-testid='force-release-error']", new() { Timeout = 15_000 });
        Assert.That(await error!.IsVisibleAsync(), Is.True, "the force-release error banner should surface");
    }
}
