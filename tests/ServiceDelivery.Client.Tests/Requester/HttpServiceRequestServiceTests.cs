using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.UI.Features.Requester.Services;

namespace ServiceDelivery.Client.Tests.Requester;

/// <summary>
/// Unit tests for <see cref="HttpServiceRequestService"/> (FE-015 AC-4/AC-5). Maps the backend
/// POST /service-requests contract over a stubbed <see cref="HttpClient"/>: a 2xx with
/// <c>{ requestId, status }</c> maps to <c>Success(requestId)</c>; a non-2xx maps to <c>Error</c>.
///
/// Includes a captured-payload REQUEST-BODY contract test (CLAUDE.md wire-contract rule / QUAL-006):
/// the backend binds <c>SubmitServiceRequestBody(Guid DtcId, double Latitude, double Longitude)</c>
/// camelCased via <see cref="JsonSerializerDefaults.Web"/>, so the serialized body must use exactly
/// <c>dtcId</c>/<c>latitude</c>/<c>longitude</c>. Distinct per-field values guard against a field-name
/// drift passing coincidentally (e.g. swapping lat/lng or using lat/lng instead of latitude/longitude).
/// </summary>
public class HttpServiceRequestServiceTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }

        public StubHandler(HttpStatusCode status, string body = "")
        {
            _status = status;
            _body = body;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            };
        }
    }

    private static HttpServiceRequestService CreateService(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

    [Fact]
    public async Task GivenApiReturns200_WhenSubmitAsyncCalled_ThenResultIsSuccess()
    {
        // Arrange
        var requestId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var json = $$"""{ "requestId": "{{requestId}}", "status": "Pending" }""";
        var service = CreateService(new StubHandler(HttpStatusCode.OK, json));

        // Act
        var result = await service.SubmitAsync(41.6, -93.6, Guid.NewGuid());

        // Assert
        var success = Assert.IsType<SubmitServiceRequestResult.Success>(result);
        Assert.Equal(requestId, success.RequestId);
    }

    [Fact]
    public async Task GivenApiReturns500_WhenSubmitAsyncCalled_ThenResultIsError()
    {
        // Arrange
        var service = CreateService(new StubHandler(HttpStatusCode.InternalServerError));

        // Act
        var result = await service.SubmitAsync(41.6, -93.6, Guid.NewGuid());

        // Assert
        var error = Assert.IsType<SubmitServiceRequestResult.Error>(result);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
    }

    [Fact]
    public async Task GivenAValidSubmission_WhenSubmitAsyncCalled_ThenItPostsToTheServiceRequestsRoute()
    {
        // Arrange
        var handler = new StubHandler(HttpStatusCode.OK, """{ "requestId": "33333333-3333-3333-3333-333333333333", "status": "Pending" }""");
        var service = CreateService(handler);

        // Act
        await service.SubmitAsync(41.6, -93.6, Guid.NewGuid());

        // Assert
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.EndsWith("service-requests", handler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GivenDistinctFieldValues_WhenSubmitAsyncCalled_ThenBodyUsesDtcIdLatitudeLongitudeFieldNames()
    {
        // Arrange
        // Distinct per-field values so a field-name drift (lat/lng instead of latitude/longitude, or a
        // swapped lat/lng) cannot pass coincidentally. The backend binds SubmitServiceRequestBody
        // (Guid DtcId, double Latitude, double Longitude) camelCased via JsonSerializerDefaults.Web.
        var dtcId = Guid.Parse("11112222-3333-4444-5555-666677778888");
        const double latitude = 41.587;
        const double longitude = -93.624;
        var handler = new StubHandler(HttpStatusCode.OK, """{ "requestId": "33333333-3333-3333-3333-333333333333", "status": "Pending" }""");
        var service = CreateService(handler);

        // Act
        await service.SubmitAsync(latitude, longitude, dtcId);

        // Assert
        using var doc = JsonDocument.Parse(handler.LastRequestBody!);
        var root = doc.RootElement;
        Assert.Equal(dtcId, root.GetProperty("dtcId").GetGuid());
        Assert.Equal(latitude, root.GetProperty("latitude").GetDouble());
        Assert.Equal(longitude, root.GetProperty("longitude").GetDouble());
        Assert.False(root.TryGetProperty("lat", out _));
        Assert.False(root.TryGetProperty("lng", out _));
    }
}
