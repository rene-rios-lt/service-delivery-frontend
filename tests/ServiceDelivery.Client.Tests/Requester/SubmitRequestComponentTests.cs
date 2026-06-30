using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.ViewModels;
using ServiceDelivery.Client.UI.Features.Requester.Pages;

namespace ServiceDelivery.Client.Tests.Requester;

/// <summary>
/// bUnit component tests for <see cref="SubmitRequest"/> (FE-015). Built to the requester-submit mockup:
/// the embedded GoogleMap (AC-1), the "Use my current location" button (AC-1), the DTC dropdown showing
/// code · title (AC-2), the "Request Service" primary button gated on the ViewModel (AC-3), the inline
/// error band (AC-5), and the responsive container (AC-6). All collaborators are mocked Core abstractions;
/// the GoogleMap's IMapsLoader is set available so the real map container renders (mirrors the rep map pages).
/// </summary>
public class SubmitRequestComponentTests : BunitContext
{
    private readonly Mock<IDtcService> _dtcService = new();
    private readonly Mock<IServiceRequestService> _requestService = new();
    private readonly Mock<IGeolocationService> _geolocation = new();
    private readonly Mock<IPersonaNavigator> _navigator = new();
    private readonly Mock<IMapsLoader> _mapsLoader = new();

    // ShellViewModel collaborators — the page drives the shared app-bar title/subtitle (Request Service /
    // Report an equipment fault), so the test registers a real ShellViewModel with a loaded menu.
    private readonly Mock<ITokenStore> _tokenStore = new();
    private readonly Mock<ILogoutSideEffect> _sideEffect = new();
    private readonly Mock<IReleaseVehicleAction> _releaseAction = new();
    private readonly Mock<IShellPresentation> _presentation = new();
    private ShellViewModel _shell = default!;

    private static DtcItem Dtc(string code = "P0700", string title = "Transmission Control Fault") =>
        new(Guid.NewGuid(), code, title);

    private SubmitRequestViewModel RegisterPage(IReadOnlyList<DtcItem>? dtcs = null)
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        _mapsLoader.Setup(l => l.LoadAsync()).ReturnsAsync(new MapsAvailability(true, null));
        Services.AddSingleton(_mapsLoader.Object);

        _dtcService.Setup(s => s.GetDtcsAsync()).ReturnsAsync(dtcs ?? new List<DtcItem>());
        var viewModel = new SubmitRequestViewModel(
            _dtcService.Object, _requestService.Object, _geolocation.Object, _navigator.Object);
        Services.AddSingleton(viewModel);

        _presentation.SetupGet(p => p.MenuStyle).Returns(ShellMenuStyle.Drawer);
        _shell = new ShellViewModel(
            _tokenStore.Object, _navigator.Object, _sideEffect.Object,
            _releaseAction.Object, _presentation.Object, new PersonaMenuFactory());
        _shell.Load(new UserProfile(
            Guid.NewGuid(), "Marcus Webb", UserRole.Requester, ServiceTier.Gold, Guid.NewGuid()));
        Services.AddSingleton(_shell);

