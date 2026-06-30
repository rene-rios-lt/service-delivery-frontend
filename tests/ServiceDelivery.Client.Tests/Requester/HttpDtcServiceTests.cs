using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ServiceDelivery.Client.UI.Features.Requester.Services;

namespace ServiceDelivery.Client.Tests.Requester;

/// <summary>
/// Unit tests for <see cref="HttpDtcService"/> (FE-015 AC-2). Maps the backend GET /dtcs contract —
/// <c>DtcDto(Guid Id, string Code, string Title, string RequiredEquipment)</c> — to the client
/// <c>IReadOnlyList&lt;DtcItem&gt;</c> over a stubbed <see cref="HttpClient"/> (no live network).
/// </summary>
public class HttpDtcServiceTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public HttpRequestMessage? LastRequest { get; private set; }

        public StubHandler(HttpStatusCode status, string body = "")
        {
            _status = status;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            });
        }
    }

    private static HttpDtcService CreateService(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

    [Fact]
    public async Task GivenValidDtcJson_WhenGetDtcsAsyncCalled_ThenListContainsCodeAndTitle()
    {
        // Arrange
        const string json =
            """
            [
              { "id": "7a1e4c2b-1111-4f3a-9a2b-0c1d2e3f4a5b",
                "code": "P0700", "title": "Transmission Control Fault",
                "requiredEquipment": "Hydraulics" }
            ]
            """;
        var service = CreateService(new StubHandler(HttpStatusCode.OK, json));

        // Act
        var dtcs = await service.GetDtcsAsync();

        // Assert
        Assert.Single(dtcs);
        Assert.Equal(Guid.Parse("7a1e4c2b-1111-4f3a-9a2b-0c1d2e3f4a5b"), dtcs[0].Id);
        Assert.Equal("P0700", dtcs[0].Code);
        Assert.Equal("Transmission Control Fault", dtcs[0].Title);
    }

    [Fact]
    public async Task GivenTheDtcsEndpoint_WhenGetDtcsAsyncCalled_ThenItCallsTheDtcsRoute()
    {
        // Arrange
        var handler = new StubHandler(HttpStatusCode.OK, "[]");
        var service = CreateService(handler);

        // Act
        await service.GetDtcsAsync();

        // Assert
        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.EndsWith("dtcs", handler.LastRequest!.RequestUri!.AbsolutePath);
    }
}
