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

    public void Load(UserProfile profile)
    {
        Menu = _menuFactory.Build(profile);
    }

    public void ToggleMenu()
    {
        IsMenuOpen = !IsMenuOpen;
    }

    public async Task LogoutAsync()
    {
        await _logoutSideEffect.RunBeforeTokenClearedAsync();
        await _tokenStore.ClearAsync();
        _navigator.NavigateToLogin();
    }

    public Task<ReleaseVehicleResult> ReleaseVehicleAsync() => _releaseVehicleAction.ReleaseAsync();
}
