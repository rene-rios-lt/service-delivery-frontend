using System.Text.Json;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Tests.Dispatcher;

/// <summary>
/// AC-5 — captured-payload wire-contract proof for the <c>GET /dispatcher/fleet</c>
/// <see cref="DispatcherFleetEntryDto"/>, and the mapping onto <see cref="FleetVehicleEntry"/>. Since BE-032
/// (backend PR #54) the fleet entry carries <c>activeRequestTitle</c> (the DTC title of the active request;
/// null when unassigned), positioned between <c>activeRequestTier</c> and <c>humanControlled</c>. This
/// deserializes a REAL captured wire JSON string — the literal field name <c>"activeRequestTitle"</c> with a
/// distinct value — through the same <see cref="JsonSerializerDefaults.Web"/> path the client uses, so a
/// field-name drift cannot pass coincidentally (ADR-0011 / the frontend CLAUDE.md wire-contract rule), then
/// proves <see cref="DispatcherFleetEntryDto.ToFleetVehicleEntry"/> flows the title through.
/// </summary>
public class DispatcherFleetEntryDtoDeserializationTests
{
    private const string RealFleetEntryJson =
        """
        {
            "repId": "50000000-0000-0000-0000-000000000001",
            "name": "J. Tran",
            "state": "EnRoute",
            "vehicleId": "30000000-0000-0000-0000-000000000007",
            "registration": "IA-4471",
            "lastPosition": { "lat": 41.8781, "lng": -93.0977 },
            "activeRequestId": "aaaaaaaa-0000-0000-0000-000000000009",
            "activeRequestTier": "Silver",
            "activeRequestTitle": "Hydraulic Pressure Loss",
            "humanControlled": true
        }
        """;

    private static DispatcherFleetEntryDto Deserialize(string json) =>
        JsonSerializer.Deserialize<DispatcherFleetEntryDto>(
            json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    [Fact]
    public void GivenFleetEntryJsonWithActiveRequestTitle_WhenDeserialized_ThenActiveRequestTitleBinds()
    {
        // Arrange
        var json = RealFleetEntryJson;

        // Act
        var dto = Deserialize(json);

        // Assert — every wire field binds by a distinct value (guards against field-name drift), with the
        // BE-032 addition front and centre.
        Assert.Equal(Guid.Parse("50000000-0000-0000-0000-000000000001"), dto.RepId);
        Assert.Equal("J. Tran", dto.Name);
        Assert.Equal("EnRoute", dto.State);
        Assert.Equal(Guid.Parse("30000000-0000-0000-0000-000000000007"), dto.VehicleId);
        Assert.Equal("IA-4471", dto.Registration);
        Assert.Equal(41.8781, dto.LastPosition!.Lat);
        Assert.Equal(-93.0977, dto.LastPosition!.Lng);
        Assert.Equal(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000009"), dto.ActiveRequestId);
        Assert.Equal("Silver", dto.ActiveRequestTier);
        Assert.Equal("Hydraulic Pressure Loss", dto.ActiveRequestTitle);
        Assert.True(dto.HumanControlled);
    }

    private static DispatcherFleetEntryDto EntryDto(string? activeRequestTitle) =>
        new(
            RepId: Guid.Parse("50000000-0000-0000-0000-000000000001"),
            Name: "J. Tran",
            State: "EnRoute",
            VehicleId: Guid.Parse("30000000-0000-0000-0000-000000000007"),
            Registration: "IA-4471",
            LastPosition: new LastPositionDto(41.8781, -93.0977),
            ActiveRequestId: Guid.Parse("aaaaaaaa-0000-0000-0000-000000000009"),
            ActiveRequestTier: "Silver",
            ActiveRequestTitle: activeRequestTitle,
            HumanControlled: true);

    [Fact]
    public void GivenFleetEntryDtoWithActiveRequestTitle_WhenMappedToFleetVehicleEntry_ThenTitleFlowsThrough()
    {
        // Arrange
        var dto = EntryDto(activeRequestTitle: "Hydraulic Pressure Loss");

        // Act
        var entry = dto.ToFleetVehicleEntry();

        // Assert — the BE-032 title is sourced from activeRequestTitle, not left null.
        Assert.Equal("Hydraulic Pressure Loss", entry.ActiveRequestTitle);
    }

    [Fact]
    public void GivenFleetEntryDtoWithNoActiveRequestTitle_WhenMappedToFleetVehicleEntry_ThenTitleIsNull()
    {
        // Arrange — an unassigned rep: the backend sends null activeRequestTitle.
        var dto = EntryDto(activeRequestTitle: null);

        // Act
        var entry = dto.ToFleetVehicleEntry();

        // Assert
        Assert.Null(entry.ActiveRequestTitle);
    }
}
