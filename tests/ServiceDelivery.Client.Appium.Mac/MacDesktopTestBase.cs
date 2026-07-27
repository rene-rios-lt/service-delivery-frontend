using System.Diagnostics;
using OpenQA.Selenium.Appium.Mac;

namespace ServiceDelivery.Client.Appium.Mac;

/// <summary>
/// Shared base for the Desktop Mac2Driver E2E fixture (FE-003 Phase 3). It opens ONE
/// <see cref="AppiumDriver"/> session in <see cref="OneTimeSetUp"/> and reuses it across the class:
/// launching the Mac Catalyst Desktop app is far slower than activating an iOS simulator app, so per-test
/// isolation is impractical.
/// <para>
/// NATIVE (mac2) design — cycle-3 redesign. The appium <c>mac2</c> driver (WebDriverAgentMac) is native-only
/// XCTest automation of the macOS accessibility (AX) tree. Unlike the iOS XCUITest base — which switches to a
/// WEBVIEW context and asserts on <c>data-testid</c> CSS selectors — mac2 implements NO contexts API, NO CSS
/// selectors, and NO DOM JavaScript execution, so this base cannot use any of those. Instead every element is
/// located by native mac2 locators (XPath over the XCTest element tree, anchored on AX-visible properties).
/// WKWebView content still surfaces in that tree because XCTest activates the app's full accessibility mode:
/// HTML inputs appear as <c>XCUIElementTypeTextField</c> / <c>…SecureTextField</c>, buttons as
/// <c>XCUIElementTypeButton</c> (visible text → label), and visible text as <c>XCUIElementTypeStaticText</c>.
/// Crucially, <c>display:none</c> content does NOT appear in the AX tree, so asserting the ABSENCE of the
/// Blazor error-banner text is inherently visibility-aware.
/// </para>
/// <para>
/// FLEET MARKERS ARE AX-INVISIBLE (cycle-10 root cause, proven by screenshot). Under this live gate the fleet
/// markers RENDER visually on the Google Map (teardown screenshots show the marker cluster at the arranged
/// coordinates) but NEVER surface in the macOS AX tree: google.maps marks its marker overlay panes
/// <c>aria-hidden</c> (and/or prunes them from accessibility), which hides ALL descendants regardless of the
/// <c>role="img"</c> / <c>aria-label</c> stamped on the marker content div. Playwright DOM queries see the
/// markers; XCTest AX queries never will. So this base does NOT anchor on markers. Instead it anchors on the
/// FleetMap component's visually-hidden-but-AX-exposed FLEET SUMMARY (<c>data-testid="fleet-a11y-summary"</c>),
/// rendered OUTSIDE the map pane: one entry per visible vehicle carrying the text
/// "Vehicle &lt;registration&gt; — &lt;state&gt; — &lt;lat&gt;,&lt;lng&gt;" (4-dp coords, recomputed on every
/// position render). Because it uses the sr-only clip pattern (not display:none / visibility:hidden), each
/// entry surfaces as accessible static text — a real fleet readout for VoiceOver and the native anchor for the
/// marker-presence / marker-move assertions here. (This is the same summary that gives VoiceOver users the
/// fleet they otherwise cannot perceive from the JS-rendered map.)
/// </para>
/// <para>
/// WAIT STRATEGY (cycle-8 observer-effect fix) — the session implicit wait is pinned to ZERO for the whole
/// session and every positive lookup goes through the explicit <see cref="WaitForSignalR"/> bounded poll
/// (500 ms laps, 15 s budget). This is NOT stylistic. Under the mac2 driver a non-zero implicit wait makes
/// WebDriverAgentMac busy-loop full AX-tree snapshots server-side for up to the entire budget on EVERY
/// FindElement(s) call, and those snapshots are serviced on the app's MAIN thread — the same thread Blazor's
/// dispatcher needs to process a hub-event-driven render. A marker-materialization poll layered on an implicit
/// wait therefore hammers the app's main thread continuously and STARVES the very render it is waiting for (a
/// classic observer effect). Live isolation proved the render itself is sound: shell-launched OUTSIDE XCTest
/// with a persisted dispatcher token against a live backend, the Desktop app renders the fleet markers on the
/// Google Map end-to-end (SignalR receive → ViewModel merge → JS interop → google.maps marker, screenshot-
/// verified); markers fail to appear ONLY under Appium/XCTest launch, i.e. only under the implicit-wait AX
/// hammering. With implicit=0 each poll lap is a single fast AX query followed by a 500 ms breather, and that
/// gap is exactly what lets the app's main thread drain its render queue between laps. (BUG-048's own warning,
/// verbatim: do NOT widen the implicit wait — mixing implicit + explicit waits compounds unpredictably.)
/// </para>
/// Each locator below is anchored on a real, AX-visible property of the committed markup; the HTML source is
/// noted inline so the anchor can be re-verified against the page.
/// </summary>
public abstract class MacDesktopTestBase
{
    protected AppiumDriver Driver { get; private set; } = default!;

