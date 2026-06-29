using System.Net;
using System.Net.Http.Headers;
using ServiceDelivery.Client.Core.Exceptions;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.UI.Features.Authentication.Services;

namespace ServiceDelivery.Client.Tests.Authentication;

/// <summary>
/// Composition-root test for the outbound auth pipeline (QUAL-007). The per-handler tests
/// (<see cref="SessionExpiryHttpHandlerTests"/>, <see cref="AuthTokenHttpHandlerTests"/>) prove each
/// <see cref="DelegatingHandler"/> in isolation — but the BUG-024 / BUG-028 defects were about the
/// handlers' <em>position and interaction</em> in the real chain, which an isolated test cannot see.
/// These tests assemble the chain in the exact order every host registers it
/// (<c>SessionExpiryHttpHandler → AuthTokenHttpHandler → network</c>; see Web <c>Program.cs</c> and
/// Mobile <c>MauiProgram.cs</c>), stubbing only the network leaf, and assert the integrated behaviour:
/// the bearer token reaches the wire (BUG-028) and a login 401 is passed through rather than turned
/// into a <see cref="SessionExpiredException"/> (BUG-024).
/// </summary>
public class AuthHandlerPipelineCompositionTests
{
    private readonly Mock<ITokenStore> _tokenStore = new();
    private readonly Mock<ISessionExpiryHandler> _expiryHandler = new();

    /// <summary>Stands in for the real <c>HttpClientHandler</c> network leaf: records the request as it
    /// arrives at the wire (after both delegating handlers have run) and returns a configured status.</summary>
    private sealed class CapturingLeafHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;

        public CapturingLeafHandler(HttpStatusCode status) => _status = status;

        public HttpRequestMessage? RequestAtWire { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestAtWire = request;
            return Task.FromResult(new HttpResponseMessage(_status));
        }
    }

    // Mirrors the host composition root exactly: expiry handler outermost, auth handler inner, network leaf last.
    private (HttpClient client, CapturingLeafHandler wire) BuildHostPipeline(HttpStatusCode leafStatus)
    {
        var wire = new CapturingLeafHandler(leafStatus);
        var authHandler = new AuthTokenHttpHandler(_tokenStore.Object) { InnerHandler = wire };
        var expiryHandler = new SessionExpiryHttpHandler(_expiryHandler.Object) { InnerHandler = authHandler };
        var client = new HttpClient(expiryHandler) { BaseAddress = new Uri("http://localhost:5180") };
        return (client, wire);
    }

    [Fact]
    public async Task GivenTheRealHandlerChain_WhenAnAuthenticatedRequestIsSent_ThenTheBearerTokenReachesTheNetworkLeaf()
    {
        // Arrange
        _tokenStore.Setup(t => t.GetTokenAsync()).ReturnsAsync("jwt-abc-123");
        var (client, wire) = BuildHostPipeline(HttpStatusCode.OK);

        // Act
        await client.GetAsync("/vehicles/available");

        // Assert — the composed pipeline (not a handler in isolation) attaches the token at the wire (BUG-028).
        Assert.NotNull(wire.RequestAtWire!.Headers.Authorization);
        Assert.Equal("Bearer", wire.RequestAtWire.Headers.Authorization!.Scheme);
        Assert.Equal("jwt-abc-123", wire.RequestAtWire.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task GivenTheRealHandlerChain_WhenTheLoginEndpointReturns401_ThenItIsPassedThroughWithNoTokenAndNoSessionExpiry()
    {
        // Arrange — no stored token yet (login is the unauthenticated call) and the backend rejects the credentials.
        _tokenStore.Setup(t => t.GetTokenAsync()).ReturnsAsync((string?)null);
        var (client, wire) = BuildHostPipeline(HttpStatusCode.Unauthorized);

        // Act
        var response = await client.PostAsync("/auth/login", new StringContent(""));

        // Assert — the outermost expiry handler must NOT convert the login 401 into a SessionExpiredException
        // (BUG-024); the caller sees the 401 and surfaces an inline error. No bearer header is attached.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(wire.RequestAtWire!.Headers.Authorization);
        _expiryHandler.Verify(h => h.HandleExpiredSessionAsync(), Times.Never);
    }

    [Fact]
    public async Task GivenTheRealHandlerChain_WhenAnAuthenticatedRequestReturns401_ThenTheTokenWasAttachedAndSessionExpiryFires()
    {
        // Arrange
        _tokenStore.Setup(t => t.GetTokenAsync()).ReturnsAsync("expired-jwt");
        var (client, wire) = BuildHostPipeline(HttpStatusCode.Unauthorized);

        // Act
        await Assert.ThrowsAsync<SessionExpiredException>(() => client.GetAsync("/users/me"));

        // Assert — proves the cross-handler order: the inner auth handler attached the token before the
        // request hit the wire, and the outer expiry handler reacted to the authenticated 401.
        Assert.Equal("Bearer", wire.RequestAtWire!.Headers.Authorization!.Scheme);
        Assert.Equal("expired-jwt", wire.RequestAtWire.Headers.Authorization.Parameter);
        _expiryHandler.Verify(h => h.HandleExpiredSessionAsync(), Times.Once);
    }
}
