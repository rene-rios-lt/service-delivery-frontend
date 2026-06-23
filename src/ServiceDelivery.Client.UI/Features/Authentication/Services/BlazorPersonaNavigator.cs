using Microsoft.AspNetCore.Components;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.UI.Features.Authentication.Services;

/// <summary>
/// Blazor-generic <see cref="IPersonaNavigator"/> shared by every host. Translates a role
/// into its persona home route via the framework-free <see cref="PersonaRouteMap"/> and
/// navigates using <see cref="NavigationManager"/>, keeping NavigationManager out of
/// Core/ViewModels.
/// </summary>
public class BlazorPersonaNavigator : IPersonaNavigator
{
    private readonly NavigationManager _navigation;

    public BlazorPersonaNavigator(NavigationManager navigation)
    {
        _navigation = navigation;
    }

    public void NavigateToPersonaHome(UserRole role) =>
        _navigation.NavigateTo(PersonaRouteMap.RouteFor(role));

    public void NavigateToLogin() =>
        _navigation.NavigateTo(PersonaRouteMap.Login);

    public void NavigateToRepIdleView() =>
        _navigation.NavigateTo(PersonaRouteMap.ServiceRepHome);
}
