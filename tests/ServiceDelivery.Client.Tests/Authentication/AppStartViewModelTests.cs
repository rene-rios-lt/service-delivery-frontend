using System;
using System.Text;
using System.Text.Json;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.ViewModels;

namespace ServiceDelivery.Client.Tests.Authentication;

public class AppStartViewModelTests
{
    private const string LoginRoute = "/login";

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
    public async Task GivenAValidStoredJwt_WhenResolvingTheStartRoute_ThenNoRedirectToLoginIsReturned()
    {
        // Arrange
        var token = TokenWithExp(DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeSeconds());
        _tokenStore.Setup(t => t.GetTokenAsync()).ReturnsAsync(token);
        var viewModel = CreateViewModel();

        // Act
        var route = await viewModel.ResolveStartRouteAsync();

        // Assert
        Assert.NotEqual(LoginRoute, route);
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
