using System.Net.Http.Json;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.UI.Features.Requester.Services;

/// <summary>
/// Blazor-generic <see cref="IServiceRequestService"/> over an injected <see cref="HttpClient"/>, shared
/// by every host since the HTTP contract is platform-agnostic (FE-015). Maps the backend contract: POST
/// /service-requests with a <c>{ dtcId, latitude, longitude }</c> body (matching the backend's
/// <c>SubmitServiceRequestBody(Guid DtcId, double Latitude, double Longitude)</c>, camelCased via
/// <see cref="System.Text.Json.JsonSerializerDefaults.Web"/>) → 2xx maps to
/// <see cref="SubmitServiceRequestResult.Success"/> carrying the new request id; non-2xx maps to
/// <see cref="SubmitServiceRequestResult.Error"/> so the ViewModel never sees HTTP status codes.
/// </summary>
public class HttpServiceRequestService : IServiceRequestService
{
    // The submit response shape: { requestId, status } (the controller returns Ok(new { requestId, status })).
    private sealed record SubmitResponse(Guid RequestId, string Status);

    // The submit request body. Field names serialize to camelCase (dtcId/latitude/longitude) under the
    // System.Text.Json Web defaults, matching the backend's SubmitServiceRequestBody binder exactly.
    private sealed record SubmitBody(Guid DtcId, double Latitude, double Longitude);

    private const string SubmitFailedMessage =
        "We couldn't submit your request. Please try again.";

    private readonly HttpClient _httpClient;

    public HttpServiceRequestService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<SubmitServiceRequestResult> SubmitAsync(double lat, double lng, Guid dtcId)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "service-requests", new SubmitBody(dtcId, lat, lng));

        if (!response.IsSuccessStatusCode)
        {
            return new SubmitServiceRequestResult.Error(SubmitFailedMessage);
        }

        var body = await response.Content.ReadFromJsonAsync<SubmitResponse>();
        return new SubmitServiceRequestResult.Success(body!.RequestId);
    }
}
