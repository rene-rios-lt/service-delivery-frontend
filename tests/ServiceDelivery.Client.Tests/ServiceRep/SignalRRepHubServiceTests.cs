using System.Net.Http;
using System.Threading.Tasks;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.UI.Features.ServiceRep.Services;

namespace ServiceDelivery.Client.Tests.ServiceRep;

public class SignalRRepHubServiceTests
{
    private readonly Mock<ITokenStore> _tokenStore = new();

    private SignalRRepHubService CreateService()
    {
        var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5180") };
        return new SignalRRepHubService(httpClient, _tokenStore.Object);
    }

    [Fact]
    public async Task GivenAStoredToken_WhenTheHubRequestsAnAccessToken_ThenTheStoredTokenIsProvided()
    {
        // Arrange — the RepHub is [Authorize]; SignalR appends ?access_token=... from this provider
        // because websockets cannot carry an Authorization header. Without it the rep's hub connection
        // is unauthenticated and never joins its rep group, so it never receives JobOfferReceived.
        _tokenStore.Setup(t => t.GetTokenAsync()).ReturnsAsync("rep-jwt-xyz");
        var service = CreateService();

        // Act
        var token = await service.ProvideAccessTokenAsync();

        // Assert
        Assert.Equal("rep-jwt-xyz", token);
    }

    [Fact]
    public async Task GivenNoStoredToken_WhenTheHubRequestsAnAccessToken_ThenNullIsProvidedWithoutThrowing()
    {
        // Arrange
        _tokenStore.Setup(t => t.GetTokenAsync()).ReturnsAsync((string?)null);
        var service = CreateService();

        // Act
        var token = await service.ProvideAccessTokenAsync();

        // Assert
        Assert.Null(token);
    }
}
