using System.Net;
using System.Net.Http.Json;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.UI.Features.ServiceRep.Services;

/// <summary>
/// Blazor-generic <see cref="IActiveJobService"/> over an injected <see cref="HttpClient"/>, shared
/// by every host since the HTTP contract is platform-agnostic. Maps the backend contract for
/// <c>GET /service-requests/my-active</c> (BE-012) onto <see cref="ActiveJobContext"/>, returning
/// <c>null</c> on 404 (the rep has no active request) so callers never see HTTP status codes.
/// </summary>
/// <remarks>
/// The backend <c>MyActiveServiceRequestDto</c> exposes the request id, tier, DTC title, request
/// <c>status</c>, and — critically for the "I've Arrived" enable rule — the rep's <c>RepState</c>
/// (EnRoute/Within15Miles/OnSite, computed server-side from the rep's proximity). This adapter maps
/// <c>RepState</c> onto <see cref="ActiveJobContext.RepState"/> so the poll reflects the rep crossing
/// 15 miles. (It previously surfaced the request <c>status</c> as the rep state — a placeholder that
/// could never equal "Within15Miles", so the button never enabled live; fixed by extending the DTO.)
/// The payload still does not carry the rep's live position/ETA, so the rep marker starts at the
/// requester pin until a richer payload is available.
/// </remarks>
public class HttpActiveJobService : IActiveJobService
{
    private const string MyActiveRoute = "service-requests/my-active";

    private readonly HttpClient _httpClient;

    public HttpActiveJobService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ActiveJobContext?> GetActiveJobAsync()
    {
        var response = await _httpClient.GetAsync(MyActiveRoute);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<MyActiveServiceRequestResponse>();
        if (dto is null)
            return null;

        return new ActiveJobContext(
            dto.RequestId,
            RequesterName: string.Empty,
            dto.DtcTitle,
            dto.RequesterLatitude,
            dto.RequesterLongitude,
            RepLat: dto.RequesterLatitude,
            RepLng: dto.RequesterLongitude,
            EtaMinutes: 0,
            RepState: dto.RepState,
            Tier: dto.Tier);
    }

    // Wire shape mirroring the backend MyActiveServiceRequestDto exactly (property names matched to
    // the real DTO so System.Text.Json binds the camelCase JSON the API emits).
    private sealed record MyActiveServiceRequestResponse(
        Guid RequestId,
        string Tier,
        string DtcTitle,
        string Status,
        string RepState,
        double RequesterLatitude,
        double RequesterLongitude,
        DateTime CreatedAt);
}
