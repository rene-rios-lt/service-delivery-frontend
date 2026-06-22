using System.Threading.Tasks;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.Services;

/// <summary>
/// Null-object default for <see cref="IReleaseVehicleAction"/>. Reports nothing to release and
/// returns a typed result rather than throwing (Liskov-honest). FE-014 registers the real release
/// action at the composition root, replacing this registration with no change to the shell.
/// </summary>
public class NoOpReleaseVehicleAction : IReleaseVehicleAction
{
    public bool CanRelease => false;

    public Task<ReleaseVehicleResult> ReleaseAsync() => Task.FromResult(ReleaseVehicleResult.NothingToRelease);
}
