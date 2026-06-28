using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.Interfaces;

/// <summary>
/// Carries the <see cref="ClaimedVehicle"/> from the take-over screen (FE-007) to the idle / waiting
/// view (FE-020). On a successful take-over <c>TakeOverViewModel</c> maps the selected idle vehicle to
/// a <see cref="ClaimedVehicle"/> and deposits it here before navigating to <c>/rep/idle</c>; the idle
/// view model reads it on construction and renders the vehicle the rep actually claimed (BUG-034).
/// Scoped lifetime, durable for the session — the idle view <em>reads</em> the claimed vehicle without
/// clearing it, so later readers (e.g. <c>ReleaseVehicleAction</c>) still see the currently-claimed
/// vehicle. It is cleared on a successful release, and should also be cleared on logout / go-off-duty
/// (tracked by BUG-043 / FE-023).
/// </summary>
public interface IClaimedVehicleStore
{
    ClaimedVehicle? CurrentVehicle { get; }

    void SetVehicle(ClaimedVehicle vehicle);

    void ClearVehicle();
}
