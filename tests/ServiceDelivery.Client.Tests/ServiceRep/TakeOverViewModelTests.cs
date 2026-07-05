using System.Collections.Generic;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.ViewModels;

namespace ServiceDelivery.Client.Tests.ServiceRep;

public class TakeOverViewModelTests
{
    private readonly Mock<IVehicleService> _vehicleService = new();
    private readonly Mock<IPersonaNavigator> _navigator = new();
    private readonly Mock<IClaimedVehicleStore> _claimedVehicleStore = new();

    private TakeOverViewModel CreateViewModel() =>
        new(_vehicleService.Object, _navigator.Object, _claimedVehicleStore.Object);

    private static IdleVehicle Vehicle(string registration = "IA-4471", string model = "Transit 350") =>
        new(Guid.NewGuid(), registration, model, new[] { "Hydraulics", "Coolant" });

    [Fact]
    public async Task GivenIdleVehiclesReturnedByService_WhenViewModelLoads_ThenIdleVehiclesListIsPopulated()
    {
        // Arrange
        var vehicles = new[] { Vehicle("IA-4471"), Vehicle("IA-2208") };
        _vehicleService.Setup(s => s.GetIdleVehiclesAsync()).ReturnsAsync(vehicles);
        var vm = CreateViewModel();

        // Act
        await vm.LoadAsync();

        // Assert
        Assert.Equal(2, vm.IdleVehicles.Count);
        Assert.Equal("IA-4471", vm.IdleVehicles[0].Registration);
    }

    [Fact]
    public async Task GivenAVehicleIsSelected_WhenTakeOverCalled_ThenServiceTakeOverAsyncIsCalledWithVehicleId()
    {
        // Arrange
        var vehicle = Vehicle("IA-4471");
        _vehicleService.Setup(s => s.GetIdleVehiclesAsync()).ReturnsAsync(new[] { vehicle });
        _vehicleService.Setup(s => s.TakeOverAsync(It.IsAny<Guid>())).ReturnsAsync(TakeOverResult.Success);
        var vm = CreateViewModel();
        await vm.LoadAsync();
        vm.Select(vehicle.VehicleId);

        // Act
        await vm.TakeOverAsync();

        // Assert
        _vehicleService.Verify(s => s.TakeOverAsync(vehicle.VehicleId), Times.Once);
    }

    [Fact]
    public async Task GivenTakeOverSucceeds_WhenTakeOverCalled_ThenResultIsSuccess()
    {
        // Arrange
        var vehicle = Vehicle("IA-4471");
        _vehicleService.Setup(s => s.GetIdleVehiclesAsync()).ReturnsAsync(new[] { vehicle });
        _vehicleService.Setup(s => s.TakeOverAsync(vehicle.VehicleId)).ReturnsAsync(TakeOverResult.Success);
        var vm = CreateViewModel();
        await vm.LoadAsync();
        vm.Select(vehicle.VehicleId);

        // Act
        await vm.TakeOverAsync();

        // Assert
        Assert.Equal(TakeOverResult.Success, vm.LastResult);
    }

    [Fact]
    public async Task GivenTakeOverSucceeds_WhenTakeOverCalled_ThenNavigatesToServiceRepHome()
    {
        // Arrange
        var vehicle = Vehicle("IA-4471");
        _vehicleService.Setup(s => s.GetIdleVehiclesAsync()).ReturnsAsync(new[] { vehicle });
        _vehicleService.Setup(s => s.TakeOverAsync(vehicle.VehicleId)).ReturnsAsync(TakeOverResult.Success);
        var vm = CreateViewModel();
        await vm.LoadAsync();
        vm.Select(vehicle.VehicleId);

        // Act
        await vm.TakeOverAsync();

        // Assert
        _navigator.Verify(n => n.NavigateToRepIdleView(), Times.Once);
    }

    [Fact]
    public async Task GivenTakeOverReturnsConflict_WhenTakeOverCalled_ThenErrorMessageIsSet()
    {
        // Arrange
        // A single-vehicle list that conflicts has no next candidate, so the bounded retry loop
        // exhausts immediately and surfaces the terminal ExhaustedMessage (BUG-045).
        var vehicle = Vehicle("IA-4471");
        _vehicleService.Setup(s => s.GetIdleVehiclesAsync()).ReturnsAsync(new[] { vehicle });
        _vehicleService.Setup(s => s.TakeOverAsync(vehicle.VehicleId)).ReturnsAsync(TakeOverResult.Conflict);
        var vm = CreateViewModel();
        await vm.LoadAsync();
        vm.Select(vehicle.VehicleId);

        // Act
        await vm.TakeOverAsync();

        // Assert
        Assert.Equal(TakeOverViewModel.ExhaustedMessage, vm.ErrorMessage);
    }

