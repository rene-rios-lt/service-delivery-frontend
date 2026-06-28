using System.Threading.Tasks;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.ViewModels;

/// <summary>
/// Orchestrates the persona shell: exposes the platform menu style, the current persona menu model,
/// menu open/close state (drawer on Mobile, account dropdown on Desktop/Web), and the logout /
/// release-vehicle sequences. It depends only on Core
/// abstractions — never on <c>HttpClient</c> or <c>NavigationManager</c> directly.
/// </summary>
public class ShellViewModel
{
    private readonly ITokenStore _tokenStore;
    private readonly IPersonaNavigator _navigator;
    private readonly ILogoutSideEffect _logoutSideEffect;
    private readonly IReleaseVehicleAction _releaseVehicleAction;
    private readonly IShellPresentation _presentation;
    private readonly PersonaMenuFactory _menuFactory;

    private const string DefaultTitle = "Service Delivery";
    private string? _titleOverride;
    private string? _subtitleOverride;

    public ShellViewModel(
        ITokenStore tokenStore,
        IPersonaNavigator navigator,
        ILogoutSideEffect logoutSideEffect,
        IReleaseVehicleAction releaseVehicleAction,
        IShellPresentation presentation,
        PersonaMenuFactory menuFactory)
    {
        _tokenStore = tokenStore;
        _navigator = navigator;
        _logoutSideEffect = logoutSideEffect;
        _releaseVehicleAction = releaseVehicleAction;
        _presentation = presentation;
        _menuFactory = menuFactory;
    }

    public ShellMenuStyle MenuStyle => _presentation.MenuStyle;

    public PersonaMenuModel? Menu { get; private set; }

    public bool IsMenuOpen { get; private set; }

    /// <summary>
    /// The app-bar title. Defaults to "Service Delivery"; a route may override it for its own screen
    /// (e.g. "Incoming Job Offer" on the job-offer view — BUG-036) via <see cref="SetTitle"/>.
    /// </summary>
    public string Title => _titleOverride ?? DefaultTitle;

    /// <summary>
    /// An app-bar subtitle override for the current route (e.g. "Navigating to requester" on the
    /// active-job view — BUG-039). When non-<c>null</c>, <c>PersonaShell</c> prefers it over the
    /// menu-derived vehicle/context line. Defaults to <c>null</c>, leaving the derived path in place.
    /// </summary>
    public string? Subtitle => _subtitleOverride;

    /// <summary>
    /// Whether the app-bar menu affordance (the hamburger) is shown. The job-offer screen hides it so
    /// the rep stays focused on accept/decline, matching the mockup (BUG-036). Defaults to visible.
    /// </summary>
    public bool IsMenuAffordanceVisible { get; private set; } = true;

    public void Load(UserProfile profile)
    {
        Menu = _menuFactory.Build(profile);
    }

    public void ToggleMenu()
    {
        IsMenuOpen = !IsMenuOpen;
    }

    /// <summary>
    /// Overrides the app-bar title for the current route. Pass <c>null</c> to restore the default
    /// ("Service Delivery"). The route that sets an override owns clearing it when it leaves.
    /// </summary>
    public void SetTitle(string? title) => _titleOverride = title;

    /// <summary>
    /// Overrides the app-bar subtitle for the current route. Pass <c>null</c> to fall back to the
    /// menu-derived vehicle/context line. The route that sets an override owns clearing it on leave.
    /// </summary>
    public void SetSubtitle(string? subtitle) => _subtitleOverride = subtitle;

    /// <summary>
    /// Shows or hides the app-bar menu affordance (the hamburger) for the current route. Defaults to
    /// visible; the route that hides it owns restoring it when it leaves.
    /// </summary>
    public void SetMenuAffordanceVisible(bool visible) => IsMenuAffordanceVisible = visible;

    /// <summary>
    /// Replaces the current menu with a copy carrying the supplied vehicle-context line (e.g.
    /// "Vehicle IA-4471 · On shift"), so the app-bar subtitle reflects the claimed vehicle after a
    /// take-over. No-op when no menu has been loaded yet.
    /// </summary>
    public void SetVehicleContext(string vehicleContext)
    {
        if (Menu is null)
        {
            return;
        }

        Menu = Menu with { VehicleContext = vehicleContext };
    }

    /// <summary>
    /// Gates the "Release vehicle" menu item's enabled state on the already-loaded menu, without a
    /// profile re-load. The InProgress screen (<c>ActiveJob.razor</c>) is the production caller: it
    /// passes <c>false</c> while a job is in progress so the rep cannot release mid-job (FE-014/AC-2),
    /// and <c>true</c> when it leaves (job complete / idle) to re-enable release. No-op when no menu
    /// has been loaded yet (mirrors <see cref="SetVehicleContext"/>).
    /// </summary>
    public void SetReleaseEnabled(bool releaseEnabled)
    {
        if (Menu is null)
        {
            return;
        }

        var items = Menu.Items
            .Select(item => item.ActionKey == PersonaMenuFactory.ReleaseActionKey
                ? item with { IsEnabled = releaseEnabled }
                : item)
            .ToList();

        Menu = Menu with { Items = items };
    }

    public async Task LogoutAsync()
    {
        await _logoutSideEffect.RunBeforeTokenClearedAsync();
        await _tokenStore.ClearAsync();
        _navigator.NavigateToLogin();
    }

    public Task<ReleaseVehicleResult> ReleaseVehicleAsync() => _releaseVehicleAction.ReleaseAsync();
}
