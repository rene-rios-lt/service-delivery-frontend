using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.ViewModels;

namespace ServiceDelivery.Client.Tests.Dispatcher;

/// <summary>
/// FE-022 — pure xUnit tests for the force-release orchestration added to <see cref="DispatcherFleetViewModel"/>
/// (ACs 1, 3, 5). Covers building <see cref="ForceReleaseInfo"/> from the selected claimed vehicle (AC-1),
/// confirming the release calls the service with the vehicle's id and clears the dialog on success (AC-3), and
/// surfacing the error while keeping the dialog open on a failed release (AC-5) — all with a mocked
/// <see cref="IForceReleaseService"/> (no rendering, no live backend). AC-4's Offline-drop mechanism is the
/// existing VehiclePositionHub path, proven by
/// <c>DispatcherFleetViewModelTests.GivenVehicleWithOfflineStateUpdate_WhenPositionUpdateHandled_ThenVehicleRemovedFromVisibleVehicles</c>
/// (scope constraint 3) — not re-tested here to avoid a masking duplicate.
/// </summary>
public class DispatcherFleetViewModelForceReleaseTests
{
    private static readonly Guid Vehicle7 = Guid.Parse("30000000-0000-0000-0000-000000000007");

    private readonly Mock<IDispatcherFleetService> _fleetService = new();
    private readonly Mock<IVehiclePositionHubService> _hub = new();
    private readonly Mock<IForceReleaseService> _forceReleaseService = new();

    private DispatcherFleetViewModel CreateViewModel() =>
        new(_fleetService.Object, _hub.Object, _forceReleaseService.Object);

    private void FleetReturns(params FleetVehicleEntry[] entries) =>
        _fleetService.Setup(s => s.GetFleetAsync()).ReturnsAsync(entries.ToList());

    // A CLAIMED vehicle (RepId not null) — the only kind the force-release action is offered for. Distinct
    // per-field values (rep name vs registration vs request title) so an assertion cannot pass by coincidence.
    private static FleetVehicleEntry ClaimedEntry(
        Guid vehicleId,
        string repName = "R. Alvarez",
        string registration = "IOW-4471",
        string? activeRequestTitle = "Hydraulic Pressure Loss",
        string state = "EnRoute") =>
        new(vehicleId.ToString(), registration, state, Guid.NewGuid(), repName,
            41.60, -93.60, activeRequestTitle, "Silver", false);

    private static FleetVehicleEntry UnclaimedEntry(Guid vehicleId) =>
        new(vehicleId.ToString(), "IOW-9000", "Available", RepId: null, RepName: null,
            41.60, -93.60, null, null, false);

    [Fact]
    public async Task GivenASelectedVehicleWithActiveRequest_WhenOpenForceReleaseAsyncCalled_ThenActiveForceReleaseInfoIsSet()
    {
        // Arrange
        FleetReturns(ClaimedEntry(Vehicle7, repName: "R. Alvarez", registration: "IOW-4471",
            activeRequestTitle: "Hydraulic Pressure Loss"));
        var vm = CreateViewModel();
        await vm.LoadAsync();

        // Act
        await vm.OpenForceReleaseAsync(Vehicle7.ToString());

        // Assert — the dialog VM carries the rep name, registration, request title, and the vehicle id (for the POST).
        var info = vm.ActiveForceReleaseInfo;
        Assert.NotNull(info);
        Assert.Equal(Vehicle7, info!.VehicleId);
        Assert.Equal("R. Alvarez", info.RepName);
        Assert.Equal("IOW-4471", info.Registration);
        Assert.Equal("Hydraulic Pressure Loss", info.RequestTitle);
    }

    [Fact]
    public async Task GivenAnUnclaimedVehicle_WhenOpenForceReleaseAsyncCalled_ThenNoDialogIsOpened()
    {
        // Arrange — an unclaimed vehicle (RepId null) has no rep session to revoke, so no dialog should open.
        FleetReturns(UnclaimedEntry(Vehicle7));
        var vm = CreateViewModel();
        await vm.LoadAsync();

        // Act
        await vm.OpenForceReleaseAsync(Vehicle7.ToString());

        // Assert
        Assert.Null(vm.ActiveForceReleaseInfo);
    }

    [Fact]
    public async Task GivenAnOpenForceReleaseDialog_WhenCancelled_ThenActiveForceReleaseInfoIsNull()
    {
        // Arrange
        FleetReturns(ClaimedEntry(Vehicle7));
        var vm = CreateViewModel();
        await vm.LoadAsync();
        await vm.OpenForceReleaseAsync(Vehicle7.ToString());
        Assert.NotNull(vm.ActiveForceReleaseInfo);

        // Act
        vm.CancelForceRelease();

        // Assert
        Assert.Null(vm.ActiveForceReleaseInfo);
    }

    [Fact]
    public async Task GivenAConfirmedForceRelease_WhenViewModelConfirms_ThenServiceIsCalledWithCorrectVehicleId()
    {
        // Arrange
        FleetReturns(ClaimedEntry(Vehicle7));
        var vm = CreateViewModel();
        await vm.LoadAsync();
        await vm.OpenForceReleaseAsync(Vehicle7.ToString());

        // Act
        await vm.ConfirmForceReleaseAsync();

        // Assert — the exact vehicle id from the selected entry is posted (not Guid.Empty / a different id).
        _forceReleaseService.Verify(s => s.ForceReleaseAsync(Vehicle7), Times.Once);
    }

    [Fact]
    public async Task GivenASuccessfulForceRelease_WhenViewModelConfirms_ThenActiveForceReleaseInfoIsNull()
    {
        // Arrange
        FleetReturns(ClaimedEntry(Vehicle7));
        _forceReleaseService.Setup(s => s.ForceReleaseAsync(It.IsAny<Guid>())).Returns(Task.CompletedTask);
        var vm = CreateViewModel();
        await vm.LoadAsync();
        await vm.OpenForceReleaseAsync(Vehicle7.ToString());

        // Act
        await vm.ConfirmForceReleaseAsync();

        // Assert — the dialog is dismissed on success and no error is left behind.
        Assert.Null(vm.ActiveForceReleaseInfo);
        Assert.Null(vm.ForceReleaseError);
        Assert.False(vm.IsForceReleasing);
    }

    [Fact]
    public async Task GivenAForceReleaseThatErrors_WhenViewModelConfirms_ThenForceReleaseErrorIsSetAndDialogRemainsOpen()
    {
        // Arrange — the backend rejects the release (e.g. the rep reconnected and self-released); the adapter
        // throws, and the ViewModel must surface the message and KEEP the dialog open so the dispatcher sees why.
        FleetReturns(ClaimedEntry(Vehicle7));
        _forceReleaseService
            .Setup(s => s.ForceReleaseAsync(It.IsAny<Guid>()))
            .ThrowsAsync(new HttpRequestException("Vehicle is no longer claimed."));
        var vm = CreateViewModel();
        await vm.LoadAsync();
        await vm.OpenForceReleaseAsync(Vehicle7.ToString());

        // Act
        await vm.ConfirmForceReleaseAsync();

        // Assert — the error carries the real message, the dialog stays open, and the in-flight flag is cleared
        // (so the dispatcher can retry or cancel).
        Assert.Equal("Vehicle is no longer claimed.", vm.ForceReleaseError);
        Assert.NotNull(vm.ActiveForceReleaseInfo);
        Assert.False(vm.IsForceReleasing);
    }
}
