using System.Text.Json;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Tests.Requester;

/// <summary>
/// Cross-process wire-contract proof for the RequesterHub <c>RepPositionUpdated</c> event (FE-017/AC-3).
/// The backend emits the payload from its <c>RepPositionUpdatedPayload</c> record
/// (<c>Latitude</c>, <c>Longitude</c>, <c>EtaMinutes</c>, <c>State</c>); SignalR serializes it camelCase.
/// This test deserializes a REAL captured wire JSON via the same System.Text.Json path the client uses
/// (<see cref="JsonSerializerDefaults.Web"/>), asserting every field by a distinct value so a field-name
/// drift cannot pass coincidentally (ADR-0011 / the frontend CLAUDE.md wire-contract rule).
/// </summary>
public class RepPositionUpdatedPayloadDeserializationTests
{
    private const string RealRepPositionUpdatedJson =
        """
        {
            "latitude": 41.601,
            "longitude": -93.609,
            "etaMinutes": 7.5,
            "state": "EnRoute"
        }
        """;

    private static RepPositionUpdatedPayload Deserialize(string json) =>
        JsonSerializer.Deserialize<RepPositionUpdatedPayload>(
            json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    [Fact]
    public void GivenARepPositionUpdatedJsonString_WhenDeserialized_ThenAllFieldsBindCorrectly()
    {
        // Arrange — every field carries a distinct value so a field-name or ordinal drift cannot pass
        // coincidentally (the anti-masking distinctness the wire-contract rule demands).
        var json = RealRepPositionUpdatedJson;

        // Act
        var payload = Deserialize(json);

        // Assert
        Assert.Equal(41.601, payload.Latitude);
        Assert.Equal(-93.609, payload.Longitude);
        Assert.Equal(7.5, payload.EtaMinutes);
        Assert.Equal("EnRoute", payload.State);
    }
}
