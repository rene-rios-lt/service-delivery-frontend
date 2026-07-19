using Bunit;
using Bunit.JSInterop;
using Microsoft.Extensions.DependencyInjection;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.ViewModels;
using ServiceDelivery.Client.UI.Features.Dispatcher.Components;

namespace ServiceDelivery.Client.Tests.Dispatcher;

/// <summary>
/// FE-003 map-interop tests for <see cref="FleetMap"/> (ACs 2, 3, 4, 7, 8). The real google.maps.Map cannot
/// render under bUnit (ADR-0010), so — exactly as <c>ActiveJobMapInteropTests</c> does — the googleMap.js
/// module is mocked and these tests assert the exact module calls the embedded GoogleMap issues on mount
/// and on each ViewModel state change: one <c>addOrUpdateMarker</c> per visible vehicle with the correct
/// rep-state colour, an <c>addOrUpdateMarker</c> on a hub position update, and a <c>removeMarker</c> when a
/// vehicle goes Offline. The <see cref="DispatcherFleetViewModel"/> is driven with mocked services.
/// </summary>
public class FleetMapInteropTests : BunitContext
{
    private const string ModulePath =
        "./_content/ServiceDelivery.Client.UI/Features/Maps/googleMap.js";

    private const string V1 = "11111111-0000-0000-0000-000000000001";
    private const string V2 = "22222222-0000-0000-0000-000000000002";
    private const string V3 = "33333333-0000-0000-0000-000000000003";
    private const string V4 = "44444444-0000-0000-0000-000000000004";
    private static readonly Guid V7 = Guid.Parse("70000000-0000-0000-0000-000000000007");

    private readonly Mock<IMapsLoader> _mapsLoader = new();
    private readonly Mock<IDispatcherFleetService> _fleetService = new();
    private readonly Mock<IVehiclePositionHubService> _hub = new();
    private readonly BunitJSModuleInterop _module;