    [Fact]
    public async Task GivenTakeOverReturnsConflict_WhenTakeOverCalled_ThenGetIdleVehiclesIsCalledAgain()
    {
        // Arrange
        var vehicle = Vehicle("IA-4471");
        _vehicleService.Setup(s => s.GetIdleVehiclesAsync()).ReturnsAsync(new[] { vehicle });
        _vehicleService.Setup(s => s.TakeOverAsync(vehicle.VehicleId)).ReturnsAsync(TakeOverResult.Conflict);
        var vm = CreateViewModel();
        await vm.LoadAsync();
        vm.Select(vehicle.VehicleId);

        // Act
        await vm.TakeOverAsync();

        // Assert
        // Once for the initial Load, once for the post-conflict refresh (AC-5).
        _vehicleService.Verify(s => s.GetIdleVehiclesAsync(), Times.Exactly(2));
    }

    [Fact]
    public async Task GivenTakeOverReturnsConflict_WhenTakeOverCalled_ThenSelectionIsCleared()
    {
        // Arrange
        // The previously chosen vehicle is gone after the refresh, so the stale selection must
        // clear — otherwise CanTakeOver stays true with no matching registration (AC-5).
        var vehicle = Vehicle("IA-4471");
        _vehicleService.Setup(s => s.GetIdleVehiclesAsync()).ReturnsAsync(new[] { vehicle });
        _vehicleService.Setup(s => s.TakeOverAsync(vehicle.VehicleId)).ReturnsAsync(TakeOverResult.Conflict);
        var vm = CreateViewModel();
        await vm.LoadAsync();
        vm.Select(vehicle.VehicleId);

        // Act
        await vm.TakeOverAsync();

        // Assert
        Assert.Null(vm.SelectedVehicleId);
        Assert.False(vm.CanTakeOver);
    }

    [Fact]
    public async Task GivenRepAlreadyMidJob_WhenTakeOverScreenLoads_ThenTakeOverIsNotPermitted()
    {
        // Arrange
        // Only an idle rep may take over (AC-6). A rep who reaches this screen already mid-job
        // is ineligible: the form is blocked even if a vehicle is selected.
        var vehicle = Vehicle("IA-4471");
        _vehicleService.Setup(s => s.GetIdleVehiclesAsync()).ReturnsAsync(new[] { vehicle });
        var vm = CreateViewModel();
        vm.SetEligibility(repIsIdle: false);
        await vm.LoadAsync();
        vm.Select(vehicle.VehicleId);

        // Act
        var canTakeOver = vm.CanTakeOver;

        // Assert
        Assert.False(canTakeOver);
        Assert.False(vm.IsEligible);
    }

