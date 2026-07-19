using System;
using System.Text;
using System.Text.Json;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.ViewModels;
using ServiceDelivery.Client.UI.Features.Dashboard.Pages;

namespace ServiceDelivery.Client.Tests.Authentication;

public class AppLaunchRoutingTests : BunitContext
{
    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    // Mints a token carrying a valid (future) exp and the given "role" claim string, mirroring the
    // backend token shape (JwtTokenService writes new Claim("role", user.Role.ToString())).
    private static string ValidStoredJwtForRole(string role)
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

    private readonly Mock<ITokenStore> _tokenStore = new();

    private void RegisterStartViewModel()
    {
        Services.AddSingleton(new AppStartViewModel(_tokenStore.Object));
    }

    [Fact]
    public void GivenNoStoredJwt_WhenAppLaunches_ThenLoginScreenIsShown()
    {
        // Arrange
        _tokenStore.Setup(t => t.GetTokenAsync()).ReturnsAsync((string?)null);
        RegisterStartViewModel();
        var navigation = Services.GetRequiredService<NavigationManager>();

        // Act
        Render<Home>();

        // Assert
        Assert.EndsWith("/login", navigation.Uri);
    }

    [Fact]
    public void GivenAValidStoredDispatcherJwt_WhenAppLaunches_ThenDispatcherHomeIsShown()
    {
        // Arrange
        _tokenStore.Setup(t => t.GetTokenAsync()).ReturnsAsync(ValidStoredJwtForRole("Dispatcher"));
        RegisterStartViewModel();
        var navigation = Services.GetRequiredService<NavigationManager>();

        // Act
        Render<Home>();

        // Assert
        Assert.EndsWith("/dispatcher", navigation.Uri);
    }

    [Fact]
    public void GivenAValidStoredServiceRepJwt_WhenAppLaunches_ThenServiceRepTakeOverIsShown()
    {
        // Arrange
        _tokenStore.Setup(t => t.GetTokenAsync()).ReturnsAsync(ValidStoredJwtForRole("ServiceRep"));
        RegisterStartViewModel();
        var navigation = Services.GetRequiredService<NavigationManager>();

        // Act
        Render<Home>();

        // Assert
        Assert.EndsWith("/rep/takeover", navigation.Uri);
    }

    [Fact]
    public void GivenAValidStoredRequesterJwt_WhenAppLaunches_ThenRequesterHomeIsShown()
    {
        // Arrange
        _tokenStore.Setup(t => t.GetTokenAsync()).ReturnsAsync(ValidStoredJwtForRole("Requester"));
        RegisterStartViewModel();
        var navigation = Services.GetRequiredService<NavigationManager>();

        // Act
        Render<Home>();

        // Assert
        Assert.EndsWith("/requester", navigation.Uri);
    }

    [Fact]
    public void GivenAValidStoredJwtWithUnrecognisedRole_WhenAppLaunches_ThenLoginScreenIsShown()
    {
        // Arrange — a valid, live token whose role is not a routable persona must still leave "/"
        // for /login, never render the authenticated shell over a blank body (BUG-050).
        _tokenStore.Setup(t => t.GetTokenAsync()).ReturnsAsync(ValidStoredJwtForRole("Overlord"));
        RegisterStartViewModel();
        var navigation = Services.GetRequiredService<NavigationManager>();

        // Act
        Render<Home>();

        // Assert
        Assert.EndsWith("/login", navigation.Uri);
    }
}
