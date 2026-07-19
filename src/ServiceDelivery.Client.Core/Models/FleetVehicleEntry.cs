namespace ServiceDelivery.Client.Core.Models;

/// <summary>
/// Clean, host-agnostic model for one vehicle on the dispatcher fleet map (FE-003). Populated from the
/// initial <c>GET /dispatcher/fleet</c> REST snapshot (via <see cref="DispatcherFleetEntryDto"/>) and
/// merged/updated by the <c>VehiclePositionUpdated</c> SignalR event (via
/// <see cref="VehiclePositionUpdatedPayload"/>).
/// <para>
/// <b>Field sourcing note (ADR-0011 / wire-contract integrity).</b> <see cref="VehicleId"/> is the string
/// marker key and the argument to <c>SelectVehicle</c>. <see cref="RepState"/> carries the backend rep-state
/// enum name (<c>Available</c> / <c>EnRoute</c> / <c>Within15Miles</c> / <c>OnSite</c> / <c>Offline</c>);
/// <see cref="RepId"/> is <c>null</c> when the vehicle is unclaimed. <see cref="ActiveRequestTier"/> is the
/// present-when-assigned signal the popover gates its active-request section on.
/// <see cref="ActiveRequestTitle"/> is sourced from the <c>activeRequestTitle</c> field of the
/// <c>GET /dispatcher/fleet</c> contract (added by BE-032) — the DTC title of the active request, <c>null</c>
/// when the rep has no active request; the popover renders the title line only when it is present.
/// </para>
/// </summary>
public record FleetVehicleEntry(
    string VehicleId,
    string Registration,
    string? RepState,
    Guid? RepId,
    string? RepName,
    double Latitude,
    double Longitude,
    string? ActiveRequestTitle,
    string? ActiveRequestTier,
    bool HumanControlled);
