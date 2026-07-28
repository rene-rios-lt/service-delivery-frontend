using Bunit;
using Bunit.JSInterop;
using Microsoft.Extensions.DependencyInjection;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.ViewModels;
using ServiceDelivery.Client.UI.Features.Dispatcher.Pages;

namespace ServiceDelivery.Client.Tests.Dispatcher;

/// <summary>
/// AC-1/AC-3/AC-4 integration at the page level — <see cref="DispatcherHome"/> owns the
/// <see cref="DispatcherRequestQueueViewModel"/> lifecycle alongside the fleet ViewModel: it loads the queue
/// on mount, renders one card per active request inside the rail, and shows the active-request count in the
/// rail header.
/// <para>
/// Crucially, these tests reproduce the REAL runtime lifecycle: the queue snapshot load
/// (<see cref="IActiveRequestQueueService.GetActiveRequestsAsync"/>) stays <em>pending</em> across the initial
/// render (via a <see cref="TaskCompletionSource{TResult}"/>), so the page commits its first render with an
/// EMPTY queue — exactly as it does against a live backend where the HTTP load completes after the DOM is up.
/// The queue is populated only <em>after</em> that first render, then <c>LoadAsync()</c> raises
/// <c>StateChanged</c>. This is where the app broke (FE-004 review cycle 1, Finding 1): the parameterless
/// <c>RequestQueueRail</c> child was not re-rendered on that post-render state change, so the header updated
/// but the rail stayed empty. A test that pre-populates the queue before the only render (as the original did)
/// masks that defect because the child's first render already sees the cards. Rendered headlessly with a
/// mocked maps loader / JS module and mocked services.
/// </para>
/// </summary>
public class DispatcherHomeQueueTests : BunitContext
{
    private const string ModulePath =
        "./_content/ServiceDelivery.Client.UI/Features/Maps/googleMap.js";

    private readonly Mock<IMapsLoader> _mapsLoader = new();
    private readonly Mock<IDispatcherFleetService> _fleetService = new();
    private readonly Mock<IVehiclePositionHubService> _positionHub = new();
    private readonly Mock<IActiveRequestQueueService> _queueService = new();
    private readonly Mock<IDispatchHubService> _dispatchHub = new();
    private readonly Mock<IRedirectEligibilityService> _eligibility = new();
    private readonly Mock<IDispatcherRedirectService> _redirectService = new();

    // The queue snapshot load — deliberately left pending so the page's first render commits with an empty
    // queue; the test completes it AFTER the initial render to drive the populate-after-render lifecycle.
    private readonly TaskCompletionSource<IReadOnlyList<ActiveRequestEntry>> _queueLoad = new();

    private static ActiveRequestEntry Entry(string requesterName, ServiceTier tier) =>
        new(Guid.NewGuid(), requesterName, tier, "Transmission Control Fault", "Pending", null,
            DateTimeOffset.UtcNow.AddMinutes(-1));

    /// <summary>
    /// Wires the mocked collaborators so the page mounts with the fleet ready but the queue load <em>pending</em>.
    /// Call <see cref="CompleteQueueLoad"/> after the first render to populate the queue and raise StateChanged.
    /// </summary>
    private void ArrangeDeferredQueue()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var module = JSInterop.SetupModule(ModulePath);
        module.Mode = JSRuntimeMode.Loose;

        _mapsLoader.Setup(l => l.LoadAsync()).ReturnsAsync(new MapsAvailability(true, null));
        _fleetService.Setup(s => s.GetFleetAsync()).ReturnsAsync(new List<FleetVehicleEntry>());
        _queueService.Setup(s => s.GetActiveRequestsAsync()).Returns(_queueLoad.Task);

        Services.AddSingleton(_mapsLoader.Object);
        Services.AddSingleton(new DispatcherFleetViewModel(_fleetService.Object, _positionHub.Object));
        Services.AddSingleton(new DispatcherRequestQueueViewModel(
            _queueService.Object, _dispatchHub.Object, _eligibility.Object, _redirectService.Object));
    }

    private Task CompleteQueueLoad(IRenderedComponent<DispatcherHome> cut, params ActiveRequestEntry[] entries) =>
        cut.InvokeAsync(() => _queueLoad.SetResult(entries.ToList()));

    private static int CardCount(IRenderedComponent<DispatcherHome> cut) =>
        cut.Find("[data-testid='dispatcher-queue-list']").QuerySelectorAll(".sd-reqcard").Length;

    [Fact]
    public async Task GivenDispatcherHomeRenderedWithAPendingQueueLoad_WhenTheLoadCompletesAfterTheInitialRender_ThenTheRailShowsOneCardPerRequest()
    {
        // Arrange — first render commits with the queue load still pending → the rail starts empty.
        ArrangeDeferredQueue();
        var cut = Render<DispatcherHome>();
        Assert.Equal(0, CardCount(cut));

        // Act — the snapshot load completes AFTER the initial render; LoadAsync populates and raises StateChanged.
        await CompleteQueueLoad(cut,
            Entry("Marcus Webb", ServiceTier.Gold),
            Entry("Dana Cole", ServiceTier.Silver));

        // Assert — the rail re-renders with one card per active request (the FE-004 Finding 1 contract).
        cut.WaitForAssertion(() => Assert.Equal(2, CardCount(cut)));
    }

    [Fact]
    public async Task GivenDispatcherHomeRenderedWithAPendingQueueLoad_WhenTheLoadCompletesAfterTheInitialRender_ThenActiveCountReflectsQueueSize()
    {
        // Arrange
        ArrangeDeferredQueue();
        var cut = Render<DispatcherHome>();

        // Act
        await CompleteQueueLoad(cut,
            Entry("Marcus Webb", ServiceTier.Gold),
            Entry("Dana Cole", ServiceTier.Silver));

        // Assert
        cut.WaitForAssertion(() =>
            Assert.Contains("2", cut.Find("[data-testid='queue-active-count']").TextContent));
    }

    [Fact]
    public void GivenDispatcherHome_WhenRendered_ThenTheDispatchHubIsStarted()
    {
        // Arrange — the page must start the DispatchHub on mount so real-time queue updates flow (AC-3). The
        // hub starts only once LoadAsync has completed, so complete the load as part of the mount.
        ArrangeDeferredQueue();

        // Act
        var cut = Render<DispatcherHome>();
        cut.InvokeAsync(() => _queueLoad.SetResult(Array.Empty<ActiveRequestEntry>()));

        // Assert
        cut.WaitForAssertion(() => _dispatchHub.Verify(h => h.StartAsync(), Times.Once));
    }
}
