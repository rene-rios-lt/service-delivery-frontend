using System.Text.Json;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Tests.Requester;

/// <summary>
/// Cross-process wire-contract proof for the RequesterHub <c>ServiceCompleted</c> event (FE-019). The
/// backend emits the payload via <c>RequesterHubService.SendServiceCompletedAsync</c> from a
/// <c>ServiceCompletedPayload</c> record carrying <c>RequestId</c> only (it is the navigation TRIGGER — the
/// completion screen's subtitle data comes from client state, never the wire). SignalR serializes it
/// camelCase. This test deserializes the REAL captured wire JSON via the same System.Text.Json path the
/// client uses (<see cref="JsonSerializerDefaults.Web"/>), asserting <c>RequestId</c> equals a known,
/// distinct GUID so a field-name drift cannot pass coincidentally (ADR-0011 / the frontend CLAUDE.md
/// wire-contract rule).
/// </summary>
public class ServiceCompletedPayloadDeserializationTests
{
    // Real captured wire shape — the backend record is `record ServiceCompletedPayload(Guid RequestId)`,
    // so the SignalR frame carries exactly one camelCase field. The GUID is a distinct, non-empty value so
    // the assertion cannot pass by coincidence (a defaulted Guid.Empty or a drifted field name both fail).
    private const string RealServiceCompletedJson =
        """
        {
            "requestId": "7c2b5e9a-1111-4a3d-8b2c-9d0e1f2a3b4c"
        }
        """;

    private static ServiceCompletedPayload Deserialize(string json) =>
        JsonSerializer.Deserialize<ServiceCompletedPayload>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    [Fact]
    public void GivenRealWireJson_WhenServiceCompletedPayloadDeserialized_ThenRequestIdMatchesExpectedGuid()
    {
        // Arrange
        var json = RealServiceCompletedJson;

        // Act
        var payload = Deserialize(json);

        // Assert
        Assert.Equal(Guid.Parse("7c2b5e9a-1111-4a3d-8b2c-9d0e1f2a3b4c"), payload.RequestId);
    }
}
