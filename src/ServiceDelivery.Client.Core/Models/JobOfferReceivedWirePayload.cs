namespace ServiceDelivery.Client.Core.Models;

/// <summary>
/// Wire-format DTO for the RepHub <c>JobOfferReceived</c> event. Its property names and types mirror
/// the backend <c>JobOfferReceivedPayload</c> EXACTLY — <c>RequesterTier</c> as the tier enum-name
/// string, <c>Latitude</c>/<c>Longitude</c>, and <c>EtaMinutes</c> as a <c>double</c> — so
/// System.Text.Json binds every field over SignalR.
/// <para>
/// The clean domain-facing <see cref="JobOfferPayload"/> uses different names (<c>Tier</c>/<c>Lat</c>/
/// <c>Lng</c>) and a different ETA type (<c>int</c>). Deserializing the event straight into that model
/// silently defaulted all four mismatched fields — <c>Tier</c> became <see cref="ServiceTier.None"/>
/// (no colour modifier → white-on-white, invisible tier badge), and <c>Lat</c>/<c>Lng</c>/ETA became
/// zero (BUG-036). Map at the SignalR boundary via <see cref="ToJobOfferPayload"/>.
/// </para>
/// </summary>
public record JobOfferReceivedWirePayload(
    Guid OfferId,
    Guid RequestId,
    string RequesterName,
    string RequesterTier,
    string DtcTitle,
    double Latitude,
    double Longitude,
    double DistanceMiles,
    double EtaMinutes)
{
    /// <summary>
    /// Projects the wire payload onto the domain-facing <see cref="JobOfferPayload"/>: parses the tier
    /// name case-insensitively (falling back to <see cref="ServiceTier.None"/> if unrecognised), rounds
    /// the ETA to whole minutes, and maps <c>Latitude</c>/<c>Longitude</c> onto <c>Lat</c>/<c>Lng</c>.
    /// <c>RequestId</c> is intentionally not surfaced — the UI keys off the offer id.
    /// </summary>
    public JobOfferPayload ToJobOfferPayload() =>
        new(
            OfferId,
            RequesterName,
            Enum.TryParse<ServiceTier>(RequesterTier, ignoreCase: true, out var tier) ? tier : ServiceTier.None,
            DtcTitle,
            DistanceMiles,
            (int)Math.Round(EtaMinutes, MidpointRounding.AwayFromZero),
            Latitude,
            Longitude);
}
