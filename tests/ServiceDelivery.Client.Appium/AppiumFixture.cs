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
        Environment.GetEnvironmentVariable("APPIUM_REP_PASSWORD") ?? "Password1!";

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
    }

    [TearDown]
    public void TearDown()
    {
        Driver?.Quit();
        Driver?.Dispose();
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
    /// Logs in as a seeded rep account via the login screen and waits for the take-over screen to
    /// appear. Reused by every test class that needs an authenticated rep session as a precondition
    /// (AC-4 reuses the <c>rep1</c>–<c>rep8</c> seed accounts).
    /// </summary>
    protected void Login(string username, string password)
    {
        Driver.FindElement(MobileBy.AccessibilityId("email-input")).SendKeys(username);
        Driver.FindElement(MobileBy.AccessibilityId("password-input")).SendKeys(password);
        Driver.FindElement(MobileBy.AccessibilityId("sign-in-button")).Click();

        // The take-over screen is the first authenticated screen for a rep (FE-001 / AC-8).
        Driver.FindElement(MobileBy.AccessibilityId("take-over-button"));
    }

    /// <summary>
    /// Drives <see cref="Login"/> as <c>rep1</c>, then selects the first idle vehicle and takes it
    /// over, leaving the session on the idle view. Shared by the FE-020 / FE-008 / FE-011 scenarios
    /// that all require an owned vehicle as a precondition.
    /// </summary>
    protected void TakeOverFirstIdleVehicle()
    {
        Login("rep1", RepPassword);

        Driver.FindElement(MobileBy.AccessibilityId("idle-vehicle-row")).Click();
        Driver.FindElement(MobileBy.AccessibilityId("take-over-button")).Click();

        // The idle / available view is the post-take-over screen (FE-020 / AC-11).
        Driver.FindElement(MobileBy.AccessibilityId("available-indicator"));
    }
}
