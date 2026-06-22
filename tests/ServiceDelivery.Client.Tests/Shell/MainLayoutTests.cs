using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.ViewModels;
using ServiceDelivery.Client.UI.Layout;

namespace ServiceDelivery.Client.Tests.Shell;

public class MainLayoutTests
{
    private readonly Mock<ITokenStore> _tokenStore = new();
    private readonly Mock<IPersonaNavigator> _navigator = new();
    private readonly Mock<ILogoutSideEffect> _sideEffect = new();
    private readonly Mock<IReleaseVehicleAction> _releaseAction = new();
    private readonly Mock<IShellPresentation> _presentation = new();
    private readonly Mock<IAuthService> _authService = new();

    private void RegisterServices(BunitContext ctx, ShellMenuStyle style = ShellMenuStyle.Drawer)
    {
        _presentation.SetupGet(p => p.MenuStyle).Returns(style);
        _authService.Setup(a => a.GetCurrentUserAsync())
            .ReturnsAsync(new UserProfile(Guid.NewGuid(), "Rosa Alvarez", UserRole.ServiceRep, ServiceTier.None, Guid.NewGuid()));

        ctx.Services.AddMudServices();
        ctx.Services.AddSingleton(_authService.Object);
        ctx.Services.AddSingleton(new ShellViewModel(
            _tokenStore.Object, _navigator.Object, _sideEffect.Object,
            _releaseAction.Object, _presentation.Object, new PersonaMenuFactory()));
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private static RenderFragment Body =>
        builder => builder.AddMarkupContent(0, "<div data-testid='page-body'>page</div>");

    [Fact]
    public async Task GivenTheLoginRoute_WhenLayoutRenders_ThenNoPersonaShellIsShown()
    {
        // Arrange
        await using var ctx = new BunitContext();
        RegisterServices(ctx);
        var nav = ctx.Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo(nav.BaseUri + "login");

        // Act
        var cut = ctx.Render<MainLayout>(p => p.Add(c => c.Body, Body));

        // Assert
        Assert.NotNull(cut.Find("[data-testid='page-body']"));
        Assert.Empty(cut.FindAll("[data-testid='persona-shell']"));
    }

    [Fact]
    public async Task GivenAnAuthenticatedRoute_WhenLayoutRenders_ThenPersonaShellWrapsTheBody()
    {
        // Arrange
        await using var ctx = new BunitContext();
        RegisterServices(ctx);
        var nav = ctx.Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo(nav.BaseUri + "rep");

        // Act
        var cut = ctx.Render<MainLayout>(p => p.Add(c => c.Body, Body));

        // Assert
        Assert.NotNull(cut.Find("[data-testid='persona-shell']"));
        Assert.NotNull(cut.Find("[data-testid='page-body']"));
    }

    [Fact]
    public async Task GivenAnAuthenticatedRoute_WhenLayoutRenders_ThenTheShellMenuReflectsTheCurrentUser()
    {
        // Arrange
        await using var ctx = new BunitContext();
        RegisterServices(ctx);
        var nav = ctx.Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo(nav.BaseUri + "rep");

        // Act
        var cut = ctx.Render<MainLayout>(p => p.Add(c => c.Body, Body));

        // Assert
        Assert.Contains("Rosa Alvarez", cut.Find("[data-testid='persona-name']").TextContent);
    }
}
