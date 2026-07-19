namespace ServiceDelivery.Client.Core.Models;

/// <summary>
/// Wire-format DTO for one entry of the <c>GET /dispatcher/fleet</c> REST snapshot (FE-003). Its property
/// names and types mirror the backend <c>DispatcherFleetEntryDto</c> record EXACTLY — <c>RepId</c>,
/// <c>Name</c>, <c>State</c>, <c>VehicleId</c>, <c>Registration</c>, a nested <c>LastPosition</c>
/// (<c>Lat</c>/<c>Lng</c>), <c>ActiveRequestId</c>, <c>ActiveRequestTier</c>, <c>ActiveRequestTitle</c> (the
/// DTC title of the active request; null when unassigned — added by BE-032), <c>HumanControlled</c> — so
/// System.Text.Json (Web defaults / camelCase) binds every field without a mapping step. The clean map
/// model <see cref="FleetVehicleEntry"/> uses different names (flat lat/lng, RepState, RepName), so map at
/// this boundary via <see cref="ToFleetVehicleEntry"/> rather than binding the endpoint straight onto the
/// model (which would silently null the renamed / nested fields — ADR-0011 / BUG-036).
/// </summary>
public record DispatcherFleetEntryDto(
    Guid RepId,
    string? Name,
    string State,
    Guid VehicleId,
    string Registration,
    LastPositionDto? LastPosition,
    Guid? ActiveRequestId,
    string? ActiveRequestTier,
    string? ActiveRequestTitle,
    bool HumanControlled)
{
    /// <summary>
    /// Projects the wire DTO onto the clean map model: <c>Guid.Empty</c> rep id (an unclaimed vehicle)
    /// becomes <c>null</c>; a null <c>LastPosition</c> (a never-positioned vehicle) becomes lat/lng 0.
    /// <see cref="FleetVehicleEntry.ActiveRequestTitle"/> flows straight through from <c>activeRequestTitle</c>
    /// (BE-032) — null when the rep has no active request; the popover renders the title line only when present.
    /// </summary>
    public FleetVehicleEntry ToFleetVehicleEntry() =>
        new(
            VehicleId.ToString(),
            Registration,
            State,
            RepId == Guid.Empty ? null : RepId,
            Name,
            LastPosition?.Lat ?? 0,
            LastPosition?.Lng ?? 0,
            ActiveRequestTitle,
            ActiveRequestTier,
            HumanControlled);
}

/// <summary>Nested last-known position of a fleet vehicle (<c>{ lat, lng }</c>); null when never positioned.</summary>
public record LastPositionDto(double Lat, double Lng);
