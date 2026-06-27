using System.Net;
using System.Net.Http.Json;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.UI.Features.ServiceRep.Services;

/// <summary>
/// Blazor-generic <see cref="IActiveJobService"/> over an injected <see cref="HttpClient"/>, shared
/// by every host since the HTTP contract is platform-agnostic. Maps the backend contract for
/// <c>GET rep/active-job-state</c> (BE-030) onto <see cref="ActiveJobContext"/>, returning
/// <c>null</c> on 404 (the rep has no active request) so callers never see HTTP status codes.
/// </summary>
/// <remarks>
/// The backend <c>ActiveJobStateDto</c> is the purpose-built active-job projection: it carries the
/// request id, the requester name, the DTC title, the requester's fixed coordinates, the rep's
/// simulator-driven live position (<c>repLat</c>/<c>repLng</c>), the server-computed ETA in minutes
/// and distance in miles, the tier, and the rep's <c>RepState</c> (EnRoute/Within15Miles/OnSite). Every
/// field maps onto <see cref="ActiveJobContext"/> — including the real rep position, ETA, and distance
/// (the previous endpoint, <c>service-requests/my-active</c>, carried none of these, so the adapter had
/// to fake rep position == requester pin and zero the ETA; BUG-039 fixes this by switching endpoints).
/// </remarks>
public class HttpActiveJobService : IActiveJobService
{
    private const string ActiveJobStateRoute = "rep/active-job-state";

    private readonly HttpClient _httpClient;

    public HttpActiveJobService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ActiveJobContext?> GetActiveJobAsync()
    {
        var response = await _httpClient.GetAsync(ActiveJobStateRoute);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<ActiveJobStateResponse>();
        if (dto is null)
            return null;

        return new ActiveJobContext(
            dto.RequestId,
            dto.RequesterName,
            dto.DtcTitle,
            dto.RequesterLat,
            dto.RequesterLng,
            dto.RepLat,
            dto.RepLng,
            dto.EtaMinutes,
            dto.DistanceMiles,
            dto.RepState,
            dto.Tier);
    }

    // Wire shape mirroring the backend ActiveJobStateDto exactly (BE-030) — property names match the
    // camelCase JSON the API emits so System.Text.Json binds without a naming policy. All fields must be
    // populated from the DTO; no field may be faked or defaulted to zero (anti-masking — BUG-016/036).
    private sealed record ActiveJobStateResponse(
        Guid RequestId,
        string RequesterName,
        string DtcTitle,
        double RequesterLat,
        double RequesterLng,
        double RepLat,
        double RepLng,
        int EtaMinutes,
        double DistanceMiles,
        string Tier,
        string RepState);
}
