using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.ViewModels;

namespace ServiceDelivery.Client.UI.Features.Authentication.Services;

public static class PersonaRouteMap
{
    // Single source of truth for the login route lives in Core (AppStartViewModel.LoginRoute);
    // Core cannot reference UI, so the shared literal is defined there and surfaced here for
    // navigator consumers. This keeps the launch-route check and the navigator from drifting.
    public const string Login = AppStartViewModel.LoginRoute;
    public const string DispatcherHome = "/dispatcher";
    public const string ServiceRepHome = "/rep";
    public const string ServiceRepTakeOver = "/rep/takeover";
    public const string ServiceRepIdle = "/rep/idle";
    public const string ServiceRepJobOffer = "/rep/offer";
    public const string RequesterHome = "/requester";

    public static string RouteFor(UserRole role) => role switch
    {
        UserRole.Dispatcher => DispatcherHome,
        UserRole.ServiceRep => ServiceRepTakeOver,
        UserRole.Requester => RequesterHome,
        _ => throw new ArgumentOutOfRangeException(
            nameof(role), role, "No persona home route is defined for this role.")
    };
}
