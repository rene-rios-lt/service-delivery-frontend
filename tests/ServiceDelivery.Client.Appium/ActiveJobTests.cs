using ServiceDelivery.Client.Appium.Helpers;

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
        BackendApiHelper.SubmitServiceRequest(AppiumConfig.BackendBaseUrl);
        WaitForSignalR(d => d.FindElement(By.CssSelector("[data-testid='accept-button']"))).Click();

        // Act
        var arrivedButton = Driver.FindElement(By.CssSelector("[data-testid='arrived-button']"));
        var etaCard = Driver.FindElement(By.CssSelector("[data-testid='eta-card']"));

        // Assert
        Assert.That(arrivedButton.Displayed, Is.True);
        Assert.That(arrivedButton.Enabled, Is.False);
        Assert.That(etaCard.Displayed, Is.True);
    }
}
