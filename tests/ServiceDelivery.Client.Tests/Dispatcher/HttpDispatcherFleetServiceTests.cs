using System.Net;
using System.Text;
using ServiceDelivery.Client.UI.Features.Dispatcher.Services;

namespace ServiceDelivery.Client.Tests.Dispatcher;

/// <summary>
/// AC-2c — wire-contract deserialization for <see cref="HttpDispatcherFleetService"/>. Round-trips a REAL
/// <c>GET /dispatcher/fleet</c> JSON array (the backend <c>DispatcherFleetEntryDto</c> shape:
/// <c>{ repId, name, state, vehicleId, registration, lastPosition { lat, lng }, activeRequestId,
/// activeRequestTier, activeRequestTitle, humanControlled }</c> — NOT the flat shape the plan text sketched;
/// <c>activeRequestTitle</c> added by BE-032) through the same System.Text.Json (Web defaults) path the
/// service uses, asserting each field binds and maps by a distinct value so field-name drift cannot pass
/// coincidentally (ADR-0011 / the frontend CLAUDE.md wire-contract rule).
/// </summary>
public class HttpDispatcherFleetServiceTests
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

    private static HttpDispatcherFleetService CreateService(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

    private const string RealFleetJson =
        """
        [
          {
            "repId": "50000000-0000-0000-0000-000000000001",
            "name": "J. Tran",
            "state": "EnRoute",
            "vehicleId": "30000000-0000-0000-0000-000000000007",
            "registration": "IA-4471",
            "lastPosition": { "lat": 41.8781, "lng": -93.0977 },
            "activeRequestId": "aaaaaaaa-0000-0000-0000-000000000009",
            "activeRequestTier": "Silver",
            "activeRequestTitle": "Hydraulic Pressure Loss",
            "humanControlled": true
          }
        ]
        """;

    [Fact]
    public async Task GivenBackendFleetJson_WhenGetFleetAsyncDeserializes_ThenAllFieldsBoundCorrectly()
    {
        // Arrange
        var handler = new StubHandler(HttpStatusCode.OK, RealFleetJson);
        var service = CreateService(handler);

        // Act
        var fleet = await service.GetFleetAsync();

        // Assert
        var entry = Assert.Single(fleet);
        Assert.Equal("30000000-0000-0000-0000-000000000007", entry.VehicleId);
        Assert.Equal("IA-4471", entry.Registration);
        Assert.Equal("EnRoute", entry.RepState);
        Assert.Equal(Guid.Parse("50000000-0000-0000-0000-000000000001"), entry.RepId);
        Assert.Equal("J. Tran", entry.RepName);
        Assert.Equal(41.8781, entry.Latitude);
        Assert.Equal(-93.0977, entry.Longitude);
        Assert.Equal("Silver", entry.ActiveRequestTier);
        Assert.Equal("Hydraulic Pressure Loss", entry.ActiveRequestTitle);
        Assert.True(entry.HumanControlled);
    }

    [Fact]
    public async Task GivenAnUnclaimedFleetEntryWithNoPosition_WhenDeserialized_ThenRepIdIsNullAndPositionIsZero()
    {
        // Arrange — the backend sends Guid.Empty repId + null lastPosition for an unclaimed, never-positioned
        // vehicle; the map model must present that as RepId null and lat/lng 0 rather than throwing.
        const string json =
            """
            [
              {
                "repId": "00000000-0000-0000-0000-000000000000",
                "name": null,
                "state": "Offline",
                "vehicleId": "30000000-0000-0000-0000-000000000008",
                "registration": "IA-9000",
                "lastPosition": null,
                "activeRequestId": null,
                "activeRequestTier": null,
                "activeRequestTitle": null,
                "humanControlled": false
              }
            ]
            """;
        var handler = new StubHandler(HttpStatusCode.OK, json);
        var service = CreateService(handler);

        // Act
        var fleet = await service.GetFleetAsync();

        // Assert
        var entry = Assert.Single(fleet);
        Assert.Null(entry.RepId);
        Assert.Null(entry.RepName);
        Assert.Equal(0, entry.Latitude);
        Assert.Equal(0, entry.Longitude);
        Assert.Null(entry.ActiveRequestTier);
        Assert.Null(entry.ActiveRequestTitle);
    }

    [Fact]
    public async Task GivenFleetEndpoint_WhenGetFleetAsync_ThenItCallsTheDispatcherFleetRoute()
    {
        // Arrange
        var handler = new StubHandler(HttpStatusCode.OK, "[]");
        var service = CreateService(handler);

        // Act
        await service.GetFleetAsync();

        // Assert
        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.EndsWith("dispatcher/fleet", handler.LastRequest!.RequestUri!.AbsolutePath);
    }
}
