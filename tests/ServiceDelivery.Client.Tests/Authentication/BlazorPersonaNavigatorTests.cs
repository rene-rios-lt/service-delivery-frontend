using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using ServiceDelivery.Client.UI.Features.Authentication.Services;

namespace ServiceDelivery.Client.Tests.Authentication;

public class BlazorPersonaNavigatorTests : BunitContext
{
    [Fact]
    public void GivenANavigateToLoginCall_WhenInvoked_ThenNavigatesToLoginRoute()
    {
        // Arrange
        var navigation = Services.GetRequiredService<NavigationManager>();
        var navigator = new BlazorPersonaNavigator(navigation);

        // Act
        navigator.NavigateToLogin();

        // Assert
        Assert.EndsWith(PersonaRouteMap.Login, navigation.Uri);
    }

    [Fact]
    public void GivenARepIdleViewCall_WhenInvoked_ThenNavigatesToServiceRepIdleRoute()
    {
        // Arrange
        // FE-020/AC-1: the idle / waiting-for-offers view now has its own route. A successful
        // take-over (and decline / post-job transitions) lands here rather than the placeholder
        // /rep home that FE-007 used as a stand-in.
        var navigation = Services.GetRequiredService<NavigationManager>();
        var navigator = new BlazorPersonaNavigator(navigation);

        // Act
        navigator.NavigateToRepIdleView();

        // Assert
        Assert.EndsWith(PersonaRouteMap.ServiceRepIdle, navigation.Uri);
    }
}
