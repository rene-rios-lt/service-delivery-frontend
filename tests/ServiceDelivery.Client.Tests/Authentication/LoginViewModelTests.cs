using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.ViewModels;

namespace ServiceDelivery.Client.Tests.Authentication;

public class LoginViewModelTests
{
    // Distinct, contract-faithful fixtures. The JWT literal is NOT reused as any
    // other identity (email, role, route) so each assertion has teeth.
    private const string ValidEmail = "alex@dealer.com";
    private const string ValidPassword = "Passw0rd!seed";
    private const string IssuedJwt = "eyJhbGciOiJIUzI1NiJ9.header.signature-issued-by-backend";

    private readonly Mock<IAuthService> _authService = new();
    private readonly Mock<ITokenStore> _tokenStore = new();
    private readonly Mock<IPersonaNavigator> _navigator = new();

    private LoginViewModel CreateViewModel() =>
        new(_authService.Object, _tokenStore.Object, _navigator.Object)
        {
            Email = ValidEmail,
            Password = ValidPassword
        };

    private void ArrangeSuccessfulLogin(UserRole role)
    {
        _authService
            .Setup(a => a.LoginAsync(It.Is<LoginRequest>(r => r.Email == ValidEmail && r.Password == ValidPassword)))
            .ReturnsAsync(new LoginResponse(IssuedJwt));
        _authService
            .Setup(a => a.GetCurrentUserAsync())
            .ReturnsAsync(new UserProfile(Guid.NewGuid(), "Alex Dispatcher", role, ServiceTier.None, Guid.NewGuid()));
    }

    [Fact]
    public async Task GivenValidCredentials_WhenLoginSucceeds_ThenTokenIsStored()
    {
        // Arrange
        ArrangeSuccessfulLogin(UserRole.Dispatcher);
        var viewModel = CreateViewModel();

        // Act
        await viewModel.LoginAsync();

        // Assert
        _tokenStore.Verify(t => t.SetTokenAsync(IssuedJwt), Times.Once);
    }

    [Fact]
    public async Task GivenValidCredentials_WhenLoginSucceeds_ThenCurrentUserRoleIsRead()
    {
        // Arrange
        ArrangeSuccessfulLogin(UserRole.Dispatcher);
        var viewModel = CreateViewModel();

        // Act
        await viewModel.LoginAsync();

        // Assert
        _authService.Verify(a => a.GetCurrentUserAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenDispatcherRole_WhenLoginSucceeds_ThenNavigatesToDispatcherHome()
    {
        // Arrange
        ArrangeSuccessfulLogin(UserRole.Dispatcher);
        var viewModel = CreateViewModel();

        // Act
        await viewModel.LoginAsync();

        // Assert
        _navigator.Verify(n => n.NavigateToPersonaHome(UserRole.Dispatcher), Times.Once);
    }

    [Fact]
    public async Task GivenServiceRepRole_WhenLoginSucceeds_ThenNavigatesToServiceRepHome()
    {
        // Arrange
        ArrangeSuccessfulLogin(UserRole.ServiceRep);
        var viewModel = CreateViewModel();

        // Act
        await viewModel.LoginAsync();

        // Assert
        _navigator.Verify(n => n.NavigateToPersonaHome(UserRole.ServiceRep), Times.Once);
    }

    [Fact]
    public async Task GivenRequesterRole_WhenLoginSucceeds_ThenNavigatesToRequesterHome()
    {
        // Arrange
        ArrangeSuccessfulLogin(UserRole.Requester);
        var viewModel = CreateViewModel();

        // Act
        await viewModel.LoginAsync();

        // Assert
        _navigator.Verify(n => n.NavigateToPersonaHome(UserRole.Requester), Times.Once);
    }

    [Fact]
    public async Task GivenInvalidCredentials_WhenLoginFails_ThenErrorMessageIsSet()
    {
        // Arrange
        _authService
            .Setup(a => a.LoginAsync(It.IsAny<LoginRequest>()))
            .ReturnsAsync((LoginResponse?)null);
        var viewModel = CreateViewModel();

        // Act
        await viewModel.LoginAsync();

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(viewModel.ErrorMessage));
    }

    [Fact]
    public async Task GivenInvalidCredentials_WhenLoginFails_ThenNoTokenStoredAndNoNavigation()
    {
        // Arrange
        _authService
            .Setup(a => a.LoginAsync(It.IsAny<LoginRequest>()))
            .ReturnsAsync((LoginResponse?)null);
        var viewModel = CreateViewModel();

        // Act
        await viewModel.LoginAsync();

        // Assert
        _tokenStore.Verify(t => t.SetTokenAsync(It.IsAny<string>()), Times.Never);
        _navigator.Verify(n => n.NavigateToPersonaHome(It.IsAny<UserRole>()), Times.Never);
        _authService.Verify(a => a.GetCurrentUserAsync(), Times.Never);
    }

    [Fact]
    public async Task GivenSuccessfulLogin_WhenRouting_ThenNavigatesDirectlyWithoutRoleSelection()
    {
        // Arrange
        ArrangeSuccessfulLogin(UserRole.Dispatcher);
        var viewModel = CreateViewModel();

        // Act
        await viewModel.LoginAsync();

        // Assert
        // Routing is automatic: exactly one navigation, driven by the JWT-backed role
        // from /users/me — there is no intermediate role-selection step that would
        // require the user to pick a persona.
        _navigator.Verify(n => n.NavigateToPersonaHome(UserRole.Dispatcher), Times.Once);
        _navigator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GivenLoginInFlight_WhenAwaitingTheAuthCall_ThenIsBusyIsTrueThenFalseOnCompletion()
    {
        // Arrange
        var loginInFlight = new TaskCompletionSource<LoginResponse?>();
        _authService.Setup(a => a.LoginAsync(It.IsAny<LoginRequest>())).Returns(loginInFlight.Task);
        _authService
            .Setup(a => a.GetCurrentUserAsync())
            .ReturnsAsync(new UserProfile(Guid.NewGuid(), "Alex", UserRole.Dispatcher, ServiceTier.None, Guid.NewGuid()));
        var viewModel = CreateViewModel();

        // Act
        var loginTask = viewModel.LoginAsync();
        var busyWhileInFlight = viewModel.IsBusy;
        loginInFlight.SetResult(new LoginResponse(IssuedJwt));
        await loginTask;

        // Assert
        Assert.True(busyWhileInFlight);
        Assert.False(viewModel.IsBusy);
    }
}
