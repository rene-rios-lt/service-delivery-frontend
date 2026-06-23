using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.UI.Features.ServiceRep.Services;

namespace ServiceDelivery.Client.Tests.ServiceRep;

public class HttpJobOfferServiceTests
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

    private static HttpJobOfferService CreateService(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

    [Fact]
    public async Task GivenAnOfferId_WhenAcceptAsync_ThenItPostsToTheAcceptRoute()
    {
        // Arrange
        // AC-1: tapping Accept calls POST /job-offers/{id}/accept.
        var offerId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var handler = new StubHandler(HttpStatusCode.OK);
        var service = CreateService(handler);

        // Act
        await service.AcceptAsync(offerId);

        // Assert
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.EndsWith($"job-offers/{offerId}/accept", handler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GivenAcceptEndpointReturns200_WhenAcceptAsync_ThenResultIsSuccess()
    {
        // Arrange
        var handler = new StubHandler(HttpStatusCode.OK);
        var service = CreateService(handler);

        // Act
        var result = await service.AcceptAsync(Guid.NewGuid());

        // Assert
        Assert.Equal(AcceptOfferResult.Success, result);
    }

    [Fact]
    public async Task GivenAcceptEndpointReturns409_WhenAcceptAsync_ThenResultIsConflict()
    {
        // Arrange
        // AC-3: the offer expired between the tap and the API call — backend returns 409.
        var handler = new StubHandler(HttpStatusCode.Conflict);
        var service = CreateService(handler);

        // Act
        var result = await service.AcceptAsync(Guid.NewGuid());

        // Assert
        Assert.Equal(AcceptOfferResult.Conflict, result);
    }
}
