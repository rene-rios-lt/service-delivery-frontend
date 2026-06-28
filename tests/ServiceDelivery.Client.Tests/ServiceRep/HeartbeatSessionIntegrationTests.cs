using System.Threading.Tasks;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.Services;
using ServiceDelivery.Client.Core.ViewModels;
using ServiceDelivery.Client.UI.Features.ServiceRep.Services;

namespace ServiceDelivery.Client.Tests.ServiceRep;

/// <summary>
/// Composition-root-level regression for BUG-043 (and the FE-023 logout path). Wires the REAL
/// <see cref="ServiceRepLogoutSideEffect"/> and a REAL <see cref="InMemoryClaimedVehicleStore"/> into a
/// REAL <see cref="ShellViewModel"/> — only the HTTP/heartbeat/token/navigation boundaries are mocked —
/// so it proves the actual logout hand-off the Mobile host wires up: <c>LogoutAsync</c> stops the
/// heartbeat and clears the claimed-vehicle store. Testing against the No-Op side-effect would prove
/// nothing; this suite exercises the production side-effect end-to-end.
/// </summary>
public class HeartbeatSessionIntegrationTests
{
    private readonly Mock<IHeartbeatService> _heartbeatService = new();
    private readonly Mock<ITokenStore> _tokenStore = new();
    private readonly Mock<IPersonaNavigator> _navigator = new();
    private readonly Mock<IReleaseVehicleAction> _releaseAction = new();
    private readonly Mock<IShellPresentation> _presentation = new();

    private static readonly Guid VehicleId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private static ClaimedVehicle Vehicle() =>
        new(VehicleId, "IA-4471", "Transit 350", new[] { "Hydraulics", "Coolant" });

    // Builds a ShellViewModel over the REAL logout side-effect and the REAL shared store.
    private ShellViewModel CreateShell(InMemoryClaimedVehicleStore store)
    {
        var sideEffect = new ServiceRepLogoutSideEffect(_heartbeatService.Object, store);
        return new ShellViewModel(
            _tokenStore.Object, _navigator.Object, sideEffect,
            _releaseAction.Object, _presentation.Object, new PersonaMenuFactory());
    }

    [Fact]
    public async Task GivenASessionWithClaimedVehicle_WhenLogoutAsyncCalled_ThenCurrentVehicleIsNull()
    {
        // Arrange — BUG-043 AC-1 end-to-end: a rep is on duty (store holds a claimed vehicle); the shell
        // is wired with the REAL ServiceRepLogoutSideEffect over the REAL store.
        var store = new InMemoryClaimedVehicleStore();
        store.SetVehicle(Vehicle());
        var shell = CreateShell(store);

        // Act
        await shell.LogoutAsync();

        // Assert — the claimed vehicle is gone, so no stale claim leaks into the next session.
        Assert.Null(store.CurrentVehicle);
    }

    [Fact]
    public async Task GivenASessionWithClaimedVehicle_WhenLogoutAsyncCalled_ThenHeartbeatIsStopped()
    {
        // Arrange — FE-023 AC-3 (logout path) end-to-end: logout stops the heartbeat loop immediately
        // through the real side-effect, not the No-Op.
        var store = new InMemoryClaimedVehicleStore();
        store.SetVehicle(Vehicle());
        var shell = CreateShell(store);

        // Act
        await shell.LogoutAsync();

        // Assert
        _heartbeatService.Verify(h => h.StopAsync(), Times.Once);
    }

    [Fact]
    public void GivenASessionWithClaimedVehicle_WhenLogoutHasNotYetBeenCalled_ThenStoreStillExposesTheVehicle()
    {
        // Arrange — BUG-043 AC-2 (no regression): during an active session the store must still expose
        // the claimed vehicle so the release action can read it. Building the shell must not clear it.
        var store = new InMemoryClaimedVehicleStore();
        store.SetVehicle(Vehicle());

        // Act
        _ = CreateShell(store);

        // Assert
        Assert.NotNull(store.CurrentVehicle);
        Assert.Equal(VehicleId, store.CurrentVehicle!.VehicleId);
    }

    [Fact]
    public async Task GivenLogoutClearedTheStore_WhenVehicleSetViaSetVehicle_ThenCurrentVehicleIsNotNull()
    {
        // Arrange — BUG-043 AC-2 (re-login → take-over): after logout clears the store, the next
        // take-over's SetVehicle must make the store readable again (no permanently-poisoned store).
        var store = new InMemoryClaimedVehicleStore();
        store.SetVehicle(Vehicle());
        var shell = CreateShell(store);
        await shell.LogoutAsync();

        // Act — the rep logs back in and takes over a different vehicle.
        var newVehicle = new ClaimedVehicle(
            Guid.Parse("66666666-6666-6666-6666-666666666666"), "V-002", "Sprinter 250",
            new[] { "Coolant" });
        store.SetVehicle(newVehicle);

        // Assert
        Assert.NotNull(store.CurrentVehicle);
        Assert.Equal("V-002", store.CurrentVehicle!.Registration);
    }
}
