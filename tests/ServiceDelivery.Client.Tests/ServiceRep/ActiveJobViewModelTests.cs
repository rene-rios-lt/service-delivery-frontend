using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.ViewModels;

namespace ServiceDelivery.Client.Tests.ServiceRep;

public class ActiveJobViewModelTests
{
    private readonly Mock<IActiveJobService> _activeJobService = new();
    private readonly Mock<IRepHubService> _repHub = new();

    private static ActiveJobContext Context(
        string requesterName = "Marcus Webb",
        string dtcTitle = "P0700 · Transmission Control Fault",
        double requesterLat = 41.60,
        double requesterLng = -93.60,
        double repLat = 41.70,
        double repLng = -93.50,
        int etaMinutes = 9,
        string repState = "EnRoute") =>
        new(Guid.NewGuid(), requesterName, dtcTitle, requesterLat, requesterLng,
            repLat, repLng, etaMinutes, repState);

    private ActiveJobViewModel CreateViewModel() =>
        new(_activeJobService.Object, _repHub.Object);

    [Fact]
    public async Task GivenAnActiveJob_WhenViewModelLoads_ThenRequesterAndDtcFieldsArePopulated()
    {
        // Arrange
        // AC-1/AC-3: on load the ViewModel surfaces the requester's fixed pin coordinates plus the
        // requester name and DTC title the bottom sheet renders.
        _activeJobService.Setup(s => s.GetActiveJobAsync()).ReturnsAsync(Context(
            requesterName: "Marcus Webb",
            dtcTitle: "P0700 · Transmission Control Fault",
            requesterLat: 41.60,
            requesterLng: -93.60));
        var vm = CreateViewModel();

        // Act
        await vm.LoadAsync();

        // Assert
        Assert.Equal("Marcus Webb", vm.RequesterName);
        Assert.Equal("P0700 · Transmission Control Fault", vm.DtcTitle);
        Assert.Equal(41.60, vm.RequesterLat);
        Assert.Equal(-93.60, vm.RequesterLng);
    }

    [Fact]
    public async Task GivenAPositionUpdate_WhenViewModelTicks_ThenRepLatLngPropertiesUpdate()
    {
        // Arrange
        // AC-1: the rep marker tracks the simulator-driven position. A poll returns the latest lat/lng
        // and the ViewModel surfaces them so the map can move the marker.
        _activeJobService.Setup(s => s.GetActiveJobAsync()).ReturnsAsync(Context(repLat: 41.70, repLng: -93.50));
        var vm = CreateViewModel();
        await vm.LoadAsync();
        _activeJobService
            .Setup(s => s.GetActiveJobAsync())
            .ReturnsAsync(Context(repLat: 41.65, repLng: -93.55));

        // Act
        await vm.PollPositionAsync();

        // Assert
        Assert.Equal(41.65, vm.RepLat);
        Assert.Equal(-93.55, vm.RepLng);
    }

    [Fact]
    public async Task GivenAPositionPollResult_WhenViewModelUpdates_ThenEtaMinutesChanges()
    {
        // Arrange
        // AC-2: ETA is recomputed on the backend as the rep moves; each poll surfaces the new value
        // so the floating ETA card updates.
        _activeJobService.Setup(s => s.GetActiveJobAsync()).ReturnsAsync(Context(etaMinutes: 9));
        var vm = CreateViewModel();
        await vm.LoadAsync();
        _activeJobService.Setup(s => s.GetActiveJobAsync()).ReturnsAsync(Context(etaMinutes: 6));

        // Act
        await vm.PollPositionAsync();

        // Assert
        Assert.Equal(6, vm.EtaMinutes);
    }

    [Fact]
    public async Task GivenARepWithin15Miles_WhenViewModelLoads_ThenIsArrivedEnabledIsTrue()
    {
        // Arrange
        // AC-4: if the rep is already within 15 miles on load, the backend reports state
        // "Within15Miles" and the "I've Arrived" button is enabled immediately.
        _activeJobService.Setup(s => s.GetActiveJobAsync()).ReturnsAsync(Context(repState: "Within15Miles"));
        var vm = CreateViewModel();

        // Act
        await vm.LoadAsync();

        // Assert
        Assert.True(vm.IsArrivedEnabled);
    }

    [Fact]
    public async Task GivenARepMoreThan15MilesAway_WhenViewModelLoads_ThenIsArrivedEnabledIsFalse()
    {
        // Arrange
        // AC-4: while the rep is still en route (> 15 miles), the backend reports state "EnRoute" and
        // the "I've Arrived" button stays disabled.
        _activeJobService.Setup(s => s.GetActiveJobAsync()).ReturnsAsync(Context(repState: "EnRoute"));
        var vm = CreateViewModel();

        // Act
        await vm.LoadAsync();

        // Assert
        Assert.False(vm.IsArrivedEnabled);
    }

    [Fact]
    public async Task GivenRepStateChangesToWithin15Miles_WhenPositionUpdated_ThenIsArrivedEnabledBecomesTrue()
    {
        // Arrange
        // AC-4: the rep loads still en route, then a later poll reports "Within15Miles" — the button
        // becomes enabled in-place without a screen reload.
        _activeJobService.Setup(s => s.GetActiveJobAsync()).ReturnsAsync(Context(repState: "EnRoute"));
        var vm = CreateViewModel();
        await vm.LoadAsync();
        _activeJobService.Setup(s => s.GetActiveJobAsync()).ReturnsAsync(Context(repState: "Within15Miles"));

        // Act
        await vm.PollPositionAsync();

        // Assert
        Assert.True(vm.IsArrivedEnabled);
    }

