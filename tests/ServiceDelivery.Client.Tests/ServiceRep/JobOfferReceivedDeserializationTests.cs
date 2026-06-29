using System.Text.Json;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Tests.ServiceRep;

/// <summary>
/// Cross-process wire-contract proof for the RepHub <c>JobOfferReceived</c> event. Unlike GET /users/me
/// (where tier is a NUMBER), this SignalR event sends <c>requesterTier</c> as the enum-NAME string —
/// the backend sets it via <c>.Tier.ToString()</c> and SignalR serializes camelCase. These tests
/// deserialize the REAL captured wire JSON via the same System.Text.Json path SignalR uses
/// (<see cref="JsonSerializerDefaults.Web"/>), then map through <see cref="JobOfferReceivedWirePayload.ToJobOfferPayload"/>.
/// Distinct values per field so a field-name drift cannot pass coincidentally; a drifted tier string
/// must THROW (ADR-0011 / BUG-036), never render an invisible <see cref="ServiceTier.None"/> badge.
/// </summary>
public class JobOfferReceivedDeserializationTests
{
    private const string RealJobOfferReceivedJson =
        """
        {
            "offerId": "7a1e4c2b-1111-4f3a-9a2b-0c1d2e3f4a5b",
            "requestId": "9c2f8d3e-2222-4b1c-8d4e-1a2b3c4d5e6f",
            "requesterName": "Marcus",
            "requesterTier": "Gold",
            "dtcTitle": "Transmission Control Fault",
            "latitude": 41.601,
            "longitude": -93.609,
            "distanceMiles": 12.4,
            "etaMinutes": 13.0
        }
        """;

    private static JobOfferReceivedWirePayload Deserialize(string json) =>
        JsonSerializer.Deserialize<JobOfferReceivedWirePayload>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    [Fact]
    public void GivenARealJobOfferReceivedJson_WhenDeserialisedAndMapped_ThenTypedValuesAreCorrect()
    {
        // Arrange
        var wire = Deserialize(RealJobOfferReceivedJson);

        // Act
        var payload = wire.ToJobOfferPayload();

        // Assert
        Assert.Equal(Guid.Parse("7a1e4c2b-1111-4f3a-9a2b-0c1d2e3f4a5b"), payload.OfferId);
        Assert.Equal("Marcus", payload.RequesterName);
        Assert.Equal(ServiceTier.Gold, payload.Tier);
        Assert.Equal("Transmission Control Fault", payload.DtcTitle);
        Assert.Equal(41.601, payload.Lat);
        Assert.Equal(-93.609, payload.Lng);
        Assert.Equal(12.4, payload.DistanceMiles);
        Assert.Equal(13, payload.EtaMinutes);
    }

    [Fact]
    public void GivenRealJsonWithADriftedTier_WhenDeserialisedAndMapped_ThenThrows()
    {
        // Arrange
        var driftedJson = RealJobOfferReceivedJson.Replace("\"requesterTier\": \"Gold\"", "\"requesterTier\": \"Platinum\"");
        var wire = Deserialize(driftedJson);

        // Act
        var act = () => wire.ToJobOfferPayload();

        // Assert
        Assert.Throws<InvalidOperationException>(act);
    }
}
