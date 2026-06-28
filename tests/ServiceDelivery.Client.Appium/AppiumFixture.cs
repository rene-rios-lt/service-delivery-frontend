namespace ServiceDelivery.Client.Appium;

/// <summary>
/// Builds the XCUITest <see cref="AppiumOptions"/> shared by every Appium test from environment
/// variables set by <c>scripts/local/test-appium.sh</c> (AC-2, AC-4). The capabilities point the
/// XCUITest driver at the booted iOS simulator and the freshly installed Mobile <c>.app</c> bundle.
///
/// There is no <c>[OneTimeSetUp]</c> driver here on purpose (AC-4): a single shared driver session
/// would couple tests through app state (auth token, navigation stack). Instead each test class
/// derives from <see cref="AppiumTestBase"/>, which spins up a fresh <see cref="IOSDriver"/> in
/// <c>[SetUp]</c> and quits it in <c>[TearDown]</c> so the suite passes in any order.
/// </summary>
public static class AppiumConfig
{
    /// <summary>Appium server URL — default <c>http://localhost:4723</c> (AC-2 default port).</summary>
    public static string ServerUrl =>
        Environment.GetEnvironmentVariable("APPIUM_SERVER_URL") ?? "http://localhost:4723";

    /// <summary>UDID of the booted iOS simulator; set by <c>test-appium.sh</c> after device resolution.</summary>
    public static string? DeviceUdid =>
        Environment.GetEnvironmentVariable("APPIUM_DEVICE_UDID");

    /// <summary>Absolute path to the built <c>.app</c> bundle; set by <c>test-appium.sh</c> after build.</summary>
    public static string? AppPath =>
        Environment.GetEnvironmentVariable("APPIUM_APP_PATH");

    /// <summary>Backend base URL the app talks to — default <c>http://localhost:5180</c>.</summary>
    public static string BackendBaseUrl =>
        Environment.GetEnvironmentVariable("APPIUM_BASE_URL") ?? "http://localhost:5180";

    /// <summary>Shared password for the seeded <c>rep1</c>–<c>rep8</c> accounts (AC-4).</summary>
    public static string RepPassword =>
        Environment.GetEnvironmentVariable("APPIUM_REP_PASSWORD") ?? "Password123!";

    /// <summary>
    /// Wait budget for SignalR-driven UI changes (AC-6): the job-offer screen appearing and the idle
    /// view populating are pushed asynchronously, so locators that depend on them must poll for at
    /// least 15 seconds rather than fail on the first DOM snapshot.
    /// </summary>
    public static readonly TimeSpan SignalRWait = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Builds the iOS XCUITest capabilities. <c>app</c> and <c>udid</c> come from the script-set env
    /// vars; if either is absent the driver creation will fail fast with a clear message rather than
    /// silently targeting the wrong device.
    /// </summary>
    public static AppiumOptions BuildOptions()
    {
        var options = new AppiumOptions
        {
            PlatformName = "iOS",
            AutomationName = "XCUITest"
        };

        if (!string.IsNullOrWhiteSpace(DeviceUdid))
        {
            options.AddAdditionalAppiumOption("udid", DeviceUdid);
        }

        if (!string.IsNullOrWhiteSpace(AppPath))
        {
            options.App = AppPath;
        }

        // The app is already installed by test-appium.sh; do not reinstall on every session so the
        // suite stays fast and each [SetUp] only resets app state, not the whole install.
        options.AddAdditionalAppiumOption("noReset", true);
        options.AddAdditionalAppiumOption("autoAcceptAlerts", true);

        return options;
    }
}

/// <summary>
/// Shared base for every Appium test class. Each test gets its own fresh <see cref="IOSDriver"/>
/// session (AC-4: no shared state — the suite passes in any order). The implicit wait is set to the
/// SignalR budget (AC-6) so locator lookups for push-driven UI tolerate asynchronous fan-out.
/// </summary>
public abstract class AppiumTestBase
{
    protected IOSDriver Driver { get; private set; } = default!;

    protected static string RepPassword => AppiumConfig.RepPassword;

    private const string AppBundleId = "com.companyname.servicedelivery.client.mobile";

