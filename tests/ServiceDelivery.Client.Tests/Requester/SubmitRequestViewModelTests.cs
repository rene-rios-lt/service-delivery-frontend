using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.ViewModels;

namespace ServiceDelivery.Client.Tests.Requester;

/// <summary>
/// Unit tests for <see cref="SubmitRequestViewModel"/> (FE-015). Pure-C# orchestration: DTC loading
/// (AC-2), location setting via map tap / device GPS (AC-1), submit-enable gating (AC-3), submit happy
/// path + navigation (AC-4), and the inline-error path (AC-5). All collaborators are mocked Core
/// abstractions — the ViewModel never reaches HTTP, JS, or the renderer.
/// </summary>
public class SubmitRequestViewModelTests
{
    private readonly Mock<IDtcService> _dtcService = new();
    private readonly Mock<IServiceRequestService> _requestService = new();
    private readonly Mock<IGeolocationService> _geolocation = new();
    private readonly Mock<IPersonaNavigator> _navigator = new();

    private SubmitRequestViewModel CreateViewModel() =>
        new(_dtcService.Object, _requestService.Object, _geolocation.Object, _navigator.Object);

    private static DtcItem Dtc(string code = "P0700", string title = "Transmission Control Fault") =>
        new(Guid.NewGuid(), code, title);

    // AC-2a
    [Fact]
    public async Task GivenDtcsReturned_WhenLoadDtcsCalled_ThenDtcListIsPopulated()
    {
        // Arrange
        var dtcs = new List<DtcItem> { Dtc("P0700", "Transmission Control Fault"), Dtc("P0420", "Catalyst Low") };
        _dtcService.Setup(s => s.GetDtcsAsync()).ReturnsAsync(dtcs);
        var vm = CreateViewModel();

        // Act
        await vm.LoadDtcsAsync();

        // Assert
        Assert.Equal(2, vm.Dtcs.Count);
        Assert.Equal("P0700", vm.Dtcs[0].Code);
    }

    // AC-1b
    [Fact]
    public void GivenMapRendered_WhenMapClicked_ThenViewModelSelectedLocationIsSet()
    {
        // Arrange
        var vm = CreateViewModel();
        var point = new GpsPoint(41.587, -93.624);

        // Act
        vm.SetLocation(point);

        // Assert
        Assert.Equal(point, vm.SelectedLocation);
    }

    // AC-1c
    [Fact]
    public async Task GivenGeolocationReturnsPosition_WhenUseMyLocationClicked_ThenViewModelSelectedLocationIsSet()
    {
        // Arrange
        var point = new GpsPoint(41.601, -93.609);
        _geolocation.Setup(g => g.GetCurrentLocationAsync()).ReturnsAsync(point);
        var vm = CreateViewModel();

        // Act
        await vm.UseMyLocationAsync();

        // Assert
        Assert.Equal(point, vm.SelectedLocation);
    }

    // AC-1c (GPS unavailable)
    [Fact]
    public async Task GivenGeolocationReturnsNull_WhenUseMyLocationClicked_ThenSelectedLocationRemainsNull()
    {
        // Arrange
        _geolocation.Setup(g => g.GetCurrentLocationAsync()).ReturnsAsync((GpsPoint?)null);
        var vm = CreateViewModel();

        // Act
        await vm.UseMyLocationAsync();

        // Assert
        Assert.Null(vm.SelectedLocation);
    }

    // AC-3a
    [Fact]
    public void GivenNoLocationAndNoDtc_WhenViewModelCreated_ThenIsSubmitEnabledIsFalse()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        // (no interaction)

