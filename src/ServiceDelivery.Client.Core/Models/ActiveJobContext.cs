namespace ServiceDelivery.Client.Core.Models;

/// <summary>
/// Immutable snapshot of the rep's active job, loaded from <c>GET /service-requests/my-active</c>
/// (BE-012) on page init and refreshed on each position poll. Carries the requester's fixed
/// location, the DTC title and requester name for the bottom sheet, the service <c>Tier</c>
/// (<c>"Gold"</c> / <c>"Silver"</c> / <c>"Bronze"</c>) for the bottom-sheet tier badge, the rep's
/// simulator-driven current position, the ETA in minutes, and the rep's state (<c>"EnRoute"</c>
/// while &gt; 15 miles out, <c>"Within15Miles"</c> once close enough to enable the "I've Arrived"
/// action). The rep device never reports GPS — the position is driven by the simulator (ADR-0009).
/// <c>Tier</c> mirrors the backend <c>MyActiveServiceRequestDto.Tier</c> field exactly (BE-012).
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
    string RepState,
    string Tier);
