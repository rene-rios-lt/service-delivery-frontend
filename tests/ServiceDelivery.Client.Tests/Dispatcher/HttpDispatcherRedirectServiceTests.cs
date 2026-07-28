using System.Net;
using System.Text;
using System.Text.Json;
using ServiceDelivery.Client.UI.Features.Dispatcher.Services;

namespace ServiceDelivery.Client.Tests.Dispatcher;

/// <summary>
/// FE-005 — the HTTP adapter for <c>POST /dispatcher/redirect</c>. Drives a stubbed
/// <see cref="HttpMessageHandler"/> so the test asserts the real request shape (method, route, JSON body
/// <c>{ repId, toRequestId }</c>) and the 200-response deserialization onto <c>RedirectRepResultDto</c>, plus
/// the fail-loud non-2xx contract the ViewModel's error path depends on — without a live backend.
/// </summary>
public class HttpDispatcherRedirectServiceTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }

        public StubHandler(HttpStatusCode status, string body = "")
        {
            _status = status;
            _body = body;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            };
        }
    }

    private static HttpDispatcherRedirectService CreateService(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

    private static readonly Guid RepId = Guid.Parse("50000000-0000-0000-0000-000000000001");
    private static readonly Guid ToRequestId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000009");

    private const string RealResultJson =
        """
        {
            "repId": "50000000-0000-0000-0000-000000000001",
            "fromRequestId": "bbbbbbbb-0000-0000-0000-000000000002",
            "toRequestId": "aaaaaaaa-0000-0000-0000-000000000009",
            "repState": "EnRoute"
        }
        """;

    [Fact]
    public async Task GivenRedirectRequest_WhenRedirectAsync_ThenItPostsToTheDispatcherRedirectRoute()
    {
        // Arrange
        var handler = new StubHandler(HttpStatusCode.OK, RealResultJson);
        var service = CreateService(handler);

        // Act
        await service.RedirectAsync(RepId, ToRequestId);

        // Assert
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.EndsWith("dispatcher/redirect", handler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GivenRedirectRequest_WhenRedirectAsync_ThenTheBodyCarriesRepIdAndToRequestId()
    {
        // Arrange
        var handler = new StubHandler(HttpStatusCode.OK, RealResultJson);
        var service = CreateService(handler);

        // Act
        await service.RedirectAsync(RepId, ToRequestId);

        // Assert — the posted JSON binds back to the same two ids (camelCase Web defaults).
        using var doc = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.Equal(RepId, doc.RootElement.GetProperty("repId").GetGuid());
        Assert.Equal(ToRequestId, doc.RootElement.GetProperty("toRequestId").GetGuid());
    }

    [Fact]
    public async Task GivenA200Response_WhenRedirectAsync_ThenTheResultDtoIsDeserialised()
    {
        // Arrange
        var handler = new StubHandler(HttpStatusCode.OK, RealResultJson);
        var service = CreateService(handler);

        // Act
        var result = await service.RedirectAsync(RepId, ToRequestId);

        // Assert
        Assert.Equal(RepId, result.RepId);
        Assert.Equal(Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002"), result.FromRequestId);
        Assert.Equal(ToRequestId, result.ToRequestId);
        Assert.Equal("EnRoute", result.RepState);
    }

    [Fact]
    public async Task GivenANon2xxResponse_WhenRedirectAsync_ThenItThrows()
    {
        // Arrange — the backend rejects the redirect (e.g. the rep moved state); the adapter must fail loud so
        // the ViewModel's AC-4 error path runs, not silently return a default result.
        var handler = new StubHandler(HttpStatusCode.UnprocessableEntity, "{}");
        var service = CreateService(handler);

        // Act
        var act = () => service.RedirectAsync(RepId, ToRequestId);

        // Assert
        await Assert.ThrowsAsync<HttpRequestException>(act);
    }
}
