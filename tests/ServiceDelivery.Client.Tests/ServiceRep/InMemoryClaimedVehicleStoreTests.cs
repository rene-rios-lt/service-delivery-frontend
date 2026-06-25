using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.Services;

namespace ServiceDelivery.Client.Tests.ServiceRep;

public class InMemoryClaimedVehicleStoreTests
{
    private static ClaimedVehicle Vehicle() =>
        new(Guid.NewGuid(), "V-001", string.Empty, new[] { "Hydraulics", "Coolant" });

    private static IClaimedVehicleStore CreateStore() => new InMemoryClaimedVehicleStore();

    [Fact]
    public void GivenNothingStored_WhenCurrentVehicleRead_ThenCurrentVehicleIsNull()
    {
        // Arrange
        var store = CreateStore();

        // Act
        var current = store.CurrentVehicle;

        // Assert
        Assert.Null(current);
    }

    [Fact]
    public void GivenAVehicle_WhenSetVehicleCalled_ThenCurrentVehicleReturnsThatVehicle()
    {
        // Arrange
        var store = CreateStore();
        var vehicle = Vehicle();

        // Act
        store.SetVehicle(vehicle);

        // Assert
        Assert.Same(vehicle, store.CurrentVehicle);
    }

    [Fact]
    public void GivenAStoredVehicle_WhenClearVehicleCalled_ThenCurrentVehicleIsNull()
    {
        // Arrange
        var store = CreateStore();
        store.SetVehicle(Vehicle());

        // Act
        store.ClearVehicle();

        // Assert
        Assert.Null(store.CurrentVehicle);
    }
}
