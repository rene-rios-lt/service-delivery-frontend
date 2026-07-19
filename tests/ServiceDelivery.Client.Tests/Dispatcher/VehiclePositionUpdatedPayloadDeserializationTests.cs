using System.Text.Json;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Tests.Dispatcher;

/// <summary>
/// AC-4d — cross-process wire-contract proof for the <c>VehiclePositionHub</c> <c>VehiclePositionUpdated</c>
/// event. The backend emits it from a <c>VehiclePositionUpdatedPayload</c> record whose real shape is
/// <c>{ repId, vehicleId, latitude, longitude, state }</c> (backend
/// <c>Application/Common/Interfaces/Payloads/VehiclePositionUpdatedPayload.cs</c> and docs/api-design.md line
/// 133) — NOT the richer shape the FE-003 plan text sketched. SignalR serializes camelCase via
/// System.Text.Json. This deserializes the REAL captured wire JSON through the same
/// <see cref="JsonSerializerDefaults.Web"/> path the client uses, asserting every field by a distinct value
/// so a field-name drift cannot pass coincidentally (ADR-0011 / the frontend CLAUDE.md wire-contract rule),
/// then proves <c>ToFleetVehicleEntry()</c> maps the fields the event actually carries.
/// </summary>
public class VehiclePositionUpdatedPayloadDeserializationTests
{
    private const string RealVehiclePositionUpdatedJson =
        """
        {
            "repId": "50000000-0000-0000-0000-000000000001",
            "vehicleId": "30000000-0000-0000-0000-000000000007",
            "latitude": 41.8781,
            "longitude": -93.0977,
            "state": "EnRoute"
        }
        """;

    private static VehiclePositionUpdatedPayload Deserialize(string json) =>
        JsonSerializer.Deserialize<VehiclePositionUpdatedPayload>(
            json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    [Fact]
    public void GivenVehiclePositionUpdatedJson_WhenDeserialized_ThenAllFieldsBoundAndMappedCorrectly()
    {
        // Arrange
        var json = RealVehiclePositionUpdatedJson;

        // Act
        var payload = Deserialize(json);
        var entry = payload.ToFleetVehicleEntry();

        // Assert — every wire field binds (distinct values guard against field-name drift).
        Assert.Equal(Guid.Parse("50000000-0000-0000-0000-000000000001"), payload.RepId);
        Assert.Equal(Guid.Parse("30000000-0000-0000-0000-000000000007"), payload.VehicleId);
        Assert.Equal(41.8781, payload.Latitude);
        Assert.Equal(-93.0977, payload.Longitude);
        Assert.Equal("EnRoute", payload.State);

        // Assert — the event carries only position + state, so ToFleetVehicleEntry maps exactly those
        // (VehicleId as the string marker key, RepId, lat/lng, RepState) and leaves the snapshot-only
        // metadata (registration / name / tier / human-controlled) at its unknown default.
        Assert.Equal("30000000-0000-0000-0000-000000000007", entry.VehicleId);
        Assert.Equal(Guid.Parse("50000000-0000-0000-0000-000000000001"), entry.RepId);
        Assert.Equal(41.8781, entry.Latitude);
        Assert.Equal(-93.0977, entry.Longitude);
        Assert.Equal("EnRoute", entry.RepState);
    }
}
