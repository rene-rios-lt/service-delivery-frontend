using System.Text.Json.Serialization;

namespace ServiceDelivery.Client.Core.Models;

/// <summary>
/// An idle vehicle a service rep may take over, as returned by GET /vehicles/available
/// (backend <c>AvailableVehicleDto</c>). Immutable view model shape — the rep picks one of
/// these to supersede the simulator's claim. The C# property is named <c>EquipmentTypes</c>
/// for readability but binds to the backend's <c>equipment</c> JSON field.
/// </summary>
public record IdleVehicle(
    Guid VehicleId,
    string Registration,
    [property: JsonPropertyName("equipment")] IReadOnlyList<string> EquipmentTypes);
