using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.ViewModels;

namespace ServiceDelivery.Client.Tests.Requester;

/// <summary>
/// Unit tests for the redirect-handling additions to <see cref="RequesterTrackingViewModel"/> (FE-018).
/// On a redirect the backend fires TWO events to the requester in order: (1) <c>RepAssigned</c> carrying the
/// new rep's full picture (position, ETA, name, vehicle registration), then (2) <c>RepRedirected</c> carrying
/// the banner concern only (old/new rep names, new ETA). The tracking VM subscribes to BOTH:
/// <see cref="RequesterTrackingViewModel.HandleRepAssignedAsync"/> is the authority for map position, ETA, rep
/// name, and vehicle registration; <see cref="RequesterTrackingViewModel.HandleRepRedirectedAsync"/> sets ONLY
/// the banner state (<c>IsRedirected</c>, <c>OldRepName</c>, <c>RedirectMessage</c>) and must never touch the
/// rep-state fields owned by <c>RepAssigned</c>. Depends only on Core abstractions.
/// </summary>
public class RequesterRedirectViewModelTests
{
    private readonly Mock<IRepAssignedStore> _store = new();
    private readonly Mock<IRequesterHubService> _hub = new();

    private static RepAssignedPayload AssignedPayload(
        string repName = "Alex Rivera",
        double etaMinutes = 14,
        double latitude = 41.820,
        double longitude = -93.410,
        string vehicleRegistration = "IA-3382") =>
        new(Guid.NewGuid(), repName, etaMinutes, latitude, longitude, vehicleRegistration);

    private static RepRedirectedPayload RedirectedPayload(
        string oldRepName = "Jordan Tran",
        string newRepName = "Alex Rivera",
        double newEtaMinutes = 14) =>
        new(oldRepName, newRepName, newEtaMinutes);

    private RequesterTrackingViewModel CreateViewModel(RepAssignedPayload? seed = null)
    {
        _store.SetupGet(s => s.CurrentPayload).Returns(
            seed ?? new RepAssignedPayload(Guid.NewGuid(), "Jordan Tran", 9, 41.601, -93.609, "IA-4471"));
        return new RequesterTrackingViewModel(_store.Object, _hub.Object);
    }

    [Fact]
    public void GivenRequesterTrackingViewModel_WhenConstructed_ThenOnRepAssignedIsRegisteredOnTheHub()
    {
        // Arrange — AC-4: the tracking VM (not just the pending VM) must subscribe to RepAssigned so the
        // redirect's second RepAssigned — which carries the new rep's position/ETA/name/vehicle — is handled
        // on the tracking screen. This asserts the subscription is registered during construction.
        var registered = false;
        _hub.Setup(h => h.OnRepAssigned(It.IsAny<Func<RepAssignedPayload, Task>>()))
            .Callback(() => registered = true);

        // Act
        CreateViewModel();

        // Assert
        Assert.True(registered);
    }

    [Fact]
    public void GivenRequesterTrackingViewModel_WhenConstructed_ThenOnRepRedirectedIsRegisteredOnTheHub()
    {
        // Arrange — AC-4: the tracking VM must also subscribe to RepRedirected so the banner appears when the
        // second of the two redirect events arrives. This asserts the subscription is registered during
        // construction.
        var registered = false;
        _hub.Setup(h => h.OnRepRedirected(It.IsAny<Func<RepRedirectedPayload, Task>>()))
            .Callback(() => registered = true);

        // Act
        CreateViewModel();

        // Assert
        Assert.True(registered);
    }

    [Fact]
    public async Task GivenRepAssignedPayload_WhenHandleRepAssignedAsyncCalled_ThenMapPositionAndEtaAreUpdated()
    {
        // Arrange — AC-2: the redirect's RepAssigned carries the new rep's coordinates and ETA; the handler
        // must move the map and refresh the ETA immediately. Seed differs from the payload on every field so
        // a mis-wired update cannot pass coincidentally.
        var viewModel = CreateViewModel();

        // Act
        await viewModel.HandleRepAssignedAsync(AssignedPayload(etaMinutes: 14, latitude: 41.820, longitude: -93.410));

        // Assert
        Assert.Equal(41.820, viewModel.RepLat);
        Assert.Equal(-93.410, viewModel.RepLng);
        Assert.Equal(14, viewModel.EtaMinutes);
    }

