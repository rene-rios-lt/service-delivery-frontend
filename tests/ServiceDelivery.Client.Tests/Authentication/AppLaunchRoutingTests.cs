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

    private static string ValidStoredJwt()
    {
        var header = Base64UrlEncode(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
        var payload = Base64UrlEncode(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { exp = DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeSeconds() })));
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
    public void GivenAValidStoredJwt_WhenAppLaunches_ThenLoginScreenIsNotShown()
    {
        // Arrange
        _tokenStore.Setup(t => t.GetTokenAsync()).ReturnsAsync(ValidStoredJwt());
        RegisterStartViewModel();
        var navigation = Services.GetRequiredService<NavigationManager>();

        // Act
        Render<Home>();

        // Assert
        Assert.DoesNotContain("/login", navigation.Uri);
    }
}
