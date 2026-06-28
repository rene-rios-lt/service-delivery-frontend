using ServiceDelivery.Client.Core.Interfaces;

namespace ServiceDelivery.Client.UI.Features.ServiceRep.Services;

/// <summary>
/// Real <see cref="ILogoutSideEffect"/> for the ServiceRep persona (FE-023 + BUG-043). Runs the
/// on-duty teardown before the JWT is cleared: it stops the heartbeat loop immediately so the rep
/// stops being marked human-controlled without waiting for the backend timeout, then clears the
/// claimed-vehicle store so no stale claim leaks into the next session (BUG-043).
///
/// Stop-then-clear ordering is deliberate: stopping first lets the loop's continuation check exit
/// cleanly, rather than letting an in-flight final tick read a null vehicle mid-cycle.
///
/// Lives in <c>UI/Features/ServiceRep/Services</c> alongside the other rep services because it depends
/// on <see cref="IHeartbeatService"/>; it depends only on Core interfaces. Registered at the Mobile
/// composition root in place of <c>NoOpLogoutSideEffect</c> (ServiceRep is Mobile-only); Desktop and
/// Web keep the null-object since no rep is ever on duty there.
/// </summary>
public sealed class ServiceRepLogoutSideEffect : ILogoutSideEffect
{
    private readonly IHeartbeatService _heartbeatService;
    private readonly IClaimedVehicleStore _claimedVehicleStore;

    public ServiceRepLogoutSideEffect(
        IHeartbeatService heartbeatService,
        IClaimedVehicleStore claimedVehicleStore)
    {
        _heartbeatService = heartbeatService;
        _claimedVehicleStore = claimedVehicleStore;
    }

    public async Task RunBeforeTokenClearedAsync()
    {
        await _heartbeatService.StopAsync();
        _claimedVehicleStore.ClearVehicle();
    }
}