    [SetUp]
    public void SetUp()
    {
        Driver = new IOSDriver(
            new Uri(AppiumConfig.ServerUrl),
            AppiumConfig.BuildOptions(),
            TimeSpan.FromSeconds(180));

        // AC-6: a 15-second implicit wait covers SignalR-driven screen transitions (job offer
        // appearing, idle view populating) without an explicit poll loop at every call site.
        Driver.Manage().Timeouts().ImplicitWait = AppiumConfig.SignalRWait;

        // Test isolation: noReset keeps the app installed and running between sessions, so without
        // this a prior test can leave the app on a deep screen (active job, offer) that the
        // logged-out reset below can't navigate away from. Terminating and re-activating forces a
        // fresh launch to the app's landing route, so every test starts from the same known state.
        Driver.TerminateApp(AppBundleId);
        Driver.ActivateApp(AppBundleId);

        // MAUI Blazor Hybrid renders all UI inside a WKWebView (BlazorWebView). XCUITest's native
        // accessibility tree sees only the WebView container — HTML elements are not exposed as
        // native accessibility IDs. Switching to the WEBVIEW context lets Selenium use standard
        // CSS selectors against the HTML DOM rendered by Blazor.
        SwitchToWebContext();

        // SecureStorageTokenStore.GetTokenAsync can throw on first launch in the iOS simulator
        // before the Keychain is ready, surfacing as Blazor's "An unhandled error has occurred"
        // banner. Clicking Reload re-initialises the circuit, which succeeds on the second
        // attempt. The root cause is guarded in SecureStorageTokenStore, but this catches any
        // remaining cases so no test fails due to this transient startup race.
        DismissStartupErrorIfPresent();

        // Test isolation: the JWT lives in the iOS Keychain (SecureStorage), which survives both
        // app reinstall and Appium's noReset session, so a token left by a prior test/run would
        // auto-authenticate the app and skip the login screen. Each test must start logged out.
        EnsureLoggedOut();
    }

    /// <summary>
    /// Guarantees the app is on the login screen before the test body runs. If a persisted session
    /// has routed the app into an authenticated view, this drives the app's own logout (app-bar menu
    /// → "Log out"), which clears the stored token — leaving a clean login screen for the test.
    /// </summary>
    private void EnsureLoggedOut()
    {
        Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(3);
        try
        {
            if (Driver.FindElements(By.CssSelector("[data-testid='login-card']")).Count > 0)
            {
                return; // already logged out
            }

            // Authenticated via a persisted session. Open the nav drawer if it isn't already open.
            if (Driver.FindElements(By.CssSelector("[data-testid='menu-item-logout']")).Count == 0)
            {
                Driver.FindElement(By.CssSelector("[data-testid='appbar-menu-affordance']")).Click();
            }

            // The Mobile drawer renders each item as a MudNavLink: the data-testid sits on the outer
            // wrapper div, but the click handler lives on the inner .mud-nav-link element. Clicking
            // the wrapper does not fire the handler, so target the inner element to log out.
            Driver.FindElement(By.CssSelector("[data-testid='menu-item-logout'] .mud-nav-link")).Click();
        }
        catch (NoSuchElementException)
        {
            // No menu/logout reachable — treat as already logged out and let the test's own
            // login-screen wait surface a clear failure if that assumption is wrong.
        }
        finally
        {
            Driver.Manage().Timeouts().ImplicitWait = AppiumConfig.SignalRWait;
        }

        Driver.FindElement(By.CssSelector("[data-testid='login-card']"));
    }

    private void SwitchToWebContext()
    {
        var deadline = DateTime.UtcNow + AppiumConfig.SignalRWait;
        while (DateTime.UtcNow < deadline)
        {
            var webContext = Driver.Contexts.FirstOrDefault(c => c.Contains("WEBVIEW"));
            if (webContext is not null)
            {
                Driver.Context = webContext;
                return;
            }
            Thread.Sleep(500);
        }
        throw new InvalidOperationException(
            "No WEBVIEW context found within the SignalR budget. The BlazorWebView may not have loaded.");
    }

    private void DismissStartupErrorIfPresent()
    {
        // Use a short wait so we don't burn the full SignalR budget if startup was clean.
        Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(3);
        try
        {
            var reload = Driver.FindElement(By.PartialLinkText("Reload"));
            if (reload.Displayed)
            {
                reload.Click();
                Thread.Sleep(2000);
            }
        }
        catch (NoSuchElementException)
        {
            // No error banner — startup was clean.
        }
        finally
        {
            Driver.Manage().Timeouts().ImplicitWait = AppiumConfig.SignalRWait;
        }
    }

    [TearDown]
    public void TearDown()
    {
        CaptureScreenshotIfRequested();
        Driver?.Quit();
        Driver?.Dispose();
    }

    // When SD_SHOT_DIR is set, save a screenshot of the final screen as <TestName>.png. Used by the
    // AI-review render-and-screenshot check to visually compare the live screen against its mockup —
    // off by default and best-effort, so a capture failure never fails the test.
    private void CaptureScreenshotIfRequested()
    {
        var dir = Environment.GetEnvironmentVariable("SD_SHOT_DIR");
        if (string.IsNullOrWhiteSpace(dir) || Driver is null)
            return;

        try
        {
            System.IO.Directory.CreateDirectory(dir);
            var name = TestContext.CurrentContext.Test.Name;
            Driver.GetScreenshot().SaveAsFile(System.IO.Path.Combine(dir, $"{name}.png"));
        }
        catch
        {
            // Screenshot capture is diagnostics only — never fail a test because of it.
        }
    }

