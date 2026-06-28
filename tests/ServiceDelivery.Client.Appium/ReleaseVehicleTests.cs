namespace ServiceDelivery.Client.Appium;

/// <summary>
/// FE-014 coverage (Release vehicle at end of shift). An authenticated rep who has taken over a
/// vehicle can open the nav drawer, tap "Release vehicle", confirm in the dialog, and be returned to
/// the take-over screen (FE-007) — the vehicle is released back to the fleet. On Mobile the
/// PersonaMenu renders as a temporary MudDrawer and the confirmation is a MudDialog.
/// </summary>
[TestFixture]
public sealed class ReleaseVehicleTests : AppiumTestBase
{
    [Test]
    public void GivenAuthenticatedRepWithVehicle_WhenNavDrawerOpened_ThenReleaseVehicleItemIsVisible()
    {
        // Arrange
        TakeOverFirstIdleVehicle();

        // Act
        Driver.FindElement(By.CssSelector("[data-testid='appbar-menu-affordance']")).Click();

        // Assert
        var releaseItem = Driver.FindElement(By.CssSelector("[data-testid='menu-item-release']"));
        Assert.That(releaseItem.Displayed, Is.True);
    }

    [Test]
    public void GivenRepWithVehicle_WhenReleaseVehicleTapped_ThenConfirmationDialogAppears()
    {
        // Arrange
        TakeOverFirstIdleVehicle();
        Driver.FindElement(By.CssSelector("[data-testid='appbar-menu-affordance']")).Click();

        // Act
        // The Mobile drawer renders each item as a MudNavLink: the data-testid sits on the outer
        // wrapper div, but the click handler lives on the inner .mud-nav-link element.
        Driver.FindElement(By.CssSelector("[data-testid='menu-item-release'] .mud-nav-link")).Click();

        // Assert
        var dialogTitle = Driver.FindElement(By.CssSelector("[data-testid='release-dialog-title']"));
        var confirmButton = Driver.FindElement(By.CssSelector("[data-testid='release-dialog-confirm']"));
        Assert.That(dialogTitle.Displayed, Is.True);
        Assert.That(confirmButton.Displayed, Is.True);
    }

    [Test]
    public void GivenConfirmationDialogShown_WhenReleaseConfirmed_ThenTakeOverScreenIsDisplayed()
    {
        // Arrange
        TakeOverFirstIdleVehicle();
        Driver.FindElement(By.CssSelector("[data-testid='appbar-menu-affordance']")).Click();
        Driver.FindElement(By.CssSelector("[data-testid='menu-item-release'] .mud-nav-link")).Click();
        Driver.FindElement(By.CssSelector("[data-testid='release-dialog-confirm']")).Click();

        // Act
        // After a successful release the rep returns to the take-over screen (FE-014/AC-4).
        var takeOverButton = WaitForSignalR(d =>
            d.FindElements(By.CssSelector("[data-testid='take-over-button']")).FirstOrDefault());

        // Assert
        Assert.That(takeOverButton, Is.Not.Null);
        Assert.That(takeOverButton!.Displayed, Is.True);
    }
}
