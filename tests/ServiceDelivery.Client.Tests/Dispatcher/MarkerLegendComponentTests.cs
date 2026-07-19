using Bunit;
using ServiceDelivery.Client.UI.Features.Dispatcher.Components;

namespace ServiceDelivery.Client.Tests.Dispatcher;

/// <summary>
/// AC-6 — the fleet marker legend (mockup: bottom-left legend box). Asserts the parameterless
/// <see cref="MarkerLegend"/> renders exactly the five fixed rows (Available / En Route / Within 15 mi /
/// On Site / Offline), each carrying its <c>RepStateColour</c> hex token and label text.
/// </summary>
public class MarkerLegendComponentTests : BunitContext
{
    [Fact]
    public void GivenMarkerLegend_WhenRendered_ThenFiveLegendRowsArePresent()
    {
        // Arrange & Act
        var cut = Render<MarkerLegend>();

        // Assert
        Assert.NotNull(cut.Find("[data-testid='fleet-legend']"));
        Assert.Equal(5, cut.FindAll(".sd-legend__row").Count);
    }

    [Theory]
    [InlineData("Available", "Available", "#2E9E5B")]
    [InlineData("EnRoute", "En Route", "#1E88E5")]
    [InlineData("Within15Miles", "Within 15 mi", "#F4A100")]
    [InlineData("OnSite", "On Site", "#E5392F")]
    [InlineData("Offline", "Offline", "#9AA0AE")]
    public void GivenMarkerLegend_WhenRendered_ThenEachRowHasCorrectColourTokenAndLabel(
        string state, string label, string hex)
    {
        // Arrange & Act
        var cut = Render<MarkerLegend>();

        // Assert
        var row = cut.Find($"[data-testid='legend-row-{state}']");
        Assert.Contains(label, row.TextContent);
        var dot = row.QuerySelector(".sd-legend__dot");
        Assert.NotNull(dot);
        Assert.Contains(hex, dot!.GetAttribute("style"));
    }
}
