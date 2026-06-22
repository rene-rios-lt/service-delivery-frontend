using System.Net;
using ServiceDelivery.Client.Core.Exceptions;
using ServiceDelivery.Client.Core.Interfaces;

namespace ServiceDelivery.Client.UI.Features.Authentication.Services;

/// <summary>
/// Reactive (401-based) expiry detection. A <see cref="DelegatingHandler"/> shared by every host
/// and attached to the outbound <see cref="HttpClient"/> pipeline. On a 401 it delegates the
/// "clear + redirect" action to <see cref="ISessionExpiryHandler"/> and then throws
/// <see cref="SessionExpiredException"/> so the in-flight caller's success continuation never runs
/// against the dead token. It does not clear the token or navigate itself — detection and action
/// are kept separate (SRP).
/// </summary>
public class SessionExpiryHttpHandler : DelegatingHandler
{
    private readonly ISessionExpiryHandler _expiryHandler;

    public SessionExpiryHttpHandler(ISessionExpiryHandler expiryHandler)
    {
        _expiryHandler = expiryHandler;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            await _expiryHandler.HandleExpiredSessionAsync();
            throw new SessionExpiredException();
        }

        return response;
    }
}
