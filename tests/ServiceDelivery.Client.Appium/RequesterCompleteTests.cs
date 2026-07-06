using System.Diagnostics;
using ServiceDelivery.Client.Appium.Helpers;

namespace ServiceDelivery.Client.Appium;

/// <summary>
/// FE-019 coverage on Mobile: the Requester "Your service is complete" screen rendered in the MAUI
/// WKWebView on the iOS simulator (the BUG-031 boundary — the live-system mobile complement to the bUnit
/// component tests). The Requester persona is supported on Mobile (system-overview). Drives the full
/// lifecycle: rep1 is prepared as an in-range Available candidate at the request site, the requester logs
/// in on the app and submits DTC-001 via the device-GPS path (reaching /requester/pending), then rep1's
/// pushed offer is driven through accept → arrive → complete via the backend. The backend fires
/// <c>ServiceCompleted</c> to the requester's group, so the app auto-navigates to /requester/complete,
/// where this asserts the completion heading (AC-1) and the "Submit a new request" button (AC-3) are
/// reachable in the WebView. Because the completion data is assembled from client state, the subtitle is
/// populated even though the wire payload carries only the request id (AC-4).
///
/// Backend-only environment: scripts/local/test-appium.sh sets SD_SKIP_SIMULATOR=1, so no simulator
/// operates rep1..rep8 — the test itself drives rep1's decisions over the API. With no rep competition rep1
/// is the sole match candidate, so the single submission routes its offer to rep1 deterministically.
///
/// Not run during the offline pipeline — requires a booted iOS simulator, the installed Mobile app, and a
/// running Appium server + backend. Execute via scripts/local/test-appium.sh against a live system with a
/// booted iOS simulator. SignalR-driven screen transitions rely on the 15 s implicit wait (AppiumConfig).
/// </summary>
[TestFixture]
public sealed class RequesterCompleteTests : AppiumTestBase
{
    private const string AppBundleId = "com.companyname.servicedelivery.client.mobile";

    // Must match BackendApiHelper.CompletionRequestLatitude/Longitude so the requester's GPS-submitted
    // location coincides with rep1's positioned vehicle (distance 0 → deterministic match).
    private static readonly string TestLatitude =
        BackendApiHelper.CompletionRequestLatitude.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static readonly string TestLongitude =
        BackendApiHelper.CompletionRequestLongitude.ToString(System.Globalization.CultureInfo.InvariantCulture);

    // Seeded DTC-001 (Hydraulic system fault) — V-001 (which rep1 claims) carries HydraulicTool, so the
    // request matches rep1. Selecting by known GUID makes the chosen fault deterministic.
    private const string Dtc001Id = "20000000-0000-0000-0000-000000000001";

    private static string RequesterEmail =>
        Environment.GetEnvironmentVariable("APPIUM_REQUESTER_EMAIL") ?? "gold1@example.com";

    private static string RequesterPassword =>
        Environment.GetEnvironmentVariable("APPIUM_REQUESTER_PASSWORD") ?? "Password123!";

    private static string BackendBaseUrl => AppiumConfig.BackendBaseUrl;

    private static string DeviceTarget =>
        Environment.GetEnvironmentVariable("APPIUM_DEVICE_UDID") ?? "booted";

    /// <summary>
    /// Pre-grants location permission and fixes the simulator's GPS BEFORE any per-test app activation, so
    /// the "Use my current location" path resolves deterministically with no CLLocationManager prompt
    /// (mirrors RequesterSubmitTests). The location must equal the request coordinates the fleet is
    /// positioned at so the match is distance 0.
    /// </summary>
    [OneTimeSetUp]
    public void ConfigureSimulatorLocation()
    {
        Simctl($"privacy {DeviceTarget} grant location {AppBundleId}");
        Simctl($"location {DeviceTarget} set {TestLatitude},{TestLongitude}");
    }

    private static void Simctl(string args)
    {
        var psi = new ProcessStartInfo("xcrun", $"simctl {args}")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        using var proc = Process.Start(psi)!;
        proc.WaitForExit(15_000);
    }

    private void LoginAsRequester()
    {
        FillInput("email-input", RequesterEmail);
        FillInput("password-input", RequesterPassword);
        Driver.FindElement(By.CssSelector("[data-testid='sign-in-button']")).Click();

        // /requester redirects to /requester/submit — the Requester's first authenticated screen.
        Driver.FindElement(By.CssSelector("[data-testid='submit-request']"));
    }

    [Test]
    public void GivenRequesterOnMobilePlatform_WhenServiceCompletedEventArrives_ThenCompletionScreenElementsAreAccessible()
    {
        // Arrange — make rep1 an in-range Available candidate at the request site, then submit as the
        // requester through the app so an offer is pushed to rep1.
        BackendApiHelper.PrepareRep1AtRequestSite(BackendBaseUrl);
        LoginAsRequester();

        // Wait for the DTC dropdown to populate over HTTP, then set the location via the device-GPS path.
        WaitForSignalR(d =>
        {
            var found = d.FindElements(By.CssSelector("[data-testid='dtc-select'] option"));
            return found.Count > 1 ? found : null;
        });
        Driver.FindElement(By.CssSelector("[data-testid='use-my-location-button']")).Click();
        Driver.FindElement(By.CssSelector("[data-testid='pin-set-label']"));

        var select = new OpenQA.Selenium.Support.UI.SelectElement(Driver.FindElement(By.Id("dtc-select")));
        select.SelectByValue(Dtc001Id);
        Driver.FindElement(By.Id("request-service-button")).Click();

        // The app auto-transitions to the pending screen once the request is submitted.
        Driver.FindElement(By.CssSelector("[data-testid='requester-pending']"));

        // Act — drive rep1's offer through accept → arrive → complete; the backend then fires
        // ServiceCompleted to this requester, and the app navigates pending → tracking → complete.
        BackendApiHelper.DriveRep1ToCompletion(BackendBaseUrl);

        var heading = WaitForSignalR(d =>
        {
            var found = d.FindElements(By.CssSelector("[data-testid='completion-heading']"));
            return found.Count > 0 ? found[0] : null;
        });
        var submitButton = Driver.FindElement(By.CssSelector("[data-testid='submit-new-request-button']"));

        // Assert — AC-1 the completion heading and AC-3 the "Submit a new request" button are reachable.
        Assert.That(heading!.Text.Trim(), Is.EqualTo("Your service is complete."));
        Assert.That(submitButton.Text, Does.Contain("Submit a new request"));
    }
}
