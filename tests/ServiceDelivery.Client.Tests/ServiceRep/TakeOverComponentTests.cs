using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.ViewModels;
using ServiceDelivery.Client.UI.Features.ServiceRep.Components;
using ServiceDelivery.Client.UI.Features.ServiceRep.Pages;

namespace ServiceDelivery.Client.Tests.ServiceRep;

public class TakeOverComponentTests : BunitContext
{
    private readonly Mock<IVehicleService> _vehicleService = new();
    private readonly Mock<IPersonaNavigator> _navigator = new();
    private readonly Mock<IClaimedVehicleStore> _claimedVehicleStore = new();

    private static IdleVehicle Vehicle(
        string registration = "IA-4471",
        params string[] equipment) =>
        new(Guid.NewGuid(), registration,
            equipment.Length == 0 ? new[] { "Hydraulics", "Coolant" } : equipment);

    private void RegisterServices()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private TakeOverViewModel RegisterPage(params IdleVehicle[] vehicles)
    {
        _vehicleService.Setup(s => s.GetIdleVehiclesAsync()).ReturnsAsync(vehicles);
        var viewModel = new TakeOverViewModel(_vehicleService.Object, _navigator.Object, _claimedVehicleStore.Object);
        RegisterServices();
        Services.AddSingleton(viewModel);
        return viewModel;
    }

    [Fact]
    public void GivenIdleVehicles_WhenComponentRendered_ThenEachRowShowsRegistrationAndEquipment()
    {
        // Arrange
        RegisterServices();
        var vehicles = new[]
        {
            Vehicle("IA-4471", "Hydraulics", "Coolant"),
            Vehicle("IA-2208", "Diagnostics")
        };

        // Act
        var cut = Render<IdleVehicleList>(p => p
            .Add(c => c.Vehicles, vehicles));

        // Assert
        var rows = cut.FindAll("[data-testid='idle-vehicle-row']");
        Assert.Equal(2, rows.Count);
        Assert.Contains("IA-4471", rows[0].TextContent);
        Assert.Contains("Hydraulics", rows[0].TextContent);
    }

    [Fact]
    public void GivenIdleVehicles_WhenIdleVehicleListRendered_ThenCardContainerIsPresent()
    {
        // Arrange
        RegisterServices();
        var vehicles = new[] { Vehicle("IA-4471", "HydraulicTool", "CoolingSystemKit") };

        // Act
        var cut = Render<IdleVehicleList>(p => p
            .Add(c => c.Vehicles, vehicles));

        // Assert
        var card = cut.Find("[data-testid='idle-vehicle-list']");
        Assert.Contains("sd-card", card.ClassList);
    }

    [Fact]
    public void GivenIdleVehicles_WhenIdleVehicleListRendered_ThenEachRowHasListItemStructure()
    {
        // Arrange
        RegisterServices();
        var vehicles = new[] { Vehicle("IA-4471", "HydraulicTool", "CoolingSystemKit") };

        // Act
        var cut = Render<IdleVehicleList>(p => p
            .Add(c => c.Vehicles, vehicles));

        // Assert
        var row = cut.Find("[data-testid='idle-vehicle-row']");
        Assert.Contains("sd-listitem", row.ClassList);
        Assert.NotNull(row.QuerySelector(".sd-listitem__icon"));
        Assert.NotNull(row.QuerySelector(".sd-listitem__title"));
    }

    [Fact]
    public void GivenIdleVehicles_WhenIdleVehicleListRendered_ThenEquipmentChipsAreRenderedPerRow()
    {
        // Arrange
        RegisterServices();
        var vehicles = new[] { Vehicle("IA-4471", "HydraulicTool", "CoolingSystemKit") };

        // Act
        var cut = Render<IdleVehicleList>(p => p
            .Add(c => c.Vehicles, vehicles));

        // Assert
        var row = cut.Find("[data-testid='idle-vehicle-row']");
        var equip = row.QuerySelector(".sd-equip");
        Assert.NotNull(equip);
        Assert.NotEmpty(equip!.QuerySelectorAll("span"));
    }

