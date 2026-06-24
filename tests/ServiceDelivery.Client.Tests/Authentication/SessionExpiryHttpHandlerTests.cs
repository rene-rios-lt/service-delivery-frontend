using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ServiceDelivery.Client.Core.Exceptions;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.UI.Features.Authentication.Services;

namespace ServiceDelivery.Client.Tests.Authentication;

public class SessionExpiryHttpHandlerTests
{
    private readonly Mock<ISessionExpiryHandler> _expiryHandler = new();

    private sealed class StubInnerHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly Action? _onSend;

        public StubInnerHandler(HttpStatusCode status, Action? onSend = null)
        {
            _status = status;
            _onSend = onSend;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _onSend?.Invoke();
            return Task.FromResult(new HttpResponseMessage(_status));
        }
    }

    private HttpClient CreateClient(HttpStatusCode innerStatus, Action? onSend = null)
    {
        var handler = new SessionExpiryHttpHandler(_expiryHandler.Object)
        {
            InnerHandler = new StubInnerHandler(innerStatus, onSend)
        };
        return new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
    }

    [Fact]
    public async Task GivenA401Response_WhenSentThroughHandler_ThenSessionExpiryIsHandled()
    {
        // Arrange
        var client = CreateClient(HttpStatusCode.Unauthorized);

        // Act
        await Assert.ThrowsAsync<SessionExpiredException>(
            () => client.GetAsync("/anything"));

        // Assert
        _expiryHandler.Verify(h => h.HandleExpiredSessionAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenA200Response_WhenSentThroughHandler_ThenSessionExpiryIsNotInvoked()
    {
        // Arrange
        var client = CreateClient(HttpStatusCode.OK);

        // Act
        var response = await client.GetAsync("/anything");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _expiryHandler.Verify(h => h.HandleExpiredSessionAsync(), Times.Never);
    }

    [Fact]
    public async Task GivenA401MidAction_WhenSentThroughHandler_ThenSessionExpiredExceptionIsThrown()
    {
        // Arrange
        var client = CreateClient(HttpStatusCode.Unauthorized);

        // Act
        var act = () => client.PostAsync("/offers/accept", new StringContent(""));

        // Assert
        await Assert.ThrowsAsync<SessionExpiredException>(act);
    }

    [Fact]
    public async Task GivenA401MidAction_WhenHandlerThrows_ThenExpiryIsHandledBeforeThrowing()
    {
        // Arrange
        var sequence = new List<string>();
        _expiryHandler.Setup(h => h.HandleExpiredSessionAsync())
            .Callback(() => sequence.Add("handled"))
            .Returns(Task.CompletedTask);
        var client = CreateClient(HttpStatusCode.Unauthorized);

        // Act
        try
        {
            await client.GetAsync("/anything");
        }
        catch (SessionExpiredException)
        {
            sequence.Add("threw");
        }

        // Assert
        Assert.Equal(new[] { "handled", "threw" }, sequence);
    }

    [Fact]
    public async Task GivenA401ResponseFromLoginEndpoint_WhenSentThroughHandler_ThenResponseIsPassedThroughWithoutSessionExpiry()
    {
        // Arrange
        var client = CreateClient(HttpStatusCode.Unauthorized);

        // Act
        var response = await client.PostAsync("http://localhost:5180/auth/login", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        _expiryHandler.Verify(h => h.HandleExpiredSessionAsync(), Times.Never);
    }

    [Fact]
    public async Task GivenA401ResponseFromAuthenticatedEndpoint_WhenSentThroughHandler_ThenSessionExpiryIsHandled()
    {
        // Arrange
        var client = CreateClient(HttpStatusCode.Unauthorized);

        // Act
        await Assert.ThrowsAsync<SessionExpiredException>(
            () => client.GetAsync("http://localhost:5180/users/me"));

        // Assert
        _expiryHandler.Verify(h => h.HandleExpiredSessionAsync(), Times.Once);
    }
}
