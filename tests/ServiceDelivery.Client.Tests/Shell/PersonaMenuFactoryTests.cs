using System.Linq;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.ViewModels;

namespace ServiceDelivery.Client.Tests.Shell;

public class PersonaMenuFactoryTests
{
    private static UserProfile ProfileFor(UserRole role, string name = "Test User") =>
        new(Guid.NewGuid(), name, role, ServiceTier.None, Guid.NewGuid());

    [Fact]
    public void GivenAServiceRepProfile_WhenMenuBuilt_ThenItemsAreJobHistoryHelpReleaseAndLogout()
    {
        // Arrange
        var factory = new PersonaMenuFactory();
        var profile = ProfileFor(UserRole.ServiceRep, "Rosa Alvarez");

        // Act
        var model = factory.Build(profile);

        // Assert
        var keys = model.Items.Select(i => i.ActionKey).ToArray();
        Assert.Equal(
            new[] { "rep-home", "job-history", "help", "release", "logout" },
            keys);
    }

    [Fact]
    public void GivenADispatcherProfile_WhenMenuBuilt_ThenItemsAreProfileSettingsAndLogout()
    {
        // Arrange
        var factory = new PersonaMenuFactory();
        var profile = ProfileFor(UserRole.Dispatcher, "Dana Morales");

        // Act
        var model = factory.Build(profile);

        // Assert
        var keys = model.Items.Select(i => i.ActionKey).ToArray();
        Assert.Equal(
            new[] { "profile", "settings", "logout" },
            keys);
    }

    [Theory]
    [InlineData(UserRole.ServiceRep)]
    [InlineData(UserRole.Dispatcher)]
    [InlineData(UserRole.Requester)]
    public void GivenAnyPersona_WhenMenuBuilt_ThenLogoutItemIsPresent(UserRole role)
    {
        // Arrange
        var factory = new PersonaMenuFactory();
        var profile = ProfileFor(role);

        // Act
        var model = factory.Build(profile);

        // Assert
        Assert.Contains(model.Items, i => i.ActionKey == "logout");
    }

    [Fact]
    public void GivenMenuItems_WhenBuilt_ThenReleaseAndLogoutAreFlaggedDestructive()
    {
        // Arrange
        var factory = new PersonaMenuFactory();
        var profile = ProfileFor(UserRole.ServiceRep);

        // Act
        var model = factory.Build(profile);

        // Assert
        Assert.True(model.Items.Single(i => i.ActionKey == "release").IsDestructive);
        Assert.True(model.Items.Single(i => i.ActionKey == "logout").IsDestructive);
        Assert.False(model.Items.Single(i => i.ActionKey == "job-history").IsDestructive);
    }

    [Fact]
    public void GivenAProfileWithName_WhenMenuBuilt_ThenTitleIsTheProfileName()
    {
        // Arrange
        var factory = new PersonaMenuFactory();
        var profile = ProfileFor(UserRole.ServiceRep, "Rosa Alvarez");

        // Act
        var model = factory.Build(profile);

        // Assert
        Assert.Equal("Rosa Alvarez", model.Title);
    }
}
