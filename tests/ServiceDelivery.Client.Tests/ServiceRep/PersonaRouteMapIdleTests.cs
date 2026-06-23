using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using ServiceDelivery.Client.Core.Services;
using ServiceDelivery.Client.UI.Features.Authentication.Services;

namespace ServiceDelivery.Client.Tests.ServiceRep;

public class PersonaRouteMapIdleTests : BunitContext
{
    [Fact]
    public void GivenNavigateToRepIdleViewCalled_WhenNavigatorInvoked_ThenRouteIsRepIdle()
    {
        // Arrange
        // The idle / waiting-for-offers view is reached via a single route constant so
        // take-over, decline, and post-job transitions all land on the same screen (AC-1).

        // Act
        var route = PersonaRouteMap.ServiceRepIdle;

        // Assert
        Assert.Equal("/rep/idle", route);
    }

    [Fact]
    public void GivenBlazorPersonaNavigator_WhenNavigateToRepIdleViewCalled_ThenNavigatesToRepIdleRoute()
    {
        // Arrange
        var navigation = Services.GetRequiredService<NavigationManager>();
        var navigator = new BlazorPersonaNavigator(navigation, new InMemoryJobOfferStore());

        // Act
        navigator.NavigateToRepIdleView();

        // Assert
        Assert.EndsWith(PersonaRouteMap.ServiceRepIdle, navigation.Uri);
    }
}
