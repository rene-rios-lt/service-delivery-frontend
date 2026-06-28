using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Tests.Maps;

/// <summary>
/// Pure xUnit tests for <see cref="RepStateColour"/> (FE-024 AC-3). Each test asserts the exact
/// design-system hex token returned for a rep/vehicle state, plus the offline-grey fallback for an
/// unrecognised state. The hex literals are the authoritative tokens from the scoped CSS
/// (RepIdle.razor.css / ActiveJob.razor.css) — the production lookup must reproduce them.
/// </summary>
public class RepStateColourTests
{
    [Fact]
    public void GivenAvailableState_WhenColourResolved_ThenReturnsGreen2E9E5B()
    {
        // Arrange
        const string state = "Available";

        // Act
        var colour = RepStateColour.ForState(state);

        // Assert
        Assert.Equal("#2E9E5B", colour);
    }

    [Fact]
    public void GivenEnRouteState_WhenColourResolved_ThenReturnsBlue1E88E5()
    {
        // Arrange
        const string state = "EnRoute";

        // Act
        var colour = RepStateColour.ForState(state);

        // Assert
        Assert.Equal("#1E88E5", colour);
    }

    [Fact]
    public void GivenWithin15MilesState_WhenColourResolved_ThenReturnsYellowF4A100()
    {
        // Arrange
        const string state = "Within15Miles";

        // Act
        var colour = RepStateColour.ForState(state);

        // Assert
        Assert.Equal("#F4A100", colour);
    }

    [Fact]
    public void GivenOnSiteState_WhenColourResolved_ThenReturnsRedE5392F()
    {
        // Arrange
        const string state = "OnSite";

        // Act
        var colour = RepStateColour.ForState(state);

        // Assert
        Assert.Equal("#E5392F", colour);
    }

    [Fact]
    public void GivenOfflineState_WhenColourResolved_ThenReturnsGreyOthers()
    {
        // Arrange
        const string state = "Offline";

        // Act
        var colour = RepStateColour.ForState(state);

        // Assert
        Assert.Equal("#9AA0AE", colour);
    }

    [Fact]
    public void GivenUnknownState_WhenColourResolved_ThenFallsBackToOfflineGrey()
    {
        // Arrange — a state the lookup does not recognise (e.g. a future/garbage value) must not return
        // an empty string or throw; it falls back to the same offline grey as the Offline state.
        const string state = "SomethingUnrecognised";

        // Act
        var colour = RepStateColour.ForState(state);

        // Assert
        Assert.Equal("#9AA0AE", colour);
    }
}
