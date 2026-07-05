namespace ServiceDelivery.Client.Core.Models;

/// <summary>
/// Wire-safe DTO for the RequesterHub <c>ServiceCompleted</c> SignalR event (FE-019). Mirrors the backend
/// <c>ServiceDelivery.Application.Common.Interfaces.Payloads.ServiceCompletedPayload</c> EXACTLY — a single
/// <c>RequestId</c> — so System.Text.Json (Web defaults, camelCase on the wire) binds it without a separate
/// wire-DTO mapping step. It is the navigation TRIGGER only: the completion screen's display data (rep name,
/// DTC title) is assembled from CLIENT state (<see cref="ServiceCompletionData"/> in
/// <c>IServiceCompletedStore</c>), never carried on this payload. The captured-payload deserialization test
/// guards against field-name drift (ADR-0011 / the frontend CLAUDE.md wire-contract rule).
/// </summary>
public record ServiceCompletedPayload(Guid RequestId);
