using Bunit;
using Bunit.JSInterop;
using Microsoft.Extensions.DependencyInjection;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.ViewModels;
using ServiceDelivery.Client.UI.Features.Dispatcher.Pages;

namespace ServiceDelivery.Client.Tests.Dispatcher;

/// <summary>
/// AC-9 — the dispatcher two-column layout (mockup: map left, ACTIVE REQUESTS rail right). Renders the real
/// <see cref="DispatcherHome"/> page and asserts the outer layout container carries the
/// <c>sd-dispatcher-layout</c> grid class and that both the map column and the (FE-004-stub) queue rail are
/// present in the markup. The embedded FleetMap's GoogleMap is driven by a mocked available IMapsLoader and
/// the mocked JS module, and the ViewModel by mocked services, so the page renders headlessly.
/// </summary>
public class DispatcherHomeLayoutTests : BunitContext
{
    private const string ModulePath =
        "./_content/ServiceDelivery.Client.UI/Features/Maps/googleMap.js";

    private readonly Mock<IMapsLoader> _mapsLoader = new();
    private readonly Mock<IDispatcherFleetService> _fleetService = new();
    private readonly Mock<IVehiclePositionHubService> _hub = new();
    private readonly Mock<IActiveRequestQueueService> _queueService = new();
    private readonly Mock<IDispatchHubService> _dispatchHub = new();
    private readonly Mock<IRedirectEligibilityService> _eligibility = new();
    private readonly Mock<IDispatcherRedirectService> _redirectService = new();

    public DispatcherHomeLayoutTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var module = JSInterop.SetupModule(ModulePath);
        module.Mode = JSRuntimeMode.Loose;

        _mapsLoader.Setup(l => l.LoadAsync()).ReturnsAsync(new MapsAvailability(true, null));
        _fleetService.Setup(s => s.GetFleetAsync()).ReturnsAsync(new List<FleetVehicleEntry>());
        _queueService.Setup(s => s.GetActiveRequestsAsync()).ReturnsAsync(new List<ActiveRequestEntry>());

        Services.AddSingleton(_mapsLoader.Object);
        Services.AddSingleton(new DispatcherFleetViewModel(_fleetService.Object, _hub.Object));
        Services.AddSingleton(new DispatcherRequestQueueViewModel(
            _queueService.Object, _dispatchHub.Object, _eligibility.Object, _redirectService.Object));
    }

    [Fact]
    public void GivenDispatcherHome_WhenRendered_ThenLayoutContainerHasSdDispatcherLayoutClass()
    {
        // Arrange & Act
        var cut = Render<DispatcherHome>();

        // Assert
        var container = cut.Find("[data-testid='dispatcher-dashboard']");
        Assert.Contains("sd-dispatcher-layout", container.GetAttribute("class"));
    }

    [Fact]
    public void GivenDispatcherHome_WhenRendered_ThenBothMapColumnAndQueueRailArePresent()
    {
        // Arrange & Act
        var cut = Render<DispatcherHome>();

        // Assert
        Assert.NotNull(cut.Find("[data-testid='dispatcher-map-column']"));
        Assert.NotNull(cut.Find("[data-testid='dispatcher-queue-rail']"));
    }

    [Fact]
    public void GivenDispatcherHome_WhenRendered_ThenQueueRailShowsActiveRequestsStubHeader()
    {
        // Arrange & Act
        var cut = Render<DispatcherHome>();

        // Assert — FE-003 renders the ACTIVE REQUESTS rail header; FE-004 populates the list.
        Assert.Contains("ACTIVE REQUESTS", cut.Find("[data-testid='dispatcher-queue-rail']").TextContent);
    }
}
