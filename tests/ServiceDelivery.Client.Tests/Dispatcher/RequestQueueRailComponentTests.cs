using Bunit;
using Microsoft.Extensions.DependencyInjection;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.ViewModels;
using ServiceDelivery.Client.UI.Features.Dispatcher.Components;

namespace ServiceDelivery.Client.Tests.Dispatcher;

/// <summary>
/// AC-3 — the request queue rail renders one <see cref="RequestCard"/> per entry in the injected
/// <see cref="DispatcherRequestQueueViewModel.Queue"/>, inside the <c>dispatcher-queue-list</c> container.
/// Also covers the empty state (zero active requests → no cards) that the ACs require but the mockup does not
/// depict. The ViewModel is real, backed by a mocked <see cref="IActiveRequestQueueService"/>.
/// </summary>
public class RequestQueueRailComponentTests : BunitContext
{
    private readonly Mock<IActiveRequestQueueService> _queueService = new();
    private readonly Mock<IDispatchHubService> _hub = new();
    private readonly Mock<IRedirectEligibilityService> _eligibility = new();
    private readonly Mock<IDispatcherRedirectService> _redirectService = new();
    private readonly Mock<IDispatcherFleetService> _fleetService = new();
    private readonly Mock<IVehiclePositionHubService> _positionHub = new();

    private async Task<DispatcherRequestQueueViewModel> LoadedViewModel(params ActiveRequestEntry[] entries)
    {
        _queueService.Setup(s => s.GetActiveRequestsAsync()).ReturnsAsync(entries.ToList());
        var vm = new DispatcherRequestQueueViewModel(
            _queueService.Object, _hub.Object, _eligibility.Object, _redirectService.Object);
        await vm.LoadAsync();
        return vm;
    }

    private static ActiveRequestEntry Entry(string requesterName, ServiceTier tier) =>
        new(Guid.NewGuid(), requesterName, tier, "Transmission Control Fault", "Pending", null,
            DateTimeOffset.UtcNow.AddMinutes(-1));

    private IRenderedComponent<RequestQueueRail> RenderRail(DispatcherRequestQueueViewModel vm)
    {
        _fleetService.Setup(s => s.GetFleetAsync()).ReturnsAsync(new List<FleetVehicleEntry>());
        Services.AddSingleton(vm);
        Services.AddSingleton(new DispatcherFleetViewModel(_fleetService.Object, _positionHub.Object));
        return Render<RequestQueueRail>();
    }

    [Fact]
    public async Task GivenViewModelWithThreeRequests_WhenRailRendered_ThenThreeCardsAreShownInTheList()
    {
        // Arrange
        var vm = await LoadedViewModel(
            Entry("Marcus Webb", ServiceTier.Gold),
            Entry("Dana Cole", ServiceTier.Silver),
            Entry("Erin Fox", ServiceTier.Bronze));

        // Act
        var cut = RenderRail(vm);

        // Assert
        var list = cut.Find("[data-testid='dispatcher-queue-list']");
        Assert.Equal(3, list.QuerySelectorAll(".sd-reqcard").Length);
    }

    [Fact]
    public async Task GivenAnEmptyQueue_WhenRailRendered_ThenListIsPresentWithNoCards()
    {
        // Arrange
        var vm = await LoadedViewModel();

        // Act
        var cut = RenderRail(vm);

        // Assert — the list container still renders (no card-stub fallback), just with zero cards.
        var list = cut.Find("[data-testid='dispatcher-queue-list']");
        Assert.Empty(list.QuerySelectorAll(".sd-reqcard"));
    }

    [Fact]
    public async Task GivenTheDispatchHubIsConnected_WhenRailRendered_ThenListExposesHubConnectedTrueForE2EReadiness()
    {
        // Arrange — the rail surfaces the DispatchHub connection state on its list element so a live E2E can
        // wait deterministically for the group join before firing a one-shot request-lifecycle event.
        _hub.Setup(h => h.IsConnected).Returns(true);
        var vm = await LoadedViewModel();

        // Act
        var cut = RenderRail(vm);

        // Assert
        var list = cut.Find("[data-testid='dispatcher-queue-list']");
        Assert.Equal("true", list.GetAttribute("data-dispatch-hub-connected"));
    }

    [Fact]
    public async Task GivenTheDispatchHubIsNotConnected_WhenRailRendered_ThenListExposesHubConnectedFalse()
    {
        // Arrange — before the connection is established the readiness hook must read "false" so the E2E gate
        // does not fire its one-shot event early.
        _hub.Setup(h => h.IsConnected).Returns(false);
        var vm = await LoadedViewModel();

        // Act
        var cut = RenderRail(vm);

        // Assert
        var list = cut.Find("[data-testid='dispatcher-queue-list']");
        Assert.Equal("false", list.GetAttribute("data-dispatch-hub-connected"));
    }

