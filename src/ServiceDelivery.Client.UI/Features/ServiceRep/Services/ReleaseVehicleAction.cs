using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.UI.Features.ServiceRep.Services;

/// <summary>
/// The real <see cref="IReleaseVehicleAction"/> for FE-014, replacing the <c>NoOpReleaseVehicleAction</c>
/// null-object at the composition root (Open/Closed — the shell is unchanged). Orchestrates the
/// end-of-shift release: it reads the claimed vehicle from <see cref="IClaimedVehicleStore"/>, asks the
/// rep to confirm via <see cref="IReleaseConfirmation"/>, posts the release via
/// <see cref="IReleaseVehicleService"/>, clears the claimed vehicle, and returns the rep to the
/// take-over screen via <see cref="IPersonaNavigator"/> on success. It depends only on Core
/// abstractions, so it carries no dialog or HTTP technology itself.
/// </summary>
public class ReleaseVehicleAction : IReleaseVehicleAction
{
    private readonly IClaimedVehicleStore _claimedVehicleStore;
    private readonly IReleaseConfirmation _confirmation;
    private readonly IReleaseVehicleService _releaseService;
    private readonly IPersonaNavigator _navigator;

    public ReleaseVehicleAction(
        IClaimedVehicleStore claimedVehicleStore,
        IReleaseConfirmation confirmation,
        IReleaseVehicleService releaseService,
        IPersonaNavigator navigator)
    {
        _claimedVehicleStore = claimedVehicleStore;
        _confirmation = confirmation;
        _releaseService = releaseService;
        _navigator = navigator;
    }

    public bool CanRelease => _claimedVehicleStore.CurrentVehicle is not null;

    public async Task<ReleaseVehicleResult> ReleaseAsync()
    {
        var vehicle = _claimedVehicleStore.CurrentVehicle;
        if (vehicle is null)
        {
            return ReleaseVehicleResult.NothingToRelease;
        }

        var confirmed = await _confirmation.ConfirmAsync(vehicle.Registration);
        if (!confirmed)
        {
            return ReleaseVehicleResult.NothingToRelease;
        }

        var released = await _releaseService.ReleaseAsync(vehicle.VehicleId);
        if (!released)
        {
            return ReleaseVehicleResult.Blocked;
        }

        _claimedVehicleStore.ClearVehicle();
        _navigator.NavigateToTakeOver();
        return ReleaseVehicleResult.Released;
    }
}
