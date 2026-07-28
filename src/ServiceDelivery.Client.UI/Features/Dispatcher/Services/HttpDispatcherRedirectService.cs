using System.Net.Http.Json;
using System.Text.Json;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.UI.Features.Dispatcher.Services;

/// <summary>
/// Blazor-generic <see cref="IDispatcherRedirectService"/> over an injected <see cref="HttpClient"/>, shared
/// by every host that serves the Dispatcher persona (Web + Desktop) since the HTTP contract is
/// platform-agnostic (FE-005). POSTs <c>{ repId, toRequestId }</c> to <c>/dispatcher/redirect</c> and
/// deserializes the backend <see cref="RedirectRepResultDto"/> (<see cref="JsonSerializerDefaults.Web"/> /
/// camelCase) on HTTP 200; throws on any non-2xx so the ViewModel can distinguish a successful redirect from
/// an error (e.g. the rep moved state between the dialog opening and confirmation).
/// </summary>
public class HttpDispatcherRedirectService : IDispatcherRedirectService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    public HttpDispatcherRedirectService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<RedirectRepResultDto> RedirectAsync(Guid repId, Guid toRequestId)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "dispatcher/redirect", new { repId, toRequestId }, JsonOptions);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<RedirectRepResultDto>(JsonOptions))!;
    }
}
