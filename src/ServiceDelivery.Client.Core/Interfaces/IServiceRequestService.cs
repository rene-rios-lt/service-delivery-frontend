using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.Interfaces;

/// <summary>
/// Submits a service request to <c>POST /service-requests</c> (FE-015 AC-4/AC-5). A focused, single
/// capability (Interface Segregation) — the concrete HTTP implementation lives in a host-shared UI
/// service and is injected via DI. Returns a typed <see cref="SubmitServiceRequestResult"/> so the
/// caller never sees HTTP status codes.
/// </summary>
public interface IServiceRequestService
{
    Task<SubmitServiceRequestResult> SubmitAsync(double lat, double lng, Guid dtcId);
}
