using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.Services;

namespace ServiceDelivery.Client.Tests.Dispatcher;

/// <summary>
/// Pure xUnit tests for <see cref="RedirectEligibilityService"/> (FE-005 ACs 1, 2, 5). No bUnit, no mocks —
/// the service is a pure function over the current queue entry and the fleet snapshot. Covers the eligibility
/// rules (EnRoute + strictly-lower tier → eligible; same-tier / Within15Miles / OnSite → ineligible) and the
/// cooldown computation from the rep's REAL <see cref="FleetVehicleEntry.RedirectCooldownExpiresAt"/>
/// (future → true; null → false; past → false).
/// </summary>
public class RedirectEligibilityServiceTests
{
    private readonly RedirectEligibilityService _service = new();

    private static readonly Guid RepId = Guid.Parse("50000000-0000-0000-0000-000000000001");
    private static readonly Guid RequestId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000009");

    private static ActiveRequestEntry Request(
        ServiceTier tier = ServiceTier.Gold, string dtcTitle = "Transmission Control Fault") =>
        new(RequestId, "Marcus Webb", tier, dtcTitle, "Pending", null, DateTimeOffset.UtcNow);

    private static FleetVehicleEntry Rep(
        string? repState = "EnRoute",
        string? activeRequestTier = "Silver",
        string? activeRequestTitle = "Hydraulic Pressure Loss",
        DateTimeOffset? redirectCooldownExpiresAt = null,
        Guid? repId = null,
        string? repName = "J. Tran") =>
        new(
            VehicleId: "30000000-0000-0000-0000-000000000007",
            Registration: "IA-4471",
            RepState: repState,
            RepId: repId ?? RepId,
            RepName: repName,
            Latitude: 41.8781,
            Longitude: -93.0977,
            ActiveRequestTitle: activeRequestTitle,
            ActiveRequestTier: activeRequestTier,
            HumanControlled: false,
            RedirectCooldownExpiresAt: redirectCooldownExpiresAt);

    // ---- AC-1: eligible EnRoute + lower-tier match ------------------------------------------------------

    [Fact]
    public void GivenGoldRequestAndEnRouteRepOnSilverJob_WhenFindEligibleRedirectCalled_ThenRedirectInfoReturned()
    {
        // Arrange — a Gold request and an EnRoute rep currently serving a Silver (lower-tier) job.
        var request = Request(tier: ServiceTier.Gold, dtcTitle: "Transmission Control Fault");
        var fleet = new[] { Rep(activeRequestTier: "Silver", activeRequestTitle: "Hydraulic Pressure Loss") };

        // Act
        var info = _service.FindEligibleRedirect(request, fleet);

        // Assert — the info carries the rep, the swap-card job details, and the POST target request id.
        Assert.NotNull(info);
        Assert.Equal(RepId, info!.RepId);
        Assert.Equal("J. Tran", info.RepName);
        Assert.Equal(ServiceTier.Silver, info.CurrentJobTier);
        Assert.Equal("Hydraulic Pressure Loss", info.CurrentJobTitle);
        Assert.Equal(ServiceTier.Gold, info.NewJobTier);
        Assert.Equal("Transmission Control Fault", info.NewJobTitle);
        Assert.Equal(RequestId, info.ToRequestId);
    }

    [Fact]
    public void GivenEnRouteRepWithSameTierActiveRequest_WhenEligibilityComputed_ThenNullReturned()
    {
        // Arrange — same tier is not "higher priority", so no redirect is offered.
        var request = Request(tier: ServiceTier.Silver);
        var fleet = new[] { Rep(activeRequestTier: "Silver") };

        // Act
        var info = _service.FindEligibleRedirect(request, fleet);

        // Assert
        Assert.Null(info);
    }

    // ---- AC-2: cooldown computation from the real RedirectCooldownExpiresAt field -----------------------

    [Fact]
    public void GivenEnRouteRepWithFutureRedirectCooldownExpiresAt_WhenFindEligibleRedirectCalled_ThenInCooldownTrue()
    {
        // Arrange — a cooldown ending in the future means the rep is still in the 5-min redirect cooldown.
        var request = Request(tier: ServiceTier.Gold);
        var fleet = new[]
        {
            Rep(redirectCooldownExpiresAt: DateTimeOffset.UtcNow.AddMinutes(3)),
        };

        // Act
        var info = _service.FindEligibleRedirect(request, fleet);

        // Assert
        Assert.NotNull(info);
        Assert.True(info!.InCooldown);
    }

    [Fact]
    public void GivenEnRouteRepWithNullRedirectCooldownExpiresAt_WhenFindEligibleRedirectCalled_ThenInCooldownFalse()
    {
        // Arrange — null means the rep has never been redirected: not in cooldown.
        var request = Request(tier: ServiceTier.Gold);
        var fleet = new[] { Rep(redirectCooldownExpiresAt: null) };

        // Act
        var info = _service.FindEligibleRedirect(request, fleet);

        // Assert
        Assert.NotNull(info);
        Assert.False(info!.InCooldown);
    }

    [Fact]
    public void GivenEnRouteRepWithPastRedirectCooldownExpiresAt_WhenFindEligibleRedirectCalled_ThenInCooldownFalse()
    {
        // Arrange — a cooldown that ended in the past has elapsed: not in cooldown.
        var request = Request(tier: ServiceTier.Gold);
        var fleet = new[]
        {
            Rep(redirectCooldownExpiresAt: DateTimeOffset.UtcNow.AddMinutes(-1)),
        };

        // Act
        var info = _service.FindEligibleRedirect(request, fleet);

        // Assert
        Assert.NotNull(info);
        Assert.False(info!.InCooldown);
    }

    // ---- AC-5: proximity states that exclude a redirect -------------------------------------------------

    [Fact]
    public void GivenWithin15MilesRep_WhenFindEligibleRedirectCalled_ThenNullReturned()
    {
        // Arrange — a rep within 15 miles of its requester cannot be redirected (backend proximity guard).
        var request = Request(tier: ServiceTier.Gold);
        var fleet = new[] { Rep(repState: "Within15Miles", activeRequestTier: "Silver") };

        // Act
        var info = _service.FindEligibleRedirect(request, fleet);

        // Assert
        Assert.Null(info);
    }

    [Fact]
    public void GivenOnSiteRep_WhenFindEligibleRedirectCalled_ThenNullReturned()
    {
        // Arrange — an OnSite rep is already working the job and cannot be redirected.
        var request = Request(tier: ServiceTier.Gold);
        var fleet = new[] { Rep(repState: "OnSite", activeRequestTier: "Silver") };

        // Act
        var info = _service.FindEligibleRedirect(request, fleet);

        // Assert
        Assert.Null(info);
    }
}
