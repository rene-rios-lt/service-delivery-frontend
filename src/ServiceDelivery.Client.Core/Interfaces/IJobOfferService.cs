using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.Interfaces;

/// <summary>
/// Job-offer operations the accept flow needs: accept the offer the rep tapped. The implementation
/// (in a host's Services folder) owns the HTTP contract; callers see only the domain outcome
/// (<see cref="AcceptOfferResult"/>), never an HTTP status code. Decline is a separate capability
/// (FE-010) and deliberately not on this interface (Interface Segregation).
/// </summary>
public interface IJobOfferService
{
    Task<AcceptOfferResult> AcceptAsync(Guid offerId);
}
