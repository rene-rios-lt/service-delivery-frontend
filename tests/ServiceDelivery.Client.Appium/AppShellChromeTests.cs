namespace ServiceDelivery.Client.Appium;

/// <summary>
/// FE-029 coverage (App-bar & navigation chrome). Live-app verification of the shell chrome changes
/// on Mobile (ServiceRep): the hamburger sits at the leading (left) edge of the app bar (AC-1), the
/// drawer no longer carries the indigo rep-name/role/vehicle header (AC-3), and the drawer menu items
/// and footer note render fully within the visible area (AC-5/AC-6). On Mobile the PersonaMenu renders
/// as a fixed, temporary MudDrawer opened from the app-bar hamburger and overlaid over a scrim.
/// </summary>
[TestFixture]
public sealed class AppShellChromeTests : AppiumTestBase
{
    [Test]
    public void GivenAuthenticatedRep_WhenAppBarRendered_ThenHamburgerAppearsBeforeTitleInDomOrder()
    {
        // Arrange
        TakeOverFirstIdleVehicle();

        // Act
        var hamburger = WaitForSignalR(d =>
            d.FindElement(By.CssSelector("[data-testid='appbar-menu-affordance']")));
        var title = WaitForSignalR(d =>
            d.FindElement(By.CssSelector("[data-testid='appbar-title']")));

        // Assert
        // AC-1: leading (left) edge — the hamburger's rendered X is left of the title's X.
        Assert.That(hamburger.Location.X, Is.LessThan(title.Location.X));
    }

    [Test]
    public void GivenAuthenticatedRep_WhenNavDrawerOpened_ThenNoDrawerHeaderIsVisible()
    {
        // Arrange
        TakeOverFirstIdleVehicle();
        Driver.FindElement(By.CssSelector("[data-testid='appbar-menu-affordance']")).Click();

        // Act
        // The drawer content appears with an animated slide-in — wait for a menu item to confirm the
        // drawer is open before asserting the header's absence.
        var releaseItem = WaitForSignalR(d =>
            d.FindElement(By.CssSelector("[data-testid='menu-item-release']")));

        // Assert
        // AC-3: the indigo drawer header (rep name / role / vehicle chip) is gone.
        Assert.That(
            Driver.FindElements(By.CssSelector("[data-testid='persona-name']")),
            Is.Empty);
        Assert.That(
            Driver.FindElements(By.CssSelector("[data-testid='vehicle-context-chip']")),
            Is.Empty);
        // AC-6: and the menu items render on-screen — not shoved off the left edge as they were before
        // the drawer was made a viewport-fixed overlay. `Displayed` alone would pass on an off-screen
        // item, so measure the item's rect against the viewport bounds.
        AssertWithinViewport(releaseItem, "the 'Release vehicle' menu item");
    }

    [Test]
    public void GivenAuthenticatedRep_WhenNavDrawerOpened_ThenFooterNoteRendersAndIsVisible()
    {
        // Arrange
        TakeOverFirstIdleVehicle();
        Driver.FindElement(By.CssSelector("[data-testid='appbar-menu-affordance']")).Click();

        // Act
        var footerNote = WaitForSignalR(d =>
            d.FindElement(By.CssSelector("[data-testid='release-disclaimer']")));

        // Assert
        // AC-5/AC-6: the footer note must render fully within the visible area (not clipped at any
        // edge). `Displayed == true` is a WebView false-positive here — it stays true for an element
        // positioned off-screen, which is exactly why the earlier broken render passed. Assert the
        // note's rectangle sits inside the viewport bounds instead (left/top >= 0, right/bottom within
        // the viewport), so a clipped or off-screen drawer fails this test.
        //
        // Run AssertWithinViewport FIRST: it polls up to 3s for the drawer's ~225ms slide-in to settle
        // (rect no longer reporting an off-screen Left < 0). WaitForSignalR returns the moment the
        // element exists in the DOM — a Temporary MudDrawer renders its content while still animating —
        // so a bare `Displayed` check fired at that instant races the slide-in and can catch the panel
        // mid-transition (still translating in from off-screen-left). Only once the rect has settled
        // on-screen is `Displayed` a valid final assertion rather than a race.
        AssertWithinViewport(footerNote, "the drawer footer note");
        Assert.That(footerNote.Displayed, Is.True);
        Assert.That(footerNote.Text, Is.Not.Empty);
    }

    /// <summary>
    /// Asserts <paramref name="element"/> renders fully inside the WebView viewport. Uses the DOM's
    /// own <c>getBoundingClientRect()</c> and <c>window.innerWidth/innerHeight</c> — all in the
    /// WebView's CSS-pixel space — rather than <c>IWebElement.Location</c>/<c>Window.Size</c>, which
    /// mix native points and web pixels in the WEBVIEW context and cannot be compared reliably. The
    /// drawer slides in over ~225 ms, so poll briefly for the panel to settle on-screen before the
    /// final hard assertion, then report the specific edge that overflows on failure.
    /// </summary>
    private void AssertWithinViewport(IWebElement element, string label)
    {
        var rect = MeasureViewportRect(element);
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        while (rect.Left < 0 && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(250);
            rect = MeasureViewportRect(element);
        }

        Assert.Multiple(() =>
        {
            Assert.That(rect.Left, Is.GreaterThanOrEqualTo(-1d),
                $"{label} is clipped off the LEFT edge (left={rect.Left}); the drawer must render flush-left within the viewport (AC-6).");
            Assert.That(rect.Top, Is.GreaterThanOrEqualTo(-1d),
                $"{label} is clipped off the TOP edge (top={rect.Top}).");
            Assert.That(rect.Right, Is.LessThanOrEqualTo(rect.ViewportWidth + 1d),
                $"{label} overflows the RIGHT edge (right={rect.Right}, viewport width={rect.ViewportWidth}).");
            Assert.That(rect.Bottom, Is.LessThanOrEqualTo(rect.ViewportHeight + 1d),
                $"{label} overflows the BOTTOM edge (bottom={rect.Bottom}, viewport height={rect.ViewportHeight}); it must clear the home indicator (AC-5).");
        });
    }

    private (double Left, double Top, double Right, double Bottom, double ViewportWidth, double ViewportHeight)
        MeasureViewportRect(IWebElement element)
    {
        var raw = ((IJavaScriptExecutor)Driver).ExecuteScript(
            "var r = arguments[0].getBoundingClientRect();" +
            "return [r.left, r.top, r.right, r.bottom, window.innerWidth, window.innerHeight];",
            element);
        var values = (System.Collections.IList)raw!;
        return (
            Convert.ToDouble(values[0]),
            Convert.ToDouble(values[1]),
            Convert.ToDouble(values[2]),
            Convert.ToDouble(values[3]),
            Convert.ToDouble(values[4]),
            Convert.ToDouble(values[5]));
    }
}
