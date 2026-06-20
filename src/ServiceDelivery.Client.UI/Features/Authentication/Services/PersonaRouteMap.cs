using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.UI.Features.Authentication.Services;

public static class PersonaRouteMap
{
    public const string DispatcherHome = "/dispatcher";
    public const string ServiceRepHome = "/rep";
    public const string RequesterHome = "/requester";

    public static string RouteFor(UserRole role) => role switch
    {
        UserRole.Dispatcher => DispatcherHome,
        UserRole.ServiceRep => ServiceRepHome,
        UserRole.Requester => RequesterHome,
        _ => throw new ArgumentOutOfRangeException(
            nameof(role), role, "No persona home route is defined for this role.")
    };
}
