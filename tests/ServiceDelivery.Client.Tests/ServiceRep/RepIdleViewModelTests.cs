using Microsoft.Extensions.Logging.Abstractions;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.Services;
using ServiceDelivery.Client.Core.ViewModels;

namespace ServiceDelivery.Client.Tests.ServiceRep;

public class RepIdleViewModelTests
{
    private readonly Mock<IRepHubService> _repHub = new();
    private readonly Mock<IPersonaNavigator> _navigator = new();
    private readonly Mock<IClaimedVehicleStore> _claimedVehicleStore = new();
    private readonly Mock<IHeartbeatService> _heartbeatService = new();

    private static ClaimedVehicle Vehicle(
        string registration = "IA-4471",
        string model = "Transit 350",
        params string[] equipment) =>
        new(Guid.NewGuid(), registration, model,
            equipment.Length == 0 ? new[] { "Hydraulics", "Coolant" } : equipment);

    private static JobOfferPayload Offer() =>
        new(Guid.NewGuid(), "Maria Lopez", ServiceTier.Gold, "Hydraulic leak", 4.2, 9, 41.6, -93.6);

    private RepIdleViewModel CreateViewModel(ClaimedVehicle? vehicle = null)
    {
        _claimedVehicleStore.SetupGet(s => s.CurrentVehicle).Returns(vehicle ?? Vehicle());
        return new RepIdleViewModel(
            _claimedVehicleStore.Object, _repHub.Object, _navigator.Object,
            NullLogger<RepIdleViewModel>.Instance, _heartbeatService.Object);
    }

    [Fact]
    public void GivenAStoredClaimedVehicle_WhenRepIdleViewModelConstructed_ThenVehicleRegistrationIsFromStore()
    {
        // Arrange
        var stored = Vehicle(registration: "V-001");
        _claimedVehicleStore.SetupGet(s => s.CurrentVehicle).Returns(stored);

        // Act
        var vm = new RepIdleViewModel(
            _claimedVehicleStore.Object, _repHub.Object, _navigator.Object,
            NullLogger<RepIdleViewModel>.Instance, _heartbeatService.Object);

        // Assert
        Assert.Equal("V-001", vm.Vehicle.Registration);
    }

    [Fact]
    public void GivenAStoredClaimedVehicle_WhenRepIdleViewModelConstructed_ThenVehicleEquipmentIsFromStore()
    {
        // Arrange
        var stored = Vehicle(equipment: new[] { "Hydraulics", "Coolant", "Diagnostics" });
        _claimedVehicleStore.SetupGet(s => s.CurrentVehicle).Returns(stored);

        // Act
        var vm = new RepIdleViewModel(
            _claimedVehicleStore.Object, _repHub.Object, _navigator.Object,
            NullLogger<RepIdleViewModel>.Instance, _heartbeatService.Object);

        // Assert
        Assert.Equal(new[] { "Hydraulics", "Coolant", "Diagnostics" }, vm.Vehicle.EquipmentTypes);
    }

    [Fact]
    public void GivenAStoredClaimedVehicle_WhenRepIdleViewModelConstructed_ThenStoreIsNotClearedAfterReading()
    {
        // Arrange
        // BUG-041: IClaimedVehicleStore is the durable source of truth for the session — shared with
        // ReleaseVehicleAction. The idle view must READ the claimed vehicle on construction but must NOT
        // clear the store; clearing here wiped the claim before the release action could read it, so the
        // confirmation dialog never opened. The store is cleared only on a successful release.
        _claimedVehicleStore.SetupGet(s => s.CurrentVehicle).Returns(Vehicle());

        // Act
        _ = new RepIdleViewModel(
            _claimedVehicleStore.Object, _repHub.Object, _navigator.Object,
            NullLogger<RepIdleViewModel>.Instance, _heartbeatService.Object);

        // Assert
        _claimedVehicleStore.Verify(s => s.ClearVehicle(), Times.Never);
    }

