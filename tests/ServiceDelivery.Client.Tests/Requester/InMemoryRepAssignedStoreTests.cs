using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.Services;

namespace ServiceDelivery.Client.Tests.Requester;

/// <summary>
/// Unit tests for <see cref="InMemoryRepAssignedStore"/> (FE-017). The store carries the full
/// <see cref="RepAssignedPayload"/> — including the FE-017 <c>VehicleRegistration</c> — from the requester
/// pending view (which receives the RepAssigned push) to the tracking view (which seeds its ViewModel from
/// it) within one session scope, mirroring <c>InMemoryJobOfferStore</c>.
/// </summary>
public class InMemoryRepAssignedStoreTests
{
    private static RepAssignedPayload Payload() =>
        new(Guid.NewGuid(), "Jordan Tran", 9, 41.601, -93.609, "IA-4471");

    [Fact]
    public void GivenANewStore_WhenNoPayloadSet_ThenCurrentPayloadIsNull()
    {
        // Arrange
        var store = new InMemoryRepAssignedStore();

        // Act
        var current = store.CurrentPayload;

        // Assert
        Assert.Null(current);
    }

    [Fact]
    public void GivenAPayload_WhenSetPayloadCalled_ThenCurrentPayloadReturnsIt()
    {
        // Arrange
        var store = new InMemoryRepAssignedStore();
        var payload = Payload();

        // Act
        store.SetPayload(payload);

        // Assert
        Assert.Same(payload, store.CurrentPayload);
    }

    [Fact]
    public void GivenAStoredPayload_WhenClearPayloadCalled_ThenCurrentPayloadIsNull()
    {
        // Arrange
        var store = new InMemoryRepAssignedStore();
        store.SetPayload(Payload());

        // Act
        store.ClearPayload();

        // Assert
        Assert.Null(store.CurrentPayload);
    }
}
