using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Tests.ServiceRep;

public class EquipmentLabelsTests
{
    [Theory]
    [InlineData("HydraulicTool", "Hydraulics")]
    [InlineData("ElectricalDiagnosticKit", "Diagnostics")]
    [InlineData("TransmissionKit", "Transmission")]
    [InlineData("BrakingSystemKit", "Braking")]
    [InlineData("CoolingSystemKit", "Coolant")]
    [InlineData("FuelSystemKit", "Fuel")]
    [InlineData("ExhaustSystemKit", "Exhaust")]
    [InlineData("SuspensionKit", "Suspension")]
    [InlineData("SteeringKit", "Steering")]
    [InlineData("PowertrainKit", "Powertrain")]
    public void GivenAKnownRawEquipmentName_WhenFriendlyLabelCalled_ThenTheMappedLabelIsReturned(
        string raw, string expected)
    {
        // Arrange & Act
        var label = EquipmentLabels.FriendlyLabel(raw);

        // Assert
        Assert.Equal(expected, label);
    }

    [Fact]
    public void GivenAnUnknownRawEquipmentName_WhenFriendlyLabelCalled_ThenTheRawStringIsReturned()
    {
        // Arrange & Act
        var label = EquipmentLabels.FriendlyLabel("SomeNewUnmappedKit");

        // Assert
        Assert.Equal("SomeNewUnmappedKit", label);
    }
}
