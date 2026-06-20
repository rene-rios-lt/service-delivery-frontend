using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.ViewModels;

namespace ServiceDelivery.Client.Tests.Authentication;

public class AppStartViewModelTests
{
    private const string LoginRoute = "/login";
    private const string StoredJwt = "eyJhbGciOiJIUzI1NiJ9.header.persisted-session-token";

    private readonly Mock<ITokenStore> _tokenStore = new();

    private AppStartViewModel CreateViewModel() => new(_tokenStore.Object);

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
    public async Task GivenAStoredJwt_WhenResolvingTheStartRoute_ThenNoRedirectToLoginIsReturned()
    {
        // Arrange
        _tokenStore.Setup(t => t.GetTokenAsync()).ReturnsAsync(StoredJwt);
        var viewModel = CreateViewModel();

        // Act
        var route = await viewModel.ResolveStartRouteAsync();

        // Assert
        Assert.NotEqual(LoginRoute, route);
    }
}
