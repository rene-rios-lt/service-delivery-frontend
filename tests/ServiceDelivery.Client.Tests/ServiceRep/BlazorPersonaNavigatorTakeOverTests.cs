using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using ServiceDelivery.Client.Core.Services;
using ServiceDelivery.Client.UI.Features.Authentication.Services;

namespace ServiceDelivery.Client.Tests.ServiceRep;

public class BlazorPersonaNavigatorTakeOverTests : BunitContext
{
    [Fact]
    public void GivenAReleasedVehicle_WhenNavigateToTakeOverCalled_ThenRouteIsTakeOver()
    {
        // Arrange
        var navigation = Services.GetRequiredService<NavigationManager>();
        var store = new InMemoryJobOfferStore();
        var navigator = new BlazorPersonaNavigator(navigation, store);

        // Act
        navigator.NavigateToTakeOver();

        // Assert
        Assert.EndsWith(PersonaRouteMap.ServiceRepTakeOver, navigation.Uri);
    }
}
