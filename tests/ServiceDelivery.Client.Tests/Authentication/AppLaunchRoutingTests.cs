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
    private const string StoredJwt = "eyJhbGciOiJIUzI1NiJ9.header.persisted-session-token";

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
        _tokenStore.Setup(t => t.GetTokenAsync()).ReturnsAsync(StoredJwt);
        RegisterStartViewModel();
        var navigation = Services.GetRequiredService<NavigationManager>();

        // Act
        Render<Home>();

        // Assert
        Assert.DoesNotContain("/login", navigation.Uri);
    }
}
