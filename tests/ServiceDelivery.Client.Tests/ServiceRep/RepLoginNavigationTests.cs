using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.UI.Features.Authentication.Services;

namespace ServiceDelivery.Client.Tests.ServiceRep;

public class RepLoginNavigationTests : BunitContext
{
    [Fact]
    public void GivenAServiceRepRole_WhenLoginSucceeds_ThenNavigatesToRepTakeOver()
    {
        // Arrange
        // Login delegates role routing to IPersonaNavigator; the Blazor navigator maps the
        // ServiceRep role to its first screen via PersonaRouteMap (AC-1).
        var navigation = Services.GetRequiredService<NavigationManager>();
        var navigator = new BlazorPersonaNavigator(navigation);

        // Act
        navigator.NavigateToPersonaHome(UserRole.ServiceRep);

        // Assert
        Assert.EndsWith(PersonaRouteMap.ServiceRepTakeOver, navigation.Uri);
    }
}
