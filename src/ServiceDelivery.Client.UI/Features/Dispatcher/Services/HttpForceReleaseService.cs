using ServiceDelivery.Client.Core.Interfaces;

namespace ServiceDelivery.Client.UI.Features.Dispatcher.Services;

/// <summary>
/// Blazor-generic <see cref="IForceReleaseService"/> over an injected <see cref="HttpClient"/>, shared by every
/// host that serves the Dispatcher persona (Web + Desktop) since the HTTP contract is platform-agnostic
/// (FE-022). POSTs to <c>/vehicles/{id}/force-release</c> and calls <see cref="HttpResponseMessage.EnsureSuccessStatusCode"/>
/// so the ViewModel's error path runs on any non-2xx (e.g. the rep moved state between the dialog opening and
/// confirmation). The dispatcher side needs no response body, so none is parsed. LSP: fully honours the
/// <see cref="IForceReleaseService"/> contract. Mirrors <c>HttpDispatcherRedirectService</c> (the FE-005
/// convention).
/// </summary>
public class HttpForceReleaseService : IForceReleaseService
{
    private readonly HttpClient _httpClient;

    public HttpForceReleaseService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task ForceReleaseAsync(Guid vehicleId)
    {
        var response = await _httpClient.PostAsync($"vehicles/{vehicleId}/force-release", content: null);
        response.EnsureSuccessStatusCode();
    }
}
