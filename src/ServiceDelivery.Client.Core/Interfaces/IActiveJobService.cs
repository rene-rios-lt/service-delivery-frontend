using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.Interfaces;

/// <summary>
/// Narrow client for the active-job REST operations. Backs the active-job navigation view (FE-011):
/// <see cref="GetActiveJobAsync"/> maps to <c>GET /service-requests/my-active</c> (BE-012),
/// returning the rep's current active job (requester location, DTC, current position, ETA, state) or
/// <c>null</c> when the rep has no active request. HTTP details live in the implementation, never in
/// the ViewModel.
/// </summary>
public interface IActiveJobService
{
    Task<ActiveJobContext?> GetActiveJobAsync();
}
