using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.UI.Features.Authentication.Services;

namespace ServiceDelivery.Client.Tests.Authentication;

public class AuthTokenHttpHandlerTests
{
    private readonly Mock<ITokenStore> _tokenStore = new();

    private sealed class CapturingInnerHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private (HttpClient client, CapturingInnerHandler inner) CreateClient()
    {
        var inner = new CapturingInnerHandler();
        var handler = new AuthTokenHttpHandler(_tokenStore.Object) { InnerHandler = inner };
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5180") };
        return (client, inner);
    }

    [Fact]
    public async Task GivenAStoredToken_WhenARequestIsSent_ThenTheAuthorizationHeaderCarriesTheBearerToken()
    {
        // Arrange
        _tokenStore.Setup(t => t.GetTokenAsync()).ReturnsAsync("jwt-abc-123");
        var (client, inner) = CreateClient();

        // Act
        await client.GetAsync("/vehicles/available");

        // Assert
        Assert.NotNull(inner.LastRequest!.Headers.Authorization);
        Assert.Equal("Bearer", inner.LastRequest.Headers.Authorization!.Scheme);
        Assert.Equal("jwt-abc-123", inner.LastRequest.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task GivenNoStoredToken_WhenARequestIsSent_ThenNoAuthorizationHeaderIsAddedAndItDoesNotThrow()
    {
        // Arrange
        _tokenStore.Setup(t => t.GetTokenAsync()).ReturnsAsync((string?)null);
        var (client, inner) = CreateClient();

        // Act
        var response = await client.GetAsync("/auth/login");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(inner.LastRequest!.Headers.Authorization);
    }

    [Fact]
    public async Task GivenARequestThatAlreadyHasAnAuthorizationHeader_WhenSent_ThenItIsNotOverwritten()
    {
        // Arrange
        _tokenStore.Setup(t => t.GetTokenAsync()).ReturnsAsync("store-token");
        var (client, inner) = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/users/me")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", "explicit-token") }
        };

        // Act
        await client.SendAsync(request);

        // Assert
        Assert.Equal("explicit-token", inner.LastRequest!.Headers.Authorization!.Parameter);
    }
}