    [Fact]
    public async Task GivenTheDispatchHubIsConnected_WhenRailRendered_ThenTheLiveUpdatesStatusTextIsAxAccessible()
    {
        // Arrange — the Desktop (mac2) E2E gate has NO WebView context, so it cannot read the DOM-only
        // data-dispatch-hub-connected attribute; it can only observe the native AX tree. The rail therefore
        // mirrors the SAME IsHubConnected signal into an sr-only-but-AX-exposed status element (the FleetMap
        // a11y-summary pattern) carrying stable text the Desktop test waits for before firing a one-shot event.
        _hub.Setup(h => h.IsConnected).Returns(true);
        var vm = await LoadedViewModel();

        // Act
        var cut = RenderRail(vm);

        // Assert
        var status = cut.Find("[data-testid='dispatch-hub-a11y-status']");
        Assert.Equal("Live request updates connected", status.TextContent.Trim());
    }

    [Fact]
    public async Task GivenTheDispatchHubIsNotConnected_WhenRailRendered_ThenTheStatusTextIsNotConnected()
    {
        // Arrange — before the connection joins its dealer group the AX status must NOT read "connected", so
        // the Desktop gate does not fire its one-shot event early.
        _hub.Setup(h => h.IsConnected).Returns(false);
        var vm = await LoadedViewModel();

        // Act
        var cut = RenderRail(vm);

        // Assert
        var status = cut.Find("[data-testid='dispatch-hub-a11y-status']");
        Assert.NotEqual("Live request updates connected", status.TextContent.Trim());
    }

    // ---- FE-005: redirect bridge (fleet → eligibility) + dialog render ----------------------------------

    private static readonly Guid GoldRequestId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000009");
    private static readonly Guid RepId = Guid.Parse("50000000-0000-0000-0000-000000000001");

    private static ActiveRequestEntry GoldEntry() =>
        new(GoldRequestId, "Marcus Webb", ServiceTier.Gold, "Transmission Control Fault", "Pending", null,
            DateTimeOffset.UtcNow.AddMinutes(-1));

    private static FleetVehicleEntry EnRouteRepOnSilver() =>
        new("30000000-0000-0000-0000-000000000007", "IA-4471", "EnRoute", RepId, "J. Tran",
            41.8781, -93.0977, "Hydraulic Pressure Loss", "Silver", false, null);

    private static RedirectInfo GoldRedirectInfo() =>
        new(RepId, "J. Tran", ServiceTier.Silver, "Hydraulic Pressure Loss", ServiceTier.Gold,
            "Transmission Control Fault", false, GoldRequestId);

    private async Task<DispatcherFleetViewModel> LoadedFleet(params FleetVehicleEntry[] fleet)
    {
        _fleetService.Setup(s => s.GetFleetAsync()).ReturnsAsync(fleet.ToList());
        var vm = new DispatcherFleetViewModel(_fleetService.Object, _positionHub.Object);
        await vm.LoadAsync();
        return vm;
    }

    private IRenderedComponent<RequestQueueRail> RenderRail(
        DispatcherRequestQueueViewModel queueVm, DispatcherFleetViewModel fleetVm)
    {
        Services.AddSingleton(queueVm);
        Services.AddSingleton(fleetVm);
        return Render<RequestQueueRail>();
    }

    [Fact]
    public async Task GivenAnEligibleEntryAndFleetWithAnEnRouteRep_WhenRailRendered_ThenTheCardShowsARedirectButton()
    {
        // Arrange — the fleet ViewModel already holds an EnRoute rep on a lower-tier job, and the eligibility
        // service reports the Gold queue entry is redirectable. The rail must bridge the fleet snapshot into the
        // queue ViewModel (UpdateFleetData) so the card surfaces its Redirect button.
        _eligibility.Setup(e => e.FindEligibleRedirect(
                It.Is<ActiveRequestEntry>(r => r.RequestId == GoldRequestId),
                It.IsAny<IReadOnlyList<FleetVehicleEntry>>()))
            .Returns(GoldRedirectInfo());
        var queueVm = await LoadedViewModel(GoldEntry());
        var fleetVm = await LoadedFleet(EnRouteRepOnSilver());

        // Act
        var cut = RenderRail(queueVm, fleetVm);

        // Assert
        Assert.NotEmpty(cut.FindAll($"[data-testid='redirect-btn-{GoldRequestId}']"));
    }

