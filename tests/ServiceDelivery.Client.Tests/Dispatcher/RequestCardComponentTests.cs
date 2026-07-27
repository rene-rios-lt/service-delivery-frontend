using Bunit;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.UI.Features.Dispatcher.Components;

namespace ServiceDelivery.Client.Tests.Dispatcher;

/// <summary>
/// AC-2 — the request queue card (mockup: dispatcher-dashboard "Marcus Webb ★ GOLD Pending" card). Asserts
/// each field renders (requester name, DTC title, tier badge + colour class, status chip text, assigned-rep
/// line with the "Unassigned" fallback, and the relative time), driven only by the injected
/// <see cref="ActiveRequestEntry"/> parameter.
/// </summary>
public class RequestCardComponentTests : BunitContext
{
    private static ActiveRequestEntry Entry(
        string requesterName = "Marcus Webb",
        ServiceTier tier = ServiceTier.Gold,
        string dtcTitle = "Transmission Control Fault",
        string status = "Pending",
        string? assignedRepName = null,
        DateTimeOffset? createdAt = null) =>
        new(
            Guid.Parse("aaaaaaaa-0000-0000-0000-000000000009"),
            requesterName,
            tier,
            dtcTitle,
            status,
            assignedRepName,
            createdAt ?? DateTimeOffset.UtcNow.AddMinutes(-1));

    private IRenderedComponent<RequestCard> RenderCard(ActiveRequestEntry entry) =>
        Render<RequestCard>(p => p.Add(c => c.Entry, entry));

    [Fact]
    public void GivenARequestEntry_WhenCardRendered_ThenRequesterNameIsDisplayed()
    {
        // Arrange
        var entry = Entry(requesterName: "Marcus Webb");

        // Act
        var cut = RenderCard(entry);

        // Assert
        Assert.Contains("Marcus Webb", cut.Find("[data-testid='reqcard-name']").TextContent);
    }

    [Theory]
    [InlineData(ServiceTier.Gold, "sd-badge--gold", "GOLD")]
    [InlineData(ServiceTier.Silver, "sd-badge--silver", "SILVER")]
    [InlineData(ServiceTier.Bronze, "sd-badge--bronze", "BRONZE")]
    public void GivenATieredRequestEntry_WhenCardRendered_ThenTierBadgeClassAndLabelApplied(
        ServiceTier tier, string expectedClass, string expectedLabel)
    {
        // Arrange
        var entry = Entry(tier: tier);

        // Act
        var cut = RenderCard(entry);

        // Assert
        var badge = cut.Find("[data-testid='reqcard-tier-badge']");
        Assert.Contains(expectedClass, badge.GetAttribute("class"));
        Assert.Contains(expectedLabel, badge.TextContent);
    }

    [Fact]
    public void GivenARequestEntry_WhenCardRendered_ThenDtcTitleIsDisplayed()
    {
        // Arrange
        var entry = Entry(dtcTitle: "Hydraulic Pressure Loss");

        // Act
        var cut = RenderCard(entry);

        // Assert
        Assert.Contains("Hydraulic Pressure Loss", cut.Find("[data-testid='reqcard-dtc']").TextContent);
    }

    [Theory]
    [InlineData("Pending", "sd-chip--pending", "Pending")]
    [InlineData("Assigned", "sd-chip--enroute", "Assigned")]
    [InlineData("InProgress", "sd-chip--onsite", "In Progress")]
    public void GivenARequestStatus_WhenCardRendered_ThenStatusChipClassAndLabelApplied(
        string status, string expectedClass, string expectedLabel)
    {
        // Arrange
        var entry = Entry(status: status);

        // Act
        var cut = RenderCard(entry);

        // Assert
        var chip = cut.Find("[data-testid='status-chip']");
        Assert.Contains(expectedClass, chip.GetAttribute("class"));
        Assert.Equal(expectedLabel, chip.TextContent.Trim());
    }

    [Fact]
    public void GivenAnUnassignedRequest_WhenCardRendered_ThenAssignedRepTextIsUnassigned()
    {
        // Arrange — a Pending request has no assigned rep.
        var entry = Entry(status: "Pending", assignedRepName: null);

        // Act
        var cut = RenderCard(entry);

        // Assert
        Assert.Contains("Unassigned", cut.Find("[data-testid='reqcard-rep']").TextContent);
    }

    [Fact]
    public void GivenAnAssignedRequest_WhenCardRendered_ThenAssignedRepNameIsDisplayed()
    {
        // Arrange
        var entry = Entry(status: "Assigned", assignedRepName: "J. Tran");

        // Act
        var cut = RenderCard(entry);

        // Assert
        Assert.Contains("J. Tran", cut.Find("[data-testid='reqcard-rep']").TextContent);
    }

    [Fact]
    public void GivenARequestEntry_WhenCardRendered_ThenCreatedAtTimeIsDisplayed()
    {
        // Arrange — created ~4.5 minutes ago renders a stable "4 min ago" (floor of elapsed minutes).
        var entry = Entry(createdAt: DateTimeOffset.UtcNow.AddMinutes(-4).AddSeconds(-30));

        // Act
        var cut = RenderCard(entry);

        // Assert
        Assert.Contains("4 min ago", cut.Find("[data-testid='reqcard-time']").TextContent);
    }

    [Fact]
    public void GivenARequestEntry_WhenCardRendered_ThenCardCarriesTierBorderClassAndRequestScopedTestId()
    {
        // Arrange — the left-border colour comes from the tier modifier; the card is keyed by request id so
        // the E2E/real-time suites can locate a specific card.
        var entry = Entry(tier: ServiceTier.Gold);

        // Act
        var cut = RenderCard(entry);

        // Assert
        var card = cut.Find("[data-testid='request-card-aaaaaaaa-0000-0000-0000-000000000009']");
        Assert.Contains("sd-reqcard", card.GetAttribute("class"));
        Assert.Contains("sd-reqcard--gold", card.GetAttribute("class"));
    }
}
