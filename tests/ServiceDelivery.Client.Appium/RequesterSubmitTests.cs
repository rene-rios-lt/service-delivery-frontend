using System.Diagnostics;

namespace ServiceDelivery.Client.Appium;

/// <summary>
/// FE-015 coverage on Mobile: the Requester submit-request form rendered in the MAUI WKWebView on the
/// iOS simulator. The Requester persona is supported on Mobile (system-overview), so this is the
/// live-system mobile complement to the bUnit component tests. Logs in as the seeded <c>gold1</c>
/// requester (which redirects /requester → /requester/submit), then asserts the map (AC-1), the DTC
/// dropdown population over HTTP (AC-2), and the submit gate enabling once both fields are set (AC-3).
///
/// Location is driven through the AC-1 "Use my current location" device-GPS path rather than a map tap:
/// an Appium <c>.Click()</c> on the map element does not reliably produce a <c>google.maps</c> click at a
/// valid lat/lng inside the WKWebView (interaction fragility — a key IS configured). The GPS path is both
/// deterministic AND exercises the real mobile <c>MauiGeolocationService</c> (MAUI Geolocation →
/// CoreLocation), which is mobile-only and otherwise unverified (bUnit mocks <c>IGeolocationService</c>;
/// the Playwright suite exercises the browser geolocation service, not the MAUI one). The iOS simulator's
/// location is fixed via <c>simctl location set</c> and location permission is pre-granted via
/// <c>simctl privacy grant location</c> so <c>GetLocationAsync()</c> resolves without an authorization
/// prompt (this also requires <c>NSLocationWhenInUseUsageDescription</c> in the Mobile Info.plist — added
/// in this story; without it iOS denies location regardless of the grant).
///
/// Not run during the offline pipeline — requires a booted iOS simulator, the installed Mobile app, and
/// a running Appium server + backend. Execute via scripts/local/test-appium.sh against a live system.
/// </summary>
[TestFixture]
public sealed class RequesterSubmitTests : AppiumTestBase
{
    private const string AppBundleId = "com.companyname.servicedelivery.client.mobile";

    // A fixed Des Moines-area point (consistent with the seed data) fed to the iOS simulator's location.
    private const string TestLatitude = "41.5868";
    private const string TestLongitude = "-93.6250";

    private static string RequesterEmail =>
        Environment.GetEnvironmentVariable("APPIUM_REQUESTER_EMAIL") ?? "gold1@example.com";

    private static string RequesterPassword =>
        Environment.GetEnvironmentVariable("APPIUM_REQUESTER_PASSWORD") ?? "Password123!";

    // The UDID of the simulator the harness booted (exported by test-appium.sh). Falls back to "booted"
    // when absent so a manual run against a single booted device still works.
    private static string DeviceTarget =>
        Environment.GetEnvironmentVariable("APPIUM_DEVICE_UDID") ?? "booted";

    /// <summary>
    /// Pre-grants location permission and fixes the simulator's GPS BEFORE any per-test app activation,
    /// so the "Use my current location" path resolves deterministically with no CLLocationManager prompt.
    /// Runs once for the fixture; the grant is idempotent and the location persists across app launches.
    /// (Done here rather than in [SetUp] because `simctl privacy grant` can terminate a running app — at
    /// [OneTimeSetUp] time the base [SetUp]'s ActivateApp has not run yet, and each test re-activates the
    /// app fresh afterwards anyway.)
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

        // The submit form is the Requester's first authenticated screen (FE-015 — /requester redirects
        // to /requester/submit). The implicit wait covers the post-login navigation + page render.
        Driver.FindElement(By.CssSelector("[data-testid='submit-request']"));
    }

    // Waits for the DTCs to load over HTTP. The 15s implicit wait covers the populate; >1 because the
    // disabled placeholder option is always present.
    private void WaitForDtcOptions()
    {
        WaitForSignalR(d =>
        {
            var found = d.FindElements(By.CssSelector("[data-testid='dtc-select'] option"));
            return found.Count > 1 ? found : null;
        });
    }

    [Test]
    public void GivenRequesterLoggedIn_WhenSubmitScreenLoads_ThenMapPanelIsShown()
    {
        // Arrange / Act
        LoginAsRequester();

        // Assert
        var map = Driver.FindElement(By.CssSelector("[data-testid='submit-map']"));
        Assert.That(map.Displayed, Is.True);
    }

    [Test]
    public void GivenRequesterLoggedIn_WhenSubmitScreenLoads_ThenDtcDropdownHasOptions()
    {
        // Arrange
        LoginAsRequester();

        // Act
        var options = WaitForSignalR(d =>
        {
            var found = d.FindElements(By.CssSelector("[data-testid='dtc-select'] option"));
            return found.Count > 1 ? found : null;
        });

        // Assert
        Assert.That(options!.Count, Is.GreaterThan(1));
    }

    [Test]
    public void GivenDtcSelectedAndLocationSet_WhenFormCompleted_ThenRequestServiceButtonIsEnabled()
    {
        // Arrange
        LoginAsRequester();
        WaitForDtcOptions();

        // Act — set location via the AC-1 device-GPS path (MauiGeolocationService → CoreLocation reads
        // the fixed simulator location pre-set in [OneTimeSetUp]), confirm the pin-set label, then select
        // a DTC. Both gates of AC-3 (location AND DTC) are now satisfied.
        Driver.FindElement(By.CssSelector("[data-testid='use-my-location-button']")).Click();
        Driver.FindElement(By.CssSelector("[data-testid='pin-set-label']"));

        var select = new OpenQA.Selenium.Support.UI.SelectElement(
            Driver.FindElement(By.Id("dtc-select")));
        select.SelectByIndex(1);

        // Assert
        var button = Driver.FindElement(By.Id("request-service-button"));
        Assert.That(button.Enabled, Is.True);
    }
}
