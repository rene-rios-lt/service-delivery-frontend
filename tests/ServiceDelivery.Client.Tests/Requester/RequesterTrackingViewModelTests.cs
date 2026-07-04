using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.ViewModels;

namespace ServiceDelivery.Client.Tests.Requester;

/// <summary>
/// Unit tests for <see cref="RequesterTrackingViewModel"/> (FE-017). The tracking ViewModel is seeded from
/// the <see cref="IRepAssignedStore"/> payload the pending view deposited (rep name, vehicle registration,
/// ETA, initial rep + requester coordinates), registers the RequesterHub <c>RepPositionUpdated</c> handler,
/// and on each position push moves the rep marker coordinates, refreshes the ETA, maps the rep state to a
/// status message ("On the way" / "Almost there" / "Arrived"), hides the ETA once on-site, and raises
/// <see cref="RequesterTrackingViewModel.StateChanged"/> so the component re-renders. Depends only on Core
/// abstractions.
/// </summary>
public class RequesterTrackingViewModelTests
{
    private readonly Mock<IRepAssignedStore> _store = new();
    private readonly Mock<IRequesterHubService> _hub = new();

    private static RepAssignedPayload Payload(
        string repName = "Jordan Tran",
        double etaMinutes = 9,
        double latitude = 41.601,
        double longitude = -93.609,
        string vehicleRegistration = "IA-4471") =>
        new(Guid.NewGuid(), repName, etaMinutes, latitude, longitude, vehicleRegistration);

    private RequesterTrackingViewModel CreateViewModel(RepAssignedPayload? payload = null)
    {
        _store.SetupGet(s => s.CurrentPayload).Returns(payload ?? Payload());
        return new RequesterTrackingViewModel(_store.Object, _hub.Object);
    }

    // Captures the handler the ViewModel registers with the hub so a test can invoke it as a server push.
    // Stores the constructed ViewModel in _viewModelUnderTest so the handler-driven tests can assert state.
    private Func<RepPositionUpdatedPayload, Task> CaptureHandler()
    {
        Func<RepPositionUpdatedPayload, Task>? captured = null;
        _hub.Setup(h => h.OnRepPositionUpdated(It.IsAny<Func<RepPositionUpdatedPayload, Task>>()))
            .Callback<Func<RepPositionUpdatedPayload, Task>>(h => captured = h);
        _viewModelUnderTest = CreateViewModel();
        return captured!;
    }

    [Fact]
    public async Task GivenTrackingViewModel_WhenStartAsyncCalled_ThenTheHubConnectionIsStarted()
    {
        // Arrange — the pending page stops the shared RequesterHub connection when it navigates away
        // (RequesterPending.DisposeAsync → StopAsync). The tracking page must therefore (re)establish the
        // connection on entry so it is live and re-joined to the requester group; otherwise the redirect's
        // RepAssigned + RepRedirected events (fired seconds later) never reach the tracking page. This test
        // drives that: the tracking ViewModel must delegate a start to the hub.
        var viewModel = CreateViewModel();

        // Act
        await viewModel.StartAsync();

        // Assert — the hub connection was started (re-established after the pending page's StopAsync).
        _hub.Verify(h => h.StartAsync(), Times.Once);
    }

    [Fact]
    public void GivenRepAssignedPayload_WhenTrackingViewModelConstructed_ThenRepNameMatchesPayload()
    {
        // Arrange — AC-2: the rep name shown above the map comes from the RepAssigned payload.
        var payload = Payload(repName: "Jordan Tran");

        // Act
        var viewModel = CreateViewModel(payload);

        // Assert
        Assert.Equal("Jordan Tran", viewModel.RepName);
    }

    [Fact]
    public void GivenRepAssignedPayloadWithRegistration_WhenTrackingViewModelConstructed_ThenVehicleSubtitleShowsRegistration()
    {
        // Arrange — AC-2: with a vehicle registration present the subtitle reads
        // "Vehicle {registration} · Service Rep". IA-4471 is a distinct value not shared with any other
        // field so the assertion cannot pass by coincidence.
        var payload = Payload(vehicleRegistration: "IA-4471");

        // Act
        var viewModel = CreateViewModel(payload);

        // Assert
        Assert.Equal("Vehicle IA-4471 · Service Rep", viewModel.VehicleSubtitle);
    }

