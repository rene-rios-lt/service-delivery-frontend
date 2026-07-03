using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.Interfaces;

/// <summary>
/// Carries the in-flight <see cref="RepAssignedPayload"/> from the requester pending view to the tracking
/// view (FE-017). When a <c>RepAssigned</c> event arrives over RequesterHub,
/// <c>RequesterPendingViewModel</c> deposits the payload here before navigating to
/// <c>/requester/tracking</c>; <c>RequesterTrackingViewModel</c> reads it on construction and seeds the
/// screen (rep name, vehicle, ETA, initial coordinates) without a re-fetch. Scoped lifetime — one
/// assignment at a time. Mirrors <see cref="IJobOfferStore"/>.
/// </summary>
public interface IRepAssignedStore
{
    RepAssignedPayload? CurrentPayload { get; }

    void SetPayload(RepAssignedPayload payload);

    void ClearPayload();
}
