using System.Collections.Generic;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.ViewModels;

namespace ServiceDelivery.Client.Tests.Dispatcher;

/// <summary>
/// Pure xUnit tests for <see cref="DispatcherFleetViewModel"/> (FE-003 ACs 2, 4, 5, 7). Covers fleet
/// loading + visibility filtering, real-time position-update merging, state-change notification, marker
/// selection, and the Offline-removal rule — all with mocked <see cref="IDispatcherFleetService"/> and
/// <see cref="IVehiclePositionHubService"/> (no rendering, no live hub).
/// </summary>
public class DispatcherFleetViewModelTests
{
    private static readonly Guid Vehicle7 = Guid.Parse("30000000-0000-0000-0000-000000000007");

    private readonly Mock<IDispatcherFleetService> _fleetService = new();
    private readonly Mock<IVehiclePositionHubService> _hub = new();
    private readonly Mock<IForceReleaseService> _forceReleaseService = new();

    private DispatcherFleetViewModel CreateViewModel() =>
        new(_fleetService.Object, _hub.Object, _forceReleaseService.Object);

    private void FleetReturns(params FleetVehicleEntry[] entries) =>
        _fleetService.Setup(s => s.GetFleetAsync()).ReturnsAsync(entries.ToList());

    private static FleetVehicleEntry Entry(
        string vehicleId, string? state = "Available", double lat = 41.60, double lng = -93.60) =>
        new(vehicleId, $"IA-{vehicleId[..4]}", state, Guid.NewGuid(), "Rep Name",
            lat, lng, null, null, false);

    [Fact]
    public async Task GivenFleetServiceReturnsThreeVehicles_WhenViewModelLoadsAsync_ThenVisibleVehiclesCountIsThree()
    {
        // Arrange
        FleetReturns(
            Entry("11111111-0000-0000-0000-000000000001", "Available"),
            Entry("22222222-0000-0000-0000-000000000002", "EnRoute"),
            Entry("33333333-0000-0000-0000-000000000003", "OnSite"));
        var vm = CreateViewModel();

        // Act
        await vm.LoadAsync();

        // Assert
        Assert.Equal(3, vm.VisibleVehicles.Count);
    }

    [Fact]
    public async Task GivenFleetIncludesOfflineAndNullStateVehicles_WhenLoaded_ThenOnlyOnlineVehiclesAreVisible()
    {
        // Arrange — VisibleVehicles excludes a null or "Offline" rep-state (legend-only states, AC-7).
        FleetReturns(
            Entry("11111111-0000-0000-0000-000000000001", "Available"),
            Entry("22222222-0000-0000-0000-000000000002", "Offline"),
            Entry("33333333-0000-0000-0000-000000000003", null));
        var vm = CreateViewModel();

        // Act
        await vm.LoadAsync();

        // Assert
        Assert.Single(vm.VisibleVehicles);
        Assert.Equal("11111111-0000-0000-0000-000000000001", vm.VisibleVehicles[0].VehicleId);
    }

    [Fact]
    public async Task GivenExistingVehicleInFleet_WhenPositionUpdateHandled_ThenFleetEntryLatLngAreUpdated()
    {
        // Arrange
        FleetReturns(Entry(Vehicle7.ToString(), "EnRoute", 41.60, -93.60));
        var vm = CreateViewModel();
        await vm.LoadAsync();

        // Act
        await vm.HandleVehiclePositionUpdatedAsync(
            new VehiclePositionUpdatedPayload(Guid.NewGuid(), Vehicle7, 42.10, -94.05, "EnRoute"));

        // Assert
        var entry = Assert.Single(vm.VisibleVehicles);
        Assert.Equal(42.10, entry.Latitude);
        Assert.Equal(-94.05, entry.Longitude);
    }

