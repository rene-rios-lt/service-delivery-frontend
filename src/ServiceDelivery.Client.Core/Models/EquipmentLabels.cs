namespace ServiceDelivery.Client.Core.Models;

/// <summary>
/// Single source of truth for mapping the raw EquipmentType enum names the backend returns
/// (DTOs serialise <c>EquipmentType.ToString()</c> — verified against
/// <c>ServiceDelivery.Domain.Enums.EquipmentType</c>) to the short, human-friendly chip labels
/// shown in the rep views. Both the take-over list (<c>IdleVehicleList</c>) and the idle card
/// (<c>RepIdle</c>) render through <see cref="FriendlyLabel"/> so the two screens never disagree.
/// Unknown keys fall through to the raw string so a new enum value never renders blank.
/// </summary>
public static class EquipmentLabels
{
    private static readonly IReadOnlyDictionary<string, string> Labels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["HydraulicTool"]            = "Hydraulics",
            ["ElectricalDiagnosticKit"] = "Diagnostics",
            ["TransmissionKit"]         = "Transmission",
            ["BrakingSystemKit"]        = "Braking",
            ["CoolingSystemKit"]        = "Coolant",
            ["FuelSystemKit"]           = "Fuel",
            ["ExhaustSystemKit"]        = "Exhaust",
            ["SuspensionKit"]           = "Suspension",
            ["SteeringKit"]             = "Steering",
            ["PowertrainKit"]           = "Powertrain",
        };

    public static string FriendlyLabel(string raw) =>
        Labels.TryGetValue(raw, out var label) ? label : raw;
}
