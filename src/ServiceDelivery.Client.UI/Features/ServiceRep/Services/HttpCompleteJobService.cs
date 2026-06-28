using ServiceDelivery.Client.Core.Interfaces;

namespace ServiceDelivery.Client.UI.Features.ServiceRep.Services;

/// <summary>
/// Blazor-generic <see cref="ICompleteJobService"/> over an injected <see cref="HttpClient"/>, shared
/// by every host since the HTTP contract is platform-agnostic. Maps the backend contract for
/// <c>POST /rep/complete</c> (BE-020) — the rep's "Mark Complete" action that closes the active job
/// and returns the rep to Available. HTTP details live here, never in the ViewModel (same pattern as
/// <see cref="HttpArriveService"/>).
/// </summary>
public class HttpCompleteJobService : ICompleteJobService
{
    private const string CompleteRoute = "rep/complete";

    private readonly HttpClient _httpClient;

    public HttpCompleteJobService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task CompleteAsync()
    {
        var response = await _httpClient.PostAsync(CompleteRoute, content: null);
        response.EnsureSuccessStatusCode();
    }
}
