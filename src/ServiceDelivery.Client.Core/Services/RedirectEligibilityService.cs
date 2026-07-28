using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.Services;

/// <summary>
/// Pure-logic <see cref="IRedirectEligibilityService"/> (FE-005). No external dependencies (no HTTP, no
/// Blazor) — a request is redirectable when the fleet holds an EnRoute rep whose active request is a strictly
/// lower <see cref="ServiceTier"/> than the request's own tier. Returns the first such rep as a
/// <see cref="RedirectInfo"/>, computing <see cref="RedirectInfo.InCooldown"/> from the rep's real
/// <see cref="FleetVehicleEntry.RedirectCooldownExpiresAt"/> (null / past → false; future → true).
/// </summary>
public class RedirectEligibilityService : IRedirectEligibilityService
{
    public RedirectInfo? FindEligibleRedirect(
        ActiveRequestEntry forRequest, IReadOnlyList<FleetVehicleEntry> fleet)
    {
        foreach (var vehicle in fleet)
        {
            if (vehicle.RepState != "EnRoute" || vehicle.RepId is not { } repId)
            {
                continue;
            }

            if (!Enum.TryParse<ServiceTier>(vehicle.ActiveRequestTier, ignoreCase: true, out var currentTier)
                || currentTier >= forRequest.Tier)
            {
                continue;
            }

            return new RedirectInfo(
                RepId: repId,
                RepName: vehicle.RepName ?? string.Empty,
                CurrentJobTier: currentTier,
                CurrentJobTitle: vehicle.ActiveRequestTitle ?? string.Empty,
                NewJobTier: forRequest.Tier,
                NewJobTitle: forRequest.DtcTitle,
                InCooldown: vehicle.RedirectCooldownExpiresAt is { } expiry && DateTimeOffset.UtcNow < expiry,
                ToRequestId: forRequest.RequestId);
        }

        return null;
    }
}
