using System;
using System.Text;
using System.Text.Json;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.ViewModels;

namespace ServiceDelivery.Client.Tests.Authentication;

public class AppStartViewModelTests
{
    private const string LoginRoute = "/login";
    private const string DispatcherHomeRoute = "/dispatcher";
    private const string ServiceRepTakeOverRoute = "/rep/takeover";
    private const string RequesterHomeRoute = "/requester";

    private readonly Mock<ITokenStore> _tokenStore = new();

    private AppStartViewModel CreateViewModel() => new(_tokenStore.Object);

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string TokenWithExp(long expUnixSeconds)
    {
        var header = Base64UrlEncode(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
        var payload = Base64UrlEncode(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { exp = expUnixSeconds })));
        return $"{header}.{payload}.signature";
    }

    // Mints a token carrying both a valid (future) exp and the given "role" claim string, mirroring
    // the backend token shape (JwtTokenService writes new Claim("role", user.Role.ToString())).
    private static string ValidTokenWithRole(string role)
    {
        var header = Base64UrlEncode(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
        var payload = Base64UrlEncode(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new
            {
                exp = DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeSeconds(),
                role
            })));
        return $"{header}.{payload}.signature";
    }

    [Fact]
    public async Task GivenNoStoredJwt_WhenResolvingTheStartRoute_ThenLoginRouteIsReturned()
    {
        // Arrange
        _tokenStore.Setup(t => t.GetTokenAsync()).ReturnsAsync((string?)null);
        var viewModel = CreateViewModel();

        // Act
        var route = await viewModel.ResolveStartRouteAsync();

        // Assert
        Assert.Equal(LoginRoute, route);
    }

    [Fact]
    public async Task GivenAValidStoredDispatcherJwt_WhenResolvingTheStartRoute_ThenDispatcherHomeRouteIsReturned()
    {
        // Arrange
        _tokenStore.Setup(t => t.GetTokenAsync()).ReturnsAsync(ValidTokenWithRole("Dispatcher"));
        var viewModel = CreateViewModel();

        // Act
        var route = await viewModel.ResolveStartRouteAsync();

        // Assert
        Assert.Equal(DispatcherHomeRoute, route);
    }

    [Fact]
    public async Task GivenAValidStoredServiceRepJwt_WhenResolvingTheStartRoute_ThenServiceRepTakeOverRouteIsReturned()
    {
        // Arrange
        _tokenStore.Setup(t => t.GetTokenAsync()).ReturnsAsync(ValidTokenWithRole("ServiceRep"));
        var viewModel = CreateViewModel();

        // Act
        var route = await viewModel.ResolveStartRouteAsync();

        // Assert
        Assert.Equal(ServiceRepTakeOverRoute, route);
    }

    [Fact]
    public async Task GivenAValidStoredRequesterJwt_WhenResolvingTheStartRoute_ThenRequesterHomeRouteIsReturned()
    {
        // Arrange
        _tokenStore.Setup(t => t.GetTokenAsync()).ReturnsAsync(ValidTokenWithRole("Requester"));
        var viewModel = CreateViewModel();

        // Act
        var route = await viewModel.ResolveStartRouteAsync();

        // Assert
        Assert.Equal(RequesterHomeRoute, route);
    }

    [Fact]
    public async Task GivenAStoredButExpiredToken_WhenAppLaunches_ThenLoginRouteIsResolved()
    {
        // Arrange
        var token = TokenWithExp(DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds());
        _tokenStore.Setup(t => t.GetTokenAsync()).ReturnsAsync(token);
        var viewModel = CreateViewModel();

        // Act
        var route = await viewModel.ResolveStartRouteAsync();

        // Assert
        Assert.Equal(LoginRoute, route);
    }

    [Fact]
    public async Task GivenAValidTokenWithNoRoleClaim_WhenResolvingTheStartRoute_ThenLoginRouteIsResolved()
    {
        // Arrange — a live-session token whose payload carries no "role" claim: an unusable session,
        // so it must route to /login rather than a blank authenticated page.
        var token = TokenWithExp(DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeSeconds());
        _tokenStore.Setup(t => t.GetTokenAsync()).ReturnsAsync(token);
        var viewModel = CreateViewModel();

        // Act
        var route = await viewModel.ResolveStartRouteAsync();

        // Assert
        Assert.Equal(LoginRoute, route);
    }

    [Fact]
    public async Task GivenAValidTokenWithUnrecognisedRole_WhenResolvingTheStartRoute_ThenLoginRouteIsResolved()
    {
        // Arrange — a role value that is not a known UserRole must be treated as an invalid session.
        _tokenStore.Setup(t => t.GetTokenAsync()).ReturnsAsync(ValidTokenWithRole("Overlord"));
        var viewModel = CreateViewModel();

        // Act
        var route = await viewModel.ResolveStartRouteAsync();

        // Assert
        Assert.Equal(LoginRoute, route);
    }

    [Fact]
    public async Task GivenAValidTokenWithARoleThatHasNoPersonaHome_WhenResolvingTheStartRoute_ThenLoginRouteIsResolved()
    {
        // Arrange — the Simulator role is a valid UserRole but has no frontend persona home; it must
        // route to /login, never a blank page.
        _tokenStore.Setup(t => t.GetTokenAsync()).ReturnsAsync(ValidTokenWithRole("Simulator"));
        var viewModel = CreateViewModel();

        // Act
        var route = await viewModel.ResolveStartRouteAsync();

        // Assert
        Assert.Equal(LoginRoute, route);
    }

    [Fact]
    public async Task GivenTokenStoreFails_WhenAppLaunches_ThenLoginRouteIsResolved()
    {
        // Arrange — simulates iOS Keychain unavailable on first launch (the race that causes the
        // "An unhandled error has occurred" Blazor banner if the exception is not caught).
        _tokenStore.Setup(t => t.GetTokenAsync())
            .ThrowsAsync(new InvalidOperationException("Keychain unavailable"));
        var viewModel = CreateViewModel();

        // Act
        var route = await viewModel.ResolveStartRouteAsync();

        // Assert
        Assert.Equal(LoginRoute, route);
    }
}
