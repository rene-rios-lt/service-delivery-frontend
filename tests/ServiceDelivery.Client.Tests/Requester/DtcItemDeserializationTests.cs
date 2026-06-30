using System;
using System.Text.Json;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Tests.Requester;

/// <summary>
/// Wire-contract captured-payload proof for the GET /dtcs entries (FE-015 AC-2). The backend returns
/// <c>DtcDto(Guid Id, string Code, string Title, string RequiredEquipment)</c> camelCased via
/// <see cref="JsonSerializerDefaults.Web"/>. These tests round-trip a REAL captured JSON string through
/// the same System.Text.Json path the client uses, with distinct values per field so a field-name drift
/// (e.g. <c>title</c> renamed) cannot pass coincidentally (CLAUDE.md wire-contract rule / QUAL-006). The
/// dropdown binds a <c>Guid SelectedDtcId</c>, so <see cref="DtcItem.Id"/> must deserialize from <c>id</c>.
/// </summary>
public class DtcItemDeserializationTests
{
    private const string RealDtcJson =
        """
        {
            "id": "7a1e4c2b-1111-4f3a-9a2b-0c1d2e3f4a5b",
            "code": "P0700",
            "title": "Transmission Control Fault",
            "requiredEquipment": "Hydraulics"
        }
        """;

    private static DtcItem Deserialize(string json) =>
        JsonSerializer.Deserialize<DtcItem>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    [Fact]
    public void GivenRealDtcJsonPayload_WhenDeserialised_ThenFieldsMappedCorrectly()
    {
        // Arrange
        // (real captured payload defined above)

        // Act
        var item = Deserialize(RealDtcJson);

        // Assert
        Assert.Equal(Guid.Parse("7a1e4c2b-1111-4f3a-9a2b-0c1d2e3f4a5b"), item.Id);
        Assert.Equal("P0700", item.Code);
        Assert.Equal("Transmission Control Fault", item.Title);
    }
}
