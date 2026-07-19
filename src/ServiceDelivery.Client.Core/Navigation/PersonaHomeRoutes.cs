using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.Navigation;

/// <summary>
/// Single source of truth for each persona's home route. Both the launch path
/// (<see cref="ViewModels.AppStartViewModel"/>, which resolves a valid stored token to its persona
/// home) and the post-login navigator (<c>PersonaRouteMap</c> / <c>BlazorPersonaNavigator</c> in the
/// UI layer, which delegate here) resolve the role → home-route mapping in this one place, so the
/// launch and post-login paths can never drift (BUG-050).
/// </summary>
public static class PersonaHomeRoutes
{
    public const string DispatcherHome = "/dispatcher";
    public const string ServiceRepTakeOver = "/rep/takeover";
    public const string RequesterHome = "/requester";

    /// <summary>
    /// Resolves the persona home for a role that is guaranteed to have one (post-login, where the
    /// role comes from the trusted profile). Throws for a role with no home so contract drift fails
    /// loud rather than routing somewhere wrong.
    /// </summary>
    public static string RouteFor(UserRole role) =>
        TryGetRoute(role, out var route)
            ? route
            : throw new ArgumentOutOfRangeException(
                nameof(role), role, "No persona home route is defined for this role.");

    /// <summary>
    /// Fail-safe variant for the launch path: returns <c>false</c> for any role without a persona
    /// home (e.g. <see cref="UserRole.Simulator"/>) so the caller can fall back to /login instead of
    /// stranding the user on a blank authenticated page.
    /// </summary>
    public static bool TryGetRoute(UserRole role, out string route)
    {
        switch (role)
        {
            case UserRole.Dispatcher:
                route = DispatcherHome;
                return true;
            case UserRole.ServiceRep:
                route = ServiceRepTakeOver;
                return true;
            case UserRole.Requester:
                route = RequesterHome;
                return true;
            default:
                route = string.Empty;
                return false;
        }
    }
}
