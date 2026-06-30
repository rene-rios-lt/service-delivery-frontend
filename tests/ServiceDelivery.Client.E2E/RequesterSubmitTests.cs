namespace ServiceDelivery.Client.E2E;

/// <summary>
/// FE-015 coverage: the Requester submit-request flow on the live Web host (black box — assertions target
/// <c>data-testid</c> selectors and the resulting URL only). Logs in as the seeded <c>gold1</c> requester,
/// which redirects to the submit screen (/requester → /requester/submit). Covers the map presence (AC-1),
/// the DTC dropdown population (AC-2), the submit-enable gate (AC-3), the post-submit navigation (AC-4),
/// and the inline-error path (AC-5).
///
/// Location is driven through the "Use my current location" path (AC-1 device-GPS) rather than a map tap:
/// Playwright grants the browser context a fixed geolocation, so the test is deterministic regardless of
/// whether the real Google map / its click listener loads in CI. The DTC dropdown is a native
/// <c>&lt;select&gt;</c>, whose <c>&lt;option&gt;</c>s are never "visible" to Playwright while the dropdown
/// is closed — so option presence is asserted via <see cref="Microsoft.Playwright.ILocator.CountAsync"/>
/// (the Attached state), never a visibility wait.
///
/// Not run during the offline pipeline — requires a running backend (start.sh) and the Web host. Execute
/// via scripts/local/test-playwright.sh (or test-e2e.sh) against a live system.
/// </summary>
[TestFixture]
public sealed class RequesterSubmitTests : E2ETestBase
{
    // A fixed position in the Des Moines area used for the deterministic "Use my current location" path.
    private const double TestLatitude = 41.5868;
    private const double TestLongitude = -93.6250;

    private static string RequesterEmail =>
        Environment.GetEnvironmentVariable("E2E_REQUESTER_EMAIL") ?? "gold1@example.com";

    private static string RequesterPassword =>
        Environment.GetEnvironmentVariable("E2E_REQUESTER_PASSWORD") ?? "Password123!";

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

    // Sets the location via the AC-1 "Use my current location" device-GPS path. Playwright grants the
    // browser context a fixed geolocation so the flow is deterministic in CI (no dependency on the real
    // Google map tile/listener loading). After the click the pin-set label confirms the location is set.
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

    [Test]
    public async Task GivenRequesterLoggedIn_WhenSubmitPageLoads_ThenGoogleMapContainerIsVisible()
    {
        // Arrange
        await LoginAsRequesterAsync();

        // Act
        var map = await Page.WaitForSelectorAsync("[data-testid='submit-map']");

        // Assert
        Assert.That(await map!.IsVisibleAsync(), Is.True);
    }

    [Test]
    public async Task GivenRequesterLoggedIn_WhenSubmitPageLoads_ThenDtcSelectHasOptions()
    {
        // Arrange
        await LoginAsRequesterAsync();

        // Act
        await WaitForDtcOptionsAsync();

        // Assert
        var optionCount = await Page.Locator("[data-testid='dtc-select'] option[value]:not([value=''])").CountAsync();
        Assert.That(optionCount, Is.GreaterThan(0));
    }

    [Test]
    public async Task GivenRequesterLoggedIn_WhenDtcSelectedAndLocationSet_ThenRequestServiceButtonIsEnabled()
    {
        // Arrange
        await LoginAsRequesterAsync();
        await WaitForDtcOptionsAsync();

        // Act
        // Set location via the device-GPS path, then select a DTC — both gates of AC-3.
        await UseDeviceLocationAsync();
        await Page.SelectOptionAsync("[data-testid='dtc-select']", new SelectOptionValue { Index = 1 });

        // Assert
        var button = Page.Locator("[data-testid='request-service-button']");
        Assert.That(await button.IsEnabledAsync(), Is.True);
    }

    [Test]
    public async Task GivenBothFieldsSet_WhenRequestServiceSubmitted_ThenNavigatesToPendingRoute()
    {
        // Arrange
        await LoginAsRequesterAsync();
        await WaitForDtcOptionsAsync();
        await UseDeviceLocationAsync();
        await Page.SelectOptionAsync("[data-testid='dtc-select']", new SelectOptionValue { Index = 1 });

        // Act
        await Page.ClickAsync("[data-testid='request-service-button']");

        // Assert
        await Page.WaitForURLAsync("**/requester/pending", new() { Timeout = 10_000 });
        Assert.That(Page.Url, Does.Contain("/requester/pending"));
    }

    [Test]
    public async Task GivenApiErrorOnSubmit_WhenRequestServiceSubmitted_ThenInlineErrorIsShown()
    {
        // Arrange
        // Force a backend error by intercepting the submit POST and returning 500, so the inline error
        // band renders and the form stays (AC-5).
        await LoginAsRequesterAsync();
        await WaitForDtcOptionsAsync();
        await Page.RouteAsync("**/service-requests", async route =>
        {
            await route.FulfillAsync(new() { Status = 500, ContentType = "application/json", Body = "{}" });
        });
        await UseDeviceLocationAsync();
        await Page.SelectOptionAsync("[data-testid='dtc-select']", new SelectOptionValue { Index = 1 });

        // Act
        await Page.ClickAsync("[data-testid='request-service-button']");

        // Assert
        var error = await Page.WaitForSelectorAsync("[data-testid='submit-error']", new() { Timeout = 10_000 });
        Assert.That(await error!.IsVisibleAsync(), Is.True);
        Assert.That(Page.Url, Does.Contain("/requester/submit"));
    }
}
