namespace ServiceDelivery.Client.E2E;

/// <summary>
/// Launches a single headless Chromium browser instance for the whole E2E run (Infra-2: tests are
/// independent — they never share a browser context or page; each test gets a fresh isolated
/// <see cref="IPage"/> via <see cref="E2ETestBase"/>). The browser is created once in
/// <see cref="OneTimeSetUp"/> and disposed in <see cref="OneTimeTearDown"/>.
/// </summary>
[SetUpFixture]
public sealed class PlaywrightFixture
{
    public static IPlaywright Playwright { get; private set; } = default!;

    public static IBrowser Browser { get; private set; } = default!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await Browser.CloseAsync();
        Playwright.Dispose();
    }
}

/// <summary>
/// Shared base for every E2E test class. Each test runs in its own fresh
/// <see cref="IBrowserContext"/> + <see cref="IPage"/> (Infra-2: no shared state between tests —
/// the suite passes in any order). The base URL is read from the <c>E2E_BASE_URL</c> environment
/// variable (default <c>http://localhost:5023</c>, the Web WASM host port).
///
/// Convention (Infra-3): all element assertions target <c>data-testid</c> selectors — never pixel
/// screenshots.
///
/// Convention (Infra-4): any assertion that waits on a SignalR-driven UI update MUST use
/// <c>page.WaitForSelectorAsync(selector, new() { Timeout = 10_000 })</c> with a timeout of at
/// least 10 000 ms, because SignalR fan-out is asynchronous and slower than a synchronous DOM
/// update. FE-001 / FE-002 / FE-021 involve no SignalR events, so no such wait appears in this
/// story's test files; the rule governs all future E2E tests.
/// </summary>
public abstract class E2ETestBase
{
    private IBrowserContext _context = default!;

    protected IPage Page { get; private set; } = default!;

    protected static string BaseUrl =>
        Environment.GetEnvironmentVariable("E2E_BASE_URL") ?? "http://localhost:5023";

    protected static string DispatcherPassword =>
        Environment.GetEnvironmentVariable("E2E_DISPATCHER_PASSWORD") ?? "Password1!";

    [SetUp]
    public async Task SetUp()
    {
        _context = await PlaywrightFixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = BaseUrl
        });
        Page = await _context.NewPageAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.CloseAsync();
    }

    /// <summary>
    /// Logs in as the seeded <c>dispatcher1</c> account and waits for the Dispatcher route. Reused by
    /// the FE-002 and FE-021 test classes, which both require an authenticated session as a precondition.
    /// </summary>
    protected async Task LoginAsDispatcherAsync()
    {
        await Page.GotoAsync("/login");
        await Page.WaitForSelectorAsync("[data-testid='login-card']");

        await Page.FillAsync("[data-testid='email-input'] input", "dispatcher1");
        await Page.FillAsync("[data-testid='password-input'] input", DispatcherPassword);
        await Page.ClickAsync("[data-testid='sign-in-button']");

        await Page.WaitForURLAsync("**/dispatcher");
    }
}
