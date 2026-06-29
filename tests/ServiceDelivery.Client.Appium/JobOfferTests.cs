using System.Globalization;
using ServiceDelivery.Client.Appium.Helpers;

namespace ServiceDelivery.Client.Appium;

/// <summary>
/// FE-008 / FE-009 / FE-010 coverage (AC-12, AC-13, AC-15): once a rep is idle and a Requester
/// submits a matching service request, the backend's matching produces a job offer that pushes the
/// offer screen over RepHub (no navigation by the rep). With the rep-operating simulator disabled
/// (backend-only run), the taken-over rep is the sole match candidate, so a single submitted request
/// routes its offer to that rep deterministically. The offer screen shows the requester, tier, DTC,
/// distance, ETA and a live countdown, and the Accept / Decline buttons drive the rep to the
/// active-job screen or back to idle. All SignalR-dependent waits use
/// <see cref="AppiumTestBase.WaitForSignalR{TResult}"/> with the ≥15-second budget (AC-6).
/// </summary>
[TestFixture]
public sealed class JobOfferTests : AppiumTestBase
{
    [Test]
    public void GivenRequesterSubmitsMatchingRequest_WhenJobOfferArrives_ThenAllOfferElementsVisibleAndCountdownDecrements()
    {
        // Arrange
        TakeOverFirstIdleVehicle();
        BackendApiHelper.SubmitServiceRequest(AppiumConfig.BackendBaseUrl);

        // Act
        var countdownRing = WaitForSignalR(d => d.FindElement(By.CssSelector("[data-testid='countdown-ring']")));
        var firstReading = ReadCountdown();
        var decremented = WaitForSignalR(_ => ReadCountdown() < firstReading);

        // Assert
        Assert.That(countdownRing.Displayed, Is.True);
        Assert.That(Driver.FindElement(By.CssSelector("[data-testid='accept-button']")).Displayed, Is.True);
        Assert.That(Driver.FindElement(By.CssSelector("[data-testid='decline-button']")).Displayed, Is.True);
        Assert.That(decremented, Is.True);
    }

    [Test]
    public void GivenRequesterSubmitsGoldRequest_WhenJobOfferArrives_ThenTierBadgeIsVisibleAndShowsGold()
    {
        // Arrange
        // BUG-036: the backend sends the tier as RequesterTier (enum-name string); the offer must
        // render a visible, gold-coloured tier pill (not the white-on-white badge that resulted from
        // the field-name mismatch defaulting Tier to None). BackendApiHelper submits a Gold request.
        TakeOverFirstIdleVehicle();
        BackendApiHelper.SubmitServiceRequest(AppiumConfig.BackendBaseUrl);

        // Act
        var badge = WaitForSignalR(d => d.FindElement(By.CssSelector("[data-testid='tier-badge']")));

        // Assert
        Assert.That(badge.Displayed, Is.True);
        Assert.That(badge.Text.ToUpperInvariant(), Does.Contain("GOLD"));
        Assert.That(badge.GetAttribute("class"), Does.Contain("sd-badge--gold"));
    }

    [Test]
    public void GivenVehicleAbout100MilesFromRequest_WhenJobOfferArrives_ThenDistanceAndEtaShowRealValues()
    {
        // Arrange
        // BackendApiHelper positions the fleet at the start point and submits the request ~100 mi east,
        // so the backend computes a real Haversine distance (~100 mi) and ETA (~100 min at 60 mph) —
        // not the degenerate 0 mi / 0 min a co-located request produced.
        TakeOverFirstIdleVehicle();
        BackendApiHelper.SubmitServiceRequest(AppiumConfig.BackendBaseUrl);

        // Act
        WaitForSignalR(d => d.FindElement(By.CssSelector("[data-testid='tier-badge']")));
        var distance = ParseLeadingNumber(Driver.FindElement(By.CssSelector("[data-testid='distance-miles']")).Text);
        var eta = ParseLeadingNumber(Driver.FindElement(By.CssSelector("[data-testid='eta-minutes']")).Text);
        TestContext.Progress.WriteLine($"[offer] distance={distance} mi, eta={eta} min");

        // Assert — real, non-zero values in the ~100 mi band (not the old co-located 0/0).
        Assert.That(distance, Is.GreaterThan(50).And.LessThan(150));
        Assert.That(eta, Is.GreaterThan(50).And.LessThan(150));
    }

    [Test]
    public void GivenJobOfferScreenShown_WhenAcceptTapped_ThenActiveJobScreenIsShown()
    {
        // Arrange
        TakeOverFirstIdleVehicle();
        BackendApiHelper.SubmitServiceRequest(AppiumConfig.BackendBaseUrl);
        var acceptButton = WaitForSignalR(d => d.FindElement(By.CssSelector("[data-testid='accept-button']")));

        // Act
        acceptButton.Click();

        // Assert
        var arrivedButton = Driver.FindElement(By.CssSelector("[data-testid='arrived-button']"));
        Assert.That(arrivedButton.Displayed, Is.True);
    }