    [Fact]
    public void GivenAnEmptyStore_WhenRepIdleViewModelConstructed_ThenVehicleIsNeutralEmptyAndDoesNotThrow()
    {
        // Arrange
        // Edge case (Checkpoint #1): direct navigation to /rep/idle with no prior take-over leaves the
        // store empty. The VM must degrade gracefully to a neutral empty ClaimedVehicle — no
        // NullReferenceException — so the card/subtitle render blank-but-safe.
        _claimedVehicleStore.SetupGet(s => s.CurrentVehicle).Returns((ClaimedVehicle?)null);

        // Act
        var vm = new RepIdleViewModel(
            _claimedVehicleStore.Object, _repHub.Object, _navigator.Object,
            NullLogger<RepIdleViewModel>.Instance, _heartbeatService.Object);

        // Assert
        Assert.NotNull(vm.Vehicle);
        Assert.Equal(string.Empty, vm.Vehicle.Registration);
        Assert.Equal(string.Empty, vm.Vehicle.Model);
        Assert.Empty(vm.Vehicle.EquipmentTypes);
    }

    [Fact]
    public void GivenRealStoreUpdatedAfterFirstConstruction_WhenVehiclePropertyRead_ThenReturnsUpdatedVehicle()
    {
        // Arrange — BUG-042. The VM is reused across navigations (AddScoped ≈ singleton for the session),
        // so Vehicle must read the store on each access rather than caching the first value at construction.
        // The same backing store returns a different vehicle after the second take-over.
        var store = new InMemoryClaimedVehicleStore();
        store.SetVehicle(new ClaimedVehicle(
            Guid.NewGuid(), "V-001", "Transit 350", new[] { "Hydraulics" }));
        var vm = new RepIdleViewModel(
            store, _repHub.Object, _navigator.Object, NullLogger<RepIdleViewModel>.Instance,
            _heartbeatService.Object);

        // Act — the store changes after construction (release then a fresh take-over).
        store.ClearVehicle();
        store.SetVehicle(new ClaimedVehicle(
            Guid.NewGuid(), "V-002", "Sprinter 250", new[] { "Coolant" }));

        // Assert
        Assert.Equal("V-002", vm.Vehicle.Registration);
    }

    [Fact]
    public void GivenClaimedVehicle_WhenRepIdleViewModelLoaded_ThenStateIsAvailable()
    {
        // Arrange
        var vm = CreateViewModel(Vehicle());

        // Act
        var state = vm.State;

        // Assert
        Assert.Equal(RepIdleState.Available, state);
    }

    [Fact]
    public void GivenClaimedVehicle_WhenRepIdleViewModelLoaded_ThenVehicleRegistrationIsExposed()
    {
        // Arrange
        var vehicle = Vehicle(registration: "IA-4471");
        var vm = CreateViewModel(vehicle);

        // Act
        var registration = vm.Vehicle.Registration;

        // Assert
        Assert.Equal("IA-4471", registration);
    }

    [Fact]
    public void GivenClaimedVehicle_WhenRepIdleViewModelLoaded_ThenEquipmentListIsExposed()
    {
        // Arrange
        var vehicle = Vehicle(equipment: new[] { "Hydraulics", "Coolant", "Diagnostics" });
        var vm = CreateViewModel(vehicle);

        // Act
        var equipment = vm.Vehicle.EquipmentTypes;

        // Assert
        Assert.Equal(new[] { "Hydraulics", "Coolant", "Diagnostics" }, equipment);
    }

    [Fact]
    public void GivenAFreshlyClaimedVehicle_WhenRepIdleViewModelLoaded_ThenJobsCompletedTodayDefaultsToZero()
    {
        // Arrange
        var vm = CreateViewModel(Vehicle());

        // Act
        var jobsCompleted = vm.JobsCompletedToday;

        // Assert
        Assert.Equal(0, jobsCompleted);
    }

    [Fact]
    public async Task GivenIdleState_WhenNoHubEventReceived_ThenViewModelRemainsInAvailableState()
    {
        // Arrange
        var vm = CreateViewModel(Vehicle());

        // Act
        await vm.StartAsync();

        // Assert
        Assert.Equal(RepIdleState.Available, vm.State);
        _navigator.Verify(n => n.NavigateToJobOffer(It.IsAny<JobOfferPayload>()), Times.Never);
    }

