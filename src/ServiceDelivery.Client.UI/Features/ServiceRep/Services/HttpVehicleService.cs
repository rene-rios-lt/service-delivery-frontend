using System.Net;
using System.Net.Http.Json;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.UI.Features.ServiceRep.Services;

/// <summary>
/// Blazor-generic <see cref="IVehicleService"/> over an injected <see cref="HttpClient"/>,
/// shared by every host since the HTTP contract is platform-agnostic. Maps the backend contract:
/// GET /vehicles/available returns the idle-vehicle list; POST /vehicles/{id}/take-over returns 2xx on
/// a successful claim and 409 when the vehicle is no longer available (translated to
/// <see cref="TakeOverResult.Conflict"/> so callers never see HTTP status codes).
/// </summary>
public class HttpVehicleService : IVehicleService
{
    private readonly HttpClient _httpClient;

    public HttpVehicleService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<IdleVehicle>> GetIdleVehiclesAsync()
    {
        var vehicles = await _httpClient.GetFromJsonAsync<List<IdleVehicle>>("vehicles/available");
        return vehicles ?? [];
    }

    public async Task<TakeOverResult> TakeOverAsync(Guid vehicleId)
    {
        var response = await _httpClient.PostAsync($"vehicles/{vehicleId}/take-over", content: null);

        if (response.StatusCode == HttpStatusCode.Conflict)
            return TakeOverResult.Conflict;

        response.EnsureSuccessStatusCode();
        return TakeOverResult.Success;
    }
}
