using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using ServiceDelivery.Client.Core.Services;
using ServiceDelivery.Client.UI.Features.Authentication.Services;
using ServiceDelivery.Client.UI.Features.Requester.Pages;

namespace ServiceDelivery.Client.Tests.Requester;

/// <summary>
/// FE-015 navigation: the BlazorPersonaNavigator's NavigateToRequesterPending (AC-4) routes to the
/// pending view, and the Requester persona home redirects to the submit screen so the requester lands
/// on the submit form after login.
/// </summary>
public class RequesterNavigationTests : BunitContext
{
    [Fact]
    public void GivenASuccessfulSubmit_WhenNavigateToRequesterPendingCalled_ThenRouteIsRequesterPending()
    {
        // Arrange
        var navigation = Services.GetRequiredService<NavigationManager>();
        var navigator = new BlazorPersonaNavigator(navigation, new InMemoryJobOfferStore());

        // Act
        navigator.NavigateToRequesterPending();

        // Assert
        Assert.EndsWith(PersonaRouteMap.RequesterPending, navigation.Uri);
    }

    [Fact]
    public void GivenTheRequesterHome_WhenRendered_ThenItRedirectsToTheSubmitScreen()
    {
        // Arrange
        var navigation = Services.GetRequiredService<NavigationManager>();

        // Act
        Render<RequesterHome>();

        // Assert
        Assert.EndsWith(PersonaRouteMap.RequesterSubmit, navigation.Uri);
    }

    [Fact]
    public void GivenPersonaRouteMap_WhenAccessingRequesterTracking_ThenRouteIsRequesterTrackingPath()
    {
        // Arrange
        // (PersonaRouteMap is a pure, stateless mapping helper; FE-016 adds the tracking route as the
        // forward-reference target for FE-017.)

        // Act
        var route = PersonaRouteMap.RequesterTracking;

        // Assert
        Assert.Equal("/requester/tracking", route);
    }

    [Fact]
    public void GivenARepAssigned_WhenNavigateToRequesterTrackingCalled_ThenRouteIsRequesterTracking()
    {
        // Arrange — FE-016/AC-3: when a RepAssigned event arrives the requester transitions to the
        // tracking view. FE-017 owns the real tracking screen; this story navigates to its stub route.
        var navigation = Services.GetRequiredService<NavigationManager>();
        var navigator = new BlazorPersonaNavigator(navigation, new InMemoryJobOfferStore());

        // Act
        navigator.NavigateToRequesterTracking();

        // Assert
        Assert.EndsWith(PersonaRouteMap.RequesterTracking, navigation.Uri);
    }
}
