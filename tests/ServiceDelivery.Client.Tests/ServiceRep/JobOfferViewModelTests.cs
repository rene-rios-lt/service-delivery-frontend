using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.ViewModels;

namespace ServiceDelivery.Client.Tests.ServiceRep;

public class JobOfferViewModelTests
{
    private readonly Mock<IPersonaNavigator> _navigator = new();
    private readonly Mock<IJobOfferService> _jobOfferService = new();
    private readonly Mock<IDeclineOfferService> _declineOfferService = new();

    public JobOfferViewModelTests()
    {
        // Default accept outcome for tests that do not exercise the accept path — keeps them free of
        // accept side effects (FE-009). Accept-path tests override this per scenario.
        _jobOfferService
            .Setup(s => s.AcceptAsync(It.IsAny<Guid>()))
            .ReturnsAsync(AcceptOfferResult.Success);

        // Default decline outcome for tests that do not exercise the decline path (FE-010).
        // Decline-path tests override this per scenario.
        _declineOfferService
            .Setup(s => s.DeclineAsync(It.IsAny<Guid>()))
            .ReturnsAsync(DeclineOfferResult.Success);
    }

    private static JobOfferPayload Offer(
        string requesterName = "Marcus",
        ServiceTier tier = ServiceTier.Gold,
        string dtcTitle = "P0700 · Transmission Control Fault",
        double distanceMiles = 12.4,
        int etaMinutes = 13,
        double lat = 41.6,
        double lng = -93.6) =>
        new(Guid.NewGuid(), requesterName, tier, dtcTitle, distanceMiles, etaMinutes, lat, lng);

    private JobOfferViewModel CreateViewModel(JobOfferPayload? offer = null) =>
        new(offer ?? Offer(), _navigator.Object, _jobOfferService.Object, _declineOfferService.Object);

    [Fact]
    public void GivenANewJobOffer_WhenViewModelCreated_ThenCountdownStartsAt60()
    {
        // Arrange
        var vm = CreateViewModel(Offer());

        // Act
        var seconds = vm.SecondsRemaining;

        // Assert
        Assert.Equal(60, seconds);
    }

    [Fact]
    public void GivenAJobOfferPayload_WhenAllFieldsMapped_ThenAllFieldsArePresentOnViewModel()
    {
        // Arrange
        var offer = Offer(
            requesterName: "Marcus",
            tier: ServiceTier.Gold,
            dtcTitle: "P0700 · Transmission Control Fault",
            distanceMiles: 12.4,
            etaMinutes: 13,
            lat: 41.6,
            lng: -93.6);
        var vm = CreateViewModel(offer);

        // Act & Assert
        Assert.Equal("Marcus", vm.RequesterName);
        Assert.Equal(ServiceTier.Gold, vm.Tier);
        Assert.Equal("P0700 · Transmission Control Fault", vm.DtcTitle);
        Assert.Equal(12.4, vm.DistanceMiles);
        Assert.Equal(13, vm.EtaMinutes);
        Assert.Equal(41.6, vm.Lat);
        Assert.Equal(-93.6, vm.Lng);
    }

    [Fact]
    public async Task GivenAJobOfferViewModel_WhenOneSecondElapses_ThenCountdownDecrementsByOne()
    {
        // Arrange
        var vm = CreateViewModel(Offer());

        // Act
        await vm.TickAsync();

        // Assert
        Assert.Equal(59, vm.SecondsRemaining);
    }

    [Fact]
    public async Task GivenCountdownAbove10_WhenIsUrgentEvaluated_ThenIsUrgentIsFalse()
    {
        // Arrange
        // 60 - 49 ticks = 11 seconds remaining, which is above the 10-second urgent threshold.
        var vm = CreateViewModel(Offer());
        await TickTimes(vm, 49);

        // Act
        var isUrgent = vm.IsUrgent;

        // Assert
        Assert.Equal(11, vm.SecondsRemaining);
        Assert.False(isUrgent);
    }

    [Fact]
    public async Task GivenCountdownAt10_WhenIsUrgentEvaluated_ThenIsUrgentIsTrue()
    {
        // Arrange
        // 60 - 50 ticks = 10 seconds remaining, the first second of the urgent window (≤ 10).
        var vm = CreateViewModel(Offer());
        await TickTimes(vm, 50);

        // Act
        var isUrgent = vm.IsUrgent;

        // Assert
        Assert.Equal(10, vm.SecondsRemaining);
        Assert.True(isUrgent);
    }

    [Fact]
    public async Task GivenAJobOfferViewModel_WhenCountdownReachesZero_ThenNavigationToIdleIsInvoked()
    {
        // Arrange
        // 60 ticks brings the countdown to zero — the offer has expired server-side, so the screen
        // dismisses by navigating back to the idle / waiting-for-offers view (AC-5).
        var vm = CreateViewModel(Offer());

        // Act
        await TickTimes(vm, 60);

        // Assert
        Assert.Equal(0, vm.SecondsRemaining);
        _navigator.Verify(n => n.NavigateToRepIdleView(), Times.Once);
    }

    [Fact]
    public async Task GivenAJobOfferAlreadyAtZero_WhenTickedAgain_ThenCountdownStaysAtZeroAndNavigationNotRepeated()
    {
        // Arrange
        // Once expired and navigated away, a stray timer tick must not drive the countdown negative
        // nor fire a second navigation. The page disposes the timer, but the ViewModel guards anyway.
        var vm = CreateViewModel(Offer());
        await TickTimes(vm, 60);

        // Act
        await vm.TickAsync();

        // Assert
        Assert.Equal(0, vm.SecondsRemaining);
        _navigator.Verify(n => n.NavigateToRepIdleView(), Times.Once);
    }

