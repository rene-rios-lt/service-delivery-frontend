using System.Net;
using System.Text;
using ServiceDelivery.Client.UI.Features.ServiceRep.Services;

namespace ServiceDelivery.Client.Tests.ServiceRep;

public class HttpActiveJobServiceTests
{
    // Mirrors the REAL backend GET rep/active-job-state response shape (BE-030)
    // (ServiceDelivery.Application.Features.ServiceRep.Queries.ActiveJobStateDto):
    // { requestId, requesterName, dtcTitle, requesterLat, requesterLng, repLat, repLng, etaMinutes,
    //   distanceMiles, tier, repState }. ASP.NET Core serializes property names as camelCase by default.
    // Every field carries a DISTINCT, realistic value so a regression that faked rep position to the
    // requester pin (old repLat==requesterLat), zeroed the ETA/distance, or dropped requesterName would
    // fail the assertions below (anti-masking — BUG-016/036). `repState` (the rep's proximity:
    // EnRoute/Within15Miles/OnSite) drives the "I've Arrived" enable rule.
    private const string ActiveJobStateJson =
        """
        {
          "requestId": "a1111111-1111-1111-1111-111111111111",
          "requesterName": "Marcus Webb",
          "dtcTitle": "P0700 · Transmission Control Fault",
          "requesterLat": 41.60,
          "requesterLng": -93.60,
          "repLat": 41.72,
          "repLng": -93.48,
          "etaMinutes": 9,
          "distanceMiles": 8.1,
          "tier": "Gold",
          "repState": "Within15Miles"
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
    public async Task GivenHttpActiveJobService_WhenGetActiveJobCalledAndSucceeds_ThenCallsActiveJobStateEndpoint()
    {
        // Arrange
        // BUG-039: the active-job view loads the rep's current job from the purpose-built
        // GET rep/active-job-state endpoint (BE-030), NOT the old service-requests/my-active query
        // (which carried no rep position, ETA, distance, or requester name).
        var handler = new StubHandler(HttpStatusCode.OK, ActiveJobStateJson);
        var service = CreateService(handler);

        // Act
        await service.GetActiveJobAsync();

        // Assert
        Assert.EndsWith("rep/active-job-state", handler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GivenHttpActiveJobService_WhenGetActiveJobCalledAndSucceeds_ThenReturnsContextWithRealRepPositionEtaDistanceAndRequesterName()
    {
        // Arrange
        // BUG-039: the rep/active-job-state payload carries the real rep position, server-computed ETA
        // and distance, and the requester name. The fixture's repLat/repLng differ from
        // requesterLat/requesterLng, and etaMinutes/distanceMiles are non-zero, so a regression back to
        // faking rep position == requester pin or zeroing ETA/distance would fail these assertions.
        var handler = new StubHandler(HttpStatusCode.OK, ActiveJobStateJson);
        var service = CreateService(handler);

        // Act
        var context = await service.GetActiveJobAsync();

        // Assert
        Assert.NotNull(context);
        Assert.Equal(Guid.Parse("a1111111-1111-1111-1111-111111111111"), context!.RequestId);
        Assert.Equal("Marcus Webb", context.RequesterName);
        Assert.Equal("P0700 · Transmission Control Fault", context.DtcTitle);
        Assert.Equal(41.60, context.RequesterLat);
        Assert.Equal(-93.60, context.RequesterLng);
        Assert.Equal(41.72, context.RepLat);
        Assert.Equal(-93.48, context.RepLng);
        Assert.NotEqual(context.RequesterLat, context.RepLat);
        Assert.NotEqual(context.RequesterLng, context.RepLng);
        Assert.Equal(9, context.EtaMinutes);
        Assert.Equal(8.1, context.DistanceMiles);
        Assert.Equal("Gold", context.Tier);
        // RepState comes from the payload's repState (proximity) — drives the "I've Arrived" enable rule.
        Assert.Equal("Within15Miles", context.RepState);
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
