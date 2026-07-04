using System.Text.Json;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Tests.Requester;

/// <summary>
/// Cross-process wire-contract proof for the RequesterHub <c>RepRedirected</c> event (FE-018/AC-4). The
/// backend emits the payload from its <c>RepRedirectedPayload</c> record
/// (<c>OldRepName</c>, <c>NewRepName</c>, <c>NewEtaMinutes</c>) when a new rep accepts a displaced job;
/// SignalR serializes it camelCase. This test deserializes a REAL captured wire JSON via the same
/// System.Text.Json path the client uses (<see cref="JsonSerializerDefaults.Web"/>), asserting every field
/// by a distinct value so a field-name or ordinal drift cannot pass coincidentally (ADR-0011 / the frontend
/// CLAUDE.md wire-contract rule).
/// </summary>
public class RepRedirectedPayloadDeserializationTests
{
    private const string RealRepRedirectedJson =
        """
        {
            "oldRepName": "Jordan Tran",
            "newRepName": "Alex Rivera",
            "newEtaMinutes": 14.0
        }
        """;

    private static RepRedirectedPayload Deserialize(string json) =>
        JsonSerializer.Deserialize<RepRedirectedPayload>(
            json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    [Fact]
    public void GivenARepRedirectedJsonString_WhenDeserialized_ThenAllFieldsBindCorrectly()
    {
        // Arrange — every field carries a distinct value so a field-name or ordinal drift cannot pass
        // coincidentally (the anti-masking distinctness the wire-contract rule demands).
        var json = RealRepRedirectedJson;

        // Act
        var payload = Deserialize(json);

        // Assert
        Assert.Equal("Jordan Tran", payload.OldRepName);
        Assert.Equal("Alex Rivera", payload.NewRepName);
        Assert.Equal(14.0, payload.NewEtaMinutes);
    }
}
