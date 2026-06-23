using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.Interfaces;

/// <summary>
/// Job-offer decline operation. Kept separate from <see cref="IJobOfferService"/> (which owns accept)
/// per Interface Segregation: a caller that only declines does not depend on accept. The
/// implementation (in a host's Services folder) owns the HTTP contract; callers see only the domain
/// outcome (<see cref="DeclineOfferResult"/>), never an HTTP status code.
/// </summary>
public interface IDeclineOfferService
{
    Task<DeclineOfferResult> DeclineAsync(Guid offerId);
}
