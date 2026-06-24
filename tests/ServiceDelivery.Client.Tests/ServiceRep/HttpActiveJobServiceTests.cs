using System.Net;
using System.Text;
using ServiceDelivery.Client.UI.Features.ServiceRep.Services;

namespace ServiceDelivery.Client.Tests.ServiceRep;

public class HttpActiveJobServiceTests
{
    // Mirrors the REAL backend GET /service-requests/my-active response shape
    // (ServiceDelivery.Application.Features.ServiceRequests.Queries.MyActiveServiceRequestDto):
    // { requestId, tier, dtcTitle, status, requesterLatitude, requesterLongitude, createdAt }.
    // ASP.NET Core serializes property names as camelCase by default. The service maps the fields it
    // needs onto ActiveJobContext; fields the backend does not yet expose (rep position, ETA, rep
    // state, requester name) are not present in this payload — see HttpActiveJobService for how they
    // are handled and the implementation report for the flagged backend gap.
    private const string MyActiveJson =
        """
        {
          "requestId": "a1111111-1111-1111-1111-111111111111",
          "tier": "Gold",
          "dtcTitle": "P0700 · Transmission Control Fault",
          "status": "Assigned",
          "requesterLatitude": 41.60,
          "requesterLongitude": -93.60,
          "createdAt": "2026-06-23T12:00:00Z"
        }
        """;

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string? _body;

        public HttpRequestMessage? LastRequest { get; private set; }

        public StubHandler(HttpStatusCode status, string? body = null)
        {
            _status = status;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            var response = new HttpResponseMessage(_status);
            if (_body is not null)
                response.Content = new StringContent(_body, Encoding.UTF8, "application/json");
            return Task.FromResult(response);
        }
    }

    private static HttpActiveJobService CreateService(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

    [Fact]
    public async Task GivenHttpActiveJobService_WhenGetActiveJobCalledAndSucceeds_ThenReturnsActiveJobContext()
    {
        // Arrange
        // AC-5: the active-job view loads the rep's current job from GET /service-requests/my-active.
        var handler = new StubHandler(HttpStatusCode.OK, MyActiveJson);
        var service = CreateService(handler);

        // Act
        var context = await service.GetActiveJobAsync();

        // Assert
        Assert.NotNull(context);
        Assert.Equal(Guid.Parse("a1111111-1111-1111-1111-111111111111"), context!.RequestId);
        Assert.Equal("P0700 · Transmission Control Fault", context.DtcTitle);
        Assert.Equal(41.60, context.RequesterLat);
        Assert.Equal(-93.60, context.RequesterLng);
        Assert.EndsWith("service-requests/my-active", handler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GivenHttpActiveJobService_WhenGetActiveJobCalledAnd404Returned_ThenReturnsNull()
    {
        // Arrange
        // AC-5: when the rep has no active request the backend returns 404 — the service maps that to
        // null so the page can show "no active job" rather than treating it as an error.
        var handler = new StubHandler(HttpStatusCode.NotFound);
        var service = CreateService(handler);

        // Act
        var context = await service.GetActiveJobAsync();

        // Assert
        Assert.Null(context);
    }
}
