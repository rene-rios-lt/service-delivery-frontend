using ServiceDelivery.Client.Core.Interfaces;

namespace ServiceDelivery.Client.UI.Features.ServiceRep.Services;

/// <summary>
/// Blazor-generic <see cref="IReleaseVehicleService"/> over an injected <see cref="HttpClient"/>,
/// shared by every host since the HTTP contract is platform-agnostic. Maps the backend contract for
/// <c>POST /vehicles/{id}/release</c> (BE-006): returns <c>true</c> on a 2xx response and
/// <c>false</c> on any non-success, so callers never see HTTP status codes (same pattern as
/// <see cref="HttpVehicleService"/>).
/// </summary>
public class HttpReleaseVehicleService : IReleaseVehicleService
{
    private readonly HttpClient _httpClient;

    public HttpReleaseVehicleService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> ReleaseAsync(Guid vehicleId)
    {
        var response = await _httpClient.PostAsync($"vehicles/{vehicleId}/release", content: null);
        return response.IsSuccessStatusCode;
    }
}