    [Fact]
    public async Task GivenExistingVehicleInFleet_WhenPositionUpdateHandled_ThenSnapshotMetadataIsPreserved()
    {
        // Arrange — the position event carries no registration/name/tier, so a merge (not a replace) must
        // keep the snapshot metadata that only the REST load provided.
        FleetReturns(Entry(Vehicle7.ToString(), "EnRoute", 41.60, -93.60));
        var vm = CreateViewModel();
        await vm.LoadAsync();

        // Act
        await vm.HandleVehiclePositionUpdatedAsync(
            new VehiclePositionUpdatedPayload(Guid.NewGuid(), Vehicle7, 42.10, -94.05, "EnRoute"));

        // Assert
        var entry = Assert.Single(vm.VisibleVehicles);
        Assert.Equal("IA-3000", entry.Registration);
        Assert.Equal("Rep Name", entry.RepName);
    }

    [Fact]
    public async Task GivenViewModelLoaded_WhenPositionUpdateHandled_ThenStateChangedEventFires()
    {
        // Arrange
        FleetReturns(Entry(Vehicle7.ToString(), "EnRoute"));
        var vm = CreateViewModel();
        await vm.LoadAsync();
        var fired = false;
        vm.StateChanged += () => fired = true;

        // Act
        await vm.HandleVehiclePositionUpdatedAsync(
            new VehiclePositionUpdatedPayload(Guid.NewGuid(), Vehicle7, 42.10, -94.05, "EnRoute"));

        // Assert
        Assert.True(fired);
    }

    [Fact]
    public async Task GivenVehicleInFleet_WhenSelectVehicleCalled_ThenSelectedVehicleIsSetAndStateChangedFires()
    {
        // Arrange
        FleetReturns(Entry(Vehicle7.ToString(), "EnRoute"));
        var vm = CreateViewModel();
        await vm.LoadAsync();
        var fired = false;
        vm.StateChanged += () => fired = true;

        // Act
        vm.SelectVehicle(Vehicle7.ToString());

        // Assert
        Assert.NotNull(vm.SelectedVehicle);
        Assert.Equal(Vehicle7.ToString(), vm.SelectedVehicle!.VehicleId);
        Assert.True(fired);
    }

    [Fact]
    public async Task GivenASelectedVehicle_WhenClearSelectionCalled_ThenSelectedVehicleIsNull()
    {
        // Arrange
        FleetReturns(Entry(Vehicle7.ToString(), "EnRoute"));
        var vm = CreateViewModel();
        await vm.LoadAsync();
        vm.SelectVehicle(Vehicle7.ToString());

        // Act
        vm.ClearSelection();

        // Assert
        Assert.Null(vm.SelectedVehicle);
    }

    [Fact]
    public async Task GivenVehicleWithOfflineStateUpdate_WhenPositionUpdateHandled_ThenVehicleRemovedFromVisibleVehicles()
    {
        // Arrange
        FleetReturns(Entry(Vehicle7.ToString(), "EnRoute"));
        var vm = CreateViewModel();
        await vm.LoadAsync();
        Assert.Single(vm.VisibleVehicles);

        // Act
        await vm.HandleVehiclePositionUpdatedAsync(
            new VehiclePositionUpdatedPayload(Guid.NewGuid(), Vehicle7, 42.10, -94.05, "Offline"));

        // Assert
        Assert.Empty(vm.VisibleVehicles);
    }

    [Fact]
    public async Task GivenViewModel_WhenStartHubAsync_ThenHandlerRegisteredAndConnectionStarted()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        await vm.StartHubAsync();

        // Assert
        _hub.Verify(h => h.OnVehiclePositionUpdated(
            It.IsAny<Func<VehiclePositionUpdatedPayload, Task>>()), Times.Once);
        _hub.Verify(h => h.StartAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenViewModel_WhenStopHubAsync_ThenConnectionStopped()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        await vm.StopHubAsync();

        // Assert
        _hub.Verify(h => h.StopAsync(), Times.Once);
    }
}
