using ServiceDelivery.Client.Core.Interfaces;

namespace ServiceDelivery.Client.Core.Authentication;

/// <summary>
/// Sole owner of the "clear + redirect" expiry action. Clears the stored token first, then
/// navigates to login, so a redirect never happens while a dead token is still persisted.
/// Both expiry detection paths (proactive claim check and reactive 401) invoke this one method.
/// </summary>
public class SessionExpiryHandler : ISessionExpiryHandler
{
    private readonly ITokenStore _tokenStore;
    private readonly IPersonaNavigator _navigator;

    public SessionExpiryHandler(ITokenStore tokenStore, IPersonaNavigator navigator)
    {
        _tokenStore = tokenStore;
        _navigator = navigator;
    }

    public async Task HandleExpiredSessionAsync()
    {
        await _tokenStore.ClearAsync();
        _navigator.NavigateToLogin();
    }
}