    [Fact]
    public async Task GivenANewPositionFromPoll_WhenViewModelAppliesUpdate_ThenRepLatLngUpdatesAndStateChangedIsRaised()
    {
        // Arrange
        // AC-5: the simulator drives the rep's position; each poll moves the marker and raises
        // StateChanged so the page re-renders the new position without a reload.
        _activeJobService.Setup(s => s.GetActiveJobAsync()).ReturnsAsync(Context(repLat: 41.70, repLng: -93.50));
        var vm = CreateViewModel();
        await vm.LoadAsync();
        _activeJobService.Setup(s => s.GetActiveJobAsync()).ReturnsAsync(Context(repLat: 41.62, repLng: -93.58));
        var raised = 0;
        vm.StateChanged += () => raised++;

        // Act
        await vm.PollPositionAsync();

        // Assert
        Assert.Equal(41.62, vm.RepLat);
        Assert.Equal(-93.58, vm.RepLng);
        Assert.Equal(1, raised);
    }

    private static RedirectPayload Redirect(
        string requesterTier = "Gold",
        string dtcTitle = "P0420 · Catalyst Efficiency Low",
        double distanceMiles = 11.2,
        double etaMinutes = 14,
        double latitude = 41.80,
        double longitude = -93.40) =>
        new(Guid.NewGuid(), requesterTier, dtcTitle, distanceMiles, etaMinutes, latitude, longitude);

    [Fact]
    public async Task GivenARedirectPayload_WhenOnRedirectReceivedHandlerCalled_ThenRequesterPinUpdatesToNewCoordinates()
    {
        // Arrange
        // AC-6: a RedirectReceived event moves the destination in-place — the requester pin jumps to
        // the new job's coordinates without a screen reload.
        _activeJobService.Setup(s => s.GetActiveJobAsync()).ReturnsAsync(Context(requesterLat: 41.60, requesterLng: -93.60));
        var vm = CreateViewModel();
        await vm.LoadAsync();

        // Act
        await vm.OnRedirectReceivedAsync(Redirect(latitude: 41.80, longitude: -93.40));

        // Assert
        Assert.Equal(41.80, vm.RequesterLat);
        Assert.Equal(-93.40, vm.RequesterLng);
    }

    [Fact]
    public async Task GivenARedirectPayload_WhenOnRedirectReceivedHandlerCalled_ThenDtcTitleUpdatesAndStateChangedIsRaised()
    {
        // Arrange
        // AC-6: the redirect carries the new job's DTC title; the bottom sheet updates in-place and
        // StateChanged fires so the page re-renders without a reload.
        _activeJobService.Setup(s => s.GetActiveJobAsync()).ReturnsAsync(Context(dtcTitle: "P0700 · Transmission Control Fault"));
        var vm = CreateViewModel();
        await vm.LoadAsync();
        var raised = 0;
        vm.StateChanged += () => raised++;

        // Act
        await vm.OnRedirectReceivedAsync(Redirect(dtcTitle: "P0420 · Catalyst Efficiency Low"));

        // Assert
        Assert.Equal("P0420 · Catalyst Efficiency Low", vm.DtcTitle);
        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task GivenARedirectPayload_WhenOnRedirectReceivedHandlerCalled_ThenEtaMinutesUpdatesFromRedirectPayload()
    {
        // Arrange
        // AC-6: the redirect carries the ETA to the new destination; the floating ETA card updates
        // in-place from the redirect payload.
        _activeJobService.Setup(s => s.GetActiveJobAsync()).ReturnsAsync(Context(etaMinutes: 9));
        var vm = CreateViewModel();
        await vm.LoadAsync();

        // Act
        await vm.OnRedirectReceivedAsync(Redirect(etaMinutes: 14));

        // Assert
        Assert.Equal(14, vm.EtaMinutes);
    }

    [Fact]
    public async Task GivenAStartedViewModel_WhenARedirectArrivesOnTheHub_ThenTheDestinationUpdatesInPlace()
    {
        // Arrange
        // AC-6: the ViewModel registers its redirect handler on RepHub during StartAsync, so a
        // RedirectReceived event pushed over the hub updates the destination without a screen reload.
        // Capture the handler the ViewModel registers and invoke it as the hub would.
        _activeJobService.Setup(s => s.GetActiveJobAsync()).ReturnsAsync(Context(requesterLat: 41.60, requesterLng: -93.60));
        Func<RedirectPayload, Task>? hubHandler = null;
        _repHub.Setup(h => h.OnRedirectReceived(It.IsAny<Func<RedirectPayload, Task>>()))
            .Callback<Func<RedirectPayload, Task>>(h => hubHandler = h);
        var vm = CreateViewModel();
        await vm.LoadAsync();
        await vm.StartAsync();

        // Act
        await hubHandler!(Redirect(latitude: 41.85, longitude: -93.35));

        // Assert
        _repHub.Verify(h => h.StartAsync(), Times.Once);
        Assert.Equal(41.85, vm.RequesterLat);
        Assert.Equal(-93.35, vm.RequesterLng);
    }
}
