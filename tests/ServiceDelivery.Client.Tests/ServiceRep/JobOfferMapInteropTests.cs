using Bunit;
using Bunit.JSInterop;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.Services;
using ServiceDelivery.Client.Core.ViewModels;
using ServiceDelivery.Client.UI.Features.ServiceRep.Pages;

namespace ServiceDelivery.Client.Tests.ServiceRep;

/// <summary>
/// FE-027 map-interop tests: asserts the imperative GoogleMap (FE-024) API call the JobOffer page issues
/// once the embedded map signals it is ready (OnMapReady → PlaceRequesterPinAsync). The real google.maps.Map
/// cannot render under bUnit (ADR-0010), so — exactly as ActiveJobMapInteropTests does — the googleMap.js
/// module is mocked and these tests assert the exact addOrUpdateMarker call (id / coords / colour / testId)
/// that flows through the embedded GoogleMap for the requester pin (AC-2). Kept separate from
/// JobOfferComponentTests so that class stays focused on countdown / card / accept-decline behaviour
/// (Single Responsibility at the test level).
/// </summary>
public class JobOfferMapInteropTests : BunitContext
{
    private const string ModulePath =
        "./_content/ServiceDelivery.Client.UI/Features/Maps/googleMap.js";

    private readonly Mock<IPersonaNavigator> _navigator = new();
    private readonly Mock<IJobOfferService> _jobOfferService = new();
    private readonly Mock<IDeclineOfferService> _declineOfferService = new();
    private readonly Mock<IRepHubService> _repHubService = new();
    private readonly Mock<IMapsLoader> _mapsLoader = new();
    private readonly InMemoryJobOfferStore _store = new();
    private readonly BunitJSModuleInterop _module;

    private readonly Mock<ITokenStore> _tokenStore = new();
    private readonly Mock<ILogoutSideEffect> _sideEffect = new();
    private readonly Mock<IReleaseVehicleAction> _releaseAction = new();
    private readonly Mock<IShellPresentation> _presentation = new();

    public JobOfferMapInteropTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _module = JSInterop.SetupModule(ModulePath);
        _module.Mode = JSRuntimeMode.Loose;
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

    private IRenderedComponent<JobOffer> RenderPage(JobOfferPayload offer)
    {
        Services.AddMudServices();
        _mapsLoader.Setup(l => l.LoadAsync()).ReturnsAsync(new MapsAvailability(true, null));
        Services.AddSingleton(_mapsLoader.Object);
        Services.AddSingleton<IJobOfferStore>(_store);
        Services.AddSingleton(_navigator.Object);
        Services.AddSingleton(_jobOfferService.Object);
        Services.AddSingleton(_declineOfferService.Object);
        Services.AddSingleton(_repHubService.Object);

        _presentation.SetupGet(p => p.MenuStyle).Returns(ShellMenuStyle.Drawer);
        var shell = new ShellViewModel(
            _tokenStore.Object, _navigator.Object, _sideEffect.Object,
            _releaseAction.Object, _presentation.Object, new PersonaMenuFactory());
        shell.Load(new UserProfile(
            Guid.NewGuid(), "Rosa Alvarez", UserRole.ServiceRep, ServiceTier.None, Guid.NewGuid()));
        Services.AddSingleton(shell);

        _declineOfferService
            .Setup(s => s.DeclineAsync(It.IsAny<Guid>()))
            .ReturnsAsync(DeclineOfferResult.Success);

        _store.SetOffer(offer);

        return Render<JobOffer>();
    }

    [Fact]
    public void GivenAnOfferWithKnownLatLng_WhenMapReady_ThenAddOrUpdateMarkerCalledWithOfferCoords()
    {
        // Arrange
        // AC-2: the requester pin is placed at the offer's Lat/Lng from the payload once the map is ready.
        RenderPage(Offer(lat: 41.62, lng: -93.71));

        // Act
        // (initial render → OnMapReady → PlaceRequesterPinAsync places the pin)

        // Assert
        var invocation = LastMarkerCall("requester");
        Assert.Equal(41.62, invocation.Arguments[2]);
        Assert.Equal(-93.71, invocation.Arguments[3]);
    }

    [Fact]
    public void GivenAnOfferWithKnownLatLng_WhenMapReady_ThenRequesterPinTestIdIsSet()
    {
        // Arrange
        // AC-2: the requester pin carries the 'requester-pin' data-testid so Appium/Playwright can locate it.
        RenderPage(Offer(lat: 41.6, lng: -93.6));

        // Act
        // (initial render places the pin)

        // Assert
        var invocation = LastMarkerCall("requester");
        Assert.Equal("requester-pin", invocation.Arguments[5]);
    }

    [Fact]
    public void GivenAnOfferWithKnownLatLng_WhenMapReady_ThenPinColourIsRequesterDestinationColour()
    {
        // Arrange
        // AC-2: the requester pin uses the neutral-dark destination colour (#2B2F3A), matching ActiveJob.
        RenderPage(Offer(lat: 41.6, lng: -93.6));

        // Act
        // (initial render places the pin)

        // Assert
        var invocation = LastMarkerCall("requester");
        Assert.Equal("#2B2F3A", invocation.Arguments[4]);
    }

    // Returns the most recent addOrUpdateMarker module call for the given marker id (e.g. "requester").
    private JSRuntimeInvocation LastMarkerCall(string markerId) =>
        _module.Invocations.Last(i =>
            i.Identifier == "addOrUpdateMarker" && (string)i.Arguments[1]! == markerId);
}
