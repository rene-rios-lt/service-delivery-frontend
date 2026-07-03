namespace ServiceDelivery.Client.Core.Models;

/// <summary>
/// Wire-safe DTO for the RequesterHub <c>RepAssigned</c> SignalR event (FE-016/AC-3). Property names
/// mirror the backend <c>ServiceDelivery.Application.Common.Interfaces.Payloads.RepAssignedPayload</c>
/// exactly (<c>RepId</c>, <c>RepName</c>, <c>EtaMinutes</c>, <c>Latitude</c>, <c>Longitude</c>,
/// <c>VehicleRegistration</c>) so System.Text.Json (Web defaults, camelCase on the wire) binds every field
/// without a separate wire-DTO mapping step. The captured-payload deserialization test guards against
/// field-name drift (ADR-0011 / the frontend CLAUDE.md wire-contract rule).
///
/// FE-017 added <see cref="VehicleRegistration"/> as the trailing positional field. It is contract-locked
/// to backend story BE-031, which adds <c>vehicleRegistration</c> to the backend record; until BE-031 ships
/// the field is absent from the wire and binds to <c>null</c>/empty (record-position default under
/// <see cref="System.Text.Json.JsonSerializerDefaults.Web"/>). The tracking view degrades gracefully to
/// "Service Rep" when it is null or empty, so FE-017 is safe to merge before BE-031 lands.
/// </summary>
public record RepAssignedPayload(
    Guid RepId,
    string RepName,
    double EtaMinutes,
    double Latitude,
    double Longitude,
    string VehicleRegistration);
