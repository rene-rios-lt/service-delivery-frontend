using System.Threading.Tasks;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.UI.Features.ServiceRep.Services;

namespace ServiceDelivery.Client.Tests.ServiceRep;

public class ServiceRepLogoutSideEffectTests
{
    private readonly Mock<IHeartbeatService> _heartbeatService = new();
    private readonly Mock<IClaimedVehicleStore> _claimedVehicleStore = new();

    private ServiceRepLogoutSideEffect CreateSideEffect() =>
        new(_heartbeatService.Object, _claimedVehicleStore.Object);

    [Fact]
    public async Task GivenAServiceRepLogoutSideEffect_WhenRunBeforeTokenClearedAsync_ThenClearVehicleIsCalledOnStore()
    {
        // Arrange — BUG-043 AC-1: logout must clear the claimed-vehicle store so no stale claim leaks
        // into the next session.
        var sideEffect = CreateSideEffect();

        // Act
        await sideEffect.RunBeforeTokenClearedAsync();

        // Assert
        _claimedVehicleStore.Verify(s => s.ClearVehicle(), Times.Once);
    }

    [Fact]
    public async Task GivenARunningHeartbeat_WhenLogoutSideEffectRuns_ThenHeartbeatStopsAndStoreIsCleared()
    {
        // Arrange — AC-3 (logout path): explicit logout stops the heartbeat loop immediately AND clears
        // the store, so the rep stops being marked human-controlled without waiting for the backend
        // timeout.
        var sideEffect = CreateSideEffect();

        // Act
        await sideEffect.RunBeforeTokenClearedAsync();

        // Assert
        _heartbeatService.Verify(h => h.StopAsync(), Times.Once);
        _claimedVehicleStore.Verify(s => s.ClearVehicle(), Times.Once);
    }

    [Fact]
    public async Task GivenAServiceRepLogoutSideEffect_WhenRunBeforeTokenClearedAsync_ThenHeartbeatIsStoppedBeforeStoreIsCleared()
    {
        // Arrange — ordering matters: stop the loop BEFORE clearing the store. If the store were cleared
        // first, an in-flight final tick could read a null vehicle mid-cycle. Stopping first lets the
        // loop's continuation check exit cleanly.
        var sideEffect = CreateSideEffect();
        var sequence = new List<string>();
        _heartbeatService.Setup(h => h.StopAsync())
            .Callback(() => sequence.Add("stop"))
            .Returns(Task.CompletedTask);
        _claimedVehicleStore.Setup(s => s.ClearVehicle())
            .Callback(() => sequence.Add("clear"));

        // Act
        await sideEffect.RunBeforeTokenClearedAsync();

        // Assert
        Assert.Equal(new[] { "stop", "clear" }, sequence);
    }
}
