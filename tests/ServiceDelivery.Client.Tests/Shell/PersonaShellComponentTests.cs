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

    [Fact]
    public async Task GivenPersonaShellWithAccountMenuStyle_WhenRendered_ThenAppBarAvatarIsAbsent()
    {
        // Arrange
        // BUG-044/AC-2: on AccountMenu (Desktop/Web) the sole avatar is the persona-avatar inside
        // PersonaMenu (the clickable dropdown affordance). The appbar-avatar in PersonaShell is the
        // duplicate and must be suppressed on this style. (This test reproduces the defect: before the
        // fix the appbar-avatar renders unconditionally when Menu is not null.)
        await using var ctx = new BunitContext();
        var vm = CreateViewModel(ctx, ShellMenuStyle.AccountMenu, UserRole.Requester, "Marcus Webb");

        // Act
        var cut = RenderShell(ctx, vm);

        // Assert
        Assert.Empty(cut.FindAll("[data-testid='appbar-avatar']"));
    }

    [Fact]
    public async Task GivenPersonaShellWithAccountMenuStyle_WhenRendered_ThenOnlyOneAvatarElementExists()
    {
        // Arrange
        // BUG-044/AC-2: the mockups show EXACTLY ONE avatar in the app-bar trailing position on
        // Desktop/Web. Counting both avatar test-ids together guards the visual contract directly —
        // exactly one avatar (the persona-avatar) is present, the duplicate appbar-avatar is gone.
        await using var ctx = new BunitContext();
        var vm = CreateViewModel(ctx, ShellMenuStyle.AccountMenu, UserRole.Requester, "Marcus Webb");

        // Act
        var cut = RenderShell(ctx, vm);

        // Assert
        var avatars = cut.FindAll("[data-testid='appbar-avatar'], [data-testid='persona-avatar']");
        Assert.Single(avatars);
    }

    [Fact]
    public async Task GivenPersonaShellWithDrawerStyle_WhenRendered_ThenAppBarAvatarIsPresent()
    {
        // Arrange
        // BUG-044/AC-2 (no regression): on the Drawer (Mobile) style the persona-avatar lives inside the
        // off-screen drawer, so the trailing appbar-avatar is the needed visible avatar and MUST remain.
        await using var ctx = new BunitContext();
        var vm = CreateViewModel(ctx, ShellMenuStyle.Drawer, UserRole.ServiceRep);

        // Act
        var cut = RenderShell(ctx, vm);

        // Assert
        Assert.NotNull(cut.Find("[data-testid='appbar-avatar']"));
    }

    [Fact]
    public async Task GivenPersonaShellWithAccountMenuStyle_WhenRendered_ThenPersonaAvatarIsInsideTheAppBarControls()
    {
        // Arrange
        // BUG-044/AC-2 (cycle 2): the single avatar must render in the TRAILING app-bar slot per the
        // mockup — not floating below the bar. On AccountMenu the PersonaMenu account surface (which owns
        // the persona-avatar) must live INSIDE the app bar's .sd-appbar__controls flex row, so the avatar
        // sits in the trailing position. Before this fix the account surface was a sibling BELOW the bar,
        // so the avatar floated top-left half-cut-off (the live-render defect the reviewer caught).
        await using var ctx = new BunitContext();
        var vm = CreateViewModel(ctx, ShellMenuStyle.AccountMenu, UserRole.Requester, "Marcus Webb");

        // Act
        var cut = RenderShell(ctx, vm);

        // Assert — the persona-avatar is a descendant of the app-bar controls (trailing slot).
        var controls = cut.Find(".sd-appbar__controls");
        var avatarInControls = controls.QuerySelector("[data-testid='persona-avatar']");
        Assert.NotNull(avatarInControls);
    }

    [Fact]
    public async Task GivenPersonaShellWithAccountMenuStyle_WhenRendered_ThenNoHamburgerAffordanceIsRendered()
    {
        // Arrange
        // BUG-044/AC-3 (cycle 2): the Requester mockups depict NO hamburger — the drawer/hamburger is a
        // ServiceRep-mobile (Drawer) concern. On AccountMenu (Desktop/Web) the persona-avatar IS the menu
        // affordance, so the app-bar hamburger must not render. Before this fix the hamburger showed on the
        // live Requester tracking bar (the reviewer's screenshot).
        await using var ctx = new BunitContext();
        var vm = CreateViewModel(ctx, ShellMenuStyle.AccountMenu, UserRole.Requester, "Marcus Webb");

        // Act
        var cut = RenderShell(ctx, vm);

        // Assert
        Assert.Empty(cut.FindAll("[data-testid='appbar-menu-affordance']"));
    }

    [Fact]
    public async Task GivenPersonaShellWithDrawerStyle_WhenRendered_ThenHamburgerAffordanceIsRendered()
    {
        // Arrange
        // BUG-044/AC-3 (cycle 2, no regression): the ServiceRep-mobile (Drawer) style keeps the hamburger —
        // it toggles the navigation drawer. Suppressing the hamburger on AccountMenu must NOT touch Drawer.
        await using var ctx = new BunitContext();
        var vm = CreateViewModel(ctx, ShellMenuStyle.Drawer, UserRole.ServiceRep);

        // Act
        var cut = RenderShell(ctx, vm);

        // Assert
        Assert.NotNull(cut.Find("[data-testid='appbar-menu-affordance']"));
    }

    [Fact]
    public async Task GivenPersonaShell_WhenSetTitleCalledAfterRender_ThenAppBarTitleUpdatesInDom()
    {
        // Arrange
        // BUG-044/AC-1: the core defect — a route sets its title AFTER the shell has rendered (as the
        // tracking page does in OnInitialized / on redirect). Before the fix, SetTitle mutated the field
        // but PersonaShell never re-rendered, so the DOM kept the default. Asserting the rendered
        // [data-testid='appbar-title'] (not the ViewModel field) is what the masking test missed.
        await using var ctx = new BunitContext();
        var vm = CreateViewModel(ctx, ShellMenuStyle.Drawer, UserRole.ServiceRep);
        var cut = RenderShell(ctx, vm);

        // Act
        await cut.InvokeAsync(() => vm.SetTitle("Your technician is on the way"));

        // Assert
        cut.WaitForAssertion(() =>
            Assert.Equal(
                "Your technician is on the way",
                cut.Find("[data-testid='appbar-title']").TextContent.Trim()));
    }
}
