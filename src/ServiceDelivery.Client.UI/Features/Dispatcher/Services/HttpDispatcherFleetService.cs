using System.Net.Http.Json;
using System.Text.Json;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.UI.Features.Dispatcher.Services;

/// <summary>
/// Blazor-generic <see cref="IDispatcherFleetService"/> over an injected <see cref="HttpClient"/>, shared by
/// every host that serves the Dispatcher persona (Web + Desktop) since the HTTP contract is platform-agnostic
/// (FE-003). GETs <c>/dispatcher/fleet</c>, deserializes the backend <see cref="DispatcherFleetEntryDto"/>
/// array with <see cref="JsonSerializerDefaults.Web"/> (camelCase), then maps each entry onto the clean map
/// model. One responsibility — the fleet snapshot read.
/// </summary>
public class HttpDispatcherFleetService : IDispatcherFleetService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    public HttpDispatcherFleetService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<FleetVehicleEntry>> GetFleetAsync()
    {
        var entries = await _httpClient.GetFromJsonAsync<List<DispatcherFleetEntryDto>>(
            "dispatcher/fleet", JsonOptions);

        return entries is null
            ? []
            : entries.Select(e => e.ToFleetVehicleEntry()).ToList();
    }
}
