using System.Net.Http.Json;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.UI.Features.Requester.Services;

/// <summary>
/// Blazor-generic <see cref="IDtcService"/> over an injected <see cref="HttpClient"/>, shared by every
/// host since the HTTP contract is platform-agnostic (FE-015). Maps the backend contract: GET /dtcs
/// returns the dealer's <c>DtcDto</c> list, which deserializes straight into <see cref="DtcItem"/>.
/// </summary>
public class HttpDtcService : IDtcService
{
    private readonly HttpClient _httpClient;

    public HttpDtcService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<DtcItem>> GetDtcsAsync()
    {
        var dtcs = await _httpClient.GetFromJsonAsync<List<DtcItem>>("dtcs");
        return dtcs ?? [];
    }
}