    // ---- Native mac2 locators (XPath over the XCTest tree), each anchored on committed AX-visible markup ----

    // Login card presence — Login.razor: <MudText Typo="Typo.body2">Sign in to continue</MudText> renders a
    // <p> that surfaces as AX static text (the MudPaper data-testid='login-card' itself is invisible to AX).
    protected static readonly By LoginCardAnchor = StaticText("Sign in to continue");

    // Email field — Login.razor: <MudTextField ... /> (default text InputType) → <input type="text"> → the
    // plain AX text field. The password field below is a secure text field, so the element TYPE alone
    // disambiguates the two inputs without needing a DOM id/testid.
    protected static readonly By EmailField = By.XPath("//XCUIElementTypeTextField");

    // Password field — Login.razor: <MudTextField InputType="InputType.Password" /> → <input type="password">
    // → AX secure text field.
    protected static readonly By PasswordField = By.XPath("//XCUIElementTypeSecureTextField");

    // Sign-in button — Login.razor: <MudButton>Sign in</MudButton> → <button> → AX button, its visible text
    // becoming the label.
    protected static readonly By SignInButton =
        By.XPath("//XCUIElementTypeButton[@label=\"Sign in\" or @title=\"Sign in\" or @value=\"Sign in\"]");

    // Dashboard presence — DispatcherHome.razor: <div class="sd-dispatcher-rail__head">ACTIVE REQUESTS</div>,
    // always rendered on the dispatcher dashboard. A plain <div>'s text can surface under a wrapping group,
    // so this matches any element type carrying the text.
    protected static readonly By DashboardAnchor = AnyText("ACTIVE REQUESTS");

    // Blazor unhandled-error banner — Desktop wwwroot/index.html: <div id="blazor-error-ui">An unhandled
    // error has occurred.</div>, shipped display:none and flipped to display:block by Blazor on an unhandled
    // exception. display:none content never enters the AX tree, so finding this static text can only happen
    // when the banner is actually SHOWN — the absence check is therefore inherently visibility-aware.
    protected static readonly By ErrorBanner = StaticTextStartsWith("An unhandled error has occurred");

    // Persona avatar (Desktop AccountMenu style — the logout affordance) — PersonaMenu.razor:
    // <MudAvatar ...>@Initials</MudAvatar>; Initials derive from the seeded dispatcher's name
    // "Alex Dispatcher" (DataSeeder) → "AD".
    protected static readonly By PersonaAvatar = AnyText("AD");

    // Logout menu item — PersonaMenu.razor: <MudListItem ...>Log out</MudListItem>, the Dispatcher menu's
    // logout entry (PersonaMenuFactory), visible once the account panel is open.
    protected static readonly By LogoutMenuItem = AnyText("Log out");

    // Fleet-summary entry text prefix — FleetMap renders a visually-hidden (sr-only) but AX-exposed summary
    // OUTSIDE the map pane, one entry per visible vehicle carrying the text
    // "Vehicle <registration> — <state> — <lat>,<lng>" (see the class summary's AX-INVISIBLE-MARKERS note).
    // Each entry surfaces as accessible static text beginning "Vehicle "; the trailing " — <state> — <lat>,<lng>"
    // portion changes on every position render, so the coordinate suffix is the marker-move signal.
    protected const string FleetSummaryEntryTextPrefix = "Vehicle ";

