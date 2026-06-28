using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using ServiceDelivery.Client.UI.Features.ServiceRep.Components;

namespace ServiceDelivery.Client.Tests.ServiceRep;

public class ReleaseConfirmationDialogTests
{
    private static IRenderedComponent<MudDialogProvider> CreateProvider(BunitContext ctx)
    {
        ctx.Services.AddMudServices();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        return ctx.Render<MudDialogProvider>();
    }

    private static async Task<IDialogReference> OpenDialogAsync(
        BunitContext ctx, IRenderedComponent<MudDialogProvider> provider, string registration)
    {
        var dialogService = ctx.Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<ReleaseConfirmationDialog>
        {
            { d => d.Registration, registration }
        };

        IDialogReference reference = null!;
        await provider.InvokeAsync(async () =>
            reference = await dialogService.ShowAsync<ReleaseConfirmationDialog>("Release", parameters));
        return reference;
    }

    [Fact]
    public async Task GivenAClaimedVehicle_WhenDialogShown_ThenTitleShowsTheRegistration()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var provider = CreateProvider(ctx);
        await OpenDialogAsync(ctx, provider, "IA-4471");

        // Act
        var title = provider.Find("[data-testid='release-dialog-title']");

        // Assert
        Assert.Contains("Release vehicle IA-4471?", title.TextContent);
    }

    [Fact]
    public async Task GivenADialog_WhenShown_ThenCancelAndReleaseButtonsAreRendered()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var provider = CreateProvider(ctx);
        await OpenDialogAsync(ctx, provider, "IA-4471");

        // Act
        var cancel = provider.Find("[data-testid='release-dialog-cancel']");
        var release = provider.Find("[data-testid='release-dialog-confirm']");

        // Assert
        Assert.Contains("Cancel", cancel.TextContent);
        Assert.Contains("Release", release.TextContent);
    }

    [Fact]
    public async Task GivenADialog_WhenReleaseClicked_ThenDialogClosesWithConfirmedResult()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var provider = CreateProvider(ctx);
        var reference = await OpenDialogAsync(ctx, provider, "IA-4471");

        // Act
        provider.Find("[data-testid='release-dialog-confirm']").Click();
        var result = await reference.Result;

        // Assert
        Assert.False(result!.Canceled);
        Assert.True((bool)result.Data!);
    }

    [Fact]
    public async Task GivenADialog_WhenCancelClicked_ThenDialogClosesCancelled()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var provider = CreateProvider(ctx);
        var reference = await OpenDialogAsync(ctx, provider, "IA-4471");

        // Act
        provider.Find("[data-testid='release-dialog-cancel']").Click();
        var result = await reference.Result;

        // Assert
        Assert.True(result!.Canceled);
    }
}
