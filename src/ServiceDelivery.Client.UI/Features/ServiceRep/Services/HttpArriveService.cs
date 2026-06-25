using ServiceDelivery.Client.Core.Interfaces;

namespace ServiceDelivery.Client.UI.Features.ServiceRep.Services;

/// <summary>
/// Blazor-generic <see cref="IArriveService"/> over an injected <see cref="HttpClient"/>, shared by
/// every host since the HTTP contract is platform-agnostic. Maps the backend contract for
/// <c>POST /rep/arrive</c> (BE-019) — the rep's "I've Arrived" action that transitions them to the
/// OnSite state. HTTP details live here, never in the ViewModel (same pattern as
/// <see cref="HttpActiveJobService"/>).
/// </summary>
public class HttpArriveService : IArriveService
{
    private const string ArriveRoute = "rep/arrive";

    private readonly HttpClient _httpClient;

    public HttpArriveService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task ArriveAsync()
    {
        var response = await _httpClient.PostAsync(ArriveRoute, content: null);
        response.EnsureSuccessStatusCode();
    }
}
