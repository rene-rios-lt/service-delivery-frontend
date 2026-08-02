namespace ServiceDelivery.Client.Core.Interfaces;

/// <summary>
/// Force-releases a vehicle over REST (FE-022). Narrow per capability (ISP): the single
/// <c>POST /vehicles/{id}/force-release</c> call — it mixes with neither redirect nor any other dispatcher
/// action. Implemented by a host <c>Services/</c> HttpClient adapter.
/// </summary>
public interface IForceReleaseService
{
    /// <summary>
    /// POSTs to <c>/vehicles/{vehicleId}/force-release</c> as the authenticated dispatcher. Throws
    /// <see cref="HttpRequestException"/> on any non-2xx response so the ViewModel's error path runs (e.g. the
    /// rep reconnected and self-released between the dialog opening and confirmation). The dispatcher side needs
    /// no response body, so nothing is returned.
    /// </summary>
    Task ForceReleaseAsync(Guid vehicleId);
}
