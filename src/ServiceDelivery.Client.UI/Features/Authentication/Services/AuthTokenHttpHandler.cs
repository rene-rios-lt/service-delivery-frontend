using System.Net.Http.Headers;
using ServiceDelivery.Client.Core.Interfaces;

namespace ServiceDelivery.Client.UI.Features.Authentication.Services;

/// <summary>
/// Attaches the stored JWT as a <c>Bearer</c> Authorization header to every outbound request. A
/// <see cref="DelegatingHandler"/> shared by every host and placed in the <see cref="HttpClient"/>
/// pipeline, so all authenticated API calls — current and future — carry the token automatically
/// without per-call code (Open/Closed). Requests that already set an Authorization header are left
/// untouched, and an absent token simply sends no header (e.g. the unauthenticated <c>/auth/login</c>
/// call). Centralising this here fixed the family of post-login 401s where data calls went out with
/// no token (BUG-028).
/// </summary>
public class AuthTokenHttpHandler : DelegatingHandler
{
    private readonly ITokenStore _tokenStore;

    public AuthTokenHttpHandler(ITokenStore tokenStore)
    {
        _tokenStore = tokenStore;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Headers.Authorization is null)
        {
            var token = await _tokenStore.GetTokenAsync();
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
