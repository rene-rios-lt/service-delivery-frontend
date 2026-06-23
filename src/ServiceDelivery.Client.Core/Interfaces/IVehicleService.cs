using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.Interfaces;

/// <summary>
/// Vehicle operations the take-over flow needs: list the idle vehicles a rep may claim, and
/// claim one. The implementation (in a host's Services folder) owns the HTTP contract; callers
/// see only domain types (<see cref="IdleVehicle"/>, <see cref="TakeOverResult"/>).
/// </summary>
public interface IVehicleService
{
    Task<IReadOnlyList<IdleVehicle>> GetIdleVehiclesAsync();

    Task<TakeOverResult> TakeOverAsync(Guid vehicleId);
}
