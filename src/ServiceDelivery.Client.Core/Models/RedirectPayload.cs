namespace ServiceDelivery.Client.Core.Models;

/// <summary>
/// Payload of the <c>RedirectReceived</c> event pushed from RepHub when a dispatcher redirects the
/// rep mid-route to a higher-priority job (BE-022 / BE-025). Immutable contract shape carrying the
/// new request id and requester tier, the new DTC title, the new distance and ETA, and the new
/// destination <see cref="Latitude"/>/<see cref="Longitude"/>. Field names mirror the backend
/// BE-022 event payload (<c>RequesterTier</c>, <c>Latitude</c>, <c>Longitude</c>, <c>EtaMinutes</c>
/// as a double). The active-job view applies this in-place — moving the requester pin and updating
/// the bottom sheet without a screen reload (FE-011/AC-6).
/// </summary>
public record RedirectPayload(
    Guid NewRequestId,
    string RequesterTier,
    string DtcTitle,
    double DistanceMiles,
    double EtaMinutes,
    double Latitude,
    double Longitude);
