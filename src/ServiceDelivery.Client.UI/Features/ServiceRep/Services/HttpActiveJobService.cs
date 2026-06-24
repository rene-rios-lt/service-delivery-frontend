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
/// The backend <c>MyActiveServiceRequestDto</c> currently exposes only the request id, tier, DTC
/// title, status, requester lat/lng, and created-at — it does NOT carry the rep's current position,
/// ETA, requester name, or the <c>EnRoute</c>/<c>Within15Miles</c> rep state the active-job ACs
/// reference. Those values are computed server-side and pushed on the <c>RepPositionUpdated</c>
/// requester-hub event (BE-008); the <c>my-active</c> REST payload has not yet been extended to
/// return them. This adapter maps the fields the endpoint does expose and surfaces the backend
/// <c>status</c> as the rep state; the rep marker starts at the requester pin until a richer payload
/// (or a position poll endpoint) is available. See the FE-011 implementation report's backend-gap note.
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
            RepState: dto.Status);
    }

    // Wire shape mirroring the backend MyActiveServiceRequestDto exactly (property names matched to
    // the real DTO so System.Text.Json binds the camelCase JSON the API emits).
    private sealed record MyActiveServiceRequestResponse(
        Guid RequestId,
        string Tier,
        string DtcTitle,
        string Status,
        double RequesterLatitude,
        double RequesterLongitude,
        DateTime CreatedAt);
}
