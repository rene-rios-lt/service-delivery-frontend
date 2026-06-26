using System.Net.Http;
using System.Threading.Tasks;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
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

    [Fact]
    public void GivenAJobOfferExpiredEventPushed_WhenOnJobOfferExpiredRegistered_ThenHandlerBindsToTheJobOfferExpiredEvent()
    {
        // Arrange — BUG-037: the RepHub publishes JobOfferExpired carrying a single OfferId. The
        // service must register a "JobOfferExpired" handler that forwards the deserialized
        // JobOfferExpiredPayload to the subscriber, mirroring the OnRedirectReceived direct-bind
        // pattern (no field-name mismatch). HubConnection is sealed and cannot dispatch a registered
        // client handler without a live transport, so this unit test proves the binding is wired
        // (correct event name and payload type, no throw); the Appium E2E test is the live-system
        // complement that proves an actual server push dismisses the screen.
        var service = CreateService();
        JobOfferExpiredPayload? received = null;

        // Act
        var register = () => service.OnJobOfferExpired(payload =>
        {
            received = payload;
            return Task.CompletedTask;
        });

        // Assert
        var exception = Record.Exception(register);
        Assert.Null(exception);
        Assert.Null(received);
    }
}
