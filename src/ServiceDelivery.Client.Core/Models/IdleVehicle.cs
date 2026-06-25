using System.Text.Json.Serialization;

namespace ServiceDelivery.Client.Core.Models;

/// <summary>
/// An idle vehicle a service rep may take over, as returned by GET /vehicles/available
/// (backend <c>AvailableVehicleDto</c>). Immutable view model shape — the rep picks one of
/// these to supersede the simulator's claim. <c>Model</c> binds to the backend's <c>model</c>
/// JSON field and <c>EquipmentTypes</c> binds to <c>equipment</c>; both are mapped by JSON
/// property name rather than record position so a contract change is explicit.
/// </summary>
public record IdleVehicle(
    Guid VehicleId,
    string Registration,
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("equipment")] IReadOnlyList<string> EquipmentTypes);
