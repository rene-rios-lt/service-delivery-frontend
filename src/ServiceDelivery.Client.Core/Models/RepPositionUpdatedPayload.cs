namespace ServiceDelivery.Client.Core.Models;

/// <summary>
/// Wire-safe DTO for the RequesterHub <c>RepPositionUpdated</c> SignalR event (FE-017/AC-3). Property names
/// mirror the backend <c>ServiceDelivery.Application.Common.Interfaces.Payloads.RepPositionUpdatedPayload</c>
/// exactly (<c>Latitude</c>, <c>Longitude</c>, <c>EtaMinutes</c>, <c>State</c>) so System.Text.Json (Web
/// defaults, camelCase on the wire) binds every field without a separate wire-DTO mapping step. Each live
/// position push carries the rep's new coordinates, the updated ETA in minutes, and the rep's current state
/// string (<c>"EnRoute"</c>, <c>"Within15Miles"</c>, <c>"OnSite"</c>). The captured-payload deserialization
/// test guards against field-name drift (ADR-0011 / the frontend CLAUDE.md wire-contract rule).
/// </summary>
public record RepPositionUpdatedPayload(
    double Latitude,
    double Longitude,
    double EtaMinutes,
    string State);
