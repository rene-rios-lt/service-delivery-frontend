namespace ServiceDelivery.Client.Core.Models;

/// <summary>
/// Wire-safe DTO for the RepHub <c>VehicleForceReleased</c> SignalR event (FE-022/AC-8). Property names
/// mirror the backend <c>ServiceDelivery.Application.Common.Interfaces.Payloads.VehicleForceReleasedPayload</c>
/// exactly (<c>VehicleId</c>, <c>Registration</c>) so System.Text.Json (Web defaults, camelCase on the wire)
/// binds every field without a separate wire-DTO mapping step. The backend emits it (BE-007/BE-025) when a
/// dispatcher force-releases a vehicle, notifying the affected rep that its session is revoked. FE-022 covers
/// the DISPATCHER side only; the rep's client-side session-revoked handling is a future ServiceRep story
/// (scope constraint 2). The captured-payload deserialization test guards against field-name drift
/// (ADR-0011 / the frontend CLAUDE.md wire-contract rule).
/// </summary>
public record VehicleForceReleasedPayload(Guid VehicleId, string Registration);
