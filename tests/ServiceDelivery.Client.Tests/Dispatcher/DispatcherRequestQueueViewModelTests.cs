using System.Text.Json;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.ViewModels;

namespace ServiceDelivery.Client.Tests.Dispatcher;

/// <summary>
/// Pure xUnit tests for <see cref="DispatcherRequestQueueViewModel"/> (FE-004 ACs 1, 3, 4) plus the
/// captured-payload wire-contract proofs for the three DispatchHub event DTOs (ADR-0011) and the
/// <see cref="ActiveRequestDto.ToActiveRequestEntry"/> mapping. All ViewModel tests use a mocked
/// <see cref="IActiveRequestQueueService"/> and <see cref="IDispatchHubService"/> — no rendering, no live hub.
/// </summary>
public class DispatcherRequestQueueViewModelTests
{
    private readonly Mock<IActiveRequestQueueService> _queueService = new();
    private readonly Mock<IDispatchHubService> _hub = new();
    private readonly Mock<IRedirectEligibilityService> _eligibility = new();
    private readonly Mock<IDispatcherRedirectService> _redirectService = new();

    private DispatcherRequestQueueViewModel CreateViewModel() =>
        new(_queueService.Object, _hub.Object, _eligibility.Object, _redirectService.Object);

    private void QueueReturns(params ActiveRequestEntry[] entries) =>
        _queueService.Setup(s => s.GetActiveRequestsAsync()).ReturnsAsync(entries.ToList());

    private static ActiveRequestEntry Entry(
        Guid? requestId = null,
        string requesterName = "Marcus Webb",
        ServiceTier tier = ServiceTier.Gold,
        string dtcTitle = "Transmission Control Fault",
        string status = "Pending",
        string? assignedRepName = null,
        DateTimeOffset? createdAt = null) =>
        new(
            requestId ?? Guid.NewGuid(),
            requesterName,
            tier,
            dtcTitle,
            status,
            assignedRepName,
            createdAt ?? DateTimeOffset.UtcNow);

    // ---- AC-1: queue ordering ---------------------------------------------------------------------------

    [Fact]
    public async Task GivenRequestsOfMixedTiers_WhenLoaded_ThenOrderIsGoldSilverBronze()
    {
        // Arrange — deliberately loaded Bronze, Gold, Silver so insertion order cannot pass coincidentally.
        var created = new DateTimeOffset(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);
        QueueReturns(
            Entry(requesterName: "Erin Fox", tier: ServiceTier.Bronze, createdAt: created),
            Entry(requesterName: "Marcus Webb", tier: ServiceTier.Gold, createdAt: created),
            Entry(requesterName: "Dana Cole", tier: ServiceTier.Silver, createdAt: created));
        var vm = CreateViewModel();

        // Act
        await vm.LoadAsync();

        // Assert
        Assert.Equal(
            new[] { "Marcus Webb", "Dana Cole", "Erin Fox" },
            vm.Queue.Select(e => e.RequesterName).ToArray());
    }

    [Fact]
    public async Task GivenMultipleGoldRequestsWithDifferentCreatedAt_WhenLoaded_ThenOrderedByCreatedAtAscending()
    {
        // Arrange — same tier, so only createdAt (ascending, oldest first) decides order.
        var older = new DateTimeOffset(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);
        var newer = new DateTimeOffset(2026, 7, 25, 12, 5, 0, TimeSpan.Zero);
        QueueReturns(
            Entry(requesterName: "Newer Gold", tier: ServiceTier.Gold, createdAt: newer),
            Entry(requesterName: "Older Gold", tier: ServiceTier.Gold, createdAt: older));
        var vm = CreateViewModel();

        // Act
        await vm.LoadAsync();

        // Assert
        Assert.Equal(
            new[] { "Older Gold", "Newer Gold" },
            vm.Queue.Select(e => e.RequesterName).ToArray());
    }

    // ---- AC-3: real-time hub event merging --------------------------------------------------------------

