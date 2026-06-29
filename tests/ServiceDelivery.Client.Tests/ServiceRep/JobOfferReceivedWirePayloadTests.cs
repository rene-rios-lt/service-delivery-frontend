using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Tests.ServiceRep;

/// <summary>
/// BUG-036: the RepHub <c>JobOfferReceived</c> event arrives in the backend's field names/types
/// (<c>RequesterTier</c> string, <c>Latitude</c>/<c>Longitude</c>, <c>EtaMinutes</c> double). These
/// tests pin the wire → domain mapping so the tier resolves to a real <see cref="ServiceTier"/> (and
/// the badge gets its colour) and the coordinates / ETA survive instead of defaulting to zero.
/// </summary>
public class JobOfferReceivedWirePayloadTests
{
    private static JobOfferReceivedWirePayload Wire(
        string requesterTier = "Gold",
        double latitude = 41.6,
        double longitude = -93.6,
        double distanceMiles = 12.4,
        double etaMinutes = 13) =>
        new(
            OfferId: Guid.NewGuid(),
            RequestId: Guid.NewGuid(),
            RequesterName: "Marcus",
            RequesterTier: requesterTier,
            DtcTitle: "Transmission Control Fault",
            Latitude: latitude,
            Longitude: longitude,
            DistanceMiles: distanceMiles,
            EtaMinutes: etaMinutes);

    [Fact]
    public void GivenAGoldWirePayload_WhenMappedToJobOfferPayload_ThenTierIsGold()
    {
        // Arrange
        var wire = Wire(requesterTier: "Gold");

        // Act
        var payload = wire.ToJobOfferPayload();

        // Assert
        Assert.Equal(ServiceTier.Gold, payload.Tier);
    }

    [Fact]
    public void GivenASilverWirePayload_WhenMappedToJobOfferPayload_ThenTierIsSilver()
    {
        // Arrange
        var wire = Wire(requesterTier: "Silver");

        // Act
        var payload = wire.ToJobOfferPayload();

        // Assert
        Assert.Equal(ServiceTier.Silver, payload.Tier);
    }

    [Fact]
    public void GivenABronzeWirePayload_WhenMappedToJobOfferPayload_ThenTierIsBronze()
    {
        // Arrange
        var wire = Wire(requesterTier: "Bronze");

        // Act
        var payload = wire.ToJobOfferPayload();

        // Assert
        Assert.Equal(ServiceTier.Bronze, payload.Tier);
    }

    [Fact]
    public void GivenALowercaseTierName_WhenMappedToJobOfferPayload_ThenTierIsParsedCaseInsensitively()
    {
        // Arrange
        var wire = Wire(requesterTier: "gold");

        // Act
        var payload = wire.ToJobOfferPayload();

        // Assert
        Assert.Equal(ServiceTier.Gold, payload.Tier);
    }

    [Fact]
    public void GivenAnUnrecognisedTierName_WhenMappedToJobOfferPayload_ThenThrows()
    {
        // Arrange
        var wire = Wire(requesterTier: "Platinum");

        // Act
        var act = () => wire.ToJobOfferPayload();

        // Assert
        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void GivenAMissingTierName_WhenMappedToJobOfferPayload_ThenThrows()
    {
        // Arrange
        var wire = Wire(requesterTier: "");

        // Act
        var act = () => wire.ToJobOfferPayload();

        // Assert
        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void GivenAWirePayload_WhenMappedToJobOfferPayload_ThenLatitudeAndLongitudeArePreserved()
    {
        // Arrange
        var wire = Wire(latitude: 41.601, longitude: -93.609);

        // Act
        var payload = wire.ToJobOfferPayload();

        // Assert
        Assert.Equal(41.601, payload.Lat);
        Assert.Equal(-93.609, payload.Lng);
    }

    [Fact]
    public void GivenAWirePayload_WhenMappedToJobOfferPayload_ThenDistanceIsPreserved()
    {
        // Arrange
        var wire = Wire(distanceMiles: 12.4);

        // Act
        var payload = wire.ToJobOfferPayload();

        // Assert
        Assert.Equal(12.4, payload.DistanceMiles);
    }

    [Fact]
    public void GivenAFractionalEtaWirePayload_WhenMappedToJobOfferPayload_ThenEtaIsRoundedToWholeMinutes()
    {
        // Arrange
        var wire = Wire(etaMinutes: 12.6);

        // Act
        var payload = wire.ToJobOfferPayload();

        // Assert
        Assert.Equal(13, payload.EtaMinutes);
    }

    [Fact]
    public void GivenAWirePayload_WhenMappedToJobOfferPayload_ThenIdentityFieldsArePreserved()
    {
        // Arrange
        var wire = Wire();

        // Act
        var payload = wire.ToJobOfferPayload();

        // Assert
        Assert.Equal(wire.OfferId, payload.OfferId);
        Assert.Equal("Marcus", payload.RequesterName);
        Assert.Equal("Transmission Control Fault", payload.DtcTitle);
    }
}