    [Fact]
    public void GivenAVehicleWithMoreThanTwoEquipmentTypes_WhenRendered_ThenChipsCollapseToFirstTwoPlusOverflow()
    {
        // Arrange
        RegisterServices();
        var vehicles = new[]
        {
            Vehicle("IA-4471",
                "Hydraulics", "Coolant", "Diagnostics", "Welding", "Lift", "Crane")
        };

        // Act
        var cut = Render<IdleVehicleList>(p => p
            .Add(c => c.Vehicles, vehicles));

        // Assert
        var row = cut.Find("[data-testid='idle-vehicle-row']");
        Assert.Contains("Hydraulics", row.TextContent);
        Assert.Contains("Coolant", row.TextContent);
        Assert.DoesNotContain("Diagnostics", row.TextContent);
        Assert.Contains("+4", cut.Find("[data-testid='equipment-overflow']").TextContent);
    }

    [Fact]
    public void GivenRawEnumEquipmentName_WhenIdleVehicleListRendered_ThenFriendlyLabelIsDisplayed()
    {
        // Arrange
        RegisterServices();
        var vehicles = new[] { Vehicle("IA-4471", "HydraulicTool", "CoolingSystemKit") };

        // Act
        var cut = Render<IdleVehicleList>(p => p
            .Add(c => c.Vehicles, vehicles));

        // Assert
        var chips = cut.FindAll("[data-testid='equipment-chip']").Select(c => c.TextContent.Trim()).ToList();
        Assert.Contains("Hydraulics", chips);
        Assert.Contains("Coolant", chips);
        Assert.DoesNotContain("HydraulicTool", chips);
        Assert.DoesNotContain("CoolingSystemKit", chips);
    }

    [Fact]
    public void GivenUnknownEquipmentKey_WhenIdleVehicleListRendered_ThenRawNameIsDisplayedAsChip()
    {
        // Arrange
        RegisterServices();
        var vehicles = new[] { Vehicle("IA-4471", "UnknownGadget") };

        // Act
        var cut = Render<IdleVehicleList>(p => p
            .Add(c => c.Vehicles, vehicles));

        // Assert
        var chips = cut.FindAll("[data-testid='equipment-chip']").Select(c => c.TextContent.Trim()).ToList();
        Assert.Contains("UnknownGadget", chips);
    }

    [Fact]
    public void GivenVehicleWithMoreThanTwoEquipmentTypes_WhenIdleVehicleListRendered_ThenOverflowChipShowsCorrectCount()
    {
        // Arrange
        RegisterServices();
        var vehicles = new[]
        {
            Vehicle("IA-4471",
                "HydraulicTool", "CoolingSystemKit", "ElectricalDiagnosticKit",
                "BrakingSystemKit", "FuelSystemKit", "SuspensionKit")
        };

        // Act
        var cut = Render<IdleVehicleList>(p => p
            .Add(c => c.Vehicles, vehicles));

        // Assert
        Assert.Contains("+4", cut.Find("[data-testid='equipment-overflow']").TextContent);
    }

    [Fact]
    public void GivenEquipmentChips_WhenIdleVehicleListRendered_ThenEachChipHasEquipmentChipTestId()
    {
        // Arrange
        RegisterServices();
        var vehicles = new[] { Vehicle("IA-4471", "HydraulicTool", "CoolingSystemKit") };

        // Act
        var cut = Render<IdleVehicleList>(p => p
            .Add(c => c.Vehicles, vehicles));

        // Assert
        Assert.Equal(2, cut.FindAll("[data-testid='equipment-chip']").Count);
    }

    [Fact]
    public void GivenSuccessfulTakeOver_WhenButtonClicked_ThenComponentNavigatesToRepHome()
    {
        // Arrange
        var vehicle = Vehicle("IA-4471");
        _vehicleService.Setup(s => s.TakeOverAsync(vehicle.VehicleId)).ReturnsAsync(TakeOverResult.Success);
        RegisterPage(vehicle);
        var cut = Render<TakeOver>();
        cut.Find("[data-testid='idle-vehicle-row']").Click();

        // Act
        cut.Find("[data-testid='take-over-button']").Click();

        // Assert
        _navigator.Verify(n => n.NavigateToRepIdleView(), Times.Once);
    }

