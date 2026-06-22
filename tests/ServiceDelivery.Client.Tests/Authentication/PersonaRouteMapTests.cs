using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.UI.Features.Authentication.Services;

namespace ServiceDelivery.Client.Tests.Authentication;

public class PersonaRouteMapTests
{
    [Theory]
    [InlineData(UserRole.Dispatcher, "/dispatcher")]
    [InlineData(UserRole.ServiceRep, "/rep")]
    [InlineData(UserRole.Requester, "/requester")]
    public void GivenAPersonaRole_WhenMapped_ThenReturnsThatPersonasHomeRoute(UserRole role, string expectedRoute)
    {
        // Arrange
        // (PersonaRouteMap is a pure, stateless mapping helper)

        // Act
        var route = PersonaRouteMap.RouteFor(role);

        // Assert
        Assert.Equal(expectedRoute, route);
    }

    [Fact]
    public void GivenTheLoginRouteConstant_WhenRead_ThenItIsTheLoginPath()
    {
        // Arrange
        // (PersonaRouteMap.Login is the single source of truth for the login route literal)

        // Act
        var route = PersonaRouteMap.Login;

        // Assert
        Assert.Equal("/login", route);
    }

    [Fact]
    public void GivenTheSimulatorRole_WhenMapped_ThenThrowsBecauseItHasNoPersonaUi()
    {
        // Arrange
        // Simulator is a backend-only account with no persona view (ADR-0008 platform matrix).

        // Act
        var act = () => PersonaRouteMap.RouteFor(UserRole.Simulator);

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(act);
    }
}
