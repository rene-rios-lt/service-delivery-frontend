using Microsoft.Extensions.Logging.Abstractions;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.Services;
using ServiceDelivery.Client.Core.ViewModels;
using ServiceDelivery.Client.UI.Features.ServiceRep.Services;

namespace ServiceDelivery.Client.Tests.ServiceRep;

/// <summary>
/// Composition-root-level regression for BUG-041. Both <see cref="RepIdleViewModel"/> and
/// <see cref="ReleaseVehicleAction"/> share a single <see cref="InMemoryClaimedVehicleStore"/> in a
/// session scope. This suite uses the REAL store and REAL ViewModel/action — only the dialog and HTTP
/// boundaries (<see cref="IReleaseConfirmation"/>, <see cref="IReleaseVehicleService"/>) are mocked —
/// so it proves the actual hand-off sequence the host wires up. It reproduces the original failure:
/// the idle view's construction must NOT wipe the store before the release action reads it.
/// </summary>
public class ClaimedVehicleStoreIntegrationTests
{
    private readonly Mock<IRepHubService> _repHub = new();
    private readonly Mock<IReleaseConfirmation> _confirmation = new();
    private readonly Mock<IReleaseVehicleService> _releaseService = new();
    private readonly Mock<IPersonaNavigator> _navigator = new();

    private static readonly Guid VehicleId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static ClaimedVehicle ClaimedVehicle() =>
        new(VehicleId, "IA-4471", "Transit 350", new[] { "Hydraulics", "Coolant" });

    [Fact]
    public async Task GivenRealStoreHasVehicle_WhenRepIdleViewModelConstructedThenReleaseAsyncCalled_ThenConfirmationIsInvoked()
    {
        // Arrange
        // The real store is the shared source of truth between the idle view and the release action.
        // TakeOverViewModel deposits the claimed vehicle here before navigating to /rep/idle.
        var store = new InMemoryClaimedVehicleStore();
        store.SetVehicle(ClaimedVehicle());

        // Constructing the real idle view performs the hand-off read. Before the BUG-041 fix it also
        // cleared the store, which is exactly the failure this test reproduces.
        _ = new RepIdleViewModel(
            store, _repHub.Object, _navigator.Object, NullLogger<RepIdleViewModel>.Instance);

        _confirmation.Setup(c => c.ConfirmAsync(It.IsAny<string>())).ReturnsAsync(true);
        _releaseService.Setup(s => s.ReleaseAsync(VehicleId)).ReturnsAsync(true);
        var action = new ReleaseVehicleAction(
            store, _confirmation.Object, _releaseService.Object, _navigator.Object);

        // Act
        await action.ReleaseAsync();

        // Assert
        // The dialog must be shown — proving the action still found the claimed vehicle after the idle
        // view was constructed. If the store had been wiped, ReleaseAsync would short-circuit to
        // NothingToRelease and never call ConfirmAsync.
        _confirmation.Verify(c => c.ConfirmAsync("IA-4471"), Times.Once);
    }

    [Fact]
    public async Task GivenRealStoreHasVehicle_WhenRepIdleViewModelConstructedThenReleaseConfirmed_ThenResultIsReleased()
    {
        // Arrange
        var store = new InMemoryClaimedVehicleStore();
        store.SetVehicle(ClaimedVehicle());

        _ = new RepIdleViewModel(
            store, _repHub.Object, _navigator.Object, NullLogger<RepIdleViewModel>.Instance);

        _confirmation.Setup(c => c.ConfirmAsync(It.IsAny<string>())).ReturnsAsync(true);
        _releaseService.Setup(s => s.ReleaseAsync(VehicleId)).ReturnsAsync(true);
        var action = new ReleaseVehicleAction(
            store, _confirmation.Object, _releaseService.Object, _navigator.Object);

        // Act
        var result = await action.ReleaseAsync();

        // Assert
        // The full happy path — confirm, release, clear — completes through the real shared store,
        // and the store is durably cleared only by the release action (AC-2).
        Assert.Equal(ReleaseVehicleResult.Released, result);
        Assert.Null(store.CurrentVehicle);
    }

    [Fact]
    public void GivenRealStoreHasVehicle_WhenRepIdleViewModelConstructed_ThenStoreStillHoldsTheClaimedVehicle()
    {
        // Arrange
        // After a take-over the idle card must still show the correct vehicle AND the store must still
        // expose it so the release action can read it (both consumers see the claimed vehicle, AC-2).
        var store = new InMemoryClaimedVehicleStore();
        store.SetVehicle(ClaimedVehicle());

        // Act
        var vm = new RepIdleViewModel(
            store, _repHub.Object, _navigator.Object, NullLogger<RepIdleViewModel>.Instance);

        // Assert
        Assert.Equal("IA-4471", vm.Vehicle.Registration);
        Assert.NotNull(store.CurrentVehicle);
        Assert.Equal(VehicleId, store.CurrentVehicle!.VehicleId);
    }
}
