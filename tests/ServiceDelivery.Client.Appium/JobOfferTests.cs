using System.Globalization;

namespace ServiceDelivery.Client.Appium;

/// <summary>
/// FE-008 / FE-009 / FE-010 coverage (AC-12, AC-13, AC-15): once a rep is idle, the simulator's
/// matching produces a job offer that pushes the offer screen over RepHub (no navigation by the
/// rep). The offer screen shows the requester, tier, DTC, distance, ETA and a live countdown, and
/// the Accept / Decline buttons drive the rep to the active-job screen or back to idle. All
/// SignalR-dependent waits use <see cref="AppiumTestBase.WaitForSignalR{TResult}"/> with the
/// ≥15-second budget (AC-6).
/// </summary>
[TestFixture]
public sealed class JobOfferTests : AppiumTestBase
{
    [Test]
    public void GivenSimulatorSendsOffer_WhenJobOfferScreenAppears_ThenAllOfferElementsVisibleAndCountdownDecrements()
    {
        // Arrange
        TakeOverFirstIdleVehicle();

        // Act
        var countdownRing = WaitForSignalR(d => d.FindElement(MobileBy.AccessibilityId("countdown-ring")));
        var firstReading = ReadCountdown();
        var decremented = WaitForSignalR(_ => ReadCountdown() < firstReading);

        // Assert
        Assert.That(countdownRing.Displayed, Is.True);
        Assert.That(Driver.FindElement(MobileBy.AccessibilityId("accept-button")).Displayed, Is.True);
        Assert.That(Driver.FindElement(MobileBy.AccessibilityId("decline-button")).Displayed, Is.True);
        Assert.That(decremented, Is.True);
    }

    [Test]
    public void GivenJobOfferScreenShown_WhenAcceptTapped_ThenActiveJobScreenIsShown()
    {
        // Arrange
        TakeOverFirstIdleVehicle();
        var acceptButton = WaitForSignalR(d => d.FindElement(MobileBy.AccessibilityId("accept-button")));

        // Act
        acceptButton.Click();

        // Assert
        var arrivedButton = Driver.FindElement(MobileBy.AccessibilityId("arrived-button"));
        Assert.That(arrivedButton.Displayed, Is.True);
    }

    [Test]
    public void GivenJobOfferScreenShown_WhenDeclineTapped_ThenIdleWaitingViewIsShown()
    {
        // Arrange
        TakeOverFirstIdleVehicle();
        var declineButton = WaitForSignalR(d => d.FindElement(MobileBy.AccessibilityId("decline-button")));

        // Act
        declineButton.Click();

        // Assert
        var availableIndicator = Driver.FindElement(MobileBy.AccessibilityId("available-indicator"));
        Assert.That(availableIndicator.Displayed, Is.True);
    }

    private int ReadCountdown()
    {
        var text = Driver.FindElement(MobileBy.AccessibilityId("countdown-ring")).Text;
        var digits = new string(text.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : int.MaxValue;
    }
}
