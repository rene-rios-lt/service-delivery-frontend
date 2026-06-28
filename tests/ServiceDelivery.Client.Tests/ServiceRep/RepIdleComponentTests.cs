using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Services;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.Services;
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

    private static RepIdleViewModel BuildViewModel(
        ClaimedVehicle vehicle, Mock<IRepHubService> repHub, Mock<IPersonaNavigator> navigator)
    {
        var store = new Mock<IClaimedVehicleStore>();
        store.SetupGet(s => s.CurrentVehicle).Returns(vehicle);
        return new RepIdleViewModel(
            store.Object, repHub.Object, navigator.Object,
            NullLogger<RepIdleViewModel>.Instance);
    }

    private RepIdleViewModel RegisterPage(ClaimedVehicle? vehicle = null)
    {
        var viewModel = BuildViewModel(vehicle ?? Vehicle(), _repHub, _navigator);
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(viewModel);
        Services.AddSingleton<ILogger<RepIdle>>(NullLogger<RepIdle>.Instance);

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
        ctx.Services.AddSingleton(BuildViewModel(vehicle, _repHub, _navigator));
        ctx.Services.AddSingleton<ILogger<RepIdle>>(NullLogger<RepIdle>.Instance);

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
    public void GivenAClaimedVehicle_WhenRepIdleComponentRendered_ThenCardShowsSelectedRegistration()
    {
        // Arrange
        RegisterPage(Vehicle("V-001", string.Empty, "Hydraulics"));

        // Act
        var cut = Render<RepIdle>();

        // Assert
        var card = cut.Find("[data-testid='claimed-vehicle-card']");
        Assert.Contains("V-001", card.TextContent);
    }

    [Fact]
    public void GivenAClaimedVehicleWithEquipment_WhenRepIdleComponentRendered_ThenCardShowsEquipment()
    {
        // Arrange
        RegisterPage(Vehicle("V-001", string.Empty, "Hydraulics", "Coolant"));

        // Act
        var cut = Render<RepIdle>();

        // Assert
        var card = cut.Find("[data-testid='claimed-vehicle-card']");
        Assert.Contains("Hydraulics", card.TextContent);
        Assert.Contains("Coolant", card.TextContent);
    }

    [Fact]
    public void GivenRawEnumEquipmentNames_WhenRepIdleComponentRendered_ThenIdleCardShowsFriendlyLabels()
    {
        // Arrange
        // BUG-034: the claimed vehicle now flows from the real take-over (raw EquipmentType enum
        // names like "HydraulicTool"), so the idle card must render the same friendly labels the
        // take-over list shows — not the raw enum names.
        RegisterPage(Vehicle("V-001", string.Empty, "HydraulicTool", "ElectricalDiagnosticKit"));

        // Act
        var cut = Render<RepIdle>();

        // Assert
        var card = cut.Find("[data-testid='claimed-vehicle-card']");
        Assert.Contains("Hydraulics", card.TextContent);
        Assert.Contains("Diagnostics", card.TextContent);
        Assert.DoesNotContain("HydraulicTool", card.TextContent);
        Assert.DoesNotContain("ElectricalDiagnosticKit", card.TextContent);
    }

    [Fact]
    public void GivenAClaimedVehicleWithEmptyModel_WhenRepIdleComponentRendered_ThenCardTitleShowsRegistrationOnly()
    {
        // Arrange
        // BUG-034: with the model deferred to BUG-035, the title is just the registration — no
        // trailing " · " separator. The conditional render appends " · {model}" only when non-empty.
        RegisterPage(Vehicle("V-001", string.Empty, "Hydraulics"));

        // Act
        var cut = Render<RepIdle>();

        // Assert
        var title = cut.Find("[data-testid='claimed-vehicle-card'] .sd-listitem__title");
        Assert.Equal("V-001", title.TextContent.Trim());
        Assert.DoesNotContain("·", title.TextContent);
    }

    [Fact]
    public void GivenAClaimedVehicle_WhenRepIdleInitialised_ThenShellSubtitleContainsSelectedRegistration()
    {
        // Arrange
        RegisterPage(Vehicle("V-001", string.Empty, "Hydraulics"));

        // Act
        Render<RepIdle>();

        // Assert
        Assert.Equal("Vehicle V-001 · On shift", _shell!.Menu!.VehicleContext);
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
    public void GivenStoreUpdatedBetweenNavigations_WhenRepIdleReInitialized_ThenShellSubtitleReflectsNewVehicle()
    {
        // Arrange — BUG-042. The VM is scoped (≈ singleton for the session), so the SAME instance is
        // reused on every navigation back to /rep/idle. Build the page's VM over a REAL store so its
        // Vehicle reads live state, register it, and render once for the first take-over (V-001).
        var store = new InMemoryClaimedVehicleStore();
        store.SetVehicle(new ClaimedVehicle(
            Guid.NewGuid(), "V-001", "Transit 350", new[] { "Hydraulics" }));
        var viewModel = new RepIdleViewModel(
            store, _repHub.Object, _navigator.Object, NullLogger<RepIdleViewModel>.Instance);
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(viewModel);
        Services.AddSingleton<ILogger<RepIdle>>(NullLogger<RepIdle>.Instance);
        _presentation.SetupGet(p => p.MenuStyle).Returns(ShellMenuStyle.Drawer);
        var shell = new ShellViewModel(
            _tokenStore.Object, _navigator.Object, _sideEffect.Object,
            _releaseAction.Object, _presentation.Object, new PersonaMenuFactory());
        shell.Load(new UserProfile(
            Guid.NewGuid(), "Rosa Alvarez", UserRole.ServiceRep, ServiceTier.None, Guid.NewGuid()));
        Services.AddSingleton(shell);
        Render<RepIdle>();

        // Act — release then a second take-over of a DIFFERENT vehicle, then re-mount the page (the same
        // scoped VM is reused) so OnInitializedAsync pushes the subtitle from the now-current vehicle.
        store.ClearVehicle();
        store.SetVehicle(new ClaimedVehicle(
            Guid.NewGuid(), "V-002", "Sprinter 250", new[] { "Coolant" }));
        Render<RepIdle>();

        // Assert — the app-bar subtitle reflects the second vehicle, not the first.
        Assert.Equal("Vehicle V-002 · On shift", shell.Menu!.VehicleContext);
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
    public void GivenRepHubStartThrows_WhenRepIdleComponentInitialised_ThenNoExceptionEscapesAndComponentRenders()
    {
        // Arrange — BUG-038/AC-1: if the hub connect throws when the idle screen mounts, OnInitializedAsync
        // must swallow it so the exception never escapes to Blazor's #blazor-error-ui. The component still
        // renders its idle body.
        _repHub.Setup(h => h.StartAsync()).ThrowsAsync(new InvalidOperationException("hub unreachable"));
        RegisterPage(Vehicle());

        // Act
        var exception = Record.Exception(() => Render<RepIdle>());

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void GivenHubNotConnected_WhenRepIdleComponentRendered_ThenReconnectingIndicatorIsVisible()
    {
        // Arrange — BUG-038: while the hub is reconnecting (IsConnected false), the screen shows an
        // unobtrusive "Reconnecting…" indicator rather than crashing or showing nothing.
        _repHub.SetupGet(h => h.IsConnected).Returns(false);
        RegisterPage(Vehicle());

        // Act
        var cut = Render<RepIdle>();

        // Assert
        var status = cut.Find("[data-testid='hub-status']");
        Assert.Contains("Reconnecting", status.TextContent);
    }

    [Fact]
    public void GivenHubConnected_WhenRepIdleComponentRendered_ThenReconnectingIndicatorIsNotShown()
    {
        // Arrange — when the hub is connected the reconnecting indicator stays hidden.
        _repHub.SetupGet(h => h.IsConnected).Returns(true);
        RegisterPage(Vehicle());

        // Act
        var cut = Render<RepIdle>();

        // Assert
        Assert.Empty(cut.FindAll("[data-testid='hub-status']"));
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
