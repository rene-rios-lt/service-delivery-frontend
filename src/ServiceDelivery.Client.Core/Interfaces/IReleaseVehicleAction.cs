using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.Interfaces;

/// <summary>
/// Open/Closed seam for releasing a claimed vehicle back to the fleet. The default
/// <c>NoOpReleaseVehicleAction</c> reports <see cref="CanRelease"/> = false and returns
/// <see cref="ReleaseVehicleResult.NothingToRelease"/>; FE-014 supplies the real release flow.
/// </summary>
public interface IReleaseVehicleAction
{
    bool CanRelease { get; }

    Task<ReleaseVehicleResult> ReleaseAsync();
}