    [Fact]
    public async Task GivenRepAssignedPayload_WhenHandleRepAssignedAsyncCalled_ThenRepNameUpdatesToNewRepName()
    {
        // Arrange — AC-3: the new rep's name replaces the old one in the UI. The seed rep is "Jordan Tran";
        // the redirect's RepAssigned carries "Alex Rivera".
        var viewModel = CreateViewModel();

        // Act
        await viewModel.HandleRepAssignedAsync(AssignedPayload(repName: "Alex Rivera"));

        // Assert
        Assert.Equal("Alex Rivera", viewModel.RepName);
    }

    [Fact]
    public async Task GivenRepAssignedPayload_WhenHandleRepAssignedAsyncCalled_ThenVehicleRegistrationUpdatesToNewRepsVehicle()
    {
        // Arrange — AC-2 (BE-031 guard): the vehicle registration must switch to the NEW rep's vehicle, so
        // the bottom-sheet subtitle reads the new registration. This directly protects the BE-031 investment.
        // Seed registration is IA-4471; the redirect's RepAssigned carries IA-3382 (the mockup value).
        var viewModel = CreateViewModel();

        // Act
        await viewModel.HandleRepAssignedAsync(AssignedPayload(vehicleRegistration: "IA-3382"));

        // Assert
        Assert.Equal("IA-3382", viewModel.VehicleRegistration);
        Assert.Equal("Vehicle IA-3382 · Service Rep", viewModel.VehicleSubtitle);
    }

    [Fact]
    public async Task GivenRepAssignedPayload_WhenHandleRepAssignedAsyncCalled_ThenStateChangedIsRaised()
    {
        // Arrange — AC-4: the handler must raise StateChanged so the page re-renders and redraws the map
        // overlays for the new rep's position.
        var viewModel = CreateViewModel();
        var raised = false;
        viewModel.StateChanged += () => raised = true;

        // Act
        await viewModel.HandleRepAssignedAsync(AssignedPayload());

        // Assert
        Assert.True(raised);
    }

    [Fact]
    public async Task GivenRepRedirectedPayload_WhenHandleRepRedirectedAsyncCalled_ThenIsRedirectedIsTrue()
    {
        // Arrange — AC-1: the redirect banner is shown only when IsRedirected is true. It starts false and is
        // flipped by the RepRedirected handler (a one-way transition).
        var viewModel = CreateViewModel();

        // Act
        await viewModel.HandleRepRedirectedAsync(RedirectedPayload());

        // Assert
        Assert.True(viewModel.IsRedirected);
    }

    [Fact]
    public async Task GivenRepRedirectedPayload_WhenHandleRepRedirectedAsyncCalled_ThenRedirectMessageContainsOldAndNewRepNames()
    {
        // Arrange — AC-1: the banner reads exactly "Our apologies, we needed to redirect {old rep name}.
        // {new rep name} is heading your way now." Distinct names per slot so a swapped-slot bug is caught.
        var viewModel = CreateViewModel();

        // Act
        await viewModel.HandleRepRedirectedAsync(
            RedirectedPayload(oldRepName: "Jordan Tran", newRepName: "Alex Rivera"));

        // Assert
        Assert.Equal(
            "Our apologies, we needed to redirect Jordan Tran. Alex Rivera is heading your way now.",
            viewModel.RedirectMessage);
    }

    [Fact]
    public async Task GivenRepRedirectedPayload_WhenHandleRepRedirectedAsyncCalled_ThenOldAndNewRepNamesAreExposed()
    {
        // Arrange — AC-1: the banner renders the two names in bold (both mockups), so the VM exposes each name
        // as its own property for the markup to wrap in <strong>. Distinct values so a swapped assignment is
        // caught.
        var viewModel = CreateViewModel();

        // Act
        await viewModel.HandleRepRedirectedAsync(
            RedirectedPayload(oldRepName: "Jordan Tran", newRepName: "Alex Rivera"));

        // Assert
        Assert.Equal("Jordan Tran", viewModel.OldRepName);
        Assert.Equal("Alex Rivera", viewModel.NewRepName);
    }

