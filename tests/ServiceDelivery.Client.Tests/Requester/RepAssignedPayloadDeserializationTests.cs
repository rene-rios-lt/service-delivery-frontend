using System.Text.Json;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Tests.Requester;

/// <summary>
/// Cross-process wire-contract proof for the RequesterHub <c>RepAssigned</c> event. The backend emits the
/// payload via <c>RequesterHubService.SendRepAssignedAsync</c> from a <c>RepAssignedPayload</c> record
/// (<c>RepId</c>, <c>RepName</c>, <c>EtaMinutes</c>, <c>Latitude</c>, <c>Longitude</c>); SignalR serializes
/// it camelCase. This test deserializes the REAL captured wire JSON via the same System.Text.Json path the
/// client uses (<see cref="JsonSerializerDefaults.Web"/>), asserting every field by a distinct value so a
/// field-name drift cannot pass coincidentally (ADR-0011 / the frontend CLAUDE.md wire-contract rule).
/// </summary>
public class RepAssignedPayloadDeserializationTests
{
    private const string RealRepAssignedJson =
        """
        {
            "repId": "3f9c1a7e-4444-4d2b-9c1a-5e6f7a8b9c0d",
            "repName": "Marcus Wright",
            "etaMinutes": 7.5,
            "latitude": 41.601,
            "longitude": -93.609
        }
        """;

    private static RepAssignedPayload Deserialize(string json) =>
        JsonSerializer.Deserialize<RepAssignedPayload>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    [Fact]
    public void GivenARepAssignedJsonString_WhenDeserialized_ThenAllFieldsBindCorrectly()
    {
        // Arrange
        var json = RealRepAssignedJson;

        // Act
        var payload = Deserialize(json);

        // Assert
        Assert.Equal(Guid.Parse("3f9c1a7e-4444-4d2b-9c1a-5e6f7a8b9c0d"), payload.RepId);
        Assert.Equal("Marcus Wright", payload.RepName);
        Assert.Equal(7.5, payload.EtaMinutes);
        Assert.Equal(41.601, payload.Latitude);
        Assert.Equal(-93.609, payload.Longitude);
    }
}
