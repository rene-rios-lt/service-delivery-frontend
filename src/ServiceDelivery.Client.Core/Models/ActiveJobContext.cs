namespace ServiceDelivery.Client.Core.Models;

/// <summary>
/// Immutable snapshot of the rep's active job, loaded from <c>GET rep/active-job-state</c>
/// (BE-030) on page init and refreshed on each position poll. Carries the requester's fixed
/// location and name, the DTC title for the bottom sheet, the service <c>Tier</c>
/// (<c>"Gold"</c> / <c>"Silver"</c> / <c>"Bronze"</c>) for the bottom-sheet tier badge, the rep's
/// simulator-driven current position, the server-computed ETA in minutes and distance in miles to
/// the requester, and the rep's state (<c>"EnRoute"</c> while &gt; 15 miles out,
/// <c>"Within15Miles"</c> once close enough to enable the "I've Arrived" action). The rep device
/// never reports GPS — the position is driven by the simulator (ADR-0009). All fields map
/// field-for-field onto the backend <c>ActiveJobStateDto</c> (BE-030).
/// </summary>
public record ActiveJobContext(
    Guid RequestId,
    string RequesterName,
    string DtcTitle,
    double RequesterLat,
    double RequesterLng,
    double RepLat,
    double RepLng,
    int EtaMinutes,
    double DistanceMiles,
    string RepState,
    string Tier);
