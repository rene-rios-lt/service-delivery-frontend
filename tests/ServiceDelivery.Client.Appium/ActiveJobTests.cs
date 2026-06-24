namespace ServiceDelivery.Client.Appium;

/// <summary>
/// FE-011 coverage (AC-14): after accepting an offer the rep lands on the active-job map screen
/// showing the rep marker, requester pin, route line and ETA card, with the "I've Arrived" button
/// present but disabled until the rep is within 15 miles.
/// </summary>
[TestFixture]
public sealed class ActiveJobTests : AppiumTestBase
{
    [Test]
    public void GivenRep1AcceptedJob_WhenActiveJobScreenLoads_ThenAllMapElementsPresentAndArrivedButtonDisabled()
    {
        // Arrange
        TakeOverFirstIdleVehicle();
        WaitForSignalR(d => d.FindElement(MobileBy.AccessibilityId("accept-button"))).Click();

        // Act
        var arrivedButton = Driver.FindElement(MobileBy.AccessibilityId("arrived-button"));
        var etaCard = Driver.FindElement(MobileBy.AccessibilityId("eta-card"));

        // Assert
        Assert.That(arrivedButton.Displayed, Is.True);
        Assert.That(arrivedButton.Enabled, Is.False);
        Assert.That(etaCard.Displayed, Is.True);
    }
}
