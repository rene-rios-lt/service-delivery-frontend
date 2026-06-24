namespace ServiceDelivery.Client.Appium;

/// <summary>
/// FE-020 coverage (AC-11): after taking over a vehicle the idle view shows the "Available" state
/// indicator and the claimed vehicle's registration without any manual refresh — the screen is
/// reached push-style straight from the take-over POST.
/// </summary>
[TestFixture]
public sealed class RepIdleTests : AppiumTestBase
{
    [Test]
    public void GivenRep1TookOverVehicle_WhenIdleViewLoads_ThenAvailableIndicatorAndRegistrationVisible()
    {
        // Arrange
        TakeOverFirstIdleVehicle();

        // Act
        var availableIndicator = Driver.FindElement(MobileBy.AccessibilityId("available-indicator"));
        var claimedVehicleCard = Driver.FindElement(MobileBy.AccessibilityId("claimed-vehicle-card"));

        // Assert
        Assert.That(availableIndicator.Displayed, Is.True);
        Assert.That(claimedVehicleCard.Displayed, Is.True);
        Assert.That(claimedVehicleCard.Text, Is.Not.Empty);
    }
}
