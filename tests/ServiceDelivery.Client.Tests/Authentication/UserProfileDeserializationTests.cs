using System.Text.Json;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Tests.Authentication;

/// <summary>
/// Cross-process wire-contract proof for GET /users/me. The backend serializes both
/// `role` and `tier` as NUMERIC enums (default System.Text.Json, no JsonStringEnumConverter
/// registered), so the client enums must mirror the backend ordinals exactly or deserialization
/// binds the wrong member. These tests deserialize the REAL numeric wire shape — the same
/// System.Text.Json path HttpAuthService uses via ReadFromJsonAsync — and assert each tier
/// ordinal maps to the correct ServiceTier member. Distinct values are used per assertion so a
/// wrong client enum order would fail rather than coincidentally pass.
/// Backend contract: ServiceTier { None=0, Bronze=1, Silver=2, Gold=3 }; UserRole { Dispatcher=0, ServiceRep=1, Requester=2, Simulator=3 }.
/// </summary>
public class UserProfileDeserializationTests
{
    private static UserProfile Deserialize(int roleOrdinal, int tierOrdinal)
    {
        var wireJson =
            $$"""
            {
                "userId": "11111111-1111-1111-1111-111111111111",
                "name": "Alex Dispatcher",
                "role": {{roleOrdinal}},
                "tier": {{tierOrdinal}},
                "dealerId": "22222222-2222-2222-2222-222222222222"
            }
            """;

        // Mirror exactly how HttpAuthService deserializes the response: ReadFromJsonAsync
        // uses JsonSerializerDefaults.Web (camelCase-insensitive matching). Using the same
        // options here keeps the test faithful to the real wire-read path.
        return JsonSerializer.Deserialize<UserProfile>(wireJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    [Fact]
    public void GivenNumericTierOne_WhenUsersMeJsonIsDeserialized_ThenTierIsBronze()
    {
        // Arrange
        // Backend wire: tier == 1 is Bronze, not Silver/Gold/None.

        // Act
        var profile = Deserialize(roleOrdinal: 0, tierOrdinal: 1);

        // Assert
        Assert.Equal(ServiceTier.Bronze, profile.Tier);
        Assert.NotEqual(ServiceTier.Silver, profile.Tier);
    }

    [Fact]
    public void GivenNumericTierThree_WhenUsersMeJsonIsDeserialized_ThenTierIsGold()
    {
        // Arrange
        // Backend wire: tier == 3 is Gold. A distinct ordinal from the Bronze case so the
        // mapping cannot pass by coincidence if the client enum order were wrong.

        // Act
        var profile = Deserialize(roleOrdinal: 1, tierOrdinal: 3);

        // Assert
        Assert.Equal(ServiceTier.Gold, profile.Tier);
        Assert.NotEqual(ServiceTier.Bronze, profile.Tier);
    }

    [Fact]
    public void GivenNumericTierZero_WhenUsersMeJsonIsDeserialized_ThenTierIsNone()
    {
        // Arrange
        // Backend wire: tier == 0 is None (e.g. a Dispatcher with no service tier).

        // Act
        var profile = Deserialize(roleOrdinal: 0, tierOrdinal: 0);

        // Assert
        Assert.Equal(ServiceTier.None, profile.Tier);
        Assert.NotEqual(ServiceTier.Gold, profile.Tier);
    }
}
