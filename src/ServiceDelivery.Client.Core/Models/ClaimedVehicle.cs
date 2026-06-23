using System.Text.Json.Serialization;

namespace ServiceDelivery.Client.Core.Models;

/// <summary>
/// The vehicle a service rep currently has claimed, carried into the idle / waiting-for-offers
/// view after a successful take-over (FE-007). Immutable display shape — registration, model, and
/// the equipment fitted to the truck. The C# property is named <c>EquipmentTypes</c> for
/// readability but binds to the backend's <c>equipment</c> JSON field.
/// </summary>
public record ClaimedVehicle(
    Guid VehicleId,
    string Registration,
    string Model,
    [property: JsonPropertyName("equipment")] IReadOnlyList<string> EquipmentTypes);
