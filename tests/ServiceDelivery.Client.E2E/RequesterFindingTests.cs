using ServiceDelivery.Client.E2E.Helpers;

namespace ServiceDelivery.Client.E2E;

/// <summary>
/// FE-016 coverage: the Requester pending / "finding your technician" view on the live Web host (black box
/// — assertions target <c>data-testid</c> selectors and the resulting URL only). Logs in as the seeded
/// <c>gold1</c> requester (redirects to the submit screen), submits a request, and arrives at
/// <c>/requester/pending</c> (AC-1). Asserts the spinner + "Finding your technician…" message (AC-2), the
/// absence of any refresh control (AC-4), and the push-driven auto-transition to the tracking route when a
/// RepAssigned event arrives over RequesterHub (AC-3).
///
/// Location is driven through the "Use my current location" path (Playwright grants a fixed geolocation)
/// so the submit is deterministic regardless of whether the real Google map / its click listener loads in
/// CI — the same approach as <see cref="RequesterSubmitTests"/>. The DTC is selected deterministically by
/// its seeded DTC-001 GUID (Hydraulic system fault → requires HydraulicTool), not by list index, so the
/// chosen fault always matches the equipment the positioned fleet carries.
///
/// AC-3 determinism (the BUG-032/040 lesson, now on the requester side). The RepAssigned event fires only
/// after the matching algorithm matches a positioned, equipment-carrying, in-range vehicle, offers it to a
/// rep, and that rep ACCEPTS. The earlier revision relied on the ambient simulator fleet happening to
/// satisfy all three within the wait window and timed out twice. The diagnosis (from the simulator log):
/// a Gold DTC-001 request matched the one Available HydraulicTool rep, which then randomly DECLINED, and
/// the other six HydraulicTool reps were busy on ambient jobs — so re-matching found no candidate and the
/// request stayed Pending. Two things make this test deterministic now:
/// (1) it positions the WHOLE fleet AT the request coordinates via <see cref="BackendApiHelper"/> (the
///     Simulator-role account — the only role allowed to post positions) immediately before the UI submits
///     DTC-001, guaranteeing an in-range HydraulicTool vehicle (V-001..V-007) at the matching snapshot; and
/// (2) the Playwright suite runner (scripts/local/test-playwright.sh) starts the simulator with
///     Simulator__AutoDeclineRatePercent=0, so the matched rep always ACCEPTS (no random decline can strand
///     the request) — the requester-side equivalent of the Appium suite's sole-candidate guarantee. With
///     decline forced off, the transition arrives within ~2 s; the 45 s wait is a generous bound that
///     absorbs match + 1–5 s accept delay + SignalR delivery and honours the E2ETestBase SignalR convention.
///
/// Not run during the offline pipeline — requires a running backend + simulator (start.sh) and the Web
/// host. Execute via scripts/local/test-playwright.sh (or test-e2e.sh) against a live system.
/// </summary>
[TestFixture]
public sealed class RequesterFindingTests : E2ETestBase
{
    // A fixed position in the Des Moines area used for the deterministic "Use my current location" path.
    // The fleet is positioned at these exact coordinates for the AC-3 test so a matching vehicle is in
    // range at the instant of submission.
    private const double TestLatitude = 41.5868;
    private const double TestLongitude = -93.6250;

    // Seeded DTC-001 (Hydraulic system fault) — requires EquipmentType.HydraulicTool, which V-001..V-007
    // all carry. Selecting the DTC by this known GUID (the <option value="..."> is the DTC id) makes the
    // chosen fault deterministic instead of depending on the dropdown's index ordering.
    private const string Dtc001Id = "20000000-0000-0000-0000-000000000001";

    // The push chain for AC-3 is match → offer → rep "reviewing" delay (1–5 s) → accept → SignalR
    // delivery, with a possible single decline→re-match adding another 1–5 s. 45 s is a comfortable bound
    // that absorbs the whole chain without being absurd, and honours the E2ETestBase SignalR-wait
    // convention (>= 10 s for any SignalR-driven UI update).
    private const int RepAssignedTimeoutMs = 45_000;

    private static string RequesterEmail =>
        Environment.GetEnvironmentVariable("E2E_REQUESTER_EMAIL") ?? "gold1@example.com";

    private static string RequesterPassword =>
        Environment.GetEnvironmentVariable("E2E_REQUESTER_PASSWORD") ?? "Password123!";

    // The backend REST base (where BackendApiHelper posts fleet positions) — the web host is :5023, the
    // backend :5180 per scripts/local/start.sh. Overridable via E2E_BACKEND_URL for non-default setups.
    private static string BackendBaseUrl =>
        Environment.GetEnvironmentVariable("E2E_BACKEND_URL") ?? "http://localhost:5180";

