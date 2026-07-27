namespace ServiceDelivery.Client.Core.Models;

/// <summary>
/// Wire-format DTO for the DispatchHub <c>ServiceRequestPending</c> event (FE-004). Its property names and
/// types mirror the backend <c>ServiceRequestPendingPayload</c> EXACTLY — <c>RequestId</c>,
/// <c>RequesterTier</c> (the tier enum-name string, e.g. "Gold"), <c>DtcTitle</c>, and <c>Location</c> (a
/// "lat,lng" string) — so System.Text.Json binds every field over SignalR. This event carries no
/// <c>requesterName</c>, so the ViewModel does a follow-up <c>GetRequestAsync</c> fetch to build the queue
/// card (ADR-0011 captured-payload contract test guards the field-name match).
/// </summary>
public record ServiceRequestPendingPayload(
    Guid RequestId,
    string RequesterTier,
    string DtcTitle,
    string Location);