        // Assert
        Assert.False(vm.IsSubmitEnabled);
    }

    // AC-3b
    [Fact]
    public void GivenLocationSetButNoDtc_WhenIsSubmitEnabledRead_ThenIsFalse()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        vm.SetLocation(new GpsPoint(41.6, -93.6));

        // Assert
        Assert.False(vm.IsSubmitEnabled);
    }

    // AC-3c
    [Fact]
    public void GivenDtcSelectedButNoLocation_WhenIsSubmitEnabledRead_ThenIsFalse()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        vm.SelectDtc(Guid.NewGuid());

        // Assert
        Assert.False(vm.IsSubmitEnabled);
    }

    // AC-3d
    [Fact]
    public void GivenBothLocationAndDtcSet_WhenIsSubmitEnabledRead_ThenIsTrue()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        vm.SetLocation(new GpsPoint(41.6, -93.6));
        vm.SelectDtc(Guid.NewGuid());

        // Assert
        Assert.True(vm.IsSubmitEnabled);
    }

    // AC-4a
    [Fact]
    public async Task GivenBothFieldsSet_WhenSubmitCalled_ThenServiceRequestServiceSubmitAsyncIsCalled()
    {
        // Arrange
        var dtcId = Guid.Parse("11112222-3333-4444-5555-666677778888");
        var point = new GpsPoint(41.587, -93.624);
        _requestService.Setup(s => s.SubmitAsync(point.Lat, point.Lng, dtcId))
            .ReturnsAsync(new SubmitServiceRequestResult.Success(Guid.NewGuid()));
        var vm = CreateViewModel();
        vm.SetLocation(point);
        vm.SelectDtc(dtcId);

        // Act
        await vm.SubmitAsync();

        // Assert
        _requestService.Verify(s => s.SubmitAsync(point.Lat, point.Lng, dtcId), Times.Once);
    }

    // AC-4b
    [Fact]
    public async Task GivenSubmitReturnsSuccess_WhenSubmitCalled_ThenNavigateToRequesterPendingIsCalled()
    {
        // Arrange
        _requestService.Setup(s => s.SubmitAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<Guid>()))
            .ReturnsAsync(new SubmitServiceRequestResult.Success(Guid.NewGuid()));
        var vm = CreateViewModel();
        vm.SetLocation(new GpsPoint(41.6, -93.6));
        vm.SelectDtc(Guid.NewGuid());

        // Act
        await vm.SubmitAsync();

        // Assert
        _navigator.Verify(n => n.NavigateToRequesterPending(), Times.Once);
    }

    // AC-3 guard: submit is a no-op when the form is incomplete (button is disabled, but guard the path too).
    [Fact]
    public async Task GivenFormIncomplete_WhenSubmitCalled_ThenServiceRequestServiceIsNotCalled()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.SetLocation(new GpsPoint(41.6, -93.6));

        // Act
        await vm.SubmitAsync();

        // Assert
        _requestService.Verify(
            s => s.SubmitAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<Guid>()), Times.Never);
    }

    // AC-5a
    [Fact]
    public async Task GivenSubmitReturnsError_WhenSubmitCalled_ThenErrorMessageIsNonEmpty()
    {
        // Arrange
        _requestService.Setup(s => s.SubmitAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<Guid>()))
            .ReturnsAsync(new SubmitServiceRequestResult.Error("Submit failed."));
        var vm = CreateViewModel();
        vm.SetLocation(new GpsPoint(41.6, -93.6));
        vm.SelectDtc(Guid.NewGuid());

        // Act
        await vm.SubmitAsync();

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(vm.ErrorMessage));
    }

    // AC-5b
    [Fact]
    public async Task GivenSubmitReturnsError_WhenSubmitCalled_ThenNavigateToRequesterPendingIsNotCalled()
    {
        // Arrange
        _requestService.Setup(s => s.SubmitAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<Guid>()))
            .ReturnsAsync(new SubmitServiceRequestResult.Error("Submit failed."));
        var vm = CreateViewModel();
        vm.SetLocation(new GpsPoint(41.6, -93.6));
        vm.SelectDtc(Guid.NewGuid());

        // Act
        await vm.SubmitAsync();

        // Assert
        _navigator.Verify(n => n.NavigateToRequesterPending(), Times.Never);
    }
}
