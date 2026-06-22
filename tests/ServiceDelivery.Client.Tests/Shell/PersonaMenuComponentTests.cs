using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.ViewModels;
using ServiceDelivery.Client.UI.Shared.Components;

namespace ServiceDelivery.Client.Tests.Shell;

public class PersonaMenuComponentTests
{
    private readonly Mock<ITokenStore> _tokenStore = new();
    private readonly Mock<IPersonaNavigator> _navigator = new();
    private readonly Mock<ILogoutSideEffect> _sideEffect = new();
    private readonly Mock<IReleaseVehicleAction> _releaseAction = new();
    private readonly Mock<IShellPresentation> _presentation = new();

    private ShellViewModel CreateViewModel(BunitContext ctx, ShellMenuStyle style, UserRole role)
    {
        _presentation.SetupGet(p => p.MenuStyle).Returns(style);
        var vm = new ShellViewModel(
            _tokenStore.Object,
            _navigator.Object,
            _sideEffect.Object,
            _releaseAction.Object,
            _presentation.Object,
            new PersonaMenuFactory());
        vm.Load(new UserProfile(Guid.NewGuid(), "Rosa Alvarez", role, ServiceTier.None, Guid.NewGuid()));

        ctx.Services.AddMudServices();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        return vm;
    }

    [Fact]
    public async Task GivenDrawerMenuStyle_WhenMenuRenders_ThenMudDrawerIsUsed()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var vm = CreateViewModel(ctx, ShellMenuStyle.Drawer, UserRole.ServiceRep);

        // Act
        var cut = ctx.Render<PersonaMenu>(p => p.Add(c => c.ViewModel, vm));

        // Assert
        Assert.NotNull(cut.Find("[data-testid='persona-menu-drawer']"));
        Assert.Empty(cut.FindAll("[data-testid='persona-menu-account']"));
    }

    [Fact]
    public async Task GivenAccountMenuStyle_WhenMenuRenders_ThenMudMenuIsUsed()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var vm = CreateViewModel(ctx, ShellMenuStyle.AccountMenu, UserRole.Dispatcher);

        // Act
        var cut = ctx.Render<PersonaMenu>(p => p.Add(c => c.ViewModel, vm));

        // Assert
        Assert.NotNull(cut.Find("[data-testid='persona-menu-account']"));
        Assert.Empty(cut.FindAll("[data-testid='persona-menu-drawer']"));
    }

    [Fact]
    public async Task GivenAServiceRepMenu_WhenRendered_ThenReleaseAndJobHistoryItemsAreVisible()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var vm = CreateViewModel(ctx, ShellMenuStyle.Drawer, UserRole.ServiceRep);

        // Act
        var cut = ctx.Render<PersonaMenu>(p => p.Add(c => c.ViewModel, vm));

        // Assert
        Assert.NotNull(cut.Find("[data-testid='menu-item-release']"));
        Assert.NotNull(cut.Find("[data-testid='menu-item-job-history']"));
    }

    [Fact]
    public async Task GivenAServiceRepMenu_WhenRendered_ThenReleaseAndLogoutHaveDestructiveStyling()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var vm = CreateViewModel(ctx, ShellMenuStyle.Drawer, UserRole.ServiceRep);

        // Act
        var cut = ctx.Render<PersonaMenu>(p => p.Add(c => c.ViewModel, vm));

        // Assert
        var release = cut.Find("[data-testid='menu-item-release']");
        var logout = cut.Find("[data-testid='menu-item-logout']");
        Assert.Contains("sd-menu-item--destructive", release.GetAttribute("class"));
        Assert.Contains("sd-menu-item--destructive", logout.GetAttribute("class"));
    }

    [Fact]
    public async Task GivenAccountMenu_WhenRendered_ThenLogoutHasDestructiveStyling()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var vm = CreateViewModel(ctx, ShellMenuStyle.AccountMenu, UserRole.Dispatcher);
        var cut = ctx.Render<PersonaMenu>(p => p.Add(c => c.ViewModel, vm));

        // Act
        cut.Find("[data-testid='persona-avatar']").Click();

        // Assert
        var logout = cut.Find("[data-testid='menu-item-logout']");
        Assert.Contains("sd-menu-item--destructive", logout.GetAttribute("class"));
    }

    [Fact]
    public async Task GivenLogoutMenuItem_WhenClicked_ThenShellViewModelLogoutIsInvoked()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var vm = CreateViewModel(ctx, ShellMenuStyle.Drawer, UserRole.ServiceRep);
        var cut = ctx.Render<PersonaMenu>(p => p.Add(c => c.ViewModel, vm));

        // Act
        cut.Find("[data-testid='menu-item-logout'] .mud-nav-link").Click();

        // Assert
        _sideEffect.Verify(s => s.RunBeforeTokenClearedAsync(), Times.Once);
        _tokenStore.Verify(t => t.ClearAsync(), Times.Once);
        _navigator.Verify(n => n.NavigateToLogin(), Times.Once);
    }
}