    [Fact]
    public void GivenNoVehicleSelected_WhenPageRendered_ThenTakeOverButtonIsDisabled()
    {
        // Arrange
        RegisterPage(Vehicle("IA-4471"));

        // Act
        var cut = Render<TakeOver>();

        // Assert
        var button = cut.Find("[data-testid='take-over-button']");
        Assert.True(button.HasAttribute("disabled"));
    }

    [Fact]
    public void GivenAVehicleSelected_WhenPageRendered_ThenTakeOverButtonShowsRegistrationAndIsEnabled()
    {
        // Arrange
        var vehicle = Vehicle("IA-4471");
        RegisterPage(vehicle);
        var cut = Render<TakeOver>();

        // Act
        cut.Find("[data-testid='idle-vehicle-row']").Click();

        // Assert
        var button = cut.Find("[data-testid='take-over-button']");
        Assert.False(button.HasAttribute("disabled"));
        Assert.Contains("Take over IA-4471", button.TextContent);
    }

    [Fact]
    public void GivenAVehicleSelected_WhenRowClicked_ThenSelectedCheckMarkIsShown()
    {
        // Arrange
        var vehicle = Vehicle("IA-4471");
        RegisterPage(vehicle);
        var cut = Render<TakeOver>();

        // Act
        cut.Find("[data-testid='idle-vehicle-row']").Click();

        // Assert
        Assert.NotNull(cut.Find("[data-testid='selected-check']"));
    }

    [Fact]
    public void GivenNoConflict_WhenComponentRendered_ThenNoErrorAlertIsShown()
    {
        // Arrange
        RegisterPage(Vehicle("IA-4471"));

        // Act
        var cut = Render<TakeOver>();

        // Assert
        Assert.Empty(cut.FindAll("[data-testid='take-over-error']"));
    }

    [Fact]
    public void GivenConflictResult_WhenComponentRendered_ThenErrorAlertIsVisible()
    {
        // Arrange
        var vehicle = Vehicle("IA-4471");
        _vehicleService.Setup(s => s.TakeOverAsync(vehicle.VehicleId)).ReturnsAsync(TakeOverResult.Conflict);
        RegisterPage(vehicle);
        var cut = Render<TakeOver>();
        cut.Find("[data-testid='idle-vehicle-row']").Click();

        // Act
        cut.Find("[data-testid='take-over-button']").Click();

        // Assert
        var alert = cut.Find("[data-testid='take-over-error']");
        Assert.Contains(TakeOverViewModel.ConflictMessage, alert.TextContent);
    }

    [Fact]
    public void GivenNoIdleVehicles_WhenPageRendered_ThenEmptyStateMessageIsShown()
    {
        // Arrange
        RegisterPage();

        // Act
        var cut = Render<TakeOver>();

        // Assert
        Assert.NotNull(cut.Find("[data-testid='no-vehicles-message']"));
        Assert.Empty(cut.FindAll("[data-testid='idle-vehicle-row']"));
    }

    [Fact]
    public void GivenLoadIsInProgress_WhenPageRendered_ThenLoadingIndicatorIsShown()
    {
        // Arrange
        var tcs = new TaskCompletionSource<IReadOnlyList<IdleVehicle>>();
        _vehicleService.Setup(s => s.GetIdleVehiclesAsync()).Returns(tcs.Task);
        var viewModel = new TakeOverViewModel(_vehicleService.Object, _navigator.Object, _claimedVehicleStore.Object);
        RegisterServices();
        Services.AddSingleton(viewModel);

        // Act
        var cut = Render<TakeOver>();

        // Assert
        Assert.NotNull(cut.Find("[data-testid='take-over-loading']"));
        tcs.SetResult([]);
    }

    [Fact]
    public void GivenRepAlreadyMidJob_WhenPageRendered_ThenIneligibleMessageShownAndButtonDisabled()
    {
        // Arrange
        var vehicle = Vehicle("IA-4471");
        var viewModel = RegisterPage(vehicle);
        viewModel.SetEligibility(repIsIdle: false);

        // Act
        var cut = Render<TakeOver>();

        // Assert
        var notice = cut.Find("[data-testid='ineligible-notice']");
        Assert.Contains(TakeOverViewModel.IneligibleMessage, notice.TextContent);
        Assert.True(cut.Find("[data-testid='take-over-button']").HasAttribute("disabled"));
    }
}