    private async Task LoginAsRequesterAsync()
    {
        await Page.GotoAsync("/login");
        await Page.WaitForSelectorAsync("[data-testid='login-card']");

        await Page.FillAsync("[data-testid='email-input']", RequesterEmail);
        await Page.FillAsync("[data-testid='password-input']", RequesterPassword);
        await Page.ClickAsync("[data-testid='sign-in-button']");

        // The Requester home redirects to the submit screen (FE-015).
        await Page.WaitForURLAsync("**/requester/submit");
    }

    // The DTCs load over HTTP after the page renders. A native <select>'s options are never "visible"
    // while the dropdown is closed, so we poll the Attached count instead of a visibility wait.
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

    // Sets the location via the "Use my current location" device-GPS path. Playwright grants the browser
    // context a fixed geolocation so the flow is deterministic in CI (no dependency on the real Google map
    // tile/listener loading). After the click the pin-set label confirms the location is set.
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

    // Logs in, submits a valid DTC-001 request, and waits for the pending route — the shared precondition
    // for every assertion below (AC-1: the pending view is shown immediately after a successful
    // submission). The DTC is chosen by its seeded GUID (Hydraulic system fault) rather than a list index
    // so the same fault is submitted every run and matches the equipment the positioned fleet carries.
    private async Task SubmitRequestAndLandOnPendingAsync()
    {
        await LoginAsRequesterAsync();
        await WaitForDtcOptionsAsync();
        await UseDeviceLocationAsync();
        await Page.SelectOptionAsync("[data-testid='dtc-select']", new SelectOptionValue { Value = Dtc001Id });

        await Page.ClickAsync("[data-testid='request-service-button']");

        await Page.WaitForURLAsync("**/requester/pending", new() { Timeout = 10_000 });
    }

    [Test]
    public async Task GivenRequesterLoggedIn_WhenRequestSubmitted_ThenPendingPageIsShown()
    {
        // Arrange / Act
        await SubmitRequestAndLandOnPendingAsync();

        // Assert
        var pending = await Page.WaitForSelectorAsync("[data-testid='requester-pending']", new() { Timeout = 10_000 });
        Assert.That(await pending!.IsVisibleAsync(), Is.True);
        Assert.That(Page.Url, Does.Contain("/requester/pending"));
    }

    [Test]
    public async Task GivenRequesterOnPendingPage_WhenPageLoads_ThenSpinnerAndFindingMessageAreVisible()
    {
        // Arrange
        await SubmitRequestAndLandOnPendingAsync();

        // Act
        var spinner = await Page.WaitForSelectorAsync("[data-testid='pending-spinner']", new() { Timeout = 10_000 });
        var heading = await Page.WaitForSelectorAsync("[data-testid='pending-heading']", new() { Timeout = 10_000 });

        // Assert
        Assert.That(await spinner!.IsVisibleAsync(), Is.True);
        Assert.That(await heading!.TextContentAsync(), Does.Contain("Finding your technician"));
    }

    [Test]
    public async Task GivenRequesterOnPendingPage_WhenPageLoads_ThenNoRefreshButtonIsPresent()
    {
        // Arrange — AC-4: the transition is push-driven, so the pending view exposes no refresh control.
        await SubmitRequestAndLandOnPendingAsync();

        // Act
        var refreshCount = await Page.Locator("[data-testid='refresh-button']").CountAsync();

        // Assert
        Assert.That(refreshCount, Is.EqualTo(0));
    }

    [Test]
    public async Task GivenRequesterOnPendingPage_WhenRepAssignedReceivedViaHub_ThenTransitionsToTrackingRoute()
    {
        // Arrange — AC-3: the push-driven auto-transition, made deterministic (the BUG-032/040 lesson).
        // Log in and set the location through the UI, then position the WHOLE fleet at the request
        // coordinates via the Simulator-role account IMMEDIATELY before submitting DTC-001, so the backend's
        // matching snapshot (taken at submit time) finds an in-range HydraulicTool vehicle (V-001..V-007).
        // The running simulator's reps then auto-accept (~85%, after a 1–5 s delay; a ~15% decline simply
        // re-matches to one of the other six in-range vehicles), which emits RepAssigned to the requester.
        await LoginAsRequesterAsync();
        await WaitForDtcOptionsAsync();
        await UseDeviceLocationAsync();

        BackendApiHelper.PositionFleetAt(BackendBaseUrl, TestLatitude, TestLongitude);

        await Page.SelectOptionAsync("[data-testid='dtc-select']", new SelectOptionValue { Value = Dtc001Id });
        await Page.ClickAsync("[data-testid='request-service-button']");
        await Page.WaitForURLAsync("**/requester/pending", new() { Timeout = 10_000 });

        // Act / Assert — the SignalR-driven transition to the tracking route. The wait absorbs the full
        // match → offer → accept-delay → (possible re-match) → delivery chain (see RepAssignedTimeoutMs).
        await Page.WaitForURLAsync("**/requester/tracking", new() { Timeout = RepAssignedTimeoutMs });
        Assert.That(Page.Url, Does.Contain("/requester/tracking"));
    }
}
