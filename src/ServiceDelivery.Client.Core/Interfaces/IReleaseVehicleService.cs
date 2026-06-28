namespace ServiceDelivery.Client.Core.Interfaces;

/// <summary>
/// The HTTP contract for releasing a claimed vehicle back to the fleet (FE-014). Backs
/// <c>POST /vehicles/{id}/release</c> (BE-006). Kept separate from <c>IVehicleService</c> (ISP):
/// callers of the take-over flow should not depend on release logic and vice versa. Returns
/// <c>true</c> on a 2xx response and <c>false</c> on any non-success — callers never see HTTP status
/// codes.
/// </summary>
public interface IReleaseVehicleService
{
    Task<bool> ReleaseAsync(Guid vehicleId);
}
