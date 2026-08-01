namespace ServiceDelivery.Client.Core.Models;

/// <summary>
/// Host-agnostic view model for a single force-release action on the dispatcher fleet map (FE-022). Built by
/// <see cref="ViewModels.DispatcherFleetViewModel.OpenForceReleaseAsync"/> from the selected
/// <see cref="FleetVehicleEntry"/>. Carries everything the force-release confirmation dialog renders
/// (<see cref="RepName"/>, <see cref="Registration"/>, the optional <see cref="RequestTitle"/> to be re-queued)
/// and the id the <c>POST /vehicles/{id}/force-release</c> route needs (<see cref="VehicleId"/>, the parsed
/// marker key). <see cref="RequestTitle"/> is null when the rep has no active request.
/// </summary>
public record ForceReleaseInfo(
    Guid VehicleId,
    string RepName,
    string Registration,
    string? RequestTitle);
