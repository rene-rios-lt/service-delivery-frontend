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
    public void GivenARepIdleViewCall_WhenInvoked_ThenNavigatesToServiceRepHomeRoute()
    {
        // Arrange
        var navigation = Services.GetRequiredService<NavigationManager>();
        var navigator = new BlazorPersonaNavigator(navigation);

        // Act
        navigator.NavigateToRepIdleView();

        // Assert
        Assert.EndsWith(PersonaRouteMap.ServiceRepHome, navigation.Uri);
    }
}
