using ServiceDelivery.Client.Appium.Mac.Helpers;

namespace ServiceDelivery.Client.Appium.Mac;

/// <summary>
/// FE-022 — the Desktop (Mac Catalyst) mirror of the dispatcher force-release flow, paired with the Playwright
/// scenarios in <c>DispatcherForceReleaseTests</c> (Dispatcher Web/Desktop E2E parity rule). Desktop is a
/// DIFFERENT runtime (MAUI Blazor Hybrid / WKWebView) from browser WASM, so proving the force-release dialog on
/// Web does not prove it on the WebView host.
/// <para>
/// <b>Every scenario here is QUARANTINED [Explicit] — a documented Desktop limitation, not a missing mirror.</b>
/// FE-022's SOLE entry point is the FE-003 rep-marker popover (scope constraint 1 — the FE-006 banner entry is
/// deferred to FE-006). That popover opens by clicking a <c>google.maps</c> marker, and those markers are
/// AX-INVISIBLE under the mac2 driver: the Maps SDK marks its marker overlay panes <c>aria-hidden</c>, so the
/// native macOS accessibility tree never sees them (this is the exact limitation documented at length in
/// <see cref="MacDesktopTestBase"/>, and why the FE-003 Desktop tests assert on the out-of-pane fleet SUMMARY
/// rather than on markers, and the FE-005 Desktop tests open their dialog from the AX-visible request-queue rail,
/// not a marker). With no AX-reachable affordance to select a vehicle, the popover — and therefore the
/// force-release dialog — cannot be opened through the native AX tree in this story. The dialog's render/behaviour
/// is covered DETERMINISTICALLY offline (bUnit <c>ForceReleaseConfirmDialogTests</c> + ViewModel
/// <c>DispatcherFleetViewModelForceReleaseTests</c>) and live on the Web WASM runtime (the paired Playwright
/// scenarios). The Desktop WebView mirror is deferred until an AX-reachable entry exists — FE-006's rail/banner
/// "Force-release vehicle" action (AX-visible text, exactly like FE-005's rail Redirect button). The arrange and
/// intended Act/Assert are authored so the mirror is READY the moment that entry lands; run them in isolation
/// with <c>--filter FullyQualifiedName~ForceRelease</c> once it does.
/// </para>
/// <para>
/// The arrange (AX-reachable) claims + positions a dedicated rep+vehicle (rep8 / V-008 — unused by the fleet-map
/// rep1, queue rep2/3 and redirect rep4/5/6 fixtures) so it renders CLAIMED in the fleet summary; the suite runs
/// backend-only (<c>SD_SKIP_SIMULATOR=1</c>), so each poll lap re-broadcasts the position to keep the summary
/// entry live (the FE-003 Desktop re-POST-each-lap pattern).
/// </para>
/// </summary>
[TestFixture]
public sealed class DispatcherForceReleaseMacTests : MacDesktopTestBase
{
    // A holding point inside the statewide view where the dedicated vehicle is parked so it renders visible.
    private const double IowaLat = 41.60;
    private const double IowaLng = -93.60;

    // Dedicated force-release rep+vehicle (unused by sibling Desktop fixtures — QUAL-030 isolation).
    private const string ForceReleaseRepEmail = "rep8@dealer.com";
    private const string ForceReleaseVehicleId = "30000000-0000-0000-0000-000000000008";

    private const string DispatcherEmail = "alex@dealer.com";

    // AX anchors: the popover button and the dialog controls surface as accessible static text by their rendered
    // labels (their data-testid is AX-invisible). "Force-release vehicle" is BOTH the popover button and the
    // dialog confirm button; "Cancel" and the session-revoke warning disambiguate the open dialog.
    private static readonly By ForceReleaseControl = AnyElementText("Force-release vehicle");
    private static readonly By CancelButton = AnyElementText("Cancel");
    private static readonly By SessionRevokeWarning =
        By.XPath("//*[contains(@value, \"session is revoked\") or contains(@label, \"session is revoked\")]");

    [OneTimeSetUp]
    public void ClaimDedicatedVehicleAndLogInOnce()
    {
        try
        {
            // Claim + position the dedicated vehicle as its rep BEFORE logging in, so the dispatcher's
            // GET /dispatcher/fleet snapshot carries it CLAIMED (RepId non-null) and visible — exactly as the
            // real system's simulator claims vehicles before any dispatcher connects.
            BackendApiHelper.ClaimAndPositionVehicle(
                MacDesktopConfig.BackendBaseUrl, ForceReleaseRepEmail, ForceReleaseVehicleId, IowaLat, IowaLng);
            LoginAsDispatcher();
        }
        catch
        {
            DumpAxTreeIfRequested("ClaimDedicatedVehicleAndLogInOnce");
            throw;
        }
    }

