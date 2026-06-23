namespace ServiceDelivery.Client.Core.Models;

/// <summary>
/// Payload of the <c>JobOfferReceived</c> event pushed from RepHub when a matched job is offered
/// to the rep. Immutable contract shape matching the backend RepHub event (BE-017/BE-019). The
/// idle / waiting-for-offers view consumes this to transition away to the job-offer screen (FE-008).
/// </summary>
public record JobOfferPayload(
    Guid OfferId,
    string RequesterName,
    ServiceTier Tier,
    string DtcTitle,
    double DistanceMiles,
    int EtaMinutes,
    double Lat,
    double Lng);
