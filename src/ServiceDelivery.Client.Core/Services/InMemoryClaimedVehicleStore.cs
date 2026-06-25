using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.Services;

/// <summary>
/// Scoped, in-memory implementation of <see cref="IClaimedVehicleStore"/>. Holds a single nullable
/// <see cref="ClaimedVehicle"/> handed from the take-over screen to the idle view within one session
/// scope. Registered in every host bootstrapper so <c>TakeOverViewModel</c> and <c>RepIdleViewModel</c>
/// can always resolve it. Mirrors <see cref="InMemoryJobOfferStore"/>.
/// </summary>
public class InMemoryClaimedVehicleStore : IClaimedVehicleStore
{
    public ClaimedVehicle? CurrentVehicle { get; private set; }

    public void SetVehicle(ClaimedVehicle vehicle) => CurrentVehicle = vehicle;

    public void ClearVehicle() => CurrentVehicle = null;
}
