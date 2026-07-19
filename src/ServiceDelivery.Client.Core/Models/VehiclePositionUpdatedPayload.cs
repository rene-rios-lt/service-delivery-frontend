namespace ServiceDelivery.Client.Core.Models;

/// <summary>
/// Wire-format DTO for the <c>VehiclePositionHub</c> <c>VehiclePositionUpdated</c> event
/// (<c>/hubs/position</c>, FE-003). Its property names and types mirror the backend
/// <c>VehiclePositionUpdatedPayload</c> record EXACTLY — <c>RepId</c>, <c>VehicleId</c>, <c>Latitude</c>,
/// <c>Longitude</c>, <c>State</c> — so System.Text.Json (Web defaults / camelCase) binds every field over
/// SignalR. The event carries ONLY live position + state (not registration / name / tier / human-controlled),
/// so the ViewModel merges it into the existing snapshot entry rather than replacing it wholesale.
/// </summary>
public record VehiclePositionUpdatedPayload(
    Guid RepId,
    Guid VehicleId,
    double Latitude,
    double Longitude,
    string State)
{
    /// <summary>
    /// Projects the position event onto a <see cref="FleetVehicleEntry"/> carrying the fields the event
    /// actually holds: the vehicle id as the string marker key, the claiming rep id (<c>Guid.Empty</c>
    /// meaning unclaimed → <c>null</c>), the live lat/lng, and the rep state. Snapshot-only metadata
    /// (registration / name / active request / human-controlled) is left at its unknown default — used only
    /// for a vehicle the ViewModel has not yet seen in the REST snapshot.
    /// </summary>
    public FleetVehicleEntry ToFleetVehicleEntry() =>
        new(
            VehicleId.ToString(),
            Registration: string.Empty,
            RepState: State,
            RepId: RepId == Guid.Empty ? null : RepId,
            RepName: null,
            Latitude: Latitude,
            Longitude: Longitude,
            ActiveRequestTitle: null,
            ActiveRequestTier: null,
            HumanControlled: false);
}
