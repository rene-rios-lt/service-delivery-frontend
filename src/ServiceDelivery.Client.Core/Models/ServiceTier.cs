namespace ServiceDelivery.Client.Core.Models;

/// <summary>
/// Service tier mirroring the backend <c>ServiceDelivery.Domain.Enums.ServiceTier</c>.
/// The backend serializes <c>tier</c> as a NUMBER (default System.Text.Json, no
/// JsonStringEnumConverter), so the ordinal order here must match the backend exactly
/// for GET /users/me to deserialize correctly. Backend: None=0, Bronze=1, Silver=2, Gold=3.
/// </summary>
public enum ServiceTier
{
    None,
    Bronze,
    Silver,
    Gold
}
