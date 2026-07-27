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

    private async Task<DispatcherRequestQueueViewModel> LoadedViewModel(params ActiveRequestEntry[] entries)
    {
        _queueService.Setup(s => s.GetActiveRequestsAsync()).ReturnsAsync(entries.ToList());
        var vm = new DispatcherRequestQueueViewModel(_queueService.Object, _hub.Object);
        await vm.LoadAsync();
        return vm;
    }

    private static ActiveRequestEntry Entry(string requesterName, ServiceTier tier) =>
        new(Guid.NewGuid(), requesterName, tier, "Transmission Control Fault", "Pending", null,
            DateTimeOffset.UtcNow.AddMinutes(-1));

    private IRenderedComponent<RequestQueueRail> RenderRail(DispatcherRequestQueueViewModel vm)
    {
        Services.AddSingleton(vm);
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
