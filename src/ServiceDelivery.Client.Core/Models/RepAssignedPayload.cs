namespace ServiceDelivery.Client.Core.Models;

/// <summary>
/// Wire-safe DTO for the RequesterHub <c>RepAssigned</c> SignalR event (FE-016/AC-3). Property names
/// mirror the backend <c>ServiceDelivery.Application.Common.Interfaces.Payloads.RepAssignedPayload</c>
/// exactly (<c>RepId</c>, <c>RepName</c>, <c>EtaMinutes</c>, <c>Latitude</c>, <c>Longitude</c>) so
/// System.Text.Json (Web defaults, camelCase on the wire) binds all five fields without a separate
/// wire-DTO mapping step. The captured-payload deserialization test guards against field-name drift
/// (ADR-0011 / the frontend CLAUDE.md wire-contract rule).
/// </summary>
public record RepAssignedPayload(
    Guid RepId,
    string RepName,
    double EtaMinutes,
    double Latitude,
    double Longitude);
