using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.ViewModels;
using ServiceDelivery.Client.UI.Shared.Components;

namespace ServiceDelivery.Client.Tests.Shell;

public class PersonaShellComponentTests
{
    private readonly Mock<ITokenStore> _tokenStore = new();
    private readonly Mock<IPersonaNavigator> _navigator = new();
    private readonly Mock<ILogoutSideEffect> _sideEffect = new();
    private readonly Mock<IReleaseVehicleAction> _releaseAction = new();
    private readonly Mock<IShellPresentation> _presentation = new();

    private ShellViewModel CreateViewModel(BunitContext ctx, ShellMenuStyle style, UserRole role, string name = "Rosa Alvarez")
    {
        _presentation.SetupGet(p => p.MenuStyle).Returns(style);
        var vm = new ShellViewModel(
            _tokenStore.Object,
            _navigator.Object,
            _sideEffect.Object,
            _releaseAction.Object,
            _presentation.Object,
            new PersonaMenuFactory());
        vm.Load(new UserProfile(Guid.NewGuid(), name, role, ServiceTier.None, Guid.NewGuid()));

        ctx.Services.AddMudServices();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        return vm;
    }

    private static IRenderedComponent<PersonaShell> RenderShell(BunitContext ctx, ShellViewModel vm)
    {
        RenderFragment body = builder => builder.AddMarkupContent(0, "<div data-testid='page-body'>page</div>");
        return ctx.Render<PersonaShell>(p => p
            .Add(c => c.ViewModel, vm)
            .Add(c => c.Body, body));
    }

    [Fact]
    public async Task GivenAnAuthenticatedProfile_WhenShellRenders_ThenAppBarShowsTitleAndMenuAffordance()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var vm = CreateViewModel(ctx, ShellMenuStyle.Drawer, UserRole.ServiceRep);

        // Act
        var cut = RenderShell(ctx, vm);

        // Assert
        Assert.Contains("Service Delivery", cut.Find("[data-testid='appbar-title']").TextContent);
        Assert.NotNull(cut.Find("[data-testid='appbar-menu-affordance']"));
    }

    [Fact]
    public async Task GivenAProfileWithName_WhenShellRenders_ThenContextLineShowsPersonaName()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var vm = CreateViewModel(ctx, ShellMenuStyle.Drawer, UserRole.ServiceRep, "Rosa Alvarez");

        // Act
        var cut = RenderShell(ctx, vm);

        // Assert
        Assert.Contains("Rosa Alvarez", cut.Find("[data-testid='persona-name']").TextContent);
    }

    [Fact]
    public async Task GivenNoClaimedVehicle_WhenShellRenders_ThenNoVehicleContextChipIsShown()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var vm = CreateViewModel(ctx, ShellMenuStyle.Drawer, UserRole.ServiceRep);

        // Act
        var cut = RenderShell(ctx, vm);

        // Assert
        Assert.Empty(cut.FindAll("[data-testid='vehicle-context-chip']"));
    }

    [Fact]
    public async Task GivenAShell_WhenRendered_ThenTheBodyContentIsRenderedInsideIt()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var vm = CreateViewModel(ctx, ShellMenuStyle.Drawer, UserRole.ServiceRep);

        // Act
        var cut = RenderShell(ctx, vm);

        // Assert
        Assert.NotNull(cut.Find("[data-testid='page-body']"));
    }

    [Fact]
    public async Task GivenADrawerStyleShellWithVehicleContext_WhenRendered_ThenSubtitleShowsVehicleContext()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var vm = CreateViewModel(ctx, ShellMenuStyle.Drawer, UserRole.ServiceRep);
        vm.SetVehicleContext("Vehicle IA-4471 · On shift");

        // Act
        var cut = RenderShell(ctx, vm);

        // Assert
        Assert.Contains("Vehicle IA-4471 · On shift", cut.Find("[data-testid='appbar-context']").TextContent);
    }

    [Fact]
    public async Task GivenASubtitleOverride_WhenShellRenders_ThenAppBarContextShowsOverrideText()
    {
        // Arrange
        // BUG-039: a route (the active-job screen) can override the app-bar subtitle; the override
        // takes precedence over the menu-derived vehicle/context line.
        await using var ctx = new BunitContext();
        var vm = CreateViewModel(ctx, ShellMenuStyle.Drawer, UserRole.ServiceRep);
        vm.SetVehicleContext("Vehicle IA-4471 · On shift");
        vm.SetSubtitle("Navigating to requester");

        // Act
        var cut = RenderShell(ctx, vm);

        // Assert
        Assert.Equal("Navigating to requester", cut.Find("[data-testid='appbar-context']").TextContent.Trim());
    }

    [Fact]
    public async Task GivenADrawerStyleShellWithVehicleContext_WhenRendered_ThenAppBarAvatarIsVisible()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var vm = CreateViewModel(ctx, ShellMenuStyle.Drawer, UserRole.ServiceRep);
        vm.SetVehicleContext("Vehicle IA-4471 · On shift");

        // Act
        var cut = RenderShell(ctx, vm);

        // Assert
        Assert.NotNull(cut.Find("[data-testid='appbar-avatar']"));
    }

    [Fact]
    public async Task GivenATitleOverride_WhenShellRenders_ThenAppBarShowsTheOverriddenTitle()
    {
        // Arrange
        // BUG-036: a route (the job-offer screen) can override the app-bar title.
        await using var ctx = new BunitContext();
        var vm = CreateViewModel(ctx, ShellMenuStyle.Drawer, UserRole.ServiceRep);
        vm.SetTitle("Incoming Job Offer");

        // Act
        var cut = RenderShell(ctx, vm);

        // Assert
        Assert.Equal("Incoming Job Offer", cut.Find("[data-testid='appbar-title']").TextContent.Trim());
    }

    [Fact]
    public async Task GivenTheMenuAffordanceHidden_WhenShellRenders_ThenNoMenuAffordanceIsRendered()
    {
        // Arrange
        // BUG-036: the offer screen hides the hamburger to match the mockup.
        await using var ctx = new BunitContext();
        var vm = CreateViewModel(ctx, ShellMenuStyle.Drawer, UserRole.ServiceRep);
        vm.SetMenuAffordanceVisible(false);

        // Act
        var cut = RenderShell(ctx, vm);

        // Assert
        Assert.Empty(cut.FindAll("[data-testid='appbar-menu-affordance']"));
    }

    [Fact]
    public async Task GivenTheMenuAffordance_WhenClicked_ThenTheMenuStateToggles()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var vm = CreateViewModel(ctx, ShellMenuStyle.Drawer, UserRole.ServiceRep);
        var cut = RenderShell(ctx, vm);
        var initial = vm.IsMenuOpen;

        // Act
        cut.Find("[data-testid='appbar-menu-affordance']").Click();

        // Assert
        Assert.NotEqual(initial, vm.IsMenuOpen);
    }
}
