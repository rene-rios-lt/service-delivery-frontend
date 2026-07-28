using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.Interfaces;

/// <summary>
/// Computes whether a given active request can be served by redirecting an EnRoute rep who is currently on a
/// strictly-lower-tier job (FE-005). One focused capability (ISP) — pure logic over the current queue entry
/// and the fleet snapshot, no HTTP and no Blazor — so the dispatcher queue ViewModel depends only on this and
/// is independent of how eligibility is computed.
/// </summary>
public interface IRedirectEligibilityService
{
    /// <summary>
    /// Returns the first eligible redirect for <paramref name="forRequest"/> — an EnRoute rep in
    /// <paramref name="fleet"/> whose active request is a strictly lower <see cref="ServiceTier"/> than the
    /// request's tier — populated as a <see cref="RedirectInfo"/>, or <c>null</c> when no such rep exists.
    /// </summary>
    RedirectInfo? FindEligibleRedirect(ActiveRequestEntry forRequest, IReadOnlyList<FleetVehicleEntry> fleet);
}