    [Fact]
    public async Task GivenIdleViewModel_WhenJobOfferReceivedEventFired_ThenNavigationToJobOfferIsTriggered()
    {
        // Arrange
        Func<JobOfferPayload, Task>? capturedHandler = null;
        _repHub.Setup(h => h.OnJobOfferReceived(It.IsAny<Func<JobOfferPayload, Task>>()))
            .Callback<Func<JobOfferPayload, Task>>(h => capturedHandler = h);
        var vm = CreateViewModel(Vehicle());
        await vm.StartAsync();
        var offer = Offer();

        // Act
        await capturedHandler!.Invoke(offer);

        // Assert
        _navigator.Verify(n => n.NavigateToJobOffer(offer), Times.Once);
    }

    [Fact]
    public async Task GivenRepIdleViewModel_WhenStartAsyncCalled_ThenRepHubServiceStartIsCalled()
    {
        // Arrange
        // The transition to an offer is push-driven — the view starts the hub and waits; there is
        // no polling or manual refresh (AC-5).
        var vm = CreateViewModel(Vehicle());

        // Act
        await vm.StartAsync();

        // Assert
        _repHub.Verify(h => h.StartAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenRepIdleViewModel_WhenStopAsyncCalled_ThenRepHubServiceStopIsCalled()
    {
        // Arrange
        var vm = CreateViewModel(Vehicle());

        // Act
        await vm.StopAsync();

        // Assert
        _repHub.Verify(h => h.StopAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenRepHubStartThrows_WhenRepIdleViewModelStartAsyncCalled_ThenExceptionIsSwallowedAndNotRethrown()
    {
        // Arrange — BUG-038: when RepHub is unreachable the connect throws. StartAsync must swallow
        // and log it so the idle screen never trips Blazor's #blazor-error-ui banner.
        _repHub.Setup(h => h.StartAsync()).ThrowsAsync(new InvalidOperationException("hub unreachable"));
        var vm = CreateViewModel(Vehicle());

        // Act
        var exception = await Record.ExceptionAsync(() => vm.StartAsync());

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task GivenRepHubStartThrows_WhenRepIdleViewModelStartAsyncCalled_ThenIsHubConnectedIsFalse()
    {
        // Arrange — a failed connect leaves the screen in the reconnecting state, surfaced via the flag.
        _repHub.Setup(h => h.StartAsync()).ThrowsAsync(new InvalidOperationException("hub unreachable"));
        _repHub.SetupGet(h => h.IsConnected).Returns(false);
        var vm = CreateViewModel(Vehicle());

        // Act
        await vm.StartAsync();

        // Assert
        Assert.False(vm.IsHubConnected);
    }

    [Fact]
    public async Task GivenRepHubStartSucceeds_WhenRepIdleViewModelStartAsyncCalled_ThenIsHubConnectedIsTrue()
    {
        // Arrange — happy path: a successful connect reports the hub as connected.
        _repHub.SetupGet(h => h.IsConnected).Returns(true);
        var vm = CreateViewModel(Vehicle());

        // Act
        await vm.StartAsync();

        // Assert
        Assert.True(vm.IsHubConnected);
    }

    [Fact]
    public async Task GivenRepHubStartThrowsThenHubReportsConnected_WhenStartAsyncCalled_ThenIsHubConnectedReflectsTheHubState()
    {
        // Arrange — ViewModel-level concern (NOT the retry itself; the bounded back-off retry is owned
        // by SignalRRepHubService and asserted in SignalRRepHubServiceTests). Here: even when the first
        // connect attempt throws, the ViewModel swallows it and IsHubConnected simply mirrors whatever
        // the hub now reports — so once the service's own retry has reconnected, the screen sees it.
        _repHub.Setup(h => h.StartAsync())
            .ThrowsAsync(new InvalidOperationException("hub unreachable"));
        _repHub.SetupGet(h => h.IsConnected).Returns(true);
        var vm = CreateViewModel(Vehicle());

        // Act
        await vm.StartAsync();

        // Assert
        Assert.True(vm.IsHubConnected);
    }

    [Fact]
    public async Task GivenRepIdleViewModelStartedTwice_WhenJobOfferArrives_ThenNavigationIsInvokedExactlyOnce()
    {
        // Arrange — BUG-042 (double-subscribe). The VM is scoped (reused across navigations) and the
        // page calls StartAsync on every entry to /rep/idle. HubConnection.On(...) accumulates handlers,
        // so registering OnJobOfferReceived inside StartAsync double-subscribes on the second navigation
        // and fires NavigateToJobOffer twice per offer. The fix registers exactly once per VM lifetime
        // (in the constructor), so a real registry must capture every distinct handler registered.
        var registeredHandlers = new List<Func<JobOfferPayload, Task>>();
        _repHub.Setup(h => h.OnJobOfferReceived(It.IsAny<Func<JobOfferPayload, Task>>()))
            .Callback<Func<JobOfferPayload, Task>>(registeredHandlers.Add);
        var vm = CreateViewModel(Vehicle());
        await vm.StartAsync();
        await vm.StartAsync();
        var offer = Offer();

        // Act — deliver the offer to every handler the hub has registered (what a real HubConnection does).
        foreach (var handler in registeredHandlers)
        {
            await handler.Invoke(offer);
        }

        // Assert
        _navigator.Verify(n => n.NavigateToJobOffer(offer), Times.Once);
    }

    [Fact]
    public async Task GivenRepHubStartThrows_WhenJobOfferReceivedEventFires_ThenTheHandlerStillNavigatesToTheOffer()
    {
        // Arrange — ViewModel-level concern: the JobOfferReceived handler is registered BEFORE the
        // connect attempt, so a failing first connect does not unsubscribe the screen. When the offer
        // later arrives (after the service's retry reconnects) the captured handler still navigates.
        // The retry mechanism itself is asserted in SignalRRepHubServiceTests, not here.
        Func<JobOfferPayload, Task>? capturedHandler = null;
        _repHub.Setup(h => h.OnJobOfferReceived(It.IsAny<Func<JobOfferPayload, Task>>()))
            .Callback<Func<JobOfferPayload, Task>>(h => capturedHandler = h);
        _repHub.Setup(h => h.StartAsync())
            .ThrowsAsync(new InvalidOperationException("hub unreachable"));
        var vm = CreateViewModel(Vehicle());
        await vm.StartAsync();
        var offer = Offer();

        // Act
        await capturedHandler!.Invoke(offer);

        // Assert
        _navigator.Verify(n => n.NavigateToJobOffer(offer), Times.Once);
    }

    [Fact]
    public async Task GivenRepIdleViewModelWithHeartbeatService_WhenStartAsyncCalled_ThenHeartbeatServiceStarted()
    {
        // Arrange — FE-023 AC-3 (start trigger). Entering the idle view (post take-over) is the single
        // "go on duty" moment: StartAsync must also start the heartbeat loop so the rep is marked
        // human-controlled. StartAsync is idempotent, so re-entering /rep/idle after a job is safe.
        var vm = CreateViewModel(Vehicle());

        // Act
        await vm.StartAsync();

        // Assert
        _heartbeatService.Verify(h => h.StartAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenRepIdleViewModelWithHeartbeatService_WhenStopAsyncCalled_ThenHeartbeatServiceIsNotStopped()
    {
        // Arrange — FE-023 AC-1 PERMANENT REGRESSION GUARD. RepIdle.razor calls StopAsync from
        // DisposeAsync on EVERY navigation away from /rep/idle (idle → offer → job). The heartbeat is an
        // on-duty-long concern spanning all rep pages; it must NOT be tied to the idle page's lifecycle.
        // If anyone adds _heartbeatService.StopAsync() to RepIdleViewModel.StopAsync(), the heartbeat
        // would die the instant a job offer arrives — exactly when the rep most needs to be marked
        // human-controlled. This test goes red the moment that line is added.
        var vm = CreateViewModel(Vehicle());

        // Act
        await vm.StopAsync();

        // Assert
        _heartbeatService.Verify(h => h.StopAsync(), Times.Never);
        _repHub.Verify(r => r.StopAsync(), Times.Once);
    }
}
