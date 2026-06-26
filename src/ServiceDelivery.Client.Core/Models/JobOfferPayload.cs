using System.Text.Json.Serialization;

namespace ServiceDelivery.Client.Core.Models;

/// <summary>
/// Payload of the <c>JobOfferReceived</c> event pushed from RepHub when a matched job is offered
/// to the rep. Immutable contract shape matching the backend RepHub event (BE-017/BE-019). The
/// idle / waiting-for-offers view consumes this to transition away to the job-offer screen (FE-008).
/// </summary>
/// <remarks>
/// The backend serializes the tier field under the JSON key <c>requesterTier</c> (not <c>tier</c>)
/// and as a *string* (e.g. "Gold"), because its domain enum carries <c>JsonStringEnumConverter</c>.
/// The <see cref="JsonPropertyNameAttribute"/> binds that key onto <see cref="Tier"/>; the string
/// form is decoded by the <c>JsonStringEnumConverter</c> configured on the RepHub connection in
/// <c>SignalRRepHubService</c>. Without both the tier always fell back to <see cref="ServiceTier.None"/>
/// and the tier badge rendered invisible (BUG-036 AC-1).
/// </remarks>
public record JobOfferPayload(
    Guid OfferId,
    string RequesterName,
    [property: JsonPropertyName("requesterTier")] ServiceTier Tier,
    string DtcTitle,
    double DistanceMiles,
    int EtaMinutes,
    double Lat,
    double Lng);