    [Fact]
    public async Task GivenAnEmptyQueue_WhenServiceRequestPendingHandled_ThenRequestAppearsInQueue()
    {
        // Arrange — the Pending event carries no requester name, so the ViewModel fetches the full entry.
        var requestId = Guid.NewGuid();
        var fetched = Entry(requestId: requestId, requesterName: "Erin Fox", tier: ServiceTier.Bronze);
        _queueService.Setup(s => s.GetRequestAsync(requestId)).ReturnsAsync(fetched);
        QueueReturns();
        var vm = CreateViewModel();
        await vm.LoadAsync();

        // Act
        await vm.HandleServiceRequestPendingAsync(
            new ServiceRequestPendingPayload(requestId, "Bronze", "Lost Comm w/ ECM", "41.6,-93.6"));

        // Assert
        var entry = Assert.Single(vm.Queue);
        Assert.Equal(requestId, entry.RequestId);
        Assert.Equal("Erin Fox", entry.RequesterName);
    }

    [Fact]
    public async Task GivenAPendingEventWhoseFollowUpFetchReturnsNull_WhenHandled_ThenQueueStaysEmpty()
    {
        // Arrange — the request was already completed server-side; the follow-up fetch returns null and the
        // event is silently skipped (plan: requesterName gap handling).
        var requestId = Guid.NewGuid();
        _queueService.Setup(s => s.GetRequestAsync(requestId)).ReturnsAsync((ActiveRequestEntry?)null);
        QueueReturns();
        var vm = CreateViewModel();
        await vm.LoadAsync();

        // Act
        await vm.HandleServiceRequestPendingAsync(
            new ServiceRequestPendingPayload(requestId, "Gold", "Transmission Control Fault", "41.6,-93.6"));

        // Assert
        Assert.Empty(vm.Queue);
    }

    [Fact]
    public async Task GivenAQueuedPendingRequest_WhenServiceRequestAssignedHandled_ThenStatusAndRepNameUpdated()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        QueueReturns(Entry(requestId: requestId, status: "Pending", assignedRepName: null));
        var vm = CreateViewModel();
        await vm.LoadAsync();

        // Act
        await vm.HandleServiceRequestAssignedAsync(
            new ServiceRequestAssignedPayload(requestId, Guid.NewGuid(), "J. Tran", 12.5));

