using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.UI.Features.ServiceRep.Services;

namespace ServiceDelivery.Client.Tests.ServiceRep;

public class HttpDeclineOfferServiceTests
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

    private static HttpDeclineOfferService CreateService(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

    [Fact]
    public async Task GivenAnOfferId_WhenDeclineAsync_ThenItPostsToTheDeclineRoute()
    {
        // Arrange
        // AC-1: tapping Decline calls POST /job-offers/{id}/decline.
        var offerId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var handler = new StubHandler(HttpStatusCode.OK);
        var service = CreateService(handler);

        // Act
        await service.DeclineAsync(offerId);

        // Assert
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.EndsWith($"job-offers/{offerId}/decline", handler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GivenDeclineEndpointReturns200_WhenDeclineAsync_ThenResultIsSuccess()
    {
        // Arrange
        // AC-2: a 2xx response means the decline succeeded.
        var handler = new StubHandler(HttpStatusCode.OK);
        var service = CreateService(handler);

        // Act
        var result = await service.DeclineAsync(Guid.NewGuid());

        // Assert
        Assert.Equal(DeclineOfferResult.Success, result);
    }

    [Fact]
    public async Task GivenDeclineEndpointReturns409_WhenDeclineAsync_ThenResultIsConflict()
    {
        // Arrange
        // AC-3: the offer expired between the tap and the API call — backend returns 409.
        var handler = new StubHandler(HttpStatusCode.Conflict);
        var service = CreateService(handler);

        // Act
        var result = await service.DeclineAsync(Guid.NewGuid());

        // Assert
        Assert.Equal(DeclineOfferResult.Conflict, result);
    }
}
