namespace ServiceDelivery.Client.Core.Models;

/// <summary>
/// Payload of the <c>JobOfferExpired</c> event pushed from RepHub when an offer's server-side
/// expiry window elapses before the rep accepts or declines (BE-019 / BUG-037). Immutable contract
/// shape carrying the single <see cref="OfferId"/> the backend event sends. The job-offer screen
/// uses it to dismiss the offer the instant the server expires it, rather than waiting for the
/// local countdown to reach zero. Field name mirrors the backend wire payload exactly, so the
/// SignalR registration binds directly with no field-name mapping.
/// </summary>
public record JobOfferExpiredPayload(Guid OfferId);
