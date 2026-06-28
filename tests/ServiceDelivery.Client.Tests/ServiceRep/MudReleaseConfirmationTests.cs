using System.Threading.Tasks;
using MudBlazor;
using ServiceDelivery.Client.UI.Features.ServiceRep.Components;
using ServiceDelivery.Client.UI.Features.ServiceRep.Services;

namespace ServiceDelivery.Client.Tests.ServiceRep;

public class MudReleaseConfirmationTests
{
    private readonly Mock<IDialogService> _dialogService = new();
    private readonly Mock<IDialogReference> _dialogReference = new();

    private MudReleaseConfirmation CreateConfirmation() => new(_dialogService.Object);

    private void SetupDialog(DialogResult result)
    {
        _dialogReference.SetupGet(r => r.Result).Returns(Task.FromResult<DialogResult?>(result));
        _dialogService
            .Setup(d => d.ShowAsync<ReleaseConfirmationDialog>(It.IsAny<string>(), It.IsAny<DialogParameters>()))
            .ReturnsAsync(_dialogReference.Object);
    }

    [Fact]
    public async Task GivenTheRepConfirms_WhenConfirmAsync_ThenItReturnsTrue()
    {
        // Arrange
        SetupDialog(DialogResult.Ok(true));
        var confirmation = CreateConfirmation();

        // Act
        var result = await confirmation.ConfirmAsync("IA-4471");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GivenTheRepCancels_WhenConfirmAsync_ThenItReturnsFalse()
    {
        // Arrange
        SetupDialog(DialogResult.Cancel());
        var confirmation = CreateConfirmation();

        // Act
        var result = await confirmation.ConfirmAsync("IA-4471");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GivenARegistration_WhenConfirmAsync_ThenItIsPassedToTheDialog()
    {
        // Arrange
        SetupDialog(DialogResult.Ok(true));
        var confirmation = CreateConfirmation();

        // Act
        await confirmation.ConfirmAsync("IA-4471");

        // Assert
        _dialogService.Verify(d => d.ShowAsync<ReleaseConfirmationDialog>(
            It.IsAny<string>(),
            It.Is<DialogParameters>(p => Equals(p[nameof(ReleaseConfirmationDialog.Registration)], "IA-4471"))),
            Times.Once);
    }
}
