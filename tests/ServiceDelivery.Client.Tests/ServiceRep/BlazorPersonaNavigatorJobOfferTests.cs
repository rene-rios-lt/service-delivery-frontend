using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.Services;
using ServiceDelivery.Client.UI.Features.Authentication.Services;

namespace ServiceDelivery.Client.Tests.ServiceRep;

public class BlazorPersonaNavigatorJobOfferTests : BunitContext
{
    private static JobOfferPayload Offer() =>
        new(Guid.NewGuid(), "Marcus", ServiceTier.Gold, "P0700 · Transmission Control Fault", 12.4, 13, 41.6, -93.6);

    [Fact]
    public void GivenAJobOfferPayload_WhenNavigateToJobOfferCalled_ThenPayloadIsStoredAndRouteIsJobOffer()
    {
        // Arrange
        var navigation = Services.GetRequiredService<NavigationManager>();
        var store = new InMemoryJobOfferStore();
        var navigator = new BlazorPersonaNavigator(navigation, store);
        var offer = Offer();

        // Act
        navigator.NavigateToJobOffer(offer);

        // Assert
        Assert.Same(offer, store.CurrentOffer);
        Assert.EndsWith(PersonaRouteMap.ServiceRepJobOffer, navigation.Uri);
    }
}
