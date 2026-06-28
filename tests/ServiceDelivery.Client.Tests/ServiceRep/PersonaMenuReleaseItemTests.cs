using System.Linq;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.ViewModels;

namespace ServiceDelivery.Client.Tests.ServiceRep;

public class PersonaMenuReleaseItemTests
{
    private static UserProfile ServiceRepProfile() =>
        new(Guid.NewGuid(), "Rosa Alvarez", UserRole.ServiceRep, ServiceTier.None, Guid.NewGuid());

    [Fact]
    public void GivenAServiceRepMenu_WhenRendered_ThenReleaseVehicleItemIsPresent()
    {
        // Arrange
        var factory = new PersonaMenuFactory();

        // Act
        var model = factory.Build(ServiceRepProfile());

        // Assert
        Assert.Contains(model.Items, i => i.ActionKey == PersonaMenuFactory.ReleaseActionKey);
    }

    [Fact]
    public void GivenAServiceRepMenu_WhenBuilt_ThenReleaseVehicleItemIsEnabledByDefault()
    {
        // Arrange
        // The factory stays pure: it always builds the release item enabled. The InProgress gate is
        // applied separately on the loaded menu by the shell (SetReleaseEnabled) — not by the factory.
        var factory = new PersonaMenuFactory();

        // Act
        var model = factory.Build(ServiceRepProfile());

        // Assert
        var release = model.Items.Single(i => i.ActionKey == PersonaMenuFactory.ReleaseActionKey);
        Assert.True(release.IsEnabled);
    }
}
