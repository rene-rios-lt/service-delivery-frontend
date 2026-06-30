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
    private readonly IJobOfferStore _jobOfferStore;

    public BlazorPersonaNavigator(NavigationManager navigation, IJobOfferStore jobOfferStore)
    {
        _navigation = navigation;
        _jobOfferStore = jobOfferStore;
    }

    public void NavigateToPersonaHome(UserRole role) =>
        _navigation.NavigateTo(PersonaRouteMap.RouteFor(role));

    public void NavigateToLogin() =>
        _navigation.NavigateTo(PersonaRouteMap.Login);

    public void NavigateToRepIdleView() =>
        _navigation.NavigateTo(PersonaRouteMap.ServiceRepIdle);

    public void NavigateToJobOffer(JobOfferPayload offer)
    {
        // Deposit the payload before navigating so the job-offer page (FE-008) renders the offer
        // without a re-fetch. The store is scoped, so the page resolves the same instance.
        _jobOfferStore.SetOffer(offer);
        _navigation.NavigateTo(PersonaRouteMap.ServiceRepJobOffer);
    }

    public void NavigateToActiveJob() =>
        _navigation.NavigateTo(PersonaRouteMap.ServiceRepActiveJob);

    public void NavigateToTakeOver() =>
        _navigation.NavigateTo(PersonaRouteMap.ServiceRepTakeOver);

    public void NavigateToRequesterPending() =>
        _navigation.NavigateTo(PersonaRouteMap.RequesterPending);
}