    [Test]
    public void GivenJobOfferScreenShown_WhenDeclineTapped_ThenIdleWaitingViewIsShown()
    {
        // Arrange
        TakeOverFirstIdleVehicle();
        BackendApiHelper.SubmitServiceRequest(AppiumConfig.BackendBaseUrl);
        var declineButton = WaitForSignalR(d => d.FindElement(By.CssSelector("[data-testid='decline-button']")));

        // Act
        declineButton.Click();

        // Assert
        var availableIndicator = Driver.FindElement(By.CssSelector("[data-testid='available-indicator']"));
        Assert.That(availableIndicator.Displayed, Is.True);
    }

    [Test]
    public void GivenJobOfferExpired_WhenServerPushesExpiredEvent_ThenOfferScreenDismissesWithoutWaitingForCountdown()
    {
        // Arrange
        // BUG-037 (AC-1 E2E): the backend's ExpiredJobOfferSweeper expires an unanswered offer after
        // ~60 s and pushes JobOfferExpired over RepHub. Before the fix the frontend ignored that event
        // and only cleared the screen when the local countdown hit zero. With the rep owning the only
        // candidate vehicle, a single submitted request routes its offer deterministically to this rep.
        TakeOverFirstIdleVehicle();
        BackendApiHelper.SubmitServiceRequest(AppiumConfig.BackendBaseUrl);
        WaitForSignalR(d => d.FindElement(By.CssSelector("[data-testid='countdown-ring']")));

        // Act
        // Wait for the server-pushed expiry to dismiss the offer back to the idle view. The sweep
        // fires ~60 s after the offer; poll the SignalR budget repeatedly (well past 70 s) for the
        // idle indicator, capturing the countdown reading just before dismissal to prove the screen
        // cleared on the server event rather than on the local timer reaching zero.
        var countdownAtDismissal = int.MaxValue;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(80);
        IWebElement? availableIndicator = null;
        while (DateTime.UtcNow < deadline && availableIndicator is null)
        {
            countdownAtDismissal = Math.Min(countdownAtDismissal, ReadCountdown());
            // BUG-040: non-throwing poll so this outer ~80 s loop governs the wait. The backend's
            // JobOfferExpired sweep can take longer than a single 15 s SignalR budget; the throwing
            // WaitForSignalR aborted the test on its first lap before the event ever arrived.
            availableIndicator = TryWaitForSignalR(d =>
                d.FindElements(By.CssSelector("[data-testid='available-indicator']")).FirstOrDefault());
        }

        // Assert
        Assert.That(availableIndicator, Is.Not.Null, "Offer screen never dismissed to the idle view.");
        Assert.That(availableIndicator!.Displayed, Is.True);
        Assert.That(countdownAtDismissal, Is.GreaterThan(0),
            "Screen cleared only when the local countdown hit zero — the server JobOfferExpired event was ignored.");
    }

    [Test]
    public void GivenJobOfferArrives_WhenMapLoads_ThenGoogleMapContainerAndRequesterPinAreVisible()
    {
        // Arrange
        // FE-027 (AC-4): the job-offer screen now renders the real FE-024 Google map in place of the CSS
        // placeholder. With the rep owning the only candidate vehicle, a single submitted request routes
        // its offer deterministically to this rep, which pushes the offer screen over RepHub.
        TakeOverFirstIdleVehicle();
        BackendApiHelper.SubmitServiceRequest(AppiumConfig.BackendBaseUrl);

        // Act
        // Wait for the map container (the live map mounts once MapsLoader reports the SDK available and
        // initMap runs), then for the requester pin overlay the page places via OnMapReady. Both waits use
        // the SignalR budget because the offer screen itself only appears after the server-pushed offer.
        var mapContainer = WaitForSignalR(d => d.FindElement(By.CssSelector("[data-testid='google-map']")));
        var requesterPin = WaitForSignalR(d => d.FindElement(By.CssSelector("[data-testid='requester-pin']")));

        // Assert
        Assert.That(mapContainer.Displayed, Is.True);
        Assert.That(requesterPin.Displayed, Is.True);
    }

    private int ReadCountdown()
    {
        var text = Driver.FindElement(By.CssSelector("[data-testid='countdown-ring']")).Text;
        var digits = new string(text.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : int.MaxValue;
    }

    // Reads the leading numeric part of a stat tile (e.g. "99.8MILES" -> 99.8, "100MIN ETA" -> 100).
    private static double ParseLeadingNumber(string text)
    {
        var prefix = new string(text.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());
        return double.TryParse(prefix, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }
}
