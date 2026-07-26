using System.Net;
using System.Text;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.UI.Features.Dispatcher.Services;

namespace ServiceDelivery.Client.Tests.Dispatcher;

/// <summary>
/// Wire-contract deserialization for <see cref="HttpActiveRequestQueueService"/> (ADR-0011). Round-trips a
/// REAL <c>GET /service-requests</c> JSON array — the backend <c>ActiveServiceRequestDto</c> shape
/// (<c>{ requestId, requesterName, tier, dtcTitle, status, assignedRepId, assignedRepName, createdAt }</c>,
/// flat assigned-rep fields, <c>tier</c>/<c>status</c> as enum-name strings) — through the same
/// System.Text.Json (Web defaults) path the service uses, asserting each field binds and maps by a distinct
/// value so field-name drift cannot pass coincidentally. Also proves the route and the
/// <c>GetRequestAsync</c> follow-up lookup.
/// </summary>
public class HttpActiveRequestQueueServiceTests
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

    private static HttpActiveRequestQueueService CreateService(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

    private const string RealRequestsJson =
        """
        [
          {
            "requestId": "aaaaaaaa-0000-0000-0000-000000000009",
            "requesterName": "Dana Cole",
            "tier": "Silver",
            "dtcTitle": "Hydraulic Pressure Loss",
            "status": "Assigned",
            "assignedRepId": "50000000-0000-0000-0000-000000000001",
            "assignedRepName": "J. Tran",
            "createdAt": "2026-07-25T12:04:00Z"
          }
        ]
        """;

    [Fact]
    public async Task GivenBackendRequestsJson_WhenGetActiveRequestsAsyncDeserializes_ThenAllFieldsBoundCorrectly()
    {
        // Arrange
        var handler = new StubHandler(HttpStatusCode.OK, RealRequestsJson);
        var service = CreateService(handler);

        // Act
        var requests = await service.GetActiveRequestsAsync();

        // Assert
        var entry = Assert.Single(requests);
        Assert.Equal(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000009"), entry.RequestId);
        Assert.Equal("Dana Cole", entry.RequesterName);
        Assert.Equal(ServiceTier.Silver, entry.Tier);
        Assert.Equal("Hydraulic Pressure Loss", entry.DtcTitle);
        Assert.Equal("Assigned", entry.Status);
        Assert.Equal("J. Tran", entry.AssignedRepName);
        Assert.Equal(new DateTimeOffset(2026, 7, 25, 12, 4, 0, TimeSpan.Zero), entry.CreatedAt);
    }

    [Fact]
    public async Task GivenAnUnassignedRequest_WhenDeserialized_ThenAssignedRepNameIsNull()
    {
        // Arrange — a Pending request: the backend sends null assignedRepId + assignedRepName.
        const string json =
            """
            [
              {
                "requestId": "bbbbbbbb-0000-0000-0000-000000000001",
                "requesterName": "Marcus Webb",
                "tier": "Gold",
                "dtcTitle": "Transmission Control Fault",
                "status": "Pending",
                "assignedRepId": null,
                "assignedRepName": null,
                "createdAt": "2026-07-25T12:00:00Z"
              }
            ]
            """;
        var handler = new StubHandler(HttpStatusCode.OK, json);
        var service = CreateService(handler);

        // Act
        var requests = await service.GetActiveRequestsAsync();

        // Assert
        var entry = Assert.Single(requests);
        Assert.Equal(ServiceTier.Gold, entry.Tier);
        Assert.Null(entry.AssignedRepName);
    }

    [Fact]
    public async Task GivenServiceRequestsEndpoint_WhenGetActiveRequestsAsync_ThenItCallsTheServiceRequestsRoute()
    {
        // Arrange
        var handler = new StubHandler(HttpStatusCode.OK, "[]");
        var service = CreateService(handler);

        // Act
        await service.GetActiveRequestsAsync();

        // Assert
        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.EndsWith("service-requests", handler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GivenARequestIdInTheActiveList_WhenGetRequestAsync_ThenReturnsTheMatchingEntry()
    {
        // Arrange — the follow-up fetch after a ServiceRequestPending event resolves the full entry.
        var handler = new StubHandler(HttpStatusCode.OK, RealRequestsJson);
        var service = CreateService(handler);

        // Act
        var entry = await service.GetRequestAsync(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000009"));

        // Assert
        Assert.NotNull(entry);
        Assert.Equal("Dana Cole", entry!.RequesterName);
    }

    [Fact]
    public async Task GivenARequestIdNotInTheActiveList_WhenGetRequestAsync_ThenReturnsNull()
    {
        // Arrange — the request was already completed server-side, so it is absent from the active list.
        var handler = new StubHandler(HttpStatusCode.OK, RealRequestsJson);
        var service = CreateService(handler);

        // Act
        var entry = await service.GetRequestAsync(Guid.NewGuid());

        // Assert
        Assert.Null(entry);
    }
}