    [Fact]
    public async Task GivenAnOffer_WhenDeclineAsyncCalled_ThenDeclineServiceIsInvokedWithOfferId()
    {
        // Arrange
        // AC-1: tapping Decline calls POST /job-offers/{id}/decline — the ViewModel delegates to the
        // decline service with this offer's id. The service owns the HTTP route.
        var offerId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var offer = Offer() with { OfferId = offerId };
        var vm = CreateViewModel(offer);

        // Act
        await vm.DeclineAsync();

        // Assert
        _declineOfferService.Verify(s => s.DeclineAsync(offerId), Times.Once);
    }

    [Fact]
    public async Task GivenDeclineReturnsSuccess_WhenDeclineAsyncCalled_ThenNavigateToRepIdleViewIsInvoked()
    {
        // Arrange
        // AC-2: a successful decline dismisses the offer screen and returns the rep to the idle /
        // waiting-for-offers view.
        _declineOfferService
            .Setup(s => s.DeclineAsync(It.IsAny<Guid>()))
            .ReturnsAsync(DeclineOfferResult.Success);
        var vm = CreateViewModel(Offer());

        // Act
        await vm.DeclineAsync();

        // Assert
        _navigator.Verify(n => n.NavigateToRepIdleView(), Times.Once);
    }

    [Fact]
    public async Task GivenDeclineReturnsConflict_WhenDeclineAsyncCalled_ThenNavigateToRepIdleViewIsInvoked()
    {
        // Arrange
        // AC-3: a 409 means the offer expired between the tap and the API call. Declining a
        // gone-already offer is the same outcome as a clean decline — the rep returns to the idle view.
        _declineOfferService
            .Setup(s => s.DeclineAsync(It.IsAny<Guid>()))
            .ReturnsAsync(DeclineOfferResult.Conflict);
        var vm = CreateViewModel(Offer());

        // Act
        await vm.DeclineAsync();

        // Assert
        _navigator.Verify(n => n.NavigateToRepIdleView(), Times.Once);
    }

    [Fact]
    public async Task GivenAJobOffer_WhenAcceptAsyncCalled_ThenJobOfferServiceAcceptIsInvokedWithOfferId()
    {
        // Arrange
        // AC-1: tapping Accept calls POST /job-offers/{id}/accept — the ViewModel delegates to the
        // service with this offer's id. The service owns the HTTP route.
        var offerId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var offer = Offer() with { OfferId = offerId };
        var vm = CreateViewModel(offer);

        // Act
        await vm.AcceptAsync();

        // Assert
        _jobOfferService.Verify(s => s.AcceptAsync(offerId), Times.Once);
    }

    [Fact]
    public async Task GivenAcceptReturnsSuccess_WhenAcceptAsyncCalled_ThenNavigateToActiveJobIsInvoked()
    {
        // Arrange
        // AC-2: on a successful accept the rep transitions to the active-job view (FE-011).
        _jobOfferService
            .Setup(s => s.AcceptAsync(It.IsAny<Guid>()))
            .ReturnsAsync(AcceptOfferResult.Success);
        var vm = CreateViewModel(Offer());

        // Act
        await vm.AcceptAsync();

        // Assert
        _navigator.Verify(n => n.NavigateToActiveJob(), Times.Once);
    }

    [Fact]
    public async Task GivenAcceptReturnsConflict_WhenAcceptAsyncCalled_ThenErrorMessageIsOfferExpired()
    {
        // Arrange
        // AC-3: a 409 means the offer expired between the tap and the API call. The ViewModel surfaces
        // an "Offer expired" message for the page to display before dismissing.
        _jobOfferService
            .Setup(s => s.AcceptAsync(It.IsAny<Guid>()))
            .ReturnsAsync(AcceptOfferResult.Conflict);
        var vm = CreateViewModel(Offer());

        // Act
        await vm.AcceptAsync();

        // Assert
        Assert.Equal("Offer expired", vm.ErrorMessage);
    }

    [Fact]
    public async Task GivenAcceptReturnsConflict_WhenAcceptAsyncCalled_ThenNavigateToRepIdleViewIsInvoked()
    {
        // Arrange
        // AC-3: after a 409 the offer is gone, so the rep returns to the idle / waiting-for-offers view.
        _jobOfferService
            .Setup(s => s.AcceptAsync(It.IsAny<Guid>()))
            .ReturnsAsync(AcceptOfferResult.Conflict);
        var vm = CreateViewModel(Offer());

        // Act
        await vm.AcceptAsync();

        // Assert
        _navigator.Verify(n => n.NavigateToRepIdleView(), Times.Once);
    }

    [Fact]
    public async Task GivenASubscriberToStateChanged_WhenTickAsyncCalled_ThenStateChangedIsRaised()
    {
        // Arrange
        // The Razor page subscribes to StateChanged and calls StateHasChanged on each tick so the
        // visible countdown updates every second (AC-3) without the page owning the timer logic.
        var vm = CreateViewModel(Offer());
        var raised = 0;
        vm.StateChanged += () => raised++;

        // Act
        await vm.TickAsync();

        // Assert
        Assert.Equal(1, raised);
    }

    private static async Task TickTimes(JobOfferViewModel vm, int count)
    {
        for (var i = 0; i < count; i++)
        {
            await vm.TickAsync();
        }
    }
}
