namespace ServiceDelivery.Client.Core.Models;

/// <summary>
/// Wire-format DTO for one entry of the <c>GET /service-requests</c> REST array (FE-004). Its property names
/// and types mirror the backend <c>ActiveServiceRequestDto</c> record EXACTLY — <c>RequestId</c>,
/// <c>RequesterName</c>, <c>Tier</c> (the tier enum-name string, e.g. "Gold"), <c>DtcTitle</c>,
/// <c>Status</c> (the request-status enum-name string: "Pending" / "Assigned" / "InProgress"),
/// <c>AssignedRepId</c> and <c>AssignedRepName</c> (both null when unassigned), <c>CreatedAt</c> — so
/// System.Text.Json (Web defaults / camelCase) binds every field. The clean <see cref="ActiveRequestEntry"/>
/// uses a mapped <see cref="ServiceTier"/> enum, so map at this boundary via <see cref="ToActiveRequestEntry"/>
/// rather than binding the endpoint straight onto the model (ADR-0011 / BUG-036).
/// </summary>
public record ActiveRequestDto(
    Guid RequestId,
    string RequesterName,
    string Tier,
    string DtcTitle,
    string Status,
    Guid? AssignedRepId,
    string? AssignedRepName,
    DateTimeOffset CreatedAt)
{
    /// <summary>
    /// Projects the wire DTO onto the clean queue model: parses the tier name case-insensitively and
    /// <b>throws</b> <see cref="InvalidOperationException"/> when it is unrecognised or missing — failing loud
    /// on wire-contract drift rather than silently defaulting to <see cref="ServiceTier.None"/> (which would
    /// render an invisible tier badge — ADR-0011 / BUG-036).
    /// </summary>
    public ActiveRequestEntry ToActiveRequestEntry() =>
        new(
            RequestId,
            RequesterName,
            Enum.TryParse<ServiceTier>(Tier, ignoreCase: true, out var tier)
                ? tier
                : throw new InvalidOperationException(
                    $"Unrecognised ServiceTier '{Tier}' on GET /service-requests — wire contract drift (ADR-0011 / BUG-036)."),
            DtcTitle,
            Status,
            AssignedRepName,
            CreatedAt);
}
