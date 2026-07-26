using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.Interfaces;

/// <summary>
/// Reads the dispatcher's active request queue over REST (FE-004). Narrow per capability (ISP): the initial
/// snapshot load and a single-request follow-up fetch used when a <c>ServiceRequestPending</c> event arrives
/// without a requester name. Implemented by a host <c>Services/</c> HttpClient adapter.
/// </summary>
public interface IActiveRequestQueueService
{
    /// <summary>The full set of active (non-Completed) requests for the dispatcher's dealer.</summary>
    Task<IReadOnlyList<ActiveRequestEntry>> GetActiveRequestsAsync();

    /// <summary>
    /// The single active request with this id, or <c>null</c> when it is no longer active (already completed
    /// server-side). Used to enrich a <c>ServiceRequestPending</c> event with the requester name.
    /// </summary>
    Task<ActiveRequestEntry?> GetRequestAsync(Guid requestId);
}
