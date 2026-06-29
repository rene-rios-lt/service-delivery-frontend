namespace ServiceDelivery.Client.Appium;

/// <summary>
/// QUAL-008 — the WebView per-runtime smoke. MAUI Blazor Hybrid renders the entire UI inside a
/// WKWebView (BlazorWebView), whose HTML is invisible to XCUITest's native accessibility tree: the
/// app's controls are reachable only after switching to the WEBVIEW context and selecting by
/// <c>data-testid</c>. That is the exact runtime boundary that broke in BUG-031 — and one that
/// neither the headless <c>smoke.sh</c> (no UI at all) nor the browser <c>smoke-web.sh</c> (a real
/// browser DOM, not a WKWebView) can exercise.
///
/// This is deliberately the thinnest possible assertion of that boundary — the mobile analogue of
/// <c>smoke-web.sh</c>'s cross-origin login: launch the Mobile app, confirm a WEBVIEW context is
/// present and active, and confirm a known <c>data-testid</c> element is reachable inside it. It is
/// NOT a flow test (the full ServiceRep journey is covered by the rest of the Appium suite); its only
/// job is to fail loudly if the BlazorWebView ever stops exposing the DOM to the WEBVIEW context.
/// Run it via <c>scripts/local/smoke-mobile.sh</c> (which runs just this class through
/// <c>test-appium.sh</c>).
/// </summary>
[TestFixture]
public sealed class WebViewReachabilitySmokeTests : AppiumTestBase
{
    [Test]
    public void GivenTheMauiMobileApp_WhenLaunched_ThenTheBlazorWebViewContextExposesDataTestIdElements()
    {
        // Arrange — AppiumTestBase.SetUp launches the app, switches to the WEBVIEW context (which
        // throws if no WEBVIEW context appears), and leaves the app on the logged-out landing screen.

        // Act
        var activeContext = Driver.Context;
        var loginCard = Driver.FindElements(By.CssSelector("[data-testid='login-card']"));

        // Assert — the active context is the BlazorWebView's WKWebView (not NATIVE_APP), and a known
        // HTML data-testid is reachable inside it. Either failing is the BUG-031 regression: the DOM
        // is no longer reachable through the WEBVIEW context.
        Assert.That(activeContext, Does.Contain("WEBVIEW"),
            "The active Appium context must be the BlazorWebView's WEBVIEW context (BUG-031).");
        Assert.That(loginCard, Is.Not.Empty,
            "A known data-testid element must be reachable inside the WEBVIEW context (BUG-031).");
    }
}
