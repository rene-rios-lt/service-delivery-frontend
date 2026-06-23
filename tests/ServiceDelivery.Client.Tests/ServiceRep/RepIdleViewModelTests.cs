using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.ViewModels;

namespace ServiceDelivery.Client.Tests.ServiceRep;

public class RepIdleViewModelTests
{
    private readonly Mock<IRepHubService> _repHub = new();
    private readonly Mock<IPersonaNavigator> _navigator = new();

    private static ClaimedVehicle Vehicle(
        string registration = "IA-4471",
        string model = "Transit 350",
        params string[] equipment) =>
        new(Guid.NewGuid(), registration, model,
            equipment.Length == 0 ? new[] { "Hydraulics", "Coolant" } : equipment);

    private static JobOfferPayload Offer() =>
        new(Guid.NewGuid(), "Maria Lopez", ServiceTier.Gold, "Hydraulic leak", 4.2, 9, 41.6, -93.6);

    private RepIdleViewModel CreateViewModel(ClaimedVehicle? vehicle = null) =>
        new(vehicle ?? Vehicle(), _repHub.Object, _navigator.Object);

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
}