        return viewModel;
    }

    [Fact]
    public void GivenSubmitPage_WhenRendered_ThenGoogleMapComponentIsPresent()
    {
        // Arrange
        // AC-1: the submit form renders the real FE-024 GoogleMap component (its available-SDK container
        // carries data-testid='google-map').
        RegisterPage();

        // Act
        var cut = Render<SubmitRequest>();

        // Assert
        Assert.NotNull(cut.Find("[data-testid='google-map']"));
    }

    [Fact]
    public void GivenSubmitPage_WhenRendered_ThenUseMyLocationButtonIsPresent()
    {
        // Arrange
        // AC-1: "Use my current location" lets the requester use device GPS.
        RegisterPage();

        // Act
        var cut = Render<SubmitRequest>();

        // Assert
        var button = cut.Find("[data-testid='use-my-location-button']");
        Assert.Contains("Use my current location", button.TextContent);
    }

    [Fact]
    public async Task GivenMapRendered_WhenMapClickedCallbackFired_ThenPinLabelShowsCoordinates()
    {
        // Arrange
        // AC-1: a map tap sets the location; the page surfaces a "Pin set" label with the coordinates.
        RegisterPage();
        var cut = Render<SubmitRequest>();
        var map = cut.FindComponent<ServiceDelivery.Client.UI.Features.Maps.Components.GoogleMap>();

        // Act
        await cut.InvokeAsync(() => map.Instance.OnMapClickedAsync(41.587, -93.624));

        // Assert
        var label = cut.Find("[data-testid='pin-set-label']");
        Assert.Contains("Pin set", label.TextContent);
        Assert.Contains("41.587", label.TextContent);
        Assert.Contains("-93.624", label.TextContent);
    }

    [Fact]
    public void GivenDtcsLoaded_WhenRendered_ThenSelectOptionsShowCodeAndTitle()
    {
        // Arrange
        // AC-2: the DTC dropdown is populated from GET /dtcs and each option shows code · title.
        RegisterPage(new List<DtcItem> { Dtc("P0700", "Transmission Control Fault") });

        // Act
        var cut = Render<SubmitRequest>();

        // Assert
        var select = cut.Find("[data-testid='dtc-select']");
        Assert.Contains("P0700", select.TextContent);
        Assert.Contains("Transmission Control Fault", select.TextContent);
    }

    [Fact]
    public void GivenNeitherLocationNorDtcSet_WhenRendered_ThenRequestServiceButtonIsDisabled()
    {
        // Arrange
        // AC-3: with the form empty the primary action is disabled.
        RegisterPage();

        // Act
        var cut = Render<SubmitRequest>();

        // Assert
        var button = cut.Find("[data-testid='request-service-button']");
        Assert.True(button.HasAttribute("disabled"));
    }

    [Fact]
    public async Task GivenBothLocationAndDtcSet_WhenRendered_ThenRequestServiceButtonIsEnabled()
    {
        // Arrange
        // AC-3: once both a location (map tap) and a DTC (dropdown) are set the button is enabled.
        var dtc = Dtc("P0700", "Transmission Control Fault");
        RegisterPage(new List<DtcItem> { dtc });
        var cut = Render<SubmitRequest>();
        var map = cut.FindComponent<ServiceDelivery.Client.UI.Features.Maps.Components.GoogleMap>();

        // Act
        await cut.InvokeAsync(() => map.Instance.OnMapClickedAsync(41.6, -93.6));
        cut.Find("[data-testid='dtc-select']").Change(dtc.Id.ToString());

        // Assert
        var button = cut.Find("[data-testid='request-service-button']");
        Assert.False(button.HasAttribute("disabled"));
    }

    [Fact]
    public async Task GivenViewModelHasError_WhenRendered_ThenErrorBandIsVisible()
    {
        // Arrange
        // AC-5: an API error surfaces an inline error band and the form stays on screen.
        var dtc = Dtc();
        _requestService
            .Setup(s => s.SubmitAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<Guid>()))
            .ReturnsAsync(new SubmitServiceRequestResult.Error("Submit failed."));
        RegisterPage(new List<DtcItem> { dtc });
        var cut = Render<SubmitRequest>();
        var map = cut.FindComponent<ServiceDelivery.Client.UI.Features.Maps.Components.GoogleMap>();
        await cut.InvokeAsync(() => map.Instance.OnMapClickedAsync(41.6, -93.6));
        cut.Find("[data-testid='dtc-select']").Change(dtc.Id.ToString());

        // Act
        cut.Find("[data-testid='request-service-button']").Click();

        // Assert
        var band = cut.Find("[data-testid='submit-error']");
        Assert.Contains("sd-banner", band.ClassList);
    }

    [Fact]
    public void GivenNoError_WhenRendered_ThenErrorBandIsAbsent()
    {
        // Arrange
        // AC-5: with no error the band is hidden.
        RegisterPage();

        // Act
        var cut = Render<SubmitRequest>();

        // Assert
        Assert.Empty(cut.FindAll("[data-testid='submit-error']"));
    }

    [Fact]
    public void GivenSubmitPage_WhenInitialized_ThenShellTitleIsRequestService()
    {
        // Arrange
        // The submit screen sets the app-bar title/subtitle from the mockup.
        RegisterPage();

        // Act
        Render<SubmitRequest>();

        // Assert
        Assert.Equal("Request Service", _shell.Title);
        Assert.Equal("Report an equipment fault", _shell.Subtitle);
    }

    [Fact]
    public void GivenMobileLayout_WhenRendered_ThenSingleColumnClassApplied()
    {
        // Arrange
        // AC-6: the submit form is a single-column stack on every platform (the container itself carries
        // the single-column hook); web/desktop additionally constrain + centre it (asserted below).
        RegisterPage();

        // Act
        var cut = Render<SubmitRequest>();

        // Assert
        var root = cut.Find("[data-testid='submit-request']");
        Assert.Contains("sd-submit", root.ClassList);
    }

    [Fact]
    public void GivenWebLayout_WhenRendered_ThenCentredContainerClassApplied()
    {
        // Arrange
        // AC-6: on web/desktop the column is constrained narrow and centred (MudContainer MaxWidth Xs).
        RegisterPage();

        // Act
        var cut = Render<SubmitRequest>();

        // Assert
        var container = cut.Find("[data-testid='submit-container']");
        Assert.Contains("sd-submit__container", container.ClassList);
    }
}
