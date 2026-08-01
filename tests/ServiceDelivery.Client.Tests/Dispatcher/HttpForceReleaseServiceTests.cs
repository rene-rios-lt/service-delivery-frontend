using System.Net;
using System.Text;
using ServiceDelivery.Client.UI.Features.Dispatcher.Services;

namespace ServiceDelivery.Client.Tests.Dispatcher;

/// <summary>
/// FE-022 — the HTTP adapter for <c>POST /vehicles/{id}/force-release</c> (ACs 3 + 5). Drives a stubbed
/// <see cref="HttpMessageHandler"/> so the test asserts the REAL request shape (method POST, the
/// <c>/vehicles/{id}/force-release</c> route carrying the vehicle id) and the fail-loud non-2xx contract the
/// ViewModel's error path depends on — without a live backend. Mirrors <c>HttpDispatcherRedirectServiceTests</c>
/// (the FE-005 convention this story follows). The dispatcher side parses no response body, so the 200 case
/// simply asserts no throw.
/// </summary>
public class HttpForceReleaseServiceTests
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

    private static HttpForceReleaseService CreateService(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

    // A distinct, non-default vehicle id so the route assertion cannot pass against an empty/other guid.
    private static readonly Guid VehicleId = Guid.Parse("30000000-0000-0000-0000-000000000007");

    [Fact]
    public async Task GivenForceReleaseRequest_WhenForceReleaseAsync_ThenItPostsToVehiclesForceReleaseRouteWithVehicleId()
    {
        // Arrange
        var handler = new StubHandler(HttpStatusCode.OK);
        var service = CreateService(handler);

        // Act
        await service.ForceReleaseAsync(VehicleId);

        // Assert — POST to /vehicles/{vehicleId}/force-release with the exact vehicle id in the route.
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal($"/vehicles/{VehicleId}/force-release", handler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GivenA200Response_WhenForceReleaseAsync_ThenNoExceptionIsThrown()
    {
        // Arrange
        var handler = new StubHandler(HttpStatusCode.OK);
        var service = CreateService(handler);

        // Act
        var act = () => service.ForceReleaseAsync(VehicleId);

        // Assert — a 2xx completes without throwing (the ViewModel's success path runs).
        var exception = await Record.ExceptionAsync(act);
        Assert.Null(exception);
    }

    [Fact]
    public async Task GivenANon2xxResponse_WhenForceReleaseAsync_ThenItThrows()
    {
        // Arrange — the backend rejects the force-release (e.g. the rep reconnected and self-released between the
        // dialog opening and confirmation); the adapter must fail loud so the ViewModel's AC-5 error path runs,
        // not silently succeed.
        var handler = new StubHandler(HttpStatusCode.UnprocessableEntity, "{}");
        var service = CreateService(handler);

        // Act
        var act = () => service.ForceReleaseAsync(VehicleId);

        // Assert
        await Assert.ThrowsAsync<HttpRequestException>(act);
    }
}
