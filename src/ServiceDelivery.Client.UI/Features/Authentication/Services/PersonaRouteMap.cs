using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.Navigation;
using ServiceDelivery.Client.Core.ViewModels;

namespace ServiceDelivery.Client.UI.Features.Authentication.Services;

public static class PersonaRouteMap
{
    // Single source of truth for the login route lives in Core (AppStartViewModel.LoginRoute) and
    // the persona-home routes live in Core (PersonaHomeRoutes); Core cannot reference UI, so those
    // shared literals are defined there and surfaced here for navigator consumers. This keeps the
    // launch path (AppStartViewModel) and this navigator catalog from drifting (BUG-050).
    public const string Login = AppStartViewModel.LoginRoute;
    public const string DispatcherHome = PersonaHomeRoutes.DispatcherHome;
    public const string ServiceRepHome = "/rep";
    public const string ServiceRepTakeOver = PersonaHomeRoutes.ServiceRepTakeOver;
    public const string ServiceRepIdle = "/rep/idle";
    public const string ServiceRepJobOffer = "/rep/offer";
    public const string ServiceRepActiveJob = "/rep/job";
    public const string RequesterHome = PersonaHomeRoutes.RequesterHome;
    public const string RequesterSubmit = "/requester/submit";
    public const string RequesterPending = "/requester/pending";
    public const string RequesterTracking = "/requester/tracking";
    public const string RequesterComplete = "/requester/complete";

    public static string RouteFor(UserRole role) => PersonaHomeRoutes.RouteFor(role);
}
