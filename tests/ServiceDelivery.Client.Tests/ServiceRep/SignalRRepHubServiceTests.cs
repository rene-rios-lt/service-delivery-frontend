using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.UI.Features.ServiceRep.Services;

namespace ServiceDelivery.Client.Tests.ServiceRep;

public class SignalRRepHubServiceTests
{
    private readonly Mock<ITokenStore> _tokenStore = new();

    private SignalRRepHubService CreateService()
    {
        var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5180") };
        return new SignalRRepHubService(httpClient, _tokenStore.Object);
    }

    [Fact]
    public async Task GivenAStoredToken_WhenTheHubRequestsAnAccessToken_ThenTheStoredTokenIsProvided()
    {
        // Arrange — the RepHub is [Authorize]; SignalR appends ?access_token=... from this provider
        // because websockets cannot carry an Authorization header. Without it the rep's hub connection
        // is unauthenticated and never joins its rep group, so it never receives JobOfferReceived.
        _tokenStore.Setup(t => t.GetTokenAsync()).ReturnsAsync("rep-jwt-xyz");
        var service = CreateService();

        // Act
        var token = await service.ProvideAccessTokenAsync();

        // Assert
        Assert.Equal("rep-jwt-xyz", token);
    }

    [Fact]
    public async Task GivenNoStoredToken_WhenTheHubRequestsAnAccessToken_ThenNullIsProvidedWithoutThrowing()
    {
        // Arrange
        _tokenStore.Setup(t => t.GetTokenAsync()).ReturnsAsync((string?)null);
        var service = CreateService();

        // Act
        var token = await service.ProvideAccessTokenAsync();

        // Assert
        Assert.Null(token);
    }

    // These deserialization tests mirror the real RepHub wire shape (BE-017/BE-019): the backend's
    // JobOfferReceivedPayload serializes the tier as the JSON key `requesterTier` with a *string*
    // value ("Gold"), because the backend domain enum carries JsonStringEnumConverter. The frontend
    // must (1) bind that key onto JobOfferPayload.Tier and (2) accept the string form via the same
    // converter on the hub connection's JSON options. The tests use that converter explicitly so they
    // isolate the JobOfferPayload attribute/shape fix from the live connection lifecycle (BUG-036 AC-1).
    private static readonly JsonSerializerOptions HubJsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public void GivenAHubJsonWithRequesterTierKey_WhenJobOfferPayloadDeserialized_ThenTierPropertyIsPopulated()
    {
        // Arrange — the backend emits the field as `requesterTier`, not `tier`/`Tier`.
        const string json = """
            {
                "offerId": "11111111-1111-1111-1111-111111111111",
                "requesterName": "Marcus",
                "requesterTier": "Gold",
                "dtcTitle": "Transmission Control Fault",
                "distanceMiles": 12.4,
                "etaMinutes": 13,
                "lat": 41.6,
                "lng": -93.6
            }
            """;

        // Act
        var payload = JsonSerializer.Deserialize<JobOfferPayload>(json, HubJsonOptions);

        // Assert
        Assert.NotNull(payload);
        Assert.NotEqual(ServiceTier.None, payload!.Tier);
    }

    [Fact]
    public void GivenAHubJsonWithStringTierValue_WhenJobOfferPayloadDeserialized_ThenTierEnumIsGold()
    {
        // Arrange — the tier value is the string "Gold", never the ordinal 3.
        const string json = """
            {
                "offerId": "11111111-1111-1111-1111-111111111111",
                "requesterName": "Marcus",
                "requesterTier": "Gold",
                "dtcTitle": "Transmission Control Fault",
                "distanceMiles": 12.4,
                "etaMinutes": 13,
                "lat": 41.6,
                "lng": -93.6
            }
            """;

        // Act
        var payload = JsonSerializer.Deserialize<JobOfferPayload>(json, HubJsonOptions);

        // Assert
        Assert.Equal(ServiceTier.Gold, payload!.Tier);
    }

    [Theory]
    [InlineData("Bronze", ServiceTier.Bronze)]
    [InlineData("Silver", ServiceTier.Silver)]
    [InlineData("Gold", ServiceTier.Gold)]
    public void GivenAHubJsonWithStringTier_WhenDeserialized_ThenCorrectEnumValueIsProduced(
        string tierString, ServiceTier expected)
    {
        // Arrange
        var json = $$"""
            {
                "offerId": "11111111-1111-1111-1111-111111111111",
                "requesterName": "Marcus",
                "requesterTier": "{{tierString}}",
                "dtcTitle": "Transmission Control Fault",
                "distanceMiles": 12.4,
                "etaMinutes": 13,
                "lat": 41.6,
                "lng": -93.6
            }
            """;

        // Act
        var payload = JsonSerializer.Deserialize<JobOfferPayload>(json, HubJsonOptions);

        // Assert
        Assert.Equal(expected, payload!.Tier);
    }
}