        // Assert
        var entry = Assert.Single(vm.Queue);
        Assert.Equal("Assigned", entry.Status);
        Assert.Equal("J. Tran", entry.AssignedRepName);
    }

    [Fact]
    public async Task GivenAnAssignedEventForARequestNotYetInTheQueue_WhenHandled_ThenTheRequestIsFetchedAndAddedAsAssigned()
    {
        // Arrange — on this backend a request that matches an available rep is assigned WITHOUT ever being
        // broadcast to the dispatcher as Pending (ServiceRequestPending fires only when no rep matches). So the
        // dispatcher's first real-time signal for such a request is ServiceRequestAssigned. The handler must
        // fetch the full entry and ADD it (upsert) — not silently ignore an assigned event for a request it has
        // never seen — otherwise a newly-assigned request never appears on the live board until a page reload.
        var requestId = Guid.NewGuid();
        var fetched = Entry(
            requestId: requestId, requesterName: "Erin Fox", tier: ServiceTier.Gold,
            status: "Pending", assignedRepName: null);
        _queueService.Setup(s => s.GetRequestAsync(requestId)).ReturnsAsync(fetched);
        QueueReturns();
        var vm = CreateViewModel();
        await vm.LoadAsync();

        // Act
        await vm.HandleServiceRequestAssignedAsync(
            new ServiceRequestAssignedPayload(requestId, Guid.NewGuid(), "J. Tran", 12.5));

        // Assert
        var entry = Assert.Single(vm.Queue);
        Assert.Equal(requestId, entry.RequestId);
        Assert.Equal("Erin Fox", entry.RequesterName);
        Assert.Equal("Assigned", entry.Status);
        Assert.Equal("J. Tran", entry.AssignedRepName);
    }

    [Fact]
    public async Task GivenAnAssignedEventForAnUnknownRequestWhoseFetchReturnsNull_WhenHandled_ThenQueueStaysEmpty()
    {
        // Arrange — the request was already completed server-side between the assign and the follow-up fetch;
        // a null fetch is skipped silently, mirroring the Pending handler (no phantom card).
        var requestId = Guid.NewGuid();
        _queueService.Setup(s => s.GetRequestAsync(requestId)).ReturnsAsync((ActiveRequestEntry?)null);
        QueueReturns();
        var vm = CreateViewModel();
        await vm.LoadAsync();

        // Act
        await vm.HandleServiceRequestAssignedAsync(
            new ServiceRequestAssignedPayload(requestId, Guid.NewGuid(), "J. Tran", 12.5));

        // Assert
        Assert.Empty(vm.Queue);
    }

    [Fact]
    public async Task GivenAQueuedRequest_WhenServiceRequestCompletedHandled_ThenRequestRemovedFromQueue()
    {
        // Arrange
        var completedId = Guid.NewGuid();
        var survivorId = Guid.NewGuid();
        QueueReturns(
            Entry(requestId: completedId, requesterName: "Sam Ortiz"),
            Entry(requestId: survivorId, requesterName: "Priya Nair"));
        var vm = CreateViewModel();
        await vm.LoadAsync();

        // Act
        await vm.HandleServiceRequestCompletedAsync(new ServiceRequestCompletedPayload(completedId));

        // Assert
        var entry = Assert.Single(vm.Queue);
        Assert.Equal(survivorId, entry.RequestId);
    }

    [Fact]
    public async Task GivenAQueuedRequest_WhenAnyHubEventHandled_ThenStateChangedEventFires()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        QueueReturns(Entry(requestId: requestId));
        var vm = CreateViewModel();
        await vm.LoadAsync();
        var fired = false;
        vm.StateChanged += () => fired = true;

        // Act
        await vm.HandleServiceRequestCompletedAsync(new ServiceRequestCompletedPayload(requestId));

        // Assert
        Assert.True(fired);
    }

    // ---- AC-4: completed request disappears -------------------------------------------------------------

    [Fact]
    public async Task GivenAQueuedRequest_WhenServiceRequestCompletedHandled_ThenQueueIsEmpty()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        QueueReturns(Entry(requestId: requestId));
        var vm = CreateViewModel();
        await vm.LoadAsync();

        // Act
        await vm.HandleServiceRequestCompletedAsync(new ServiceRequestCompletedPayload(requestId));

        // Assert
        Assert.Empty(vm.Queue);
    }

    // ---- Hub lifecycle ----------------------------------------------------------------------------------

    [Fact]
    public async Task GivenViewModel_WhenStartHubAsync_ThenHandlersRegisteredAndConnectionStarted()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        await vm.StartHubAsync();

        // Assert
        _hub.Verify(h => h.OnServiceRequestPending(
            It.IsAny<Func<ServiceRequestPendingPayload, Task>>()), Times.Once);
        _hub.Verify(h => h.OnServiceRequestAssigned(
            It.IsAny<Func<ServiceRequestAssignedPayload, Task>>()), Times.Once);
        _hub.Verify(h => h.OnServiceRequestCompleted(
            It.IsAny<Func<ServiceRequestCompletedPayload, Task>>()), Times.Once);
        _hub.Verify(h => h.StartAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenViewModel_WhenStopHubAsync_ThenConnectionStopped()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        await vm.StopHubAsync();

        // Assert
        _hub.Verify(h => h.StopAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenViewModel_WhenStartHubAsyncCompletes_ThenStateChangedFiresSoTheConnectedStateSurfaces()
    {
        // Arrange — the hub connect is awaited by StartHubAsync; once it completes the queue's connectivity
        // has changed, so the rail must be told to re-render (this is the deterministic signal the E2E real-time
        // scenarios gate on before firing a one-shot DispatchHub event — the group join must be live first).
        var vm = CreateViewModel();
        var fired = false;
        vm.StateChanged += () => fired = true;

        // Act
        await vm.StartHubAsync();

        // Assert
        Assert.True(fired);
    }

    [Fact]
    public void GivenTheDispatchHubIsConnected_WhenIsHubConnectedIsRead_ThenItReturnsTrue()
    {
        // Arrange
        _hub.Setup(h => h.IsConnected).Returns(true);
        var vm = CreateViewModel();

        // Act & Assert — the ViewModel surfaces the hub's live connection state (mirrors
        // RequesterPendingViewModel.IsHubConnected) so the rail can expose it as an E2E readiness hook.
        Assert.True(vm.IsHubConnected);
    }

    [Fact]
    public void GivenTheDispatchHubIsNotConnected_WhenIsHubConnectedIsRead_ThenItReturnsFalse()
    {
        // Arrange
        _hub.Setup(h => h.IsConnected).Returns(false);
        var vm = CreateViewModel();

        // Act & Assert
        Assert.False(vm.IsHubConnected);
    }

    // ---- ActiveRequestDto → ActiveRequestEntry mapping (fail-loud tier) ---------------------------------

    [Fact]
    public void GivenAnActiveRequestDto_WhenMappedToEntry_ThenAllFieldsFlowThrough()
    {
        // Arrange — distinct values per field so a mis-wired mapping cannot pass coincidentally.
        var createdAt = new DateTimeOffset(2026, 7, 25, 12, 4, 0, TimeSpan.Zero);
        var dto = new ActiveRequestDto(
            RequestId: Guid.Parse("aaaaaaaa-0000-0000-0000-000000000009"),
            RequesterName: "Dana Cole",
            Tier: "Silver",
            DtcTitle: "Hydraulic Pressure Loss",
            Status: "Assigned",
            AssignedRepId: Guid.Parse("50000000-0000-0000-0000-000000000001"),
            AssignedRepName: "J. Tran",
            CreatedAt: createdAt);

        // Act
        var entry = dto.ToActiveRequestEntry();

        // Assert
        Assert.Equal(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000009"), entry.RequestId);
        Assert.Equal("Dana Cole", entry.RequesterName);
        Assert.Equal(ServiceTier.Silver, entry.Tier);
        Assert.Equal("Hydraulic Pressure Loss", entry.DtcTitle);
        Assert.Equal("Assigned", entry.Status);
        Assert.Equal("J. Tran", entry.AssignedRepName);
        Assert.Equal(createdAt, entry.CreatedAt);
    }

    [Fact]
    public void GivenAnActiveRequestDtoWithUnknownTier_WhenMapped_ThenThrowsInvalidOperationException()
    {
        // Arrange — a drifted tier value must fail loud, not silently default to ServiceTier.None (ADR-0011).
        var dto = new ActiveRequestDto(
            RequestId: Guid.NewGuid(),
            RequesterName: "Marcus Webb",
            Tier: "Platinum",
            DtcTitle: "Transmission Control Fault",
            Status: "Pending",
            AssignedRepId: null,
            AssignedRepName: null,
            CreatedAt: DateTimeOffset.UtcNow);

        // Act
        var act = dto.ToActiveRequestEntry;

        // Assert
        var ex = Assert.Throws<InvalidOperationException>(() => act());
        Assert.Contains("Platinum", ex.Message);
    }

    // ---- ADR-0011 captured-payload deserialization: DispatchHub events ----------------------------------

    private static T DeserializeWire<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    [Fact]
    public void GivenAServiceRequestPendingJson_WhenDeserialised_ThenAllFieldsBindCorrectly()
    {
        // Arrange — the real DispatchHub ServiceRequestPending wire shape (camelCase), distinct value per field.
        const string json =
            """
            {
                "requestId": "aaaaaaaa-0000-0000-0000-000000000009",
                "requesterTier": "Gold",
                "dtcTitle": "Transmission Control Fault",
                "location": "41.8781,-93.0977"
            }
            """;

        // Act
        var payload = DeserializeWire<ServiceRequestPendingPayload>(json);

        // Assert
        Assert.Equal(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000009"), payload.RequestId);
        Assert.Equal("Gold", payload.RequesterTier);
        Assert.Equal("Transmission Control Fault", payload.DtcTitle);
        Assert.Equal("41.8781,-93.0977", payload.Location);
    }

    [Fact]
    public void GivenAServiceRequestAssignedJson_WhenDeserialised_ThenAllFieldsBindCorrectly()
    {
        // Arrange — the real DispatchHub ServiceRequestAssigned wire shape (eta is a double).
        const string json =
            """
            {
                "requestId": "aaaaaaaa-0000-0000-0000-000000000009",
                "repId": "50000000-0000-0000-0000-000000000001",
                "repName": "J. Tran",
                "eta": 12.5
            }
            """;

        // Act
        var payload = DeserializeWire<ServiceRequestAssignedPayload>(json);

        // Assert
        Assert.Equal(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000009"), payload.RequestId);
        Assert.Equal(Guid.Parse("50000000-0000-0000-0000-000000000001"), payload.RepId);
        Assert.Equal("J. Tran", payload.RepName);
        Assert.Equal(12.5, payload.Eta);
    }

    [Fact]
    public void GivenAServiceRequestCompletedJson_WhenDeserialised_ThenAllFieldsBindCorrectly()
    {
        // Arrange — the real DispatchHub ServiceRequestCompleted wire shape (requestId only).
        const string json =
            """
            { "requestId": "aaaaaaaa-0000-0000-0000-000000000009" }
            """;

        // Act
        var payload = DeserializeWire<ServiceRequestCompletedPayload>(json);

        // Assert
        Assert.Equal(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000009"), payload.RequestId);
    }
}
