using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ServiceDelivery.Client.UI.Features.ServiceRep.Services;

namespace ServiceDelivery.Client.Tests.ServiceRep;

public class HttpReleaseVehicleServiceTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;

        public HttpRequestMessage? LastRequest { get; private set; }

        public StubHandler(HttpStatusCode status)
        {
            _status = status;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(_status));
        }
    }

    private static HttpReleaseVehicleService CreateService(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

    [Fact]
    public async Task GivenReleaseEndpointReturns200_WhenReleaseAsync_ThenResultIsTrue()
    {
        // Arrange
        var handler = new StubHandler(HttpStatusCode.OK);
        var service = CreateService(handler);

        // Act
        var result = await service.ReleaseAsync(Guid.NewGuid());

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GivenReleaseEndpointReturns409_WhenReleaseAsync_ThenResultIsFalse()
    {
        // Arrange
        var handler = new StubHandler(HttpStatusCode.Conflict);
        var service = CreateService(handler);

        // Act
        var result = await service.ReleaseAsync(Guid.NewGuid());

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GivenAVehicleId_WhenReleaseAsync_ThenItPostsToTheReleaseRoute()
    {
        // Arrange
        var vehicleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var handler = new StubHandler(HttpStatusCode.OK);
        var service = CreateService(handler);

        // Act
        await service.ReleaseAsync(vehicleId);

        // Assert
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.EndsWith($"vehicles/{vehicleId}/release", handler.LastRequest!.RequestUri!.AbsolutePath);
    }
}
