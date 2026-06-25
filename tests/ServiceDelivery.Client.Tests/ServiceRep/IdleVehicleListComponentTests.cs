using Bunit;
using MudBlazor.Services;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.UI.Features.ServiceRep.Components;

namespace ServiceDelivery.Client.Tests.ServiceRep;

public class IdleVehicleListComponentTests : BunitContext
{
    private static IdleVehicle Vehicle(
        string registration,
        string model,
        params string[] equipment) =>
        new(Guid.NewGuid(), registration, model,
            equipment.Length == 0 ? new[] { "Hydraulics", "Coolant" } : equipment);

    private void RegisterServices()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void GivenAnIdleVehicleWithAModel_WhenIdleVehicleListRendered_ThenRowTitleShowsRegistrationDotModel()
    {
        // Arrange
        RegisterServices();
        var vehicles = new[] { Vehicle("IA-4471", "Transit 350", "Hydraulics", "Coolant") };

        // Act
        var cut = Render<IdleVehicleList>(p => p
            .Add(c => c.Vehicles, vehicles));

        // Assert
        var title = cut.Find("[data-testid='idle-vehicle-row'] .sd-listitem__title");
        Assert.Equal("IA-4471 · Transit 350", title.TextContent.Trim());
    }

    [Fact]
    public void GivenAnIdleVehicleWithNoModel_WhenIdleVehicleListRendered_ThenRowTitleShowsRegistrationOnly()
    {
        // Arrange
        RegisterServices();
        var vehicles = new[] { Vehicle("IA-4471", string.Empty, "Hydraulics", "Coolant") };

        // Act
        var cut = Render<IdleVehicleList>(p => p
            .Add(c => c.Vehicles, vehicles));

        // Assert
        var title = cut.Find("[data-testid='idle-vehicle-row'] .sd-listitem__title");
        Assert.Equal("IA-4471", title.TextContent.Trim());
        Assert.DoesNotContain("·", title.TextContent);
    }

    [Fact]
    public void GivenMultipleIdleVehicles_WhenIdleVehicleListRendered_ThenEveryRowTitleShowsItsRegistrationDotModel()
    {
        // Arrange
        RegisterServices();
        var vehicles = new[]
        {
            Vehicle("IA-4471", "Transit 350", "Hydraulics"),
            Vehicle("IA-2208", "Sprinter", "Diagnostics"),
            Vehicle("IA-9015", "Transit 250", "Coolant")
        };

        // Act
        var cut = Render<IdleVehicleList>(p => p
            .Add(c => c.Vehicles, vehicles));

        // Assert
        var titles = cut.FindAll("[data-testid='idle-vehicle-row'] .sd-listitem__title")
            .Select(t => t.TextContent.Trim())
            .ToList();
        Assert.Equal("IA-4471 · Transit 350", titles[0]);
        Assert.Equal("IA-2208 · Sprinter", titles[1]);
        Assert.Equal("IA-9015 · Transit 250", titles[2]);
    }
}
