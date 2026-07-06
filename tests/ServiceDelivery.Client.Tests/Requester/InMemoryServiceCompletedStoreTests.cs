using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.Services;

namespace ServiceDelivery.Client.Tests.Requester;

/// <summary>
/// Unit tests for <see cref="InMemoryServiceCompletedStore"/> (FE-019). The store is a two-phase
/// cross-navigation hand-off: <c>SubmitRequestViewModel</c> deposits the selected DTC title at submit
/// success (<see cref="Core.Interfaces.IServiceCompletedStore.SetDtcTitle"/>), and
/// <c>RequesterTrackingViewModel</c> deposits the assembled <see cref="ServiceCompletionData"/> on the
/// <c>ServiceCompleted</c> push (<see cref="Core.Interfaces.IServiceCompletedStore.SetPayload"/>); the
/// completion ViewModel reads <c>CurrentPayload</c>. Mirrors <c>InMemoryRepAssignedStore</c>.
/// </summary>
public class InMemoryServiceCompletedStoreTests
{
    private static ServiceCompletionData Payload() =>
        new("Jordan Tran", "Transmission Control Fault");

    [Fact]
    public void GivenANewStore_WhenNoPayloadSet_ThenCurrentPayloadIsNull()
    {
        // Arrange
        var store = new InMemoryServiceCompletedStore();

        // Act
        var current = store.CurrentPayload;

        // Assert
        Assert.Null(current);
    }

    [Fact]
    public void GivenANewStore_WhenNoDtcTitleSet_ThenDtcTitleIsNull()
    {
        // Arrange
        var store = new InMemoryServiceCompletedStore();

        // Act
        var dtcTitle = store.DtcTitle;

        // Assert
        Assert.Null(dtcTitle);
    }

    [Fact]
    public void GivenADtcTitle_WhenSetDtcTitleCalled_ThenDtcTitleReturnsIt()
    {
        // Arrange
        var store = new InMemoryServiceCompletedStore();

        // Act — the submit VM threads the selected DTC title forward at the earliest point it is known.
        store.SetDtcTitle("Transmission Control Fault");

        // Assert
        Assert.Equal("Transmission Control Fault", store.DtcTitle);
    }

    [Fact]
    public void GivenAPayload_WhenSetPayloadCalled_ThenCurrentPayloadReturnsIt()
    {
        // Arrange
        var store = new InMemoryServiceCompletedStore();
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
        var store = new InMemoryServiceCompletedStore();
        store.SetPayload(Payload());

        // Act
        store.ClearPayload();

        // Assert
        Assert.Null(store.CurrentPayload);
    }
}
