using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.UI.Features.Authentication.Services;

namespace ServiceDelivery.Client.Tests.ServiceRep;

public class PersonaRouteMapRepTakeOverTests
{
    [Fact]
    public void GivenAServiceRepRole_WhenPersonaRouteMapQueried_ThenRouteIsTakeOver()
    {
        // Arrange
        // A ServiceRep must land on the take-over screen first, before the idle rep view (AC-1).

        // Act
        var route = PersonaRouteMap.RouteFor(UserRole.ServiceRep);

        // Assert
        Assert.Equal("/rep/takeover", route);
    }
}
