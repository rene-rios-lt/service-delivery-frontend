using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
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
    public async Task GivenDrawerMenuStyle_WhenMenuRendered_ThenMudDrawerIsLeftAnchored()
    {
        // Arrange
        // FE-029/AC-2: the drawer must stay anchored to the LEFT (Anchor.Start) — the rep-nav-drawer
        // mockup slides it in from the left. MudBlazor renders Anchor.Start as `mud-drawer-pos-left`;
        // a regression to the right would surface as `mud-drawer-pos-right`.
        await using var ctx = new BunitContext();
        var vm = CreateViewModel(ctx, ShellMenuStyle.Drawer, UserRole.ServiceRep);

        // Act
        var cut = ctx.Render<PersonaMenu>(p => p.Add(c => c.ViewModel, vm));

        // Assert
        var drawerClass = cut.Find("[data-testid='persona-menu-drawer']").GetAttribute("class");
        Assert.Contains("mud-drawer-pos-left", drawerClass);
        Assert.DoesNotContain("mud-drawer-pos-right", drawerClass);
    }

    [Fact]
    public async Task GivenDrawerMenuStyle_WhenMenuRendered_ThenMudDrawerIsFixedOverlay()
    {
        // Arrange
        // FE-029/AC-5+AC-6 (cycle 2 — blocking-finding fix): the temporary drawer must be a
        // viewport-FIXED overlay so it renders flush-left over the scrim without displacing the body.
        // MudBlazor's base rule makes a NON-fixed drawer `position: absolute` relative to the nearest
        // positioned ancestor (`.mud-layout`, which PersonaShell nests inside) — on the live iOS host
        // that landed the panel off-screen-left and shoved the page body right (the reviewer's broken
        // screenshots). `Fixed="true"` emits `mud-drawer-fixed`, restoring `position: fixed` to the
        // viewport. Without Fixed the class is absent and this assertion fails (the red state).
        await using var ctx = new BunitContext();
        var vm = CreateViewModel(ctx, ShellMenuStyle.Drawer, UserRole.ServiceRep);

        // Act
        // MudDrawer only emits `mud-drawer-fixed` once it resolves the MudDrawerContainer cascade that
        // MudLayout provides — which in production is always present (MainLayout > MudLayout > shell).
        // Render inside a MudLayout here so the test exercises the same container context the app uses.
        var cut = ctx.Render<MudLayout>(p => p.AddChildContent<PersonaMenu>(
            cp => cp.Add(c => c.ViewModel, vm)));

        // Assert
        var drawerClass = cut.Find("[data-testid='persona-menu-drawer']").GetAttribute("class");
        Assert.Contains("mud-drawer-fixed", drawerClass);
    }

    [Fact]
    public async Task GivenDrawerMenuStyle_WhenMenuRendered_ThenNoDrawerHeaderIsPresent()
    {
        // Arrange
        // FE-029/AC-3: the indigo drawer header (rep name / role / vehicle chip) is removed — the
        // drawer contains only the menu items and the footer note. Neither the MudDrawerHeader
        // (persona-name) nor the vehicle-context chip may render in the drawer.
        await using var ctx = new BunitContext();
        var vm = CreateViewModel(ctx, ShellMenuStyle.Drawer, UserRole.ServiceRep);

        // Act
        var cut = ctx.Render<PersonaMenu>(p => p.Add(c => c.ViewModel, vm));

        // Assert
        Assert.Empty(cut.FindAll("[data-testid='persona-name']"));
        Assert.Empty(cut.FindAll("[data-testid='vehicle-context-chip']"));
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
    public async Task GivenDrawerMenuStyle_WhenMenuRendered_ThenNoMenuItemHasInlineStyleAttribute()
    {
        // Arrange
        // FE-029/AC-7 (proxy): styling must flow through design-system token classes, not ad-hoc
        // inline colours. No menu-item element may carry an inline `style` attribute. (Full token
        // audit is a code-review criterion; this guards the most common ad-hoc-styling regression.)
        await using var ctx = new BunitContext();
        var vm = CreateViewModel(ctx, ShellMenuStyle.Drawer, UserRole.ServiceRep);

        // Act
        var cut = ctx.Render<PersonaMenu>(p => p.Add(c => c.ViewModel, vm));

        // Assert
        var menuItems = cut.FindAll("[data-testid^='menu-item-']");
        Assert.NotEmpty(menuItems);
        Assert.All(menuItems, item => Assert.True(
            string.IsNullOrEmpty(item.GetAttribute("style")),
            $"menu item '{item.GetAttribute("data-testid")}' must not carry an inline style attribute"));
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
    public async Task GivenServiceRepDrawerMenu_WhenRendered_ThenRepHomeItemShowsActiveClass()
    {
        // Arrange
        // FE-029/AC-7: the ServiceRep's active item ("Waiting for offers" / rep-home) carries the
        // active-state class so it renders the --sd-primary-soft highlight from the mockup.
        await using var ctx = new BunitContext();
        var vm = CreateViewModel(ctx, ShellMenuStyle.Drawer, UserRole.ServiceRep);

        // Act
        var cut = ctx.Render<PersonaMenu>(p => p.Add(c => c.ViewModel, vm));

        // Assert
        var repHome = cut.Find("[data-testid='menu-item-rep-home']");
        Assert.Contains("sd-menu-item--active", repHome.GetAttribute("class"));
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
