using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.Interfaces;

/// <summary>
/// Redirects an EnRoute rep to a higher-priority request over REST (FE-005). Narrow per capability (ISP): the
/// single <c>POST /dispatcher/redirect</c> call. Implemented by a host <c>Services/</c> HttpClient adapter.
/// </summary>
public interface IDispatcherRedirectService
{
    /// <summary>
    /// POSTs <c>{ repId, toRequestId }</c> to <c>/dispatcher/redirect</c>. Returns the
    /// <see cref="RedirectRepResultDto"/> on HTTP 200; throws on any non-2xx response so the caller can
    /// distinguish a successful redirect from an error (e.g. the rep moved state between the dialog opening
    /// and confirmation).
    /// </summary>
    Task<RedirectRepResultDto> RedirectAsync(Guid repId, Guid toRequestId);
}
