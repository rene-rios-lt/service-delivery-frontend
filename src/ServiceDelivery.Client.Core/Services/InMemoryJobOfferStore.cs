using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.Services;

/// <summary>
/// Scoped, in-memory implementation of <see cref="IJobOfferStore"/>. Holds a single nullable
/// <see cref="JobOfferPayload"/> handed from the navigator to the job-offer page within one session
/// scope. Registered in every host bootstrapper so <c>BlazorPersonaNavigator</c> can always resolve it.
/// </summary>
public class InMemoryJobOfferStore : IJobOfferStore
{
    public JobOfferPayload? CurrentOffer { get; private set; }

    public void SetOffer(JobOfferPayload offer) => CurrentOffer = offer;

    public void ClearOffer() => CurrentOffer = null;
}
