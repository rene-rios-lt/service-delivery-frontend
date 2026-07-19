using Bunit;
using Microsoft.AspNetCore.Components;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.UI.Features.Dispatcher.Components;

namespace ServiceDelivery.Client.Tests.Dispatcher;

/// <summary>
/// ACs 5 + 8 — the rep marker popover (mockup: "J. Tran · En Route" card). Asserts the assigned /
/// unassigned / human-controlled / simulator-controlled variants render the right data-testid content, and
/// that the close button raises <c>OnClose</c>.
/// </summary>
public class RepMarkerPopoverTests : BunitContext
{
    private static FleetVehicleEntry Entry(
        string repName = "J. Tran",
        string repState = "EnRoute",
        string registration = "IA-4471",
        string? activeRequestTitle = null,
        string? activeRequestTier = null,
        bool humanControlled = false) =>
        new("30000000-0000-0000-0000-000000000007", registration, repState, Guid.NewGuid(),
            repName, 41.60, -93.60, activeRequestTitle, activeRequestTier, humanControlled);

    private IRenderedComponent<RepMarkerPopover> RenderPopover(
        FleetVehicleEntry entry, EventCallback? onClose = null) =>
        Render<RepMarkerPopover>(p =>
        {
            p.Add(c => c.Entry, entry);
            if (onClose is not null)
            {
                p.Add(c => c.OnClose, onClose.Value);
            }
        });

    [Fact]
    public void GivenFleetEntryWithEnRouteState_WhenPopoverRendered_ThenRepNameStateAndRegistrationAreVisible()
    {
        // Arrange
        var entry = Entry(repName: "J. Tran", repState: "EnRoute", registration: "IA-4471");

        // Act
        var cut = RenderPopover(entry);

        // Assert
        Assert.Contains("J. Tran", cut.Find("[data-testid='popover-rep-name']").TextContent);
        Assert.Contains("En Route", cut.Find("[data-testid='popover-state-chip']").TextContent);
        Assert.Contains("IA-4471", cut.Find("[data-testid='popover-registration']").TextContent);
    }

    [Fact]
    public void GivenFleetEntryWithActiveRequest_WhenPopoverRendered_ThenDtcTitleAndTierBadgeAreVisible()
    {
        // Arrange
        var entry = Entry(activeRequestTitle: "Hydraulic Pressure Loss", activeRequestTier: "Silver");

        // Act
        var cut = RenderPopover(entry);

        // Assert
        var section = cut.Find("[data-testid='popover-active-request']");
        Assert.Contains("Hydraulic Pressure Loss", section.TextContent);
        Assert.Contains("Silver", cut.Find("[data-testid='popover-tier-badge']").TextContent);
    }

    [Fact]
    public void GivenFleetEntryWithNoActiveRequest_WhenPopoverRendered_ThenActiveRequestSectionIsAbsent()
    {
        // Arrange — no assignment: activeRequestTier is null (the backend's present-when-assigned signal).
        var entry = Entry(activeRequestTitle: null, activeRequestTier: null);

        // Act
        var cut = RenderPopover(entry);

        // Assert
        Assert.Empty(cut.FindAll("[data-testid='popover-active-request']"));
        Assert.Empty(cut.FindAll("[data-testid='popover-tier-badge']"));
    }

    [Fact]
    public void GivenHumanControlledFleetEntry_WhenPopoverRendered_ThenHumanControlledLabelIsVisible()
    {
        // Arrange
        var entry = Entry(humanControlled: true);

        // Act
        var cut = RenderPopover(entry);

        // Assert
        Assert.NotNull(cut.Find("[data-testid='popover-human-controlled']"));
    }

    [Fact]
    public void GivenSimulatorControlledFleetEntry_WhenPopoverRendered_ThenHumanControlledLabelIsAbsent()
    {
        // Arrange — a simulator-operated rep (HumanControlled false) shows no human-controlled label.
        var entry = Entry(humanControlled: false);

        // Act
        var cut = RenderPopover(entry);

        // Assert
        Assert.Empty(cut.FindAll("[data-testid='popover-human-controlled']"));
    }

    [Fact]
    public void GivenAPopover_WhenCloseClicked_ThenOnCloseIsRaised()
    {
        // Arrange
        var closed = false;
        var onClose = EventCallback.Factory.Create(this, () => closed = true);
        var cut = RenderPopover(Entry(), onClose);

        // Act
        cut.Find("[data-testid='popover-close']").Click();

        // Assert
        Assert.True(closed);
    }
}
