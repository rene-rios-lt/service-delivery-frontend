using System.Net;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.UI.Features.ServiceRep.Services;

/// <summary>
/// Blazor-generic <see cref="IJobOfferService"/> over an injected <see cref="HttpClient"/>, shared
/// by every host since the HTTP contract is platform-agnostic. Maps the backend contract: POST
/// /job-offers/{id}/accept returns 2xx on a successful accept and 409 when the offer expired between
/// the tap and the API call (translated to <see cref="AcceptOfferResult.Conflict"/> so the ViewModel
/// never sees HTTP status codes).
/// </summary>
public class HttpJobOfferService : IJobOfferService
{
    private readonly HttpClient _httpClient;

    public HttpJobOfferService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<AcceptOfferResult> AcceptAsync(Guid offerId)
    {
        var response = await _httpClient.PostAsync($"job-offers/{offerId}/accept", content: null);

        if (response.StatusCode == HttpStatusCode.Conflict)
            return AcceptOfferResult.Conflict;

        response.EnsureSuccessStatusCode();
        return AcceptOfferResult.Success;
    }
}
