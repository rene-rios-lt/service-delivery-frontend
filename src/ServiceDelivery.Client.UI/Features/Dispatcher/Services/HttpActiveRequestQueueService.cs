using System.Net.Http.Json;
using System.Text.Json;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.UI.Features.Dispatcher.Services;

/// <summary>
/// Blazor-generic <see cref="IActiveRequestQueueService"/> over an injected <see cref="HttpClient"/>, shared
/// by every host that serves the Dispatcher persona (Web + Desktop) since the HTTP contract is
/// platform-agnostic (FE-004). GETs <c>/service-requests</c>, deserializes the backend
/// <see cref="ActiveRequestDto"/> array with <see cref="JsonSerializerDefaults.Web"/> (camelCase), then maps
/// each entry onto the clean queue model.
/// <para>
/// <see cref="GetRequestAsync"/> resolves a single request by re-reading the active list and filtering — the
/// request enriched after a <c>ServiceRequestPending</c> event is, by definition, active (Pending), so it is
/// present in that list. This reuses the one wire shape rather than the differently-shaped
/// <c>GET /service-requests/{id}</c> detail projection (nested <c>assignedRep</c>, <c>offerHistory</c>),
/// keeping a single DTO and a single deserialization contract.
/// </para>
/// </summary>
public class HttpActiveRequestQueueService : IActiveRequestQueueService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    public HttpActiveRequestQueueService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<ActiveRequestEntry>> GetActiveRequestsAsync()
    {
        var dtos = await _httpClient.GetFromJsonAsync<List<ActiveRequestDto>>(
            "service-requests", JsonOptions);

        return dtos is null
            ? []
            : dtos.Select(d => d.ToActiveRequestEntry()).ToList();
    }

    public async Task<ActiveRequestEntry?> GetRequestAsync(Guid requestId)
    {
        var active = await GetActiveRequestsAsync();
        return active.FirstOrDefault(e => e.RequestId == requestId);
    }
}
