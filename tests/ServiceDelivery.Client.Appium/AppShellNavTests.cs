namespace ServiceDelivery.Client.Appium;

/// <summary>
/// FE-021 coverage (AC-16): an authenticated rep can open the navigation drawer from the app-bar
/// menu affordance and see both the "Release Vehicle" and "Log out" items as distinct, tappable
/// entries. On Mobile the PersonaMenu renders as a temporary MudDrawer.
/// </summary>
[TestFixture]
public sealed class AppShellNavTests : AppiumTestBase
{
    [Test]
    public void GivenAuthenticatedRep_WhenNavDrawerOpened_ThenReleaseVehicleAndLogoutAreVisible()
    {
        // Arrange
        TakeOverFirstIdleVehicle();

        // Act
        Driver.FindElement(MobileBy.AccessibilityId("appbar-menu-affordance")).Click();

        // Assert
        var releaseItem = Driver.FindElement(MobileBy.AccessibilityId("menu-item-release"));
        var logoutItem = Driver.FindElement(MobileBy.AccessibilityId("menu-item-logout"));
        Assert.That(releaseItem.Displayed, Is.True);
        Assert.That(logoutItem.Displayed, Is.True);
    }
}
