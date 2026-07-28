namespace ServiceDelivery.Client.Core.Models;

/// <summary>
/// Wire-format DTO for the <c>POST /dispatcher/redirect</c> 200 response (FE-005). Its property names and
/// types mirror the backend <c>RedirectRepResult</c> schema EXACTLY — <c>RepId</c>, <c>FromRequestId</c>,
/// <c>ToRequestId</c>, <c>RepState</c> — so System.Text.Json (Web defaults / camelCase) binds every field
/// without a mapping step. Backed by a captured-payload deserialization test (ADR-0011 / the frontend
/// CLAUDE.md wire-contract rule) so a field-name drift cannot pass coincidentally.
/// </summary>
public record RedirectRepResultDto(
    Guid RepId,
    Guid FromRequestId,
    Guid ToRequestId,
    string RepState);