    [Fact]
    public async Task GivenAFleetStateChange_WhenTheFleetViewModelRaisesStateChanged_ThenTheRailRecomputesEligibility()
    {
        // Arrange — no rep is EnRoute at render time, so no Redirect button initially. When the fleet later
        // gains an EnRoute rep and raises StateChanged, the rail must re-bridge and the button appears.
        _eligibility.Setup(e => e.FindEligibleRedirect(
                It.IsAny<ActiveRequestEntry>(), It.IsAny<IReadOnlyList<FleetVehicleEntry>>()))
            .Returns((ActiveRequestEntry _, IReadOnlyList<FleetVehicleEntry> fleet) =>
                fleet.Any(v => v.RepState == "EnRoute") ? GoldRedirectInfo() : null);
        var queueVm = await LoadedViewModel(GoldEntry());
        var fleetVm = await LoadedFleet();
        var cut = RenderRail(queueVm, fleetVm);
        Assert.Empty(cut.FindAll($"[data-testid='redirect-btn-{GoldRequestId}']"));

        // Act — a live position update brings a rep EnRoute; the fleet ViewModel raises StateChanged.
        await cut.InvokeAsync(() => fleetVm.HandleVehiclePositionUpdatedAsync(
            new VehiclePositionUpdatedPayload(
                RepId, Guid.Parse("30000000-0000-0000-0000-000000000007"), 41.8781, -93.0977, "EnRoute")));

        // Assert
        cut.WaitForAssertion(() =>
            Assert.NotEmpty(cut.FindAll($"[data-testid='redirect-btn-{GoldRequestId}']")));
    }

    [Fact]
    public async Task GivenTheQueueViewModelHasAnActiveRedirect_WhenRailRendered_ThenTheConfirmDialogIsShown()
    {
        // Arrange — open the redirect dialog on the queue ViewModel, then render the rail.
        _eligibility.Setup(e => e.FindEligibleRedirect(
                It.IsAny<ActiveRequestEntry>(), It.IsAny<IReadOnlyList<FleetVehicleEntry>>()))
            .Returns(GoldRedirectInfo());
        var queueVm = await LoadedViewModel(GoldEntry());
        queueVm.UpdateFleetData(new[] { EnRouteRepOnSilver() });
        queueVm.ShowRedirectDialog(GoldRequestId);
        var fleetVm = await LoadedFleet(EnRouteRepOnSilver());

        // Act
        var cut = RenderRail(queueVm, fleetVm);

        // Assert
        Assert.NotEmpty(cut.FindAll("[data-testid='redirect-dialog']"));
    }

    [Fact]
    public async Task GivenNoActiveRedirect_WhenRailRendered_ThenNoConfirmDialogIsShown()
    {
        // Arrange
        var queueVm = await LoadedViewModel(GoldEntry());
        var fleetVm = await LoadedFleet(EnRouteRepOnSilver());

        // Act
        var cut = RenderRail(queueVm, fleetVm);

        // Assert
        Assert.Empty(cut.FindAll("[data-testid='redirect-dialog']"));
    }

    [Fact]
    public async Task GivenAConfirmedRedirectThatErrors_WhenTheDialogReappears_ThenTheErrorBannerShowsTheRealMessageThroughTheRailBinding()
    {
        // Arrange — drive the WHOLE rail→dialog binding path (not the isolated dialog with a code-set
        // parameter): eligibility reports the Gold entry is redirectable, the dialog is open, and the redirect
        // service fails. Confirming re-surfaces the dialog carrying ViewModel.RedirectError, which the rail must
        // bind as an EXPRESSION (@ViewModel.RedirectError) — a literal-string binding would render the attribute
        // text "ViewModel.RedirectError" instead of this real message, which is the FE-005 defect this guards.
        const string realError = "Rep is no longer redirectable.";
        _eligibility.Setup(e => e.FindEligibleRedirect(
                It.IsAny<ActiveRequestEntry>(), It.IsAny<IReadOnlyList<FleetVehicleEntry>>()))
            .Returns(GoldRedirectInfo());
        _redirectService.Setup(s => s.RedirectAsync(RepId, GoldRequestId))
            .ThrowsAsync(new InvalidOperationException(realError));
        var queueVm = await LoadedViewModel(GoldEntry());
        queueVm.UpdateFleetData(new[] { EnRouteRepOnSilver() });
        queueVm.ShowRedirectDialog(GoldRequestId);
        var fleetVm = await LoadedFleet(EnRouteRepOnSilver());
        var cut = RenderRail(queueVm, fleetVm);

        // Act — confirm the redirect through the rendered dialog button (the real OnConfirm binding).
        await cut.Find("[data-testid='redirect-confirm']").ClickAsync(new());

        // Assert — the re-surfaced dialog's error banner shows the ACTUAL message, proving the rail bound the
        // value, not the literal expression text.
        cut.WaitForAssertion(() =>
        {
            var banner = cut.Find("[data-testid='redirect-error']");
            Assert.Contains(realError, banner.TextContent);
            Assert.DoesNotContain("ViewModel.RedirectError", banner.TextContent);
        });
    }

    [Fact]
    public async Task GivenViewModelWithMixedTiers_WhenRailRendered_ThenGoldCardIsRenderedFirst()
    {
        // Arrange — loaded Bronze-first so DOM order proves the ViewModel's Gold→Silver→Bronze sort, not
        // insertion order.
        var vm = await LoadedViewModel(
            Entry("Erin Fox", ServiceTier.Bronze),
            Entry("Marcus Webb", ServiceTier.Gold));

        // Act
        var cut = RenderRail(vm);

        // Assert
        var firstCardName = cut.FindAll("[data-testid='reqcard-name']")[0].TextContent;
        Assert.Contains("Marcus Webb", firstCardName);
    }
}
