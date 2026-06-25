using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.Interfaces;

/// <summary>
/// Carries the <see cref="ClaimedVehicle"/> from the take-over screen (FE-007) to the idle / waiting
/// view (FE-020). On a successful take-over <c>TakeOverViewModel</c> maps the selected idle vehicle to
/// a <see cref="ClaimedVehicle"/> and deposits it here before navigating to <c>/rep/idle</c>; the idle
/// view model reads it on construction and renders the vehicle the rep actually claimed (BUG-034).
/// Scoped lifetime — one claimed vehicle at a time, cleared once the idle view consumes it. Mirrors
/// <see cref="IJobOfferStore"/>.
/// </summary>
public interface IClaimedVehicleStore
{
    ClaimedVehicle? CurrentVehicle { get; }

    void SetVehicle(ClaimedVehicle vehicle);

    void ClearVehicle();
}
