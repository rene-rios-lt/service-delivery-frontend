namespace ServiceDelivery.Client.Core.Models;

/// <summary>
/// Clean projection of one dispatcher queue item (FE-004). Independent of any wire shape — the REST
/// <see cref="ActiveRequestDto"/> maps onto this via <c>ToActiveRequestEntry</c>, and the real-time
/// DispatchHub events merge into it inside <c>DispatcherRequestQueueViewModel</c>. <see cref="Tier"/> is the
/// mapped <see cref="ServiceTier"/> (so the queue can sort Gold → Silver → Bronze without re-parsing the wire
/// string), and <see cref="Status"/> is the raw request-status name ("Pending" / "Assigned" / "InProgress")
/// which the card maps to its display label + chip colour.
/// </summary>
public record ActiveRequestEntry(
    Guid RequestId,
    string RequesterName,
    ServiceTier Tier,
    string DtcTitle,
    string Status,
    string? AssignedRepName,
    DateTimeOffset CreatedAt);
