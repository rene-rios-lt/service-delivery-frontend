using ServiceDelivery.Client.E2E.Helpers;

namespace ServiceDelivery.Client.E2E;

/// <summary>
/// FE-019 coverage: the Requester "Your service is complete" view on the live Web host (black box —
/// assertions target <c>data-testid</c> selectors and text/presence only). Reaches the tracking route the
/// same deterministic way <see cref="RequesterTrackingTests"/> does — log in as the seeded <c>gold1</c>
/// requester, set the location via the device-GPS path, position the whole fleet at the request coordinates
/// via the Simulator-role account immediately before submitting DTC-001, submit, wait for the pending route,
/// then wait for the push-driven auto-transition to <c>/requester/tracking</c> when a rep accepts — then
/// drives the assigned rep's job through to completion (arrive → complete) via
/// <see cref="BackendApiHelper.CompleteAssignedRequestAt"/>. The backend fires <c>ServiceCompleted</c> to the
/// tracked requester, so the tracking page auto-navigates to <c>/requester/complete</c>, where this asserts
/// the completion heading (AC-1) and the "Submit a new request" button (AC-3). Because the completion data is
/// assembled from client state (rep name from the last RepAssigned, DTC title threaded from submit) the
/// subtitle is populated even though the wire payload carries only the request id (AC-4).
///
/// Determinism follows the BUG-032/040 lesson exactly as RequesterTrackingTests documents it: the fleet is
/// positioned in range at the matching snapshot, and scripts/local/test-playwright.sh starts the simulator
/// with Simulator__AutoDeclineRatePercent=0 so the matched rep always ACCEPTS.
///
/// Not run during the offline pipeline — requires a running backend + simulator (start.sh) and the Web host.
/// Execute via scripts/local/test-playwright.sh (or test-e2e.sh) against a live system.
/// </summary>
[TestFixture]
public sealed class RequesterCompleteTests : E2ETestBase
{
    private const double TestLatitude = 41.5868;
    private const double TestLongitude = -93.6250;

    // Seeded DTC-001 (Hydraulic system fault) — V-001..V-007 all carry HydraulicTool, so a positioned
    // in-range candidate always matches. Selecting by known GUID makes the chosen fault deterministic.
    private const string Dtc001Id = "20000000-0000-0000-0000-000000000001";

    // The push chain match → offer → accept → SignalR delivery, same comfortable bound RequesterTrackingTests
    // uses; honours the E2ETestBase SignalR-wait convention (>= 10 s for any SignalR-driven UI update).
    private const int RepAssignedTimeoutMs = 45_000;

    // The completion transition is a single SignalR push once the rep completes; >= 10 s per the convention.
    private const int ServiceCompletedTimeoutMs = 20_000;

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
    // push-driven auto-transition to the tracking route — the shared precondition for reaching completion.
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

    [Test]
    public async Task GivenRequesterOnTrackingPage_WhenServiceCompletedEventArrives_ThenCompletionMessageAndSubmitButtonAreShown()
    {
        // Arrange — reach the tracking route (rep assigned + EnRoute), then drive that rep's job to
        // completion so the backend fires ServiceCompleted to this requester.
        await ReachTrackingRouteAsync();

        // Act — the assigned rep arrives then completes; the tracking page auto-navigates on the push.
        BackendApiHelper.CompleteAssignedRequestAt(BackendBaseUrl, TestLatitude, TestLongitude);
        await Page.WaitForURLAsync("**/requester/complete", new() { Timeout = ServiceCompletedTimeoutMs });

        var heading = await Page.WaitForSelectorAsync(
            "[data-testid='completion-heading']", new() { Timeout = 10_000 });
        var submitButton = await Page.WaitForSelectorAsync(
            "[data-testid='submit-new-request-button']", new() { Timeout = 10_000 });

        // Assert — AC-1 the completion heading and AC-3 the "Submit a new request" button are shown.
        Assert.That((await heading!.TextContentAsync())?.Trim(), Is.EqualTo("Your service is complete."));
        Assert.That(await submitButton!.TextContentAsync(), Does.Contain("Submit a new request"));
    }
}
