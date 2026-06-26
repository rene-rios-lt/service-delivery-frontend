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
    /// When non-null, the app bar renders this string instead of the default "Service Delivery"
    /// title. Set by <see cref="SetFocusedMode"/> for focused decision screens (e.g. the job offer).
    /// </summary>
    public string? TitleOverride { get; private set; }

    /// <summary>
    /// When <c>true</c>, the app bar hides both the menu (hamburger) affordance and the persona
    /// avatar. Used on focused decision screens where the menu is irrelevant (BUG-036 AC-4).
    /// </summary>
    public bool SuppressMenuAffordance { get; private set; }

    public void Load(UserProfile profile)
    {
        Menu = _menuFactory.Build(profile);
    }

    public void ToggleMenu()
    {
        IsMenuOpen = !IsMenuOpen;
    }

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
    /// Enters focused mode: overrides the app-bar title, suppresses the menu affordance + avatar, and
    /// sets the subtitle to <paramref name="subtitle"/>. The job-offer screen calls this on init so the
    /// app bar reads "Incoming Job Offer" / "Vehicle IA-4471" with no menu chrome (BUG-036 AC-2, AC-4).
    /// </summary>
    public void SetFocusedMode(string titleOverride, string subtitle)
    {
        TitleOverride = titleOverride;
        SuppressMenuAffordance = true;
        SetVehicleContext(subtitle);
    }

    /// <summary>
    /// Exits focused mode, restoring the default "Service Delivery" title and the menu affordance +
    /// avatar. Called when leaving the focused screen (accept, decline, or offer expiry) so the normal
    /// shell chrome returns. Leaves the vehicle context untouched — the underlying claim is unchanged.
    /// </summary>
    public void ClearFocusedMode()
    {
        TitleOverride = null;
        SuppressMenuAffordance = false;
    }

    public async Task LogoutAsync()
    {
        await _logoutSideEffect.RunBeforeTokenClearedAsync();
        await _tokenStore.ClearAsync();
        _navigator.NavigateToLogin();
    }

    public Task<ReleaseVehicleResult> ReleaseVehicleAsync() => _releaseVehicleAction.ReleaseAsync();
}