    /// <summary>
    /// Explicit poll for a SignalR-driven condition (AC-6): polls every 500 ms for up to the
    /// 15-second SignalR budget until <paramref name="condition"/> returns a non-null/true value.
    /// Used by the job-offer scenarios where the screen and countdown change in response to async
    /// fan-out rather than a synchronous tap.
    /// </summary>
    protected TResult WaitForSignalR<TResult>(Func<IOSDriver, TResult> condition)
    {
        var wait = new WebDriverWait(Driver, AppiumConfig.SignalRWait)
        {
            PollingInterval = TimeSpan.FromMilliseconds(500)
        };
        wait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(StaleElementReferenceException));
        return wait.Until(_ => condition(Driver));
    }

    /// <summary>
    /// Non-throwing variant of <see cref="WaitForSignalR{TResult}"/> (BUG-040): returns
    /// <c>default</c> (e.g. <c>null</c> for an element) instead of throwing
    /// <see cref="OpenQA.Selenium.WebDriverTimeoutException"/> when the 15-second SignalR budget
    /// elapses. This lets a caller's own outer retry loop govern the overall wait — e.g. waiting out
    /// a server-pushed offer expiry that can take longer than a single budget — rather than aborting
    /// the whole test on its first lap.
    /// </summary>
    protected TResult? TryWaitForSignalR<TResult>(Func<IOSDriver, TResult> condition)
    {
        try
        {
            return WaitForSignalR(condition);
        }
        catch (OpenQA.Selenium.WebDriverTimeoutException)
        {
            return default;
        }
    }

    /// <summary>
    /// Logs in as a seeded rep account via the login screen and waits for the take-over screen to
    /// appear. Reused by every test class that needs an authenticated rep session as a precondition
    /// (AC-4 reuses the <c>rep1</c>–<c>rep8</c> seed accounts).
    /// </summary>
    protected void Login(string username, string password)
    {
        FillInput("email-input", username);
        FillInput("password-input", password);

        Driver.FindElement(By.CssSelector("[data-testid='sign-in-button']")).Click();

        // The take-over screen is the first authenticated screen for a rep (FE-001 / AC-8).
        Driver.FindElement(By.CssSelector("[data-testid='take-over-button']"));
    }

    /// <summary>
    /// Types <paramref name="value"/> into the <c>data-testid</c> input and commits the Blazor
    /// two-way binding. MudTextField binds <c>@bind-Value</c> on the <c>change</c> event (blur),
    /// which Appium <c>SendKeys</c> does not raise — so the bound ViewModel property never updates
    /// and the login submits empty/partial credentials ("Invalid email or password"). We Clear()
    /// first (noReset:true persists prior text across sessions), type, then dispatch <c>input</c>
    /// and <c>change</c> via JS so the binding commits before submit — mirroring what Playwright's
    /// FillAsync does for the web E2E suite.
    /// </summary>
    protected void FillInput(string testId, string value)
    {
        var el = Driver.FindElement(By.CssSelector($"[data-testid='{testId}']"));
        el.Click();
        el.Clear();
        el.SendKeys(value);
        ((IJavaScriptExecutor)Driver).ExecuteScript(
            "arguments[0].dispatchEvent(new Event('input', { bubbles: true }));" +
            "arguments[0].dispatchEvent(new Event('change', { bubbles: true }));",
            el);
    }

    /// <summary>
    /// Drives <see cref="Login"/> as <c>rep1</c>, then selects the first idle vehicle and takes it
    /// over, leaving the session on the idle view. Shared by the FE-020 / FE-008 / FE-011 scenarios
    /// that all require an owned vehicle as a precondition.
    /// </summary>
    protected void TakeOverFirstIdleVehicle()
    {
        Login("rep1@dealer.com", RepPassword);

        Driver.FindElement(By.CssSelector("[data-testid='idle-vehicle-row']")).Click();
        Driver.FindElement(By.CssSelector("[data-testid='take-over-button']")).Click();

        // The idle / available view is the post-take-over screen (FE-020 / AC-11).
        Driver.FindElement(By.CssSelector("[data-testid='available-indicator']"));
    }

    /// <summary>
    /// Closes (terminates) the app under test (QUAL-009). The FE-023 heartbeat runs on a background
    /// timer inside the BlazorWebView; terminating the app stops it abruptly — the "app closed /
    /// backgrounded" path of AC-2 — so the backend's stale-heartbeat sweep can then time the rep out.
    /// No graceful teardown runs, which is exactly the case under test.
    /// </summary>
    protected void CloseAppUnderTest()
    {
        Driver.TerminateApp(AppBundleId);
    }
}
