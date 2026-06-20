using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.UI.Features.Authentication.Services;

/// <summary>
/// Blazor-generic <see cref="IAuthService"/> over an injected <see cref="HttpClient"/>,
/// shared by every host (Desktop/Mobile/Web) since the HTTP contract is platform-agnostic.
/// Mirrors the verified backend contract: POST /auth/login { email, password } returning
/// { token } on 200 and an empty 401 on invalid credentials (returned as null, not thrown);
/// GET /users/me returning the role-bearing profile using the stored bearer token.
/// </summary>
public class HttpAuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly ITokenStore _tokenStore;

    public HttpAuthService(HttpClient httpClient, ITokenStore tokenStore)
    {
        _httpClient = httpClient;
        _tokenStore = tokenStore;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("auth/login", request);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return null;

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<LoginResponse>();
    }

    public async Task<UserProfile> GetCurrentUserAsync()
    {
        var token = await _tokenStore.GetTokenAsync();

        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, "users/me");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(requestMessage);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<UserProfile>())!;
    }
}
