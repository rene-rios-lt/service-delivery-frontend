using Microsoft.AspNetCore.Components;
using ServiceDelivery.Client.UI.Features.Authentication.Services;

namespace ServiceDelivery.Client.UI.Features.Requester.Pages;

/// <summary>
/// Code-behind for <see cref="RequesterHome"/> (FE-015). The Requester's home is the submit screen, so
/// the role-routing target (<c>/requester</c>) immediately redirects to <c>/requester/submit</c>. Kept as
/// a thin redirect class so the landing route stays stable while the real home screen lives at its own route.
/// </summary>
public partial class RequesterHome
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    protected override void OnInitialized()
    {
        Navigation.NavigateTo(PersonaRouteMap.RequesterSubmit, replace: true);
    }
}
