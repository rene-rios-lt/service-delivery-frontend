using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.Interfaces;

/// <summary>
/// Carries the in-flight <see cref="JobOfferPayload"/> from the persona navigator to the job-offer
/// page (FE-008). When a <c>JobOfferReceived</c> event arrives over RepHub, the navigator deposits
/// the payload here before navigating to <c>/rep/offer</c>; the page reads it on init and renders the
/// offer without a re-fetch. Scoped lifetime — one offer at a time, cleared once the page consumes it.
/// </summary>
public interface IJobOfferStore
{
    JobOfferPayload? CurrentOffer { get; }

    void SetOffer(JobOfferPayload offer);

    void ClearOffer();
}
