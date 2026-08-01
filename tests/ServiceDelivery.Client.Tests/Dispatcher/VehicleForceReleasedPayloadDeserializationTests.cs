using System.Text.Json;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Tests.Dispatcher;

/// <summary>
/// FE-022 AC-8 — cross-process wire-contract proof for the RepHub <c>VehicleForceReleased</c> event. The
/// backend emits it (BE-007/BE-025) from its
/// <c>ServiceDelivery.Application.Common.Interfaces.Payloads.VehicleForceReleasedPayload</c> record
/// (<c>VehicleId</c>, <c>Registration</c>) when a dispatcher force-releases a vehicle; SignalR serializes it
/// camelCase. This test deserializes a REAL captured wire JSON via the same System.Text.Json path a client
/// consumer uses (<see cref="JsonSerializerDefaults.Web"/>), asserting every field by a DISTINCT value so a
/// field-name or ordinal drift cannot pass coincidentally (ADR-0011 / the frontend CLAUDE.md wire-contract
/// rule). FE-022 covers the wire contract only — the rep's client-side session-revoked handling is a future
/// ServiceRep story (scope constraint 2).
/// </summary>
public class VehicleForceReleasedPayloadDeserializationTests
{
    // Real backend wire shape: camelCase, GUID string for VehicleId, plain string for Registration. Distinct
    // per-field values (a GUID vs a registration string) so a swapped field name cannot pass by coincidence.
    private const string RealVehicleForceReleasedJson =
        """
        {
            "vehicleId": "30000000-0000-0000-0000-000000000007",
            "registration": "IOW-4471"
        }
        """;

    private static VehicleForceReleasedPayload Deserialize(string json) =>
        JsonSerializer.Deserialize<VehicleForceReleasedPayload>(
            json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    [Fact]
    public void GivenVehicleForceReleasedJson_WhenDeserialized_ThenVehicleIdAndRegistrationMapCorrectly()
    {
        // Arrange — every field carries a distinct value so a field-name or ordinal drift cannot pass
        // coincidentally (the anti-masking distinctness the wire-contract rule demands).
        var json = RealVehicleForceReleasedJson;

        // Act
        var payload = Deserialize(json);

        // Assert
        Assert.Equal(Guid.Parse("30000000-0000-0000-0000-000000000007"), payload.VehicleId);
        Assert.Equal("IOW-4471", payload.Registration);
    }
}
