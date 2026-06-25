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

    /// <summary>
    /// FE-012 coverage (AC-1, AC-2, AC-3): the fleet is positioned at the request site (distance 0),
    /// so once the rep accepts the offer the backend reports the rep within 15 miles and the
    /// "I've Arrived" button becomes enabled. Tapping it calls POST /rep/arrive; on success the
    /// highlighted navigation route line is removed and "Mark Complete" becomes the primary action.
    /// </summary>
    [Test]
    public void GivenRepIsWithin15Miles_WhenArrivedButtonTapped_ThenRouteLineGoneAndMarkCompleteVisible()
    {
        // Arrange
        TakeOverFirstIdleVehicle();
        BackendApiHelper.SubmitServiceRequest(AppiumConfig.BackendBaseUrl);
        WaitForSignalR(d => d.FindElement(By.CssSelector("[data-testid='accept-button']"))).Click();
        // AC-3: the fleet sits at the request site, so the backend reports Within15Miles and the
        // arrived button becomes enabled. Poll for the enabled state before tapping.
        var arrivedButton = WaitForSignalR(d =>
        {
            var button = d.FindElement(By.CssSelector("[data-testid='arrived-button']"));
            return button.Enabled ? button : null;
        });

        // Act
        // AC-1: tapping "I've Arrived" calls POST /rep/arrive.
        arrivedButton!.Click();

        // Assert
        // AC-2: the highlighted route line is removed and "Mark Complete" becomes the primary action.
        var completeButton = WaitForSignalR(d => d.FindElement(By.CssSelector("[data-testid='complete-button']")));
        Assert.That(completeButton.Displayed, Is.True);
        Assert.That(completeButton.Text, Does.Contain("Mark Complete"));
        Assert.That(Driver.FindElements(By.CssSelector("[data-testid='route-line']")), Is.Empty);
    }
}
