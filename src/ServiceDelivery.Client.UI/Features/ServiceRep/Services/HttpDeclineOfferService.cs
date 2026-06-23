using System.Net;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.UI.Features.ServiceRep.Services;

/// <summary>
/// Blazor-generic <see cref="IDeclineOfferService"/> over an injected <see cref="HttpClient"/>,
/// shared by every host since the HTTP contract is platform-agnostic. Maps the backend contract:
/// POST /job-offers/{id}/decline returns 2xx on a successful decline and 409 when the offer expired
/// between the tap and the API call (translated to <see cref="DeclineOfferResult.Conflict"/> so the
/// ViewModel never sees HTTP status codes).
/// </summary>
public class HttpDeclineOfferService : IDeclineOfferService
{
    private readonly HttpClient _httpClient;

    public HttpDeclineOfferService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<DeclineOfferResult> DeclineAsync(Guid offerId)
    {
        var response = await _httpClient.PostAsync($"job-offers/{offerId}/decline", content: null);

        if (response.StatusCode == HttpStatusCode.Conflict)
            return DeclineOfferResult.Conflict;

        response.EnsureSuccessStatusCode();
        return DeclineOfferResult.Success;
    }
}