    [Fact]
    public async Task GivenRepAlreadyMidJob_WhenTakeOverInvoked_ThenServiceIsNotCalled()
    {
        // Arrange
        var vehicle = Vehicle("IA-4471");
        _vehicleService.Setup(s => s.GetIdleVehiclesAsync()).ReturnsAsync(new[] { vehicle });
        var vm = CreateViewModel();
        vm.SetEligibility(repIsIdle: false);
        await vm.LoadAsync();
        vm.Select(vehicle.VehicleId);

        // Act
        await vm.TakeOverAsync();

        // Assert
        _vehicleService.Verify(s => s.TakeOverAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task GivenASelectedVehicle_WhenTakeOverSucceeds_ThenClaimedVehicleStoreContainsSelectedVehicle()
    {
        // Arrange
        var vehicle = Vehicle("V-001");
        _vehicleService.Setup(s => s.GetIdleVehiclesAsync()).ReturnsAsync(new[] { vehicle });
        _vehicleService.Setup(s => s.TakeOverAsync(vehicle.VehicleId)).ReturnsAsync(TakeOverResult.Success);
        var vm = CreateViewModel();
        await vm.LoadAsync();
        vm.Select(vehicle.VehicleId);

        // Act
        await vm.TakeOverAsync();

        // Assert
        _claimedVehicleStore.Verify(
            s => s.SetVehicle(It.Is<ClaimedVehicle>(c => c.VehicleId == vehicle.VehicleId)),
            Times.Once);
    }

    [Fact]
    public async Task GivenASelectedVehicle_WhenTakeOverSucceeds_ThenStoredRegistrationMatchesSelectedIdleVehicle()
    {
        // Arrange
        var vehicle = Vehicle("V-001");
        _vehicleService.Setup(s => s.GetIdleVehiclesAsync()).ReturnsAsync(new[] { vehicle });
        _vehicleService.Setup(s => s.TakeOverAsync(vehicle.VehicleId)).ReturnsAsync(TakeOverResult.Success);
        var vm = CreateViewModel();
        await vm.LoadAsync();
        vm.Select(vehicle.VehicleId);

        // Act
        await vm.TakeOverAsync();

        // Assert
        _claimedVehicleStore.Verify(
            s => s.SetVehicle(It.Is<ClaimedVehicle>(c => c.Registration == "V-001")),
            Times.Once);
    }

    [Fact]
    public async Task GivenASelectedVehicle_WhenTakeOverSucceeds_ThenStoredEquipmentMatchesSelectedIdleVehicle()
    {
        // Arrange
        var vehicle = new IdleVehicle(Guid.NewGuid(), "V-001", "Transit 350", new[] { "Hydraulics", "Coolant" });
        _vehicleService.Setup(s => s.GetIdleVehiclesAsync()).ReturnsAsync(new[] { vehicle });
        _vehicleService.Setup(s => s.TakeOverAsync(vehicle.VehicleId)).ReturnsAsync(TakeOverResult.Success);
        var vm = CreateViewModel();
        await vm.LoadAsync();
        vm.Select(vehicle.VehicleId);

        // Act
        await vm.TakeOverAsync();

        // Assert
        _claimedVehicleStore.Verify(
            s => s.SetVehicle(It.Is<ClaimedVehicle>(c =>
                c.EquipmentTypes.SequenceEqual(new[] { "Hydraulics", "Coolant" }))),
            Times.Once);
    }

    [Fact]
    public async Task GivenASelectedVehicle_WhenTakeOverSucceeds_ThenStoredModelMatchesSelectedIdleVehicle()
    {
        // Arrange
        // The claimed vehicle must carry the selected idle vehicle's model end to end (BUG-035) —
        // not an empty string and not an echoed constant. A distinct model proves the real value flows.
        var vehicle = Vehicle("V-001", model: "Transit 350");
        _vehicleService.Setup(s => s.GetIdleVehiclesAsync()).ReturnsAsync(new[] { vehicle });
        _vehicleService.Setup(s => s.TakeOverAsync(vehicle.VehicleId)).ReturnsAsync(TakeOverResult.Success);
        var vm = CreateViewModel();
        await vm.LoadAsync();
        vm.Select(vehicle.VehicleId);

        // Act
        await vm.TakeOverAsync();

        // Assert
        _claimedVehicleStore.Verify(
            s => s.SetVehicle(It.Is<ClaimedVehicle>(c => c.Model == vehicle.Model && c.Model == "Transit 350")),
            Times.Once);
    }

    [Fact]
    public async Task GivenASelectedVehicleWithNoModel_WhenTakeOverSucceeds_ThenStoredModelIsEmptyString()
    {
        // Arrange
        // When the idle vehicle carries no model, the claimed vehicle's Model stays empty so the
        // idle card renders the registration only (the empty-guard path).
        var vehicle = Vehicle("V-001", model: string.Empty);
        _vehicleService.Setup(s => s.GetIdleVehiclesAsync()).ReturnsAsync(new[] { vehicle });
        _vehicleService.Setup(s => s.TakeOverAsync(vehicle.VehicleId)).ReturnsAsync(TakeOverResult.Success);
        var vm = CreateViewModel();
        await vm.LoadAsync();
        vm.Select(vehicle.VehicleId);

        // Act
        await vm.TakeOverAsync();

        // Assert
        _claimedVehicleStore.Verify(
            s => s.SetVehicle(It.Is<ClaimedVehicle>(c => c.Model == string.Empty)),
            Times.Once);
    }

    [Fact]
    public async Task GivenTakeOverConflict_WhenTakeOverCalled_ThenClaimedVehicleStoreIsNotPopulated()
    {
        // Arrange
        var vehicle = Vehicle("V-001");
        _vehicleService.Setup(s => s.GetIdleVehiclesAsync()).ReturnsAsync(new[] { vehicle });
        _vehicleService.Setup(s => s.TakeOverAsync(vehicle.VehicleId)).ReturnsAsync(TakeOverResult.Conflict);
        var vm = CreateViewModel();
        await vm.LoadAsync();
        vm.Select(vehicle.VehicleId);

        // Act
        await vm.TakeOverAsync();

        // Assert
        _claimedVehicleStore.Verify(s => s.SetVehicle(It.IsAny<ClaimedVehicle>()), Times.Never);
    }

    [Fact]
    public async Task GivenFirstVehicleConflicts_WhenTakeOverCalled_ThenSecondVehicleIsAttempted()
    {
        // Arrange
        // Vehicle A is claimed out from under the rep (409). The refreshed list still holds a
        // distinct candidate B; the bounded retry loop must automatically attempt B (AC-1 retry).
        var vehicleA = Vehicle("IA-4471");
        var vehicleB = Vehicle("IA-2208");
        _vehicleService.SetupSequence(s => s.GetIdleVehiclesAsync())
            .ReturnsAsync(new[] { vehicleA, vehicleB })
            .ReturnsAsync(new[] { vehicleB });
        _vehicleService.Setup(s => s.TakeOverAsync(vehicleA.VehicleId)).ReturnsAsync(TakeOverResult.Conflict);
        _vehicleService.Setup(s => s.TakeOverAsync(vehicleB.VehicleId)).ReturnsAsync(TakeOverResult.Success);
        var vm = CreateViewModel();
        await vm.LoadAsync();
        vm.Select(vehicleA.VehicleId);

        // Act
        await vm.TakeOverAsync();

        // Assert
        _vehicleService.Verify(s => s.TakeOverAsync(vehicleB.VehicleId), Times.Once);
    }

    [Fact]
    public async Task GivenFirstVehicleConflictsAndSecondSucceeds_WhenTakeOverCalled_ThenClaimedVehicleIsSecondCandidate()
    {
        // Arrange
        // Vehicle A conflicts, vehicle B succeeds. The claimed vehicle store must hold B's id —
        // proving the retry claimed the RIGHT vehicle, not merely that some claim occurred (AC-1).
        var vehicleA = Vehicle("IA-4471");
        var vehicleB = Vehicle("IA-2208");
        _vehicleService.SetupSequence(s => s.GetIdleVehiclesAsync())
            .ReturnsAsync(new[] { vehicleA, vehicleB })
            .ReturnsAsync(new[] { vehicleB });
        _vehicleService.Setup(s => s.TakeOverAsync(vehicleA.VehicleId)).ReturnsAsync(TakeOverResult.Conflict);
        _vehicleService.Setup(s => s.TakeOverAsync(vehicleB.VehicleId)).ReturnsAsync(TakeOverResult.Success);
        var vm = CreateViewModel();
        await vm.LoadAsync();
        vm.Select(vehicleA.VehicleId);

        // Act
        await vm.TakeOverAsync();

        // Assert
        _claimedVehicleStore.Verify(
            s => s.SetVehicle(It.Is<ClaimedVehicle>(c => c.VehicleId == vehicleB.VehicleId)),
            Times.Once);
        _claimedVehicleStore.Verify(
            s => s.SetVehicle(It.Is<ClaimedVehicle>(c => c.VehicleId == vehicleA.VehicleId)),
            Times.Never);
    }

    [Fact]
    public async Task GivenFirstVehicleConflictsAndSecondSucceeds_WhenTakeOverCalled_ThenResultIsSuccess()
    {
        // Arrange
        var vehicleA = Vehicle("IA-4471");
        var vehicleB = Vehicle("IA-2208");
        _vehicleService.SetupSequence(s => s.GetIdleVehiclesAsync())
            .ReturnsAsync(new[] { vehicleA, vehicleB })
            .ReturnsAsync(new[] { vehicleB });
        _vehicleService.Setup(s => s.TakeOverAsync(vehicleA.VehicleId)).ReturnsAsync(TakeOverResult.Conflict);
        _vehicleService.Setup(s => s.TakeOverAsync(vehicleB.VehicleId)).ReturnsAsync(TakeOverResult.Success);
        var vm = CreateViewModel();
        await vm.LoadAsync();
        vm.Select(vehicleA.VehicleId);

        // Act
        await vm.TakeOverAsync();

        // Assert
        Assert.Equal(TakeOverResult.Success, vm.LastResult);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task GivenFirstVehicleConflictsAndSecondSucceeds_WhenTakeOverCalled_ThenNavigatesToRepIdleView()
    {
        // Arrange
        var vehicleA = Vehicle("IA-4471");
        var vehicleB = Vehicle("IA-2208");
        _vehicleService.SetupSequence(s => s.GetIdleVehiclesAsync())
            .ReturnsAsync(new[] { vehicleA, vehicleB })
            .ReturnsAsync(new[] { vehicleB });
        _vehicleService.Setup(s => s.TakeOverAsync(vehicleA.VehicleId)).ReturnsAsync(TakeOverResult.Conflict);
        _vehicleService.Setup(s => s.TakeOverAsync(vehicleB.VehicleId)).ReturnsAsync(TakeOverResult.Success);
        var vm = CreateViewModel();
        await vm.LoadAsync();
        vm.Select(vehicleA.VehicleId);

        // Act
        await vm.TakeOverAsync();

        // Assert
        _navigator.Verify(n => n.NavigateToRepIdleView(), Times.Once);
    }

    [Fact]
    public async Task GivenAllVehiclesConflict_WhenTakeOverCalled_ThenExhaustedMessageIsSet()
    {
        // Arrange
        // Two distinct candidates both conflict across successive refreshes; the bounded loop
        // attempts each exactly once, exhausts the list, and surfaces the terminal message (AC-1).
        var vehicleA = Vehicle("IA-4471");
        var vehicleB = Vehicle("IA-2208");
        _vehicleService.SetupSequence(s => s.GetIdleVehiclesAsync())
            .ReturnsAsync(new[] { vehicleA, vehicleB })
            .ReturnsAsync(new[] { vehicleB })
            .ReturnsAsync(Array.Empty<IdleVehicle>());
        _vehicleService.Setup(s => s.TakeOverAsync(vehicleA.VehicleId)).ReturnsAsync(TakeOverResult.Conflict);
        _vehicleService.Setup(s => s.TakeOverAsync(vehicleB.VehicleId)).ReturnsAsync(TakeOverResult.Conflict);
        var vm = CreateViewModel();
        await vm.LoadAsync();
        vm.Select(vehicleA.VehicleId);

        // Act
        await vm.TakeOverAsync();

        // Assert
        Assert.Equal(TakeOverViewModel.ExhaustedMessage, vm.ErrorMessage);
        _vehicleService.Verify(s => s.TakeOverAsync(vehicleA.VehicleId), Times.Once);
        _vehicleService.Verify(s => s.TakeOverAsync(vehicleB.VehicleId), Times.Once);
    }

    [Fact]
    public async Task GivenAllVehiclesConflict_WhenTakeOverCalled_ThenNoNavigationOccurs()
    {
        // Arrange
        var vehicleA = Vehicle("IA-4471");
        var vehicleB = Vehicle("IA-2208");
        _vehicleService.SetupSequence(s => s.GetIdleVehiclesAsync())
            .ReturnsAsync(new[] { vehicleA, vehicleB })
            .ReturnsAsync(new[] { vehicleB })
            .ReturnsAsync(Array.Empty<IdleVehicle>());
        _vehicleService.Setup(s => s.TakeOverAsync(vehicleA.VehicleId)).ReturnsAsync(TakeOverResult.Conflict);
        _vehicleService.Setup(s => s.TakeOverAsync(vehicleB.VehicleId)).ReturnsAsync(TakeOverResult.Conflict);
        var vm = CreateViewModel();
        await vm.LoadAsync();
        vm.Select(vehicleA.VehicleId);

        // Act
        await vm.TakeOverAsync();

        // Assert
        _navigator.Verify(n => n.NavigateToRepIdleView(), Times.Never);
        Assert.Null(vm.SelectedVehicleId);
    }

    [Fact]
    public async Task GivenAllVehiclesConflict_WhenTakeOverCalled_ThenClaimedVehicleStoreIsNotPopulated()
    {
        // Arrange
        var vehicleA = Vehicle("IA-4471");
        var vehicleB = Vehicle("IA-2208");
        _vehicleService.SetupSequence(s => s.GetIdleVehiclesAsync())
            .ReturnsAsync(new[] { vehicleA, vehicleB })
            .ReturnsAsync(new[] { vehicleB })
            .ReturnsAsync(Array.Empty<IdleVehicle>());
        _vehicleService.Setup(s => s.TakeOverAsync(vehicleA.VehicleId)).ReturnsAsync(TakeOverResult.Conflict);
        _vehicleService.Setup(s => s.TakeOverAsync(vehicleB.VehicleId)).ReturnsAsync(TakeOverResult.Conflict);
        var vm = CreateViewModel();
        await vm.LoadAsync();
        vm.Select(vehicleA.VehicleId);

        // Act
        await vm.TakeOverAsync();

        // Assert
        _claimedVehicleStore.Verify(s => s.SetVehicle(It.IsAny<ClaimedVehicle>()), Times.Never);
    }

    [Fact]
    public async Task GivenRefreshKeepsReturningTheSameConflictingVehicles_WhenTakeOverCalled_ThenLoopTerminatesWithExhaustedMessage()
    {
        // Arrange
        // The refreshed list keeps returning the SAME two already-attempted, conflicting vehicles.
        // The loop must not re-attempt a vehicle it already tried — it must recognise no fresh
        // candidate remains and terminate (the bound is per-invocation attempted-set, not a raw
        // counter; this guards against an infinite spin, BUG-045 "bounded" requirement).
        var vehicleA = Vehicle("IA-4471");
        var vehicleB = Vehicle("IA-2208");
        _vehicleService.Setup(s => s.GetIdleVehiclesAsync()).ReturnsAsync(new[] { vehicleA, vehicleB });
        _vehicleService.Setup(s => s.TakeOverAsync(vehicleA.VehicleId)).ReturnsAsync(TakeOverResult.Conflict);
        _vehicleService.Setup(s => s.TakeOverAsync(vehicleB.VehicleId)).ReturnsAsync(TakeOverResult.Conflict);
        var vm = CreateViewModel();
        await vm.LoadAsync();
        vm.Select(vehicleA.VehicleId);

        // Act
        await vm.TakeOverAsync();

        // Assert
        Assert.Equal(TakeOverViewModel.ExhaustedMessage, vm.ErrorMessage);
        _vehicleService.Verify(s => s.TakeOverAsync(vehicleA.VehicleId), Times.Once);
        _vehicleService.Verify(s => s.TakeOverAsync(vehicleB.VehicleId), Times.Once);
    }

    [Fact]
    public async Task GivenFirstVehicleSucceeds_WhenTakeOverCalled_ThenGetIdleVehiclesCalledExactlyOnce()
    {
        // Arrange
        // A first-attempt success must not enter the retry loop: no post-conflict refresh, so
        // GetIdleVehiclesAsync is called only for the initial load (AC-3 no-regression).
        var vehicle = Vehicle("IA-4471");
        _vehicleService.Setup(s => s.GetIdleVehiclesAsync()).ReturnsAsync(new[] { vehicle });
        _vehicleService.Setup(s => s.TakeOverAsync(vehicle.VehicleId)).ReturnsAsync(TakeOverResult.Success);
        var vm = CreateViewModel();
        await vm.LoadAsync();
        vm.Select(vehicle.VehicleId);

        // Act
        await vm.TakeOverAsync();

        // Assert
        _vehicleService.Verify(s => s.GetIdleVehiclesAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenFirstVehicleSucceeds_WhenTakeOverCalled_ThenNoErrorMessageAndNavigationOccurs()
    {
        // Arrange
        var vehicle = Vehicle("IA-4471");
        _vehicleService.Setup(s => s.GetIdleVehiclesAsync()).ReturnsAsync(new[] { vehicle });
        _vehicleService.Setup(s => s.TakeOverAsync(vehicle.VehicleId)).ReturnsAsync(TakeOverResult.Success);
        var vm = CreateViewModel();
        await vm.LoadAsync();
        vm.Select(vehicle.VehicleId);

        // Act
        await vm.TakeOverAsync();

        // Assert
        Assert.Null(vm.ErrorMessage);
        _navigator.Verify(n => n.NavigateToRepIdleView(), Times.Once);
    }

    [Fact]
    public async Task GivenAPendingLoad_WhenLoadAsyncIsRunning_ThenIsBusyIsTrueDuringTheCall()
    {
        // Arrange
        var isBusyDuringCall = false;
        var vm = CreateViewModel();
        _vehicleService.Setup(s => s.GetIdleVehiclesAsync())
            .Returns(() =>
            {
                isBusyDuringCall = vm.IsBusy;
                return Task.FromResult<IReadOnlyList<IdleVehicle>>(new[] { Vehicle() });
            });

        // Act
        await vm.LoadAsync();

        // Assert
        Assert.True(isBusyDuringCall);
        Assert.False(vm.IsBusy);
    }
}
