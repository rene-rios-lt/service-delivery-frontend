using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.ViewModels;
using ServiceDelivery.Client.UI.Features.ServiceRep.Pages;
using ServiceDelivery.Client.UI.Layout;

namespace ServiceDelivery.Client.Tests.ServiceRep;

public class RepIdleComponentTests : BunitContext
{
    private readonly Mock<IRepHubService> _repHub = new();
    private readonly Mock<IPersonaNavigator> _navigator = new();
    private readonly Mock<ITokenStore> _tokenStore = new();
    private readonly Mock<ILogoutSideEffect> _sideEffect = new();
    private readonly Mock<IReleaseVehicleAction> _releaseAction = new();
    private readonly Mock<IShellPresentation> _presentation = new();
    private readonly Mock<IAuthService> _authService = new();

    private static ClaimedVehicle Vehicle(
        string registration = "IA-4471",
        string model = "Transit 350",
        params string[] equipment) =>
        new(Guid.NewGuid(), registration, model,
            equipment.Length == 0 ? new[] { "Hydraulics", "Coolant" } : equipment);

    private ShellViewModel? _shell;

    private RepIdleViewModel RegisterPage(ClaimedVehicle? vehicle = null)
    {
        var viewModel = new RepIdleViewModel(vehicle ?? Vehicle(), _repHub.Object, _navigator.Object);
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(viewModel);

        _presentation.SetupGet(p => p.MenuStyle).Returns(ShellMenuStyle.Drawer);
        _shell = new ShellViewModel(
            _tokenStore.Object, _navigator.Object, _sideEffect.Object,
            _releaseAction.Object, _presentation.Object, new PersonaMenuFactory());
        _shell.Load(new UserProfile(
            Guid.NewGuid(), "Rosa Alvarez", UserRole.ServiceRep, ServiceTier.None, Guid.NewGuid()));
        Services.AddSingleton(_shell);
        return viewModel;
    }

    // MainLayout renders MudBlazor's providers, which register an IAsyncDisposable service. The
    // inherited (synchronous) BunitContext cannot dispose that, so the shell-wrapped test uses its
    // own `await using` BunitContext instance — the same pattern as MainLayoutTests.
    private void RegisterShell(BunitContext ctx, ClaimedVehicle vehicle)
    {
        ctx.Services.AddMudServices();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddSingleton(new RepIdleViewModel(vehicle, _repHub.Object, _navigator.Object));

        _presentation.SetupGet(p => p.MenuStyle).Returns(ShellMenuStyle.Drawer);
        _authService.Setup(a => a.GetCurrentUserAsync())
            .ReturnsAsync(new UserProfile(
                Guid.NewGuid(), "Rosa Alvarez", UserRole.ServiceRep, ServiceTier.None, Guid.NewGuid()));
        ctx.Services.AddSingleton(_authService.Object);
        ctx.Services.AddSingleton(new ShellViewModel(
            _tokenStore.Object, _navigator.Object, _sideEffect.Object,
            _releaseAction.Object, _presentation.Object, new PersonaMenuFactory()));
    }

    [Fact]
    public void GivenAvailableState_WhenRepIdleComponentRendered_ThenAvailableChipIsVisible()
    {
        // Arrange
        RegisterPage(Vehicle());

        // Act
        var cut = Render<RepIdle>();

        // Assert
        var chip = cut.Find("[data-testid='available-chip']");
        Assert.Contains("Available", chip.TextContent);
    }

    [Fact]
    public void GivenClaimedVehicle_WhenRepIdleComponentRendered_ThenVehicleCardShowsRegistrationAndEquipment()
    {
        // Arrange
        RegisterPage(Vehicle("IA-4471", "Transit 350", "Hydraulics", "Coolant"));

        // Act
        var cut = Render<RepIdle>();

        // Assert
        var card = cut.Find("[data-testid='claimed-vehicle-card']");
        Assert.Contains("IA-4471", card.TextContent);
        Assert.Contains("Transit 350", card.TextContent);
        Assert.Contains("Hydraulics", card.TextContent);
        Assert.Contains("Coolant", card.TextContent);
    }

    [Fact]
    public async Task GivenIdleComponent_WhenJobOfferPayloadDelivered_ThenNavigationIsInvoked()
    {
        // Arrange
        // The page subscribes to RepHub on init (push-driven, AC-5). When a JobOfferReceived event
        // arrives, the captured handler navigates to the offer screen (AC-3) — no manual refresh.
        Func<JobOfferPayload, Task>? capturedHandler = null;
        _repHub.Setup(h => h.OnJobOfferReceived(It.IsAny<Func<JobOfferPayload, Task>>()))
            .Callback<Func<JobOfferPayload, Task>>(h => capturedHandler = h);
        RegisterPage(Vehicle());
        Render<RepIdle>();
        var offer = new JobOfferPayload(
            Guid.NewGuid(), "Maria Lopez", ServiceTier.Gold, "Hydraulic leak", 4.2, 9, 41.6, -93.6);

        // Act
        await capturedHandler!.Invoke(offer);

        // Assert
        _navigator.Verify(n => n.NavigateToJobOffer(offer), Times.Once);
    }

    [Fact]
    public void GivenRepIdleComponent_WhenInitialized_ThenShellVehicleContextIsSet()
    {
        // Arrange
        RegisterPage(Vehicle("IA-4471", "Transit 350", "HydraulicTool"));

        // Act
        Render<RepIdle>();

        // Assert
        Assert.Equal("Vehicle IA-4471 · On shift", _shell!.Menu!.VehicleContext);
    }

    [Fact]
    public async Task GivenRepIdleComponent_WhenRendered_ThenPersonaShellMenuAffordanceIsPresent()
    {
        // Arrange
        // The navigation menu (FE-021) is reachable from the idle screen without leaving the
        // waiting state (AC-4): the shared PersonaShell wraps the page, so the hamburger affordance
        // sits in the app bar above the idle body while the rep stays on /rep/idle.
        await using var ctx = new BunitContext();
        RegisterShell(ctx, Vehicle());
        var nav = ctx.Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo(nav.BaseUri + "rep/idle");
        RenderFragment body = builder =>
        {
            builder.OpenComponent<RepIdle>(0);
            builder.CloseComponent();
        };

        // Act
        var cut = ctx.Render<MainLayout>(p => p.Add(c => c.Body, body));

        // Assert
        Assert.NotNull(cut.Find("[data-testid='appbar-menu-affordance']"));
        Assert.NotNull(cut.Find("[data-testid='rep-idle']"));
    }
}
