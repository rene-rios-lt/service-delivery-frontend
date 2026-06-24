namespace ServiceDelivery.Client.Appium;

/// <summary>
/// FE-007 coverage (AC-10): from the take-over screen a logged-in rep sees the idle-vehicle list
/// populated from <c>GET /vehicles/available</c>, selects the first vehicle, takes it over, and lands
/// on the idle / available view. Requires the simulator (via <c>start.sh</c>) to be driving idle
/// vehicles so the list is non-empty.
/// </summary>
[TestFixture]
public sealed class TakeOverTests : AppiumTestBase
{
    [Test]
    public void GivenRep1LoggedIn_WhenVehicleSelectedAndTakenOver_ThenIdleViewIsShown()
    {
        // Arrange
        Login("rep1", RepPassword);
        var firstVehicle = Driver.FindElement(MobileBy.AccessibilityId("idle-vehicle-row"));

        // Act
        firstVehicle.Click();
        Driver.FindElement(MobileBy.AccessibilityId("take-over-button")).Click();

        // Assert
        var availableIndicator = Driver.FindElement(MobileBy.AccessibilityId("available-indicator"));
        Assert.That(availableIndicator.Displayed, Is.True);
    }
}
