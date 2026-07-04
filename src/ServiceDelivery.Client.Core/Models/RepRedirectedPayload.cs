namespace ServiceDelivery.Client.Core.Models;

/// <summary>
/// Wire-safe DTO for the RequesterHub <c>RepRedirected</c> SignalR event (FE-018/AC-4). Property names
/// mirror the backend <c>ServiceDelivery.Application.Common.Interfaces.Payloads.RepRedirectedPayload</c>
/// exactly (<c>OldRepName</c>, <c>NewRepName</c>, <c>NewEtaMinutes</c>) so System.Text.Json (Web defaults,
/// camelCase on the wire) binds every field without a separate wire-DTO mapping step. The backend emits it
/// (deferred) when the NEW rep accepts the displaced job (<c>AcceptJobOfferCommandHandler</c>), immediately
/// after the accompanying <c>RepAssigned</c> event that carries the new rep's full picture (position, ETA,
/// name, vehicle registration). This payload carries the banner concern only — the requester-facing apology
/// text and the new rep's name — never the map/ETA state, which comes exclusively from the concurrent
/// <c>RepAssigned</c>. The captured-payload deserialization test guards against field-name drift
/// (ADR-0011 / the frontend CLAUDE.md wire-contract rule).
/// </summary>
public record RepRedirectedPayload(
    string OldRepName,
    string NewRepName,
    double NewEtaMinutes);
