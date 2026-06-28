using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.ViewModels;
using ServiceDelivery.Client.UI.Shared.Components;

namespace ServiceDelivery.Client.Tests.ServiceRep;

public class PersonaMenuReleaseComponentTests
{
    private readonly Mock<ITokenStore> _tokenStore = new();
    private readonly Mock<IPersonaNavigator> _navigator = new();
    private readonly Mock<ILogoutSideEffect> _sideEffect = new();
    private readonly Mock<IReleaseVehicleAction> _releaseAction = new();
    private readonly Mock<IShellPresentation> _presentation = new();

    private ShellViewModel CreateViewModel(BunitContext ctx, bool releaseEnabled)
    {
        _presentation.SetupGet(p => p.MenuStyle).Returns(ShellMenuStyle.Drawer);
        var vm = new ShellViewModel(
            _tokenStore.Object,
            _navigator.Object,
            _sideEffect.Object,
            _releaseAction.Object,
            _presentation.Object,
            new PersonaMenuFactory());
        vm.Load(
            new UserProfile(Guid.NewGuid(), "Rosa Alvarez", UserRole.ServiceRep, ServiceTier.None, Guid.NewGuid()));
        // Drive the InProgress gate through the real production seam the active-job screen uses
        // (SetReleaseEnabled), not by injecting a build-time flag.
        vm.SetReleaseEnabled(releaseEnabled);

        ctx.Services.AddMudServices();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        return vm;
    }

    [Fact]
    public async Task GivenRepStateIsInProgress_WhenMenuRendered_ThenReleaseNavLinkIsDisabled()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var vm = CreateViewModel(ctx, releaseEnabled: false);

        // Act
        var cut = ctx.Render<PersonaMenu>(p => p.Add(c => c.ViewModel, vm));

        // Assert
        var release = cut.Find("[data-testid='menu-item-release'] .mud-nav-link");
        Assert.Contains("mud-nav-link-disabled", release.GetAttribute("class"));
    }

    [Fact]
    public async Task GivenRepStateIsAvailable_WhenMenuRendered_ThenReleaseNavLinkIsEnabled()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var vm = CreateViewModel(ctx, releaseEnabled: true);

        // Act
        var cut = ctx.Render<PersonaMenu>(p => p.Add(c => c.ViewModel, vm));

        // Assert
        var release = cut.Find("[data-testid='menu-item-release'] .mud-nav-link");
        Assert.DoesNotContain("mud-nav-link-disabled", release.GetAttribute("class"));
    }

    [Fact]
    public async Task GivenAServiceRepMenuWithReleaseItem_WhenRendered_ThenFooterDisclaimerNarratesTheReleaseAndDisabledRule()
    {
        // Arrange
        // Mockup rep-nav-drawer footer: a disclaimer line narrates the release action and the AC-2
        // disabled-while-in-progress rule. (Advisory mockup-fidelity gap closed alongside the AC-2 wiring.)
        await using var ctx = new BunitContext();
        var vm = CreateViewModel(ctx, releaseEnabled: true);

        // Act
        var cut = ctx.Render<PersonaMenu>(p => p.Add(c => c.ViewModel, vm));

        // Assert
        var disclaimer = cut.Find("[data-testid='release-disclaimer']");
        Assert.Contains("returns", disclaimer.TextContent);
        Assert.Contains("fleet", disclaimer.TextContent);
        Assert.Contains("Disabled while a job is in progress", disclaimer.TextContent);
    }
}
