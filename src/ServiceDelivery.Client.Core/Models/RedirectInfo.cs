namespace ServiceDelivery.Client.Core.Models;

/// <summary>
/// Host-agnostic view model for a single redirect opportunity on the dispatcher queue (FE-005). Computed by
/// <see cref="Interfaces.IRedirectEligibilityService"/> from an <see cref="ActiveRequestEntry"/> (the
/// higher-priority target) and the fleet snapshot (the EnRoute rep on a lower-tier job). Carries everything
/// the redirect confirmation dialog renders and everything the <c>POST /dispatcher/redirect</c> body needs
/// (<see cref="RepId"/> + <see cref="ToRequestId"/>).
/// <para>
/// <see cref="CurrentJobTier"/> / <see cref="CurrentJobTitle"/> describe the rep's present (lower-tier) job;
/// <see cref="NewJobTier"/> / <see cref="NewJobTitle"/> describe the higher-priority request they would be
/// redirected to. <see cref="InCooldown"/> is <c>true</c> only when the rep's real
/// <see cref="FleetVehicleEntry.RedirectCooldownExpiresAt"/> is a future timestamp (null / past → false).
/// </para>
/// </summary>
public record RedirectInfo(
    Guid RepId,
    string RepName,
    ServiceTier CurrentJobTier,
    string CurrentJobTitle,
    ServiceTier NewJobTier,
    string NewJobTitle,
    bool InCooldown,
    Guid ToRequestId);
