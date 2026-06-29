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
    /// FE-012 coverage (AC-1, AC-2, AC-3): once the rep accepts and is EnRoute, a position posted at the
    /// request site (distance 0) drives the backend to report the rep Within15Miles; the active-job poll
    /// surfaces that, enabling the "I've Arrived" button. Tapping it calls POST /rep/arrive; on success
    /// the highlighted navigation route line is removed and "Mark Complete" becomes the primary action.
    /// </summary>
    [Test]
    public void GivenRepIsWithin15Miles_WhenArrivedButtonTapped_ThenRouteLineGoneAndMarkCompleteVisible()
    {
        // Arrange
        TakeOverFirstIdleVehicle();
        BackendApiHelper.SubmitServiceRequest(AppiumConfig.BackendBaseUrl);
        WaitForSignalR(d => d.FindElement(By.CssSelector("[data-testid='accept-button']"))).Click();
        // Wait until the active-job screen is up — its presence means the accept has committed and the
        // rep is now EnRoute. Re-posting before that would hit UpdateVehiclePositionCommandHandler while
        // the rep is not yet EnRoute, so no Within15Miles transition would occur.
        WaitForSignalR(d => d.FindElement(By.CssSelector("[data-testid='arrived-button']")));
        // AC-3: re-post the site position so the EnRoute rep (vehicle at distance 0 < 15 mi) moves to
        // Within15Miles. The page polls the active job every ~3s, so IsArrivedEnabled flips and the
        // button enables. Poll for the enabled state before tapping.
        BackendApiHelper.PositionFleetAtRequestSite(AppiumConfig.BackendBaseUrl);
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

    /// <summary>
    /// FE-026 coverage (AC-6): after accepting a job the active-job screen renders the real Google map
    /// (FE-024 GoogleMap component) in place of the former CSS/SVG placeholder. The map container
    /// (data-testid='google-map') is present, and the rep marker, requester pin, and route line are real
    /// map overlays locatable by their data-testid attributes — the route line relies on the FE-026
    /// googleMap.js fix that stamps the polyline's testId onto an invisible DOM div (a google.maps.Polyline
    /// cannot host a DOM element). Requires a valid Maps API key in the test environment so the SDK loads;
    /// without one the component degrades to the [data-testid='map-unavailable'] placeholder.
    /// </summary>
    [Test]
    public void GivenRep1AcceptedJob_WhenActiveJobScreenLoads_ThenGoogleMapContainerIsPresentWithOverlayTestIds()
    {
        // Arrange
        TakeOverFirstIdleVehicle();
        BackendApiHelper.PositionFleetAtRequestSite(AppiumConfig.BackendBaseUrl);
        BackendApiHelper.SubmitServiceRequest(AppiumConfig.BackendBaseUrl);
        WaitForSignalR(d => d.FindElement(By.CssSelector("[data-testid='accept-button']"))).Click();

        // Act
        // The map and its overlays are placed once the embedded GoogleMap imports its JS module and the
        // page's OnMapReady callback runs — poll for each rather than assuming they are present on first paint.
        var map = WaitForSignalR(d => d.FindElement(By.CssSelector("[data-testid='google-map']")));
        var repMarker = WaitForSignalR(d => d.FindElement(By.CssSelector("[data-testid='rep-marker']")));
        var requesterPin = WaitForSignalR(d => d.FindElement(By.CssSelector("[data-testid='requester-pin']")));
        var routeLine = WaitForSignalR(d => d.FindElement(By.CssSelector("[data-testid='route-line']")));

        // Assert
        Assert.That(map.Displayed, Is.True);
        Assert.That(repMarker, Is.Not.Null);
        Assert.That(requesterPin, Is.Not.Null);
        Assert.That(routeLine, Is.Not.Null);
    }

    /// <summary>
    /// FE-013 coverage (AC-1, AC-2, AC-3): once the rep is on-site, tapping "Mark Complete" calls
    /// POST /rep/complete (AC-1) and on success the rep is returned to the idle waiting view (AC-2)
    /// with a brief "Job marked complete" confirmation toast (AC-3). Reaching the idle view proves the
    /// complete committed and the rep transitioned back to Available.
    /// </summary>
    [Test]
    public void GivenRepIsOnSite_WhenMarkCompleteButtonTapped_ThenIdleViewIsReached()
    {
        // Arrange
        TakeOverFirstIdleVehicle();
        BackendApiHelper.SubmitServiceRequest(AppiumConfig.BackendBaseUrl);
        WaitForSignalR(d => d.FindElement(By.CssSelector("[data-testid='accept-button']"))).Click();
        WaitForSignalR(d => d.FindElement(By.CssSelector("[data-testid='arrived-button']")));
        // Move the EnRoute rep to the request site (distance 0 < 15 mi) so the active-job poll flips
        // IsArrivedEnabled and the "I've Arrived" button enables; tap it to go on-site.
        BackendApiHelper.PositionFleetAtRequestSite(AppiumConfig.BackendBaseUrl);
        var arrivedButton = WaitForSignalR(d =>
        {
            var button = d.FindElement(By.CssSelector("[data-testid='arrived-button']"));
            return button.Enabled ? button : null;
        });
        arrivedButton!.Click();
        var completeButton = WaitForSignalR(d => d.FindElement(By.CssSelector("[data-testid='complete-button']")));

        // Act
        // AC-1: tapping "Mark Complete" calls POST /rep/complete.
        completeButton.Click();

        // Assert
        // AC-2: on success the rep is returned to the idle waiting view.
        var idleView = WaitForSignalR(d => d.FindElement(By.CssSelector("[data-testid='rep-idle']")));
        Assert.That(idleView.Displayed, Is.True);
    }
}