    // Gates (AX-reachable) on the dedicated vehicle's fleet-summary entry rendering on the Desktop map, then
    // attempts to open the force-release dialog. The marker click that opens the popover is AX-INVISIBLE under
    // mac2 (see the class summary), so the dialog cannot actually be opened here — this is why every scenario is
    // [Explicit]. Re-broadcasts the position on each poll lap so the backend-only run keeps the summary live.
    private void ArrangeVisibleVehicleAndOpenDialog()
    {
        var appeared = WaitForSignalR(d =>
        {
            BackendApiHelper.ClaimAndPositionVehicle(
                MacDesktopConfig.BackendBaseUrl, ForceReleaseRepEmail, ForceReleaseVehicleId, IowaLat, IowaLng);
            return d.FindElements(FleetSummaryEntryAny).Count > 0;
        });
        Assert.That(appeared, Is.True, "The claimed vehicle should render in the Desktop fleet summary.");

        // The rep-marker popover opens by clicking a google.maps marker — AX-invisible under mac2 — so this
        // control never surfaces in the AX tree in FE-022 (deferred to FE-006's AX-reachable rail/banner entry).
        WaitForSignalR(d => d.FindElement(ForceReleaseControl)).Click();
        WaitForSignalR(d => d.FindElements(CancelButton).Count > 0);
    }

    // QUARANTINED [Explicit] — AX-invisible marker entry (see the class summary). AC-3 confirm behaviour is
    // covered live on Web (Playwright) and deterministically offline (ViewModel success test + dialog bUnit).
    [Test, Explicit(
        "FE-022's sole entry is the FE-003 marker popover, which opens from an AX-invisible google.maps marker — " +
        "not drivable through the mac2 AX tree. Covered on Web (Playwright) + offline; Desktop mirror deferred " +
        "to FE-006's AX-reachable rail/banner entry.")]
    public void GivenDispatcherOnDesktopWithClaimedVehicle_WhenForceReleaseConfirmed_ThenDialogDismissed()
    {
        // Arrange — AC-3: open the confirmation dialog on a claimed vehicle.
        ArrangeVisibleVehicleAndOpenDialog();
        var dialogShown = WaitForSignalR(d => d.FindElements(SessionRevokeWarning).Count > 0);
        Assert.That(dialogShown, Is.True, "the confirmation dialog should open (session-revoke warning visible)");

        // Act — confirm (the dialog confirm button shares the "Force-release vehicle" label).
        Driver.FindElement(ForceReleaseControl).Click();

        // Assert — the dialog dismisses (the session-revoke warning leaves the AX tree).
        var dismissed = WaitForSignalR(d => d.FindElements(SessionRevokeWarning).Count == 0);
        Assert.That(dismissed, Is.True, "the dialog should dismiss after confirm on the Desktop host (AC-3).");
    }

    // QUARANTINED [Explicit] — AX-invisible marker entry (see the class summary). AC-7 dialog-visible is proven
    // live on Web (Playwright) + offline (dialog bUnit).
    [Test, Explicit(
        "FE-022's sole entry is the FE-003 marker popover, which opens from an AX-invisible google.maps marker — " +
        "not drivable through the mac2 AX tree. Covered on Web (Playwright) + offline; Desktop mirror deferred " +
        "to FE-006's AX-reachable rail/banner entry.")]
    public void GivenDispatcherOnDesktop_WhenForceReleaseDialogOpened_ThenDialogVisible()
    {
        // Arrange — AC-7: reach a claimed-vehicle state.
        ArrangeVisibleVehicleAndOpenDialog();

        // Assert — the dialog's session-revoke warning and Cancel/Force-release controls are AX-accessible.
        var visible = WaitForSignalR(d =>
            d.FindElements(SessionRevokeWarning).Count > 0 && d.FindElements(CancelButton).Count > 0);
        Assert.That(visible, Is.True, "the force-release dialog should be visible on the Desktop host (AC-7).");
    }

    // QUARANTINED [Explicit] — AX-invisible marker entry AND idempotent backend (re-release returns 200, so the
    // error banner never surfaces live — see the Web AC-5 quarantine). AC-5 error behaviour is covered
    // deterministically offline (ViewModel error test + dialog error-banner/disabled-confirm bUnit tests).
    [Test, Explicit(
        "FE-022's marker entry is AX-invisible under mac2 AND the force-release endpoint is idempotent (re-release " +
        "returns 200), so the AC-5 error banner is not reachable live — covered by the offline ViewModel + dialog " +
        "tests; Desktop mirror deferred to FE-006's AX-reachable entry.")]
    public void GivenDispatcherOnDesktop_WhenForceReleaseApiErrors_ThenErrorBannerVisibleAndConfirmButtonPresent()
    {
        // Arrange — AC-5: open the dialog, then release the vehicle server-side before the UI confirm.
        ArrangeVisibleVehicleAndOpenDialog();
        BackendApiHelper.ForceReleaseVehicleAs(
            MacDesktopConfig.BackendBaseUrl, ForceReleaseVehicleId, DispatcherEmail);

        // Act — confirm; the backend would need to reject this for the error path to run.
        Driver.FindElement(ForceReleaseControl).Click();

        // Assert — the dialog re-surfaces with the confirm button still reachable (an errored release does not
        // silently close). Would require a non-2xx the idempotent endpoint does not return.
        var stillShown = WaitForSignalR(d => d.FindElements(ForceReleaseControl).Count > 0);
        Assert.That(stillShown, Is.True, "an errored force-release should keep the dialog open on Desktop (AC-5).");
    }

    // Any element type carrying EXACTLY the given text — the popover/dialog controls are plain HTML elements
    // whose text WebKit may expose as the AX value or label (and may nest under a wrapping group).
    private static By AnyElementText(string text) =>
        By.XPath($"//*[@value=\"{text}\" or @label=\"{text}\"]");
}
