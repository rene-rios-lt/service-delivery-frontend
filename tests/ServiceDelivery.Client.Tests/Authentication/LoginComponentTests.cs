using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.ViewModels;
using ServiceDelivery.Client.UI.Features.Authentication.Pages;

namespace ServiceDelivery.Client.Tests.Authentication;

public class LoginComponentTests : BunitContext
{
    private readonly Mock<IAuthService> _authService = new();
    private readonly Mock<ITokenStore> _tokenStore = new();
    private readonly Mock<IPersonaNavigator> _navigator = new();

    private LoginViewModel RegisterViewModel()
    {
        var viewModel = new LoginViewModel(_authService.Object, _tokenStore.Object, _navigator.Object);
        Services.AddMudServices();
        Services.AddSingleton(viewModel);
        JSInterop.Mode = JSRuntimeMode.Loose;
        return viewModel;
    }

    [Fact]
    public void GivenTheLoginScreen_WhenRendered_ThenItRendersTheSharedFormAndCardLayout()
    {
        // Arrange
        RegisterViewModel();

        // Act
        var cut = Render<Login>();

        // Assert
        Assert.NotNull(cut.Find("[data-testid='login-card']"));
        Assert.NotNull(cut.Find("[data-testid='email-input']"));
        Assert.NotNull(cut.Find("[data-testid='password-input']"));
        Assert.NotNull(cut.Find("[data-testid='sign-in-button']"));
    }

    [Fact]
    public void GivenTheLoginScreen_WhenRendered_ThenItUsesAResponsiveContainer()
    {
        // Arrange
        RegisterViewModel();

        // Act
        var cut = Render<Login>();

        // Assert
        // MudContainer emits the responsive `mud-container` class plus a max-width
        // modifier — the single shared UI adapts per platform via this responsive
        // container rather than per-host layouts (AC-4).
        var container = cut.Find(".mud-container");
        Assert.Contains("mud-container-maxwidth-xs", container.GetAttribute("class"));
    }

    [Fact]
    public void GivenTheLoginScreen_WhenRendered_ThenAllDataTestIdHooksArePresent()
    {
        // Arrange
        RegisterViewModel();

        // Act
        var cut = Render<Login>();

        // Assert
        Assert.NotNull(cut.Find("[data-testid='login-card']"));
        Assert.NotNull(cut.Find("[data-testid='email-input']"));
        Assert.NotNull(cut.Find("[data-testid='password-input']"));
        Assert.NotNull(cut.Find("[data-testid='sign-in-button']"));
    }

    [Fact]
    public void GivenTheLoginScreen_WhenRendered_ThenTheBrandMarkIsPresent()
    {
        // Arrange
        RegisterViewModel();

        // Act
        var cut = Render<Login>();

        // Assert
        Assert.NotNull(cut.Find("[data-testid='brand-mark']"));
    }

    [Fact]
    public void GivenAFailedLogin_WhenRendered_ThenInlineErrorShownAndFormRemains()
    {
        // Arrange
        _authService
            .Setup(a => a.LoginAsync(It.IsAny<LoginRequest>()))
            .ReturnsAsync((LoginResponse?)null);
        RegisterViewModel();
        var cut = Render<Login>();

        // Act
        cut.Find("[data-testid='sign-in-button']").Click();

        // Assert
        var error = cut.Find("[data-testid='login-error']");
        Assert.Contains(LoginViewModel.InvalidCredentialsMessage, error.TextContent);
        Assert.NotNull(cut.Find("[data-testid='email-input']"));
        Assert.NotNull(cut.Find("[data-testid='password-input']"));
        Assert.NotNull(cut.Find("[data-testid='sign-in-button']"));
    }

    [Fact]
    public void GivenSuccessfulLogin_WhenLoginPageRenders_ThenNoRoleSelectionControlIsPresent()
    {
        // Arrange
        _authService
            .Setup(a => a.LoginAsync(It.IsAny<LoginRequest>()))
            .ReturnsAsync(new LoginResponse("eyJhbGciOiJIUzI1NiJ9.header.sig"));
        _authService
            .Setup(a => a.GetCurrentUserAsync())
            .ReturnsAsync(new UserProfile(Guid.NewGuid(), "Alex", UserRole.Dispatcher, ServiceTier.None, Guid.NewGuid()));
        RegisterViewModel();
        var cut = Render<Login>();

        // Act
        cut.Find("[data-testid='sign-in-button']").Click();

        // Assert
        // Routing is automatic by role — there must be no persona/role picker anywhere
        // on the login flow (AC-5). Navigation is delegated to IPersonaNavigator.
        Assert.Empty(cut.FindAll("[data-testid='role-selector']"));
        _navigator.Verify(n => n.NavigateToPersonaHome(UserRole.Dispatcher), Times.Once);
    }
}