    public FleetMapInteropTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _module = JSInterop.SetupModule(ModulePath);
        _module.Mode = JSRuntimeMode.Loose;
    }

    private static FleetVehicleEntry Entry(
        string vehicleId, string state, double lat = 41.60, double lng = -93.60,
        bool human = false, string repName = "Rep") =>
        new(vehicleId, "IA-1234", state, Guid.NewGuid(), repName, lat, lng, null, null, human);

    private DispatcherFleetViewModel BuildLoadedViewModel(params FleetVehicleEntry[] entries)
    {
        _fleetService.Setup(s => s.GetFleetAsync()).ReturnsAsync(entries.ToList());
        var vm = new DispatcherFleetViewModel(_fleetService.Object, _hub.Object);
        vm.LoadAsync().GetAwaiter().GetResult();
        return vm;
    }

    private IRenderedComponent<FleetMap> RenderFleetMap(DispatcherFleetViewModel vm)
    {
        _mapsLoader.Setup(l => l.LoadAsync()).ReturnsAsync(new MapsAvailability(true, null));
        Services.AddSingleton(_mapsLoader.Object);
        Services.AddSingleton(vm);
        return Render<FleetMap>();
    }

    private JSRuntimeInvocation LastMarkerCall(string vehicleId) =>
        _module.Invocations.Last(i =>
            i.Identifier == "addOrUpdateMarker" && (string)i.Arguments[1]! == vehicleId);

    [Fact]
    public void GivenFleetServiceReturnsThreeVehicles_WhenFleetMapMounts_ThenGoogleMapReceivesThreeAddOrUpdateMarkerCalls()
    {
        // Arrange
        var vm = BuildLoadedViewModel(
            Entry(V1, "Available"), Entry(V2, "EnRoute"), Entry(V3, "OnSite"));

        // Act
        RenderFleetMap(vm);

        // Assert
        Assert.Equal(3, _module.Invocations.Count(i => i.Identifier == "addOrUpdateMarker"));
    }

    [Fact]
    public void GivenRepInEachState_WhenFleetMapRendered_ThenAddOrUpdateMarkerCalledWithCorrectColour()
    {
        // Arrange
        var vm = BuildLoadedViewModel(
            Entry(V1, "Available"), Entry(V2, "EnRoute"), Entry(V3, "Within15Miles"), Entry(V4, "OnSite"));

        // Act
        RenderFleetMap(vm);

        // Assert
        Assert.Equal(RepStateColour.ForState("Available"), LastMarkerCall(V1).Arguments[4]);
        Assert.Equal(RepStateColour.ForState("EnRoute"), LastMarkerCall(V2).Arguments[4]);
        Assert.Equal(RepStateColour.ForState("Within15Miles"), LastMarkerCall(V3).Arguments[4]);
        Assert.Equal(RepStateColour.ForState("OnSite"), LastMarkerCall(V4).Arguments[4]);
    }

    [Fact]
    public void GivenRepInEnRouteState_WhenFleetMapRendered_ThenAddOrUpdateMarkerCalledWithBlue1E88E5()
    {
        // Arrange
        var vm = BuildLoadedViewModel(Entry(V2, "EnRoute"));

        // Act
        RenderFleetMap(vm);

        // Assert
        Assert.Equal("#1E88E5", LastMarkerCall(V2).Arguments[4]);
    }

    [Fact]
    public void GivenLoadedFleet_WhenFleetMapRendered_ThenFleetCountAttributeMatchesVisibleCount()
    {
        // Arrange — the invisible data-fleet-count hook is the E2E/Mac2Driver detectability signal for the
        // JS-rendered (non-DOM-queryable) markers.
        var vm = BuildLoadedViewModel(Entry(V1, "Available"), Entry(V2, "EnRoute"));

        // Act
        var cut = RenderFleetMap(vm);

        // Assert
        Assert.Equal("2", cut.Find("[data-testid='fleet-map-panel']").GetAttribute("data-fleet-count"));
    }

    [Fact]
    public async Task GivenMountedFleetMap_WhenHubFiresPositionUpdate_ThenGoogleMapReceivesUpdateMarkerCall()
    {
        // Arrange
        var vm = BuildLoadedViewModel(Entry(V7.ToString(), "EnRoute", 41.60, -93.60));
        var cut = RenderFleetMap(vm);

        // Act — simulate the hub delivering a position update through the ViewModel's handler.
        await cut.InvokeAsync(() => vm.HandleVehiclePositionUpdatedAsync(
            new VehiclePositionUpdatedPayload(Guid.NewGuid(), V7, 42.10, -94.05, "EnRoute")));

        // Assert
        var call = LastMarkerCall(V7.ToString());
        Assert.Equal(42.10, call.Arguments[2]);
        Assert.Equal(-94.05, call.Arguments[3]);
    }

    [Fact]
    public async Task GivenOnlineVehicleInFleet_WhenHubFiresOfflineUpdate_ThenGoogleMapReceivesRemoveMarkerCall()
    {
        // Arrange
        var vm = BuildLoadedViewModel(Entry(V7.ToString(), "EnRoute"));
        var cut = RenderFleetMap(vm);

        // Act
        await cut.InvokeAsync(() => vm.HandleVehiclePositionUpdatedAsync(
            new VehiclePositionUpdatedPayload(Guid.NewGuid(), V7, 42.10, -94.05, "Offline")));

        // Assert
        Assert.Contains(_module.Invocations, i =>
            i.Identifier == "removeMarker" && (string)i.Arguments[1]! == V7.ToString());
    }

    [Fact]
    public void GivenHumanControlledAndSimulatedVehiclesWithSameState_WhenRendered_ThenMarkerColoursAreIdentical()
    {
        // Arrange — AC-8: control mode does not change the marker colour; state alone drives it.
        var vm = BuildLoadedViewModel(
            Entry(V1, "EnRoute", human: true), Entry(V2, "EnRoute", human: false));

        // Act
        RenderFleetMap(vm);

        // Assert
        Assert.Equal(LastMarkerCall(V1).Arguments[4], LastMarkerCall(V2).Arguments[4]);
    }

    [Fact]
    public async Task GivenMountedFleetMap_WhenMarkerClickedCallbackInvoked_ThenVehicleIsSelectedAndPopoverShows()
    {
        // Arrange — AC-5: a marker tap selects that vehicle and opens its popover.
        var vm = BuildLoadedViewModel(Entry(V7.ToString(), "EnRoute", repName: "J. Tran"));
        var cut = RenderFleetMap(vm);

        // Act
        await cut.InvokeAsync(() => cut.Instance.OnMarkerClickedAsync(V7.ToString()));

        // Assert
        Assert.NotNull(cut.Find("[data-testid='rep-popover']"));
        Assert.Contains("J. Tran", cut.Find("[data-testid='popover-rep-name']").TextContent);
    }

    // ---- Accessible fleet summary (FE-003 review cycle 10) ----------------------------------------------
    // The google.maps marker DOM lives inside the Maps SDK's aria-hidden overlay panes, so markers never
    // surface in the accessibility tree (VoiceOver or native mac2/XCTest) however we stamp role/aria-label on
    // them. FleetMap therefore renders a visually-hidden but AX-exposed per-vehicle summary OUTSIDE the map
    // pane. These tests pin that the summary mirrors VisibleVehicles (identity + state + live coords), excludes
    // hidden vehicles, and re-renders when a position update arrives.

    [Fact]
    public void GivenVisibleVehicles_WhenFleetMapRendered_ThenAccessibleSummaryListsEachVehicleWithStateAndCoords()
    {
        // Arrange — two visible vehicles at distinct positions.
        var vm = BuildLoadedViewModel(
            Entry(V1, "Available", 41.6012, -93.6034),
            Entry(V2, "EnRoute", 42.0100, -92.9000));

        // Act
        var cut = RenderFleetMap(vm);

        // Assert — one AX-visible entry per visible vehicle, each carrying its state and 4-dp live coordinates.
        var entries = cut.FindAll("[data-testid^='fleet-a11y-entry-']");
        Assert.Equal(2, entries.Count);

        var first = cut.Find($"[data-testid='fleet-a11y-entry-{V1}']").TextContent;
        Assert.Contains("Available", first);
        Assert.Contains("41.6012", first);
        Assert.Contains("-93.6034", first);
    }

    [Fact]
    public void GivenOfflineAndVisibleVehicles_WhenFleetMapRendered_ThenAccessibleSummaryExcludesHiddenVehicles()
    {
        // Arrange — V1 is Available (a visible vehicle); V2 is Offline (hidden from the map, per AC-7).
        var vm = BuildLoadedViewModel(
            Entry(V1, "Available"),
            Entry(V2, "Offline"));

        // Act
        var cut = RenderFleetMap(vm);

        // Assert — the summary mirrors VisibleVehicles only: the visible vehicle is listed, the Offline one is not.
        Assert.NotNull(cut.Find($"[data-testid='fleet-a11y-entry-{V1}']"));
        Assert.Empty(cut.FindAll($"[data-testid='fleet-a11y-entry-{V2}']"));
    }

    [Fact]
    public async Task GivenRenderedFleetMap_WhenAVehiclePositionUpdates_ThenTheAccessibleSummaryEntryTextUpdates()
    {
        // Arrange — one visible vehicle at a known start position; capture its summary entry text.
        var vm = BuildLoadedViewModel(Entry(V7.ToString(), "EnRoute", 41.6000, -93.6000));
        var cut = RenderFleetMap(vm);
        var before = cut.Find($"[data-testid='fleet-a11y-entry-{V7}']").TextContent;

        // Act — a hub position update flows through the ViewModel and re-renders the summary.
        await cut.InvokeAsync(() => vm.HandleVehiclePositionUpdatedAsync(
            new VehiclePositionUpdatedPayload(Guid.NewGuid(), V7, 42.1000, -94.0500, "EnRoute")));

        // Assert — the same entry now carries the new coordinates (the AX-visible move signal).
        var after = cut.Find($"[data-testid='fleet-a11y-entry-{V7}']").TextContent;
        Assert.NotEqual(before, after);
        Assert.Contains("42.1000", after);
        Assert.Contains("-94.0500", after);
    }
}
