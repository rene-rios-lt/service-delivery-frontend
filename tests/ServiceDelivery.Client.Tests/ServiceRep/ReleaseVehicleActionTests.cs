using System.Threading.Tasks;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.Services;
using ServiceDelivery.Client.UI.Features.ServiceRep.Services;

namespace ServiceDelivery.Client.Tests.ServiceRep;

public class ReleaseVehicleActionTests
{
    private readonly Mock<IReleaseVehicleService> _releaseService = new();
    private readonly Mock<IReleaseConfirmation> _confirmation = new();
    private readonly Mock<IPersonaNavigator> _navigator = new();
    private readonly InMemoryClaimedVehicleStore _store = new();

    private static readonly Guid VehicleId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static ClaimedVehicle ClaimedVehicle() =>
        new(VehicleId, "IA-4471", "Transit 350", new[] { "Hydraulics" });

    private ReleaseVehicleAction CreateAction()
    {
        return new ReleaseVehicleAction(
            _store,
            _confirmation.Object,
            _releaseService.Object,
            _navigator.Object);
    }

    [Fact]
    public void GivenAVehicleIsClaimed_WhenCanReleaseRead_ThenItIsTrue()
    {
        // Arrange
        _store.SetVehicle(ClaimedVehicle());
        var action = CreateAction();

        // Act
        var canRelease = action.CanRelease;

        // Assert
        Assert.True(canRelease);
    }

    [Fact]
    public void GivenNoVehicleIsClaimed_WhenCanReleaseRead_ThenItIsFalse()
    {
        // Arrange
        var action = CreateAction();

        // Act
        var canRelease = action.CanRelease;

        // Assert
        Assert.False(canRelease);
    }

    [Fact]
    public async Task GivenReleaseItemTapped_WhenDialogConfirmed_ThenReleaseServiceIsCalledWithClaimedVehicleId()
    {
        // Arrange
        _store.SetVehicle(ClaimedVehicle());
        _confirmation.Setup(c => c.ConfirmAsync("IA-4471")).ReturnsAsync(true);
        _releaseService.Setup(s => s.ReleaseAsync(VehicleId)).ReturnsAsync(true);
        var action = CreateAction();

        // Act
        await action.ReleaseAsync();

        // Assert
        _releaseService.Verify(s => s.ReleaseAsync(VehicleId), Times.Once);
    }

    [Fact]
    public async Task GivenReleaseItemTapped_WhenDialogNotYetConfirmed_ThenReleaseServiceIsNotCalled()
    {
        // Arrange
        _store.SetVehicle(ClaimedVehicle());
        _confirmation.Setup(c => c.ConfirmAsync(It.IsAny<string>())).ReturnsAsync(false);
        var action = CreateAction();

        // Act
        await action.ReleaseAsync();

        // Assert
        _releaseService.Verify(s => s.ReleaseAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task GivenReleaseServiceReturnsSuccess_WhenReleaseCompletes_ThenNavigatesToTakeOver()
    {
        // Arrange
        _store.SetVehicle(ClaimedVehicle());
        _confirmation.Setup(c => c.ConfirmAsync(It.IsAny<string>())).ReturnsAsync(true);
        _releaseService.Setup(s => s.ReleaseAsync(VehicleId)).ReturnsAsync(true);
        var action = CreateAction();

        // Act
        await action.ReleaseAsync();

        // Assert
        _navigator.Verify(n => n.NavigateToTakeOver(), Times.Once);
    }

    [Fact]
    public async Task GivenReleaseServiceReturnsSuccess_WhenReleaseCompletes_ThenResultIsReleased()
    {
        // Arrange
        _store.SetVehicle(ClaimedVehicle());
        _confirmation.Setup(c => c.ConfirmAsync(It.IsAny<string>())).ReturnsAsync(true);
        _releaseService.Setup(s => s.ReleaseAsync(VehicleId)).ReturnsAsync(true);
        var action = CreateAction();

        // Act
        var result = await action.ReleaseAsync();

        // Assert
        Assert.Equal(ReleaseVehicleResult.Released, result);
    }

    [Fact]
    public async Task GivenReleaseServiceReturnsSuccess_WhenReleaseCompletes_ThenClaimedVehicleIsCleared()
    {
        // Arrange
        _store.SetVehicle(ClaimedVehicle());
        _confirmation.Setup(c => c.ConfirmAsync(It.IsAny<string>())).ReturnsAsync(true);
        _releaseService.Setup(s => s.ReleaseAsync(VehicleId)).ReturnsAsync(true);
        var action = CreateAction();

        // Act
        await action.ReleaseAsync();

        // Assert
        Assert.Null(_store.CurrentVehicle);
    }

    [Fact]
    public async Task GivenReleaseDialogCancelled_WhenDialogDismissed_ThenNavigateToTakeOverIsNotCalled()
    {
        // Arrange
        _store.SetVehicle(ClaimedVehicle());
        _confirmation.Setup(c => c.ConfirmAsync(It.IsAny<string>())).ReturnsAsync(false);
        var action = CreateAction();

        // Act
        await action.ReleaseAsync();

        // Assert
        _navigator.Verify(n => n.NavigateToTakeOver(), Times.Never);
    }

    [Fact]
    public async Task GivenNoVehicleIsClaimed_WhenReleaseAsync_ThenResultIsNothingToRelease()
    {
        // Arrange
        var action = CreateAction();

        // Act
        var result = await action.ReleaseAsync();

        // Assert
        Assert.Equal(ReleaseVehicleResult.NothingToRelease, result);
        _confirmation.Verify(c => c.ConfirmAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GivenReleaseServiceReturnsFailure_WhenReleaseCompletes_ThenResultIsBlockedAndNoNavigation()
    {
        // Arrange
        _store.SetVehicle(ClaimedVehicle());
        _confirmation.Setup(c => c.ConfirmAsync(It.IsAny<string>())).ReturnsAsync(true);
        _releaseService.Setup(s => s.ReleaseAsync(VehicleId)).ReturnsAsync(false);
        var action = CreateAction();

        // Act
        var result = await action.ReleaseAsync();

        // Assert
        Assert.Equal(ReleaseVehicleResult.Blocked, result);
        _navigator.Verify(n => n.NavigateToTakeOver(), Times.Never);
        Assert.NotNull(_store.CurrentVehicle);
    }
}
