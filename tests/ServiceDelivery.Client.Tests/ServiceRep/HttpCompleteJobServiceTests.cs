using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ServiceDelivery.Client.UI.Features.ServiceRep.Services;

namespace ServiceDelivery.Client.Tests.ServiceRep;

public class HttpCompleteJobServiceTests
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

    private static HttpCompleteJobService CreateService(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

    [Fact]
    public async Task GivenCompleteJobServiceCalled_WhenCompleteAsyncExecuted_ThenPostRepCompleteIsSent()
    {
        // Arrange
        // AC-1: tapping "Mark Complete" calls POST /rep/complete.
        var handler = new StubHandler(HttpStatusCode.OK);
        var service = CreateService(handler);

        // Act
        await service.CompleteAsync();

        // Assert
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.EndsWith("rep/complete", handler.LastRequest!.RequestUri!.AbsolutePath);
    }
}