    // Any visible-vehicle summary entry (the marker-presence anchor). Uses AnyTextStartsWith rather than a
    // static-text-only match because WebKit may surface a plain <div>'s text under a wrapping group.
    protected static readonly By FleetSummaryEntryAny = AnyTextStartsWith(FleetSummaryEntryTextPrefix);

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        try
        {
            // Per-FIXTURE cold-start hygiene. test-appium-mac.sh wipes the Desktop app's persisted Preferences
            // (the NSUserDefaults JWT written by PreferencesTokenStore) ONCE before the whole run, so the FIRST
            // fixture cold-starts unauthenticated. But every fixture opens its OWN mac2 session, and a prior
            // fixture's PASSING login persists that token — so a LATER fixture's app launches straight into the
            // authenticated dashboard. EnsureLoggedOut would then have to drive the app's own logout, an
            // AX-fragile path (the persona-avatar initials are not reliably AX-exposed). Wiping here, before the
            // driver launches the app, generalises the script's per-run wipe to per-fixture: every fixture
            // cold-starts on the login card and takes EnsureLoggedOut's clean early-return path. Best-effort —
            // a token-less app simply has nothing to delete.
            WipePersistedAppState();

            // MacDriver is the concrete mac2 AppiumDriver (AppiumDriver itself is abstract); the Driver
            // property stays typed as the AppiumDriver base so the shared helpers are driver-agnostic.
            Driver = new MacDriver(
                new Uri(MacDesktopConfig.ServerUrl),
                MacDesktopConfig.BuildOptions(),
                TimeSpan.FromSeconds(300));

            // Pin the implicit wait to ZERO for the whole session (see the class summary's WAIT STRATEGY
            // note). A non-zero implicit wait makes WebDriverAgentMac busy-loop full AX-tree snapshots on the
            // app's MAIN thread per FindElement(s) call, starving the hub-driven Blazor render the marker
            // polls are waiting for (observer effect). Every positive lookup is instead an explicit
            // WaitForSignalR bounded poll whose 500 ms lap gap lets the main thread drain its render queue.
            Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;

            // No WEBVIEW context switch — mac2 has none. The WKWebView content is reached natively via the
            // AX tree (see the class summary).
            EnsureLoggedOut();
        }
        catch
        {
            DumpAxTreeIfRequested("OneTimeSetUp");
            throw;
        }
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        CaptureScreenshotIfRequested();
        Driver?.Quit();
        Driver?.Dispose();
    }

    /// <summary>
    /// Logs in as the seeded Dispatcher (<c>alex@dealer.com</c>) via the login card and waits for the
    /// dispatcher dashboard to route in. Reused by the fleet-map scenarios as their shared precondition.
    /// </summary>
    protected void LoginAsDispatcher()
    {
        // Idempotent: all scenarios share ONE Mac2 session (launching the Desktop app is slow), so a prior
        // test in the fixture may already be authenticated. If the dashboard anchor is already present there
        // is no login card to fill — return immediately so NUnit's alphabetical test ordering never matters.
        //
        // The zero-wait probe is SAFE here — unlike EnsureLoggedOut's cold-start entry, which had to defend
        // against the persisted-token launch race. By the time any test calls this the app state is already
        // SETTLED, not mid-launch: OneTimeSetUp's EnsureLoggedOut ends by awaiting (bounded) the login card,
        // and every prior test in the fixture ends by awaiting (bounded) the dashboard below. So whichever
        // state is showing has already been rendered and polled for — there is no un-rendered-WebView race
        // for a zero-wait ExistsNow to lose, and it reports the true current state immediately rather than
        // needing another bounded either-or poll.
        if (ExistsNow(DashboardAnchor))
        {
            return;
        }

        FillInput(EmailField, "alex@dealer.com");
        FillInput(PasswordField, MacDesktopConfig.DispatcherPassword);

        // Bounded poll, never a bare FindElement: with a zero implicit wait a bare snapshot could race the
        // login card's render (the sign-in button is async-rendered MudBlazor markup). BUG-048 house rule.
        WaitForSignalR(d => d.FindElement(SignInButton)).Click();

        WaitForSignalR(d => d.FindElement(DashboardAnchor));
    }

    /// <summary>
    /// Guarantees the app is on the login screen before the fixture logs in. If a persisted Preferences
    /// token has routed the app into the dashboard, this drives the app's own logout; otherwise it confirms
    /// the login card is visible. All checks use native anchors.
    /// </summary>
    protected void EnsureLoggedOut()
    {
        // Cold-start launch race (persisted token). A prior fixture run's PASSING login test persists the
        // dispatcher JWT via the Desktop host's PreferencesTokenStore — and MAUI Preferences survive an app
        // restart — so this session's app can launch straight into the dashboard. But right after the mac2
        // session is created the WKWebView has rendered NEITHER entry state yet, so the earlier zero-wait
        // probes of both anchors missed and the method fell through to a wait-for-login-card that the
        // dashboard then won the race to → 15 s timeout. Await the SETTLED entry state with a bounded
        // either-or poll instead of a zero-wait snapshot on cold start (the App-Nap / WebView timing lesson:
        // cold-start states must be awaited with bounded either-or polls, never zero-wait probes).
        if (WaitForEntryState() == EntryState.LoginCard)
        {
            // Token-less launch: already on the login screen, nothing to log out of.
            return;
        }

        // Persisted-token launch: drive the app's own logout. AccountMenu style (Desktop) — the persona
        // avatar opens the inline account panel, whose "Log out" item ends the session. The panel opens
        // ASYNCHRONOUSLY, so each step is a bounded poll, never a bare FindElement (BUG-048 house rule).
        WaitForSignalR(d => d.FindElement(PersonaAvatar)).Click();
        WaitForSignalR(d => d.FindElement(LogoutMenuItem)).Click();

        // Assert we are (now) on the login screen — bounded, since a logout routes asynchronously.
        WaitForSignalR(d => d.FindElement(LoginCardAnchor));
    }

    /// <summary>
    /// Wipes the Desktop app's persisted Preferences (the bundle's NSUserDefaults, where PreferencesTokenStore
    /// keeps the JWT) so the next app launch cold-starts unauthenticated. Mirrors the
    /// <c>defaults delete com.companyname.servicedelivery.client.desktop</c> that test-appium-mac.sh runs once
    /// before the suite, applied per fixture (each fixture opens its own session; a prior fixture's login would
    /// otherwise leave a persisted token). Best-effort and non-fatal: a token-less app has nothing to delete
    /// (non-zero exit), and a missing <c>defaults</c> tool must never break the run.
    /// </summary>
    private static void WipePersistedAppState()
    {
        try
        {
            var psi = new ProcessStartInfo("defaults", $"delete {MacDesktopConfig.DesktopBundleId}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var process = Process.Start(psi);
            process?.WaitForExit(5000);
        }
        catch
        {
            // Cold-start hygiene is best-effort — never fail (or mask) a fixture because the wipe could not run.
        }
    }

    /// <summary>The app's two cold-start entry states, as reported by <see cref="WaitForEntryState"/>.</summary>
    private enum EntryState
    {
        LoginCard,
        Dashboard,
    }

    /// <summary>
    /// Bounded-poll (500 ms laps, the 15 s SignalR budget) until the Desktop app finishes launching into
    /// ONE of its two entry states — the login card (token-less launch) or the dispatcher dashboard
    /// (persisted-token launch) — and report which. This is the cold-start launch-race guard: right after
    /// the mac2 session is created the WKWebView has rendered NEITHER anchor, so a single zero-wait probe of
    /// either one misses; only a bounded either-or poll reliably catches whichever state the app settles
    /// into. Each per-lap probe is <see cref="ExistsNow"/> — a single non-blocking AX snapshot under the
    /// session's permanent zero implicit wait — so a lap that misses the first anchor moves straight to the
    /// second within the same lap, and the 500 ms gap between laps lets the WebView keep rendering. If NEITHER
    /// anchor appears within the budget it throws a clear <see cref="WebDriverTimeoutException"/>
    /// (the <c>OneTimeSetUp</c> catch then triggers the SD_AX_DUMP diagnostic).
    /// </summary>
    private EntryState WaitForEntryState()
    {
        EntryState? state = null;
        try
        {
            // Condition returns bool (never a nullable value type — that trips DefaultWait.Until, cycle 5
            // FAILURE 1); the discovered state is captured via the closure and read back after the poll wins.
            WaitForSignalR(_ =>
            {
                if (ExistsNow(LoginCardAnchor))
                {
                    state = EntryState.LoginCard;
                    return true;
                }

                if (ExistsNow(DashboardAnchor))
                {
                    state = EntryState.Dashboard;
                    return true;
                }

                return false;
            });
        }
        catch (WebDriverTimeoutException ex)
        {
            throw new WebDriverTimeoutException(
                "EnsureLoggedOut: the Desktop app rendered NEITHER the login card (\"Sign in to continue\") " +
                "nor the dispatcher dashboard (\"ACTIVE REQUESTS\") within the 15 s launch budget — the " +
                "WKWebView never reached a known cold-start entry state. Re-run with SD_AX_DUMP=1 to capture " +
                "the AX tree for inspection.",
                ex);
        }

        return state!.Value;
    }

    /// <summary>
    /// Polls (every 500 ms, up to the 15 s SignalR budget) until <paramref name="condition"/> yields a
    /// non-null/true value. Used for every async lookup (login routing, marker render, hub-driven marker
    /// move) so no bare, single-snapshot FindElement can race an async update (BUG-048 house rule).
    /// </summary>
    protected TResult WaitForSignalR<TResult>(Func<AppiumDriver, TResult> condition)
    {
        var wait = new WebDriverWait(Driver, MacDesktopConfig.SignalRWait)
        {
            PollingInterval = TimeSpan.FromMilliseconds(500),
        };
        wait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(StaleElementReferenceException));
        return wait.Until(_ => condition(Driver));
    }

    /// <summary>
    /// True if any element matching <paramref name="by"/> exists RIGHT NOW — a single, non-blocking AX
    /// snapshot. The session implicit wait is pinned to zero (see the class summary's WAIT STRATEGY note), so
    /// <c>FindElements</c> already returns immediately with the current matches (empty list, never a throw)
    /// rather than blocking for the SignalR budget — no per-call implicit-wait juggling is needed. Used for
    /// negative checks (the hidden error banner) and settled-state probes (the two entry-state anchors in
    /// <see cref="WaitForEntryState"/>, "am I already logged in?" in <see cref="LoginAsDispatcher"/>).
    /// </summary>
    protected bool ExistsNow(By by) => Driver.FindElements(by).Count > 0;

    /// <summary>
    /// Types <paramref name="value"/> into the native text field. mac2 has no JS execution, but it does not
    /// need one: real keystrokes over SendKeys raise genuine input/blur events in the WKWebView, so moving
    /// focus to the next field / the sign-in button commits the MudTextField two-way binding (which binds on
    /// change/blur) — the old JS event-dispatch hack was only ever needed inside a WebView context, which
    /// mac2 does not have.
    /// </summary>
    protected void FillInput(By locator, string value)
    {
        // Bounded poll, never a bare FindElement: with a zero implicit wait a bare snapshot could race the
        // login card's async render. The field is located via WaitForSignalR so the 500 ms lap gap lets the
        // WebView finish rendering the input before we type into it (BUG-048 house rule).
        var field = WaitForSignalR(d => d.FindElement(locator));
        field.Click();
        field.SendKeys(value);
    }

    /// <summary>
    /// Diagnostics for the live gate (which the pipeline cannot run): when SD_AX_DUMP=1, writes the current
    /// AX tree (Driver.PageSource) to TestResults/ax-tree-&lt;reason&gt;.xml so a missed anchor can be
    /// inspected against the actual AX exposure. Cheap, bounded, best-effort — never itself fails a test.
    /// </summary>
    protected void DumpAxTreeIfRequested(string reason)
    {
        if (Environment.GetEnvironmentVariable("SD_AX_DUMP") != "1" || Driver is null)
        {
            return;
        }

        try
        {
            var dir = Path.Combine(Directory.GetCurrentDirectory(), "TestResults");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, $"ax-tree-{reason}.xml");
            File.WriteAllText(file, Driver.PageSource);
            TestContext.Progress.WriteLine($"[SD_AX_DUMP] Wrote AX tree ({reason}) to {file}");
        }
        catch
        {
            // Diagnostics only — never fail (or mask) a test because the dump could not be written.
        }
    }

    /// <summary>
    /// Locates a fleet-summary entry whose accessible text STARTS WITH the given prefix. The full entry text is
    /// "Vehicle &lt;registration&gt; — &lt;state&gt; — &lt;lat&gt;,&lt;lng&gt;", and the trailing coordinate
    /// portion changes on every position render — so an exact-text match would go stale the moment a vehicle
    /// moves. Pass the stable "Vehicle &lt;registration&gt;" identity portion to keep re-resolving the SAME
    /// entry across a move.
    /// </summary>
    protected static By FleetSummaryEntryWithTextPrefix(string prefix) => AnyTextStartsWith(prefix);

    /// <summary>
    /// Reads an AX element's accessible name: mac2 surfaces it as @label for most elements but as @value for
    /// some element/OS combos, so read @label first and fall back to @value (mirrors the dual match every
    /// summary-entry locator uses).
    /// </summary>
    protected static string? AccessibleName(IWebElement element)
    {
        var label = element.GetAttribute("label");
        return string.IsNullOrEmpty(label) ? element.GetAttribute("value") : label;
    }

    // Static text carrying EXACTLY the given text — matches either the AX value or label, since WebKit may
    // expose HTML text as either depending on the element.
    private static By StaticText(string text) =>
        By.XPath($"//XCUIElementTypeStaticText[@value=\"{text}\" or @label=\"{text}\"]");

    // Static text whose value/label STARTS WITH the given text (the error banner ships extra trailing markup).
    private static By StaticTextStartsWith(string text) =>
        By.XPath($"//XCUIElementTypeStaticText[starts-with(@value, \"{text}\") or starts-with(@label, \"{text}\")]");

    // Any element type carrying EXACTLY the given text — for plain <div> text (dashboard rail head, avatar
    // initials, account-menu items) that WebKit may nest under a wrapping group rather than bare static text.
    private static By AnyText(string text) =>
        By.XPath($"//*[@value=\"{text}\" or @label=\"{text}\"]");

    // Any element type whose value/label STARTS WITH the given text — for the fleet-summary entries, whose
    // plain <div> text WebKit may likewise nest under a wrapping group rather than expose as bare static text.
    private static By AnyTextStartsWith(string text) =>
        By.XPath($"//*[starts-with(@value, \"{text}\") or starts-with(@label, \"{text}\")]");

    // When SD_SHOT_DIR is set, save a screenshot of the final screen as <TestClass>.png (best-effort). Used
    // by the AI-review render-and-screenshot check to compare the live Desktop render against its mockup.
    // Driver.GetScreenshot works on mac2 (native screen capture, not a WebView call).
    private void CaptureScreenshotIfRequested()
    {
        var dir = Environment.GetEnvironmentVariable("SD_SHOT_DIR");
        if (string.IsNullOrWhiteSpace(dir) || Driver is null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(dir);
            var name = TestContext.CurrentContext.Test.ClassName ?? "MacDesktop";
            Driver.GetScreenshot().SaveAsFile(Path.Combine(dir, $"{name}.png"));
        }
        catch
        {
            // Screenshot capture is diagnostics only — never fail a test because of it.
        }
    }
}