    [Fact]
    public void GivenRepAssignedPayloadWithEmptyRegistration_WhenTrackingViewModelConstructed_ThenVehicleSubtitleFallsBackToServiceRep()
    {
        // Arrange — AC-2 pre-BE-031 fallback: when the registration is absent (empty) the subtitle degrades
        // cleanly to "Service Rep" — no dangling "Vehicle ·". FE-017 must be merge-safe before BE-031 ships.
        var payload = Payload(vehicleRegistration: string.Empty);

        // Act
        var viewModel = CreateViewModel(payload);

        // Assert
        Assert.Equal("Service Rep", viewModel.VehicleSubtitle);
    }

    [Fact]
    public void GivenRepAssignedPayload_WhenTrackingViewModelConstructed_ThenInitialCoordinatesAndEtaSeedFromPayload()
    {
        // Arrange — AC-1/AC-3: the initial rep marker position and ETA are seeded from the RepAssigned
        // payload so the map and ETA chip render before the first RepPositionUpdated push arrives. Distinct
        // values per field so a mis-wired seeding cannot pass coincidentally.
        var payload = Payload(etaMinutes: 9, latitude: 41.701, longitude: -93.501);

        // Act
        var viewModel = CreateViewModel(payload);

        // Assert
        Assert.Equal(41.701, viewModel.RepLat);
        Assert.Equal(-93.501, viewModel.RepLng);
        Assert.Equal(9, viewModel.EtaMinutes);
    }

    [Fact]
    public async Task GivenTrackingViewModel_WhenRepPositionUpdated_ThenRepLatLngUpdateFromPayload()
    {
        // Arrange — AC-1: the rep's moving marker follows each RepPositionUpdated push. New coordinates
        // (distinct from the seed) must replace the ViewModel's rep position.
        var handler = CaptureHandler();

        // Act
        await handler(new RepPositionUpdatedPayload(41.820, -93.410, 4, "EnRoute"));

        // Assert
        Assert.Equal(41.820, _viewModelUnderTest.RepLat);
        Assert.Equal(-93.410, _viewModelUnderTest.RepLng);
    }

    [Fact]
    public async Task GivenTrackingViewModel_WhenHandlePositionUpdatedAsyncCalled_ThenEtaMinutesUpdatesAndStateChangedRaised()
    {
        // Arrange — AC-3: a RepPositionUpdated push refreshes the ETA and raises StateChanged so the chip
        // re-renders without a screen reload. The seed ETA is 9; the push carries 4.
        var handler = CaptureHandler();
        var stateChangedRaised = false;
        _viewModelUnderTest.StateChanged += () => stateChangedRaised = true;

        // Act
        await handler(new RepPositionUpdatedPayload(41.601, -93.609, 4, "EnRoute"));

        // Assert
        Assert.Equal(4, _viewModelUnderTest.EtaMinutes);
        Assert.True(stateChangedRaised);
    }

    [Theory]
    [InlineData("EnRoute", "On the way")]
    [InlineData("Within15Miles", "Almost there")]
    [InlineData("OnSite", "Your technician has arrived")]
    public async Task GivenRepState_WhenPositionUpdated_ThenStatusMessageMatchesExpectedText(
        string state, string expectedMessage)
    {
        // Arrange — AC-4/AC-5: the status message reflects the rep state. OnSite reads "Your technician has
        // arrived" (AC-5), the other two are "On the way" / "Almost there" (AC-4).
        var handler = CaptureHandler();

        // Act
        await handler(new RepPositionUpdatedPayload(41.601, -93.609, 4, state));

        // Assert
        Assert.Equal(expectedMessage, _viewModelUnderTest.StatusMessage);
    }

    [Fact]
    public async Task GivenRepStateOnSite_WhenPositionUpdated_ThenIsEtaVisibleIsFalseAndStatusMessageIsArrived()
    {
        // Arrange — AC-5: once the rep is OnSite the ETA is hidden and the message becomes the arrival text.
        var handler = CaptureHandler();

        // Act
        await handler(new RepPositionUpdatedPayload(41.601, -93.609, 0, "OnSite"));

        // Assert
        Assert.False(_viewModelUnderTest.IsEtaVisible);
        Assert.Equal("Your technician has arrived", _viewModelUnderTest.StatusMessage);
    }

    [Fact]
    public async Task GivenRepStateEnRoute_WhenPositionUpdated_ThenIsEtaVisibleIsTrue()
    {
        // Arrange — AC-3: while the rep is still travelling the ETA chip is shown.
        var handler = CaptureHandler();

        // Act
        await handler(new RepPositionUpdatedPayload(41.601, -93.609, 4, "EnRoute"));

        // Assert
        Assert.True(_viewModelUnderTest.IsEtaVisible);
    }

    // Holds the ViewModel captured by CaptureHandler so the handler-driven tests can assert its state.
    private RequesterTrackingViewModel _viewModelUnderTest = null!;
}
