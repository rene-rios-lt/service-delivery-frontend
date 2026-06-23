using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.Services;

namespace ServiceDelivery.Client.Tests.ServiceRep;

public class InMemoryJobOfferStoreTests
{
    private static JobOfferPayload Offer() =>
        new(Guid.NewGuid(), "Marcus", ServiceTier.Gold, "P0700 · Transmission Control Fault", 12.4, 13, 41.6, -93.6);

    private static IJobOfferStore CreateStore() => new InMemoryJobOfferStore();

    [Fact]
    public void GivenNothingStored_WhenCurrentOfferRead_ThenCurrentOfferIsNull()
    {
        // Arrange
        var store = CreateStore();

        // Act
        var current = store.CurrentOffer;

        // Assert
        Assert.Null(current);
    }

    [Fact]
    public void GivenAnOffer_WhenSetOfferCalled_ThenCurrentOfferReturnsThatOffer()
    {
        // Arrange
        var store = CreateStore();
        var offer = Offer();

        // Act
        store.SetOffer(offer);

        // Assert
        Assert.Same(offer, store.CurrentOffer);
    }

    [Fact]
    public void GivenAStoredOffer_WhenClearOfferCalled_ThenCurrentOfferIsNull()
    {
        // Arrange
        var store = CreateStore();
        store.SetOffer(Offer());

        // Act
        store.ClearOffer();

        // Assert
        Assert.Null(store.CurrentOffer);
    }
}