    [Fact]
    public async Task GivenRepRedirectedPayload_WhenHandleRepRedirectedAsyncCalled_ThenRepStateFieldsAreUnchanged()
    {
        // Arrange — AC-1 (critical guard, revised two-event choreography): RepRedirected is a banner-ONLY
        // concern. The rep's position, ETA, name, and vehicle registration are owned exclusively by the
        // concurrent RepAssigned. If HandleRepRedirectedAsync ever touched them it would clobber the correct
        // new-rep state (including the BE-031 vehicle registration). Seed the VM, capture the rep-state
        // fields, fire ONLY the redirect, and assert none moved. NewRepName in the payload differs from the
        // seed RepName so a leak into RepName is caught.
        var viewModel = CreateViewModel(
            new RepAssignedPayload(Guid.NewGuid(), "Jordan Tran", 9, 41.601, -93.609, "IA-4471"));
        var repNameBefore = viewModel.RepName;
        var etaBefore = viewModel.EtaMinutes;
        var repLatBefore = viewModel.RepLat;
        var repLngBefore = viewModel.RepLng;
        var registrationBefore = viewModel.VehicleRegistration;

        // Act
        await viewModel.HandleRepRedirectedAsync(
            RedirectedPayload(newRepName: "Alex Rivera", newEtaMinutes: 14));

        // Assert
        Assert.Equal(repNameBefore, viewModel.RepName);
        Assert.Equal(etaBefore, viewModel.EtaMinutes);
        Assert.Equal(repLatBefore, viewModel.RepLat);
        Assert.Equal(repLngBefore, viewModel.RepLng);
        Assert.Equal(registrationBefore, viewModel.VehicleRegistration);
    }

    [Fact]
    public async Task GivenRepRedirectedPayload_WhenHandleRepRedirectedAsyncCalled_ThenStateChangedIsRaised()
    {
        // Arrange — AC-4: the handler must raise StateChanged so the component re-renders the banner, the
        // app-bar title, and the "NEW" chip.
        var viewModel = CreateViewModel();
        var raised = false;
        viewModel.StateChanged += () => raised = true;

        // Act
        await viewModel.HandleRepRedirectedAsync(RedirectedPayload());

        // Assert
        Assert.True(raised);
    }

    [Fact]
    public async Task GivenRepAssignedThenRepRedirectedInProductionOrder_WhenBothHandlersFire_ThenFinalStateIsNewRepWithBannerVisible()
    {
        // Arrange — AC-2/AC-3 (the revised two-event choreography end-to-end): on a redirect the backend fires
        // RepAssigned FIRST (new rep's full picture) then RepRedirected SECOND (banner only). After both, the
        // rep-state fields must all reflect the NEW rep (from RepAssigned) AND the banner must be visible with
        // the correct old→new message (from RepRedirected). Seeded with the OLD rep so every new-rep field is
        // a genuine change.
        var viewModel = CreateViewModel(
            new RepAssignedPayload(Guid.NewGuid(), "Jordan Tran", 9, 41.601, -93.609, "IA-4471"));

        // Act — production event order.
        await viewModel.HandleRepAssignedAsync(
            AssignedPayload(repName: "Alex Rivera", etaMinutes: 14, latitude: 41.820, longitude: -93.410, vehicleRegistration: "IA-3382"));
        await viewModel.HandleRepRedirectedAsync(
            RedirectedPayload(oldRepName: "Jordan Tran", newRepName: "Alex Rivera", newEtaMinutes: 14));

        // Assert — rep state is the NEW rep (RepAssigned wins), banner reflects the redirect (RepRedirected).
        Assert.Equal("Alex Rivera", viewModel.RepName);
        Assert.Equal(14, viewModel.EtaMinutes);
        Assert.Equal(41.820, viewModel.RepLat);
        Assert.Equal(-93.410, viewModel.RepLng);
        Assert.Equal("IA-3382", viewModel.VehicleRegistration);
        Assert.True(viewModel.IsRedirected);
        Assert.Equal(
            "Our apologies, we needed to redirect Jordan Tran. Alex Rivera is heading your way now.",
            viewModel.RedirectMessage);
    }
}
