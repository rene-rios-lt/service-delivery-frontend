using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.Services;

namespace ServiceDelivery.Client.Tests.Shell;

public class NullObjectSeamTests
{
    [Fact]
    public async Task GivenNoOpLogoutSideEffect_WhenRunBeforeTokenCleared_ThenCompletesWithoutThrowing()
    {
        // Arrange
        var sideEffect = new NoOpLogoutSideEffect();

        // Act
        var act = sideEffect.RunBeforeTokenClearedAsync();
        await act;

        // Assert
        Assert.True(act.IsCompletedSuccessfully);
    }

    [Fact]
    public void GivenNoOpReleaseVehicleAction_WhenCanReleaseRead_ThenIsFalse()
    {
        // Arrange
        var action = new NoOpReleaseVehicleAction();

        // Act
        var canRelease = action.CanRelease;

        // Assert
        Assert.False(canRelease);
    }

    [Fact]
    public async Task GivenNoOpReleaseVehicleAction_WhenReleaseAsyncCalled_ThenReturnsNothingToRelease()
    {
        // Arrange
        var action = new NoOpReleaseVehicleAction();

        // Act
        var result = await action.ReleaseAsync();

        // Assert
        Assert.Equal(ReleaseVehicleResult.NothingToRelease, result);
    }
}
