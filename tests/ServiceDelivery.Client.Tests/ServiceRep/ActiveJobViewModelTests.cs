using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.ViewModels;

namespace ServiceDelivery.Client.Tests.ServiceRep;

public class ActiveJobViewModelTests
{
    private readonly Mock<IActiveJobService> _activeJobService = new();
    private readonly Mock<IRepHubService> _repHub = new();
    private readonly Mock<IArriveService> _arriveService = new();

    private static ActiveJobContext Context(
        string requesterName = "Marcus Webb",
        string dtcTitle = "P0700 · Transmission Control Fault",
        double requesterLat = 41.60,
        double requesterLng = -93.60,
        double repLat = 41.70,
        double repLng = -93.50,
        int etaMinutes = 9,
        double distanceMiles = 8.1,
        string repState = "EnRoute",
        string tier = "Gold") =>
        new(Guid.NewGuid(), requesterName, dtcTitle, requesterLat, requesterLng,
            repLat, repLng, etaMinutes, distanceMiles, repState, tier);

    private ActiveJobViewModel CreateViewModel() =>
        new(_activeJobService.Object, _repHub.Object, _arriveService.Object);

    [Fact]
    public async Task GivenAnActiveJobWithTier_WhenViewModelLoads_ThenTierIsSurfacedFromContext()
    {
        // Arrange
        // FE-012 fidelity: the active-job context carries the request tier (mirrors the backend
        // MyActiveServiceRequestDto.Tier field); the ViewModel surfaces it so the bottom sheet can
        // render the tier badge.
        _activeJobService.Setup(s => s.GetActiveJobAsync()).ReturnsAsync(Context(tier: "Gold"));
        var vm = CreateViewModel();

        // Act
        await vm.LoadAsync();

        // Assert
        Assert.Equal("Gold", vm.Tier);
    }

    [Fact]
    public async Task GivenRepTapsArrivedButton_WhenArriveAsyncCalled_ThenArriveServiceIsInvoked()
    {
        // Arrange
        // AC-1: tapping "I've Arrived" calls POST /rep/arrive via IArriveService.
        _activeJobService.Setup(s => s.GetActiveJobAsync()).ReturnsAsync(Context(repState: "Within15Miles"));
        var vm = CreateViewModel();
        await vm.LoadAsync();

        // Act
        await vm.ArriveAsync();

        // Assert
        _arriveService.Verify(s => s.ArriveAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenRepArrives_WhenArriveAsyncSucceeds_ThenIsOnSiteIsTrue()
    {
        // Arrange
        // AC-2: on a successful arrive the ViewModel transitions to OnSite, which the page uses to
        // remove the route line and swap in the "Mark Complete" action.
        _activeJobService.Setup(s => s.GetActiveJobAsync()).ReturnsAsync(Context(repState: "Within15Miles"));
        var vm = CreateViewModel();
        await vm.LoadAsync();

        // Act
        await vm.ArriveAsync();

        // Assert
        Assert.True(vm.IsOnSite);
    }

    [Fact]
    public async Task GivenRepArrives_WhenArriveAsyncSucceeds_ThenStateChangedIsRaised()
    {
        // Arrange
        // AC-2: arriving raises StateChanged so the page re-renders — the map re-centres on the rep's
        // current (on-site) location and the bottom sheet swaps to "Mark Complete" without a reload.
        _activeJobService.Setup(s => s.GetActiveJobAsync()).ReturnsAsync(Context(repState: "Within15Miles"));
        var vm = CreateViewModel();
        await vm.LoadAsync();
        var raised = 0;
        vm.StateChanged += () => raised++;

        // Act
        await vm.ArriveAsync();

        // Assert
        Assert.Equal(1, raised);
    }

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
    public async Task GivenAnActiveJobContext_WhenApplyContextCalled_ThenDistanceMilesIsSetFromContext()
    {
        // Arrange
        // BUG-039: the ETA card shows the server-computed distance to the requester. The ViewModel
        // surfaces DistanceMiles straight from the active-job context (carried from rep/active-job-state).
        // 23.7 is a controlled value distinct from the helper default (8.1) — the assertion proves the
        // value is carried, not echoed from a coincidental default.
        _activeJobService.Setup(s => s.GetActiveJobAsync()).ReturnsAsync(Context(distanceMiles: 23.7));
        var vm = CreateViewModel();

        // Act
        await vm.LoadAsync();

        // Assert
        Assert.Equal(23.7, vm.DistanceMiles);
    }

    [Fact]
    public async Task GivenARedirectPayload_WhenOnRedirectReceivedHandlerCalled_ThenDistanceMilesUpdatesFromRedirectPayload()
    {
        // Arrange
        // BUG-039: a redirect carries the distance to the new destination; the ETA card's distance
        // line updates in-place from the redirect payload (parallels the EtaMinutes redirect behaviour).
        // 17.4 is distinct from the load value (8.1 default) and from the redirect helper default (11.2).
        _activeJobService.Setup(s => s.GetActiveJobAsync()).ReturnsAsync(Context(distanceMiles: 8.1));
        var vm = CreateViewModel();
        await vm.LoadAsync();

        // Act
        await vm.OnRedirectReceivedAsync(Redirect(distanceMiles: 17.4));

        // Assert
        Assert.Equal(17.4, vm.DistanceMiles);
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

    [Fact]
    public async Task GivenRepStateIsOnSite_WhenViewModelLoads_ThenIsArrivedEnabledIsTrue()
    {
        // Arrange
        // AC-3: if the backend already reports the rep OnSite on load (e.g. a page reload after
        // arriving), the arrive action remains enabled — the rep is on-site at any time once OnSite.
        _activeJobService.Setup(s => s.GetActiveJobAsync()).ReturnsAsync(Context(repState: "OnSite"));
        var vm = CreateViewModel();

        // Act
        await vm.LoadAsync();

        // Assert
        Assert.True(vm.IsArrivedEnabled);
    }

    [Fact]
    public async Task GivenRepStateIsOnSite_WhenViewModelLoads_ThenIsOnSiteIsTrue()
    {
        // Arrange
        // AC-3: a backend-reported OnSite state on load also puts the ViewModel into the on-site
        // presentation (route line gone, "Mark Complete" primary) without a fresh arrive call.
        _activeJobService.Setup(s => s.GetActiveJobAsync()).ReturnsAsync(Context(repState: "OnSite"));
        var vm = CreateViewModel();

        // Act
        await vm.LoadAsync();

        // Assert
        Assert.True(vm.IsOnSite);
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
