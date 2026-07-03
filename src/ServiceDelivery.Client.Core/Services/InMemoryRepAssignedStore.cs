using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.Services;

/// <summary>
/// Scoped, in-memory implementation of <see cref="IRepAssignedStore"/>. Holds a single nullable
/// <see cref="RepAssignedPayload"/> handed from the requester pending view to the tracking view within one
/// session scope (FE-017). Registered in every host bootstrapper so the tracking ViewModel can always
/// resolve it. Mirrors <c>InMemoryJobOfferStore</c>.
/// </summary>
public class InMemoryRepAssignedStore : IRepAssignedStore
{
    public RepAssignedPayload? CurrentPayload { get; private set; }

    public void SetPayload(RepAssignedPayload payload) => CurrentPayload = payload;

    public void ClearPayload() => CurrentPayload = null;
}
