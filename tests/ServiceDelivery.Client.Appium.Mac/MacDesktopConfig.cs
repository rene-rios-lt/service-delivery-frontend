namespace ServiceDelivery.Client.Appium.Mac;

/// <summary>
/// Builds the Mac2Driver <see cref="AppiumOptions"/> shared by the Desktop E2E fixture from environment
/// variables set by <c>scripts/local/test-appium-mac.sh</c> (FE-003 Phase 3). The capabilities point the
/// mac2 driver at the Debug-built Mac Catalyst <c>.app</c> bundle. Mirrors the iOS <c>AppiumConfig</c> but
/// for the Mac platform — a separate config because mac2 and XCUITest use different platform / automation
/// names and capability sets (<c>APPIUM_DEVICE_UDID</c> is meaningless for mac2).
/// </summary>
public static class MacDesktopConfig
{
    /// <summary>Bundle id of the Desktop Mac Catalyst app (from its .csproj ApplicationId).</summary>
    public const string DesktopBundleId = "com.companyname.servicedelivery.client.desktop";

    /// <summary>Appium server URL — default <c>http://localhost:4723</c>.</summary>
    public static string ServerUrl =>
        Environment.GetEnvironmentVariable("APPIUM_SERVER_URL") ?? "http://localhost:4723";

    /// <summary>Absolute path to the built Desktop <c>.app</c> bundle; set by test-appium-mac.sh after build.</summary>
    public static string? AppPath =>
        Environment.GetEnvironmentVariable("APPIUM_APP_PATH");

    /// <summary>Backend base URL the app talks to — default <c>http://localhost:5180</c>.</summary>
    public static string BackendBaseUrl =>
        Environment.GetEnvironmentVariable("APPIUM_BASE_URL") ?? "http://localhost:5180";

    /// <summary>Seeded Dispatcher account password — default <c>Password123!</c>.</summary>
    public static string DispatcherPassword =>
        Environment.GetEnvironmentVariable("APPIUM_DISPATCHER_PASSWORD") ?? "Password123!";

    /// <summary>
    /// Wait budget for SignalR-driven UI changes: the fleet count climbing above zero is pushed
    /// asynchronously over the VehiclePositionHub, so locators that depend on it must poll for at least
    /// 15 seconds rather than fail on the first DOM snapshot.
    /// </summary>
    public static readonly TimeSpan SignalRWait = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Builds the mac2 capabilities. <c>appium:app</c> (the built bundle path) and <c>appium:bundleId</c>
    /// come from the env vars / the known bundle id; if the app path is absent the driver launches the
    /// installed bundle by id. <c>appium:showServerLogs</c> aids diagnosis on a developer machine.
    /// </summary>
    public static AppiumOptions BuildOptions()
    {
        var options = new AppiumOptions
        {
            PlatformName = "Mac",
            AutomationName = "mac2",
        };

        options.AddAdditionalAppiumOption("bundleId", DesktopBundleId);

        if (!string.IsNullOrWhiteSpace(AppPath))
        {
            options.App = AppPath;
        }

        return options;
    }
}
