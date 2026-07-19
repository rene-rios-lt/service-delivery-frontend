using ServiceDelivery.Client.Core.Authentication;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Navigation;

namespace ServiceDelivery.Client.Core.ViewModels;

public class AppStartViewModel
{
    private readonly ITokenStore _tokenStore;

    public AppStartViewModel(ITokenStore tokenStore)
    {
        _tokenStore = tokenStore;
    }

    public const string LoginRoute = "/login";

    /// <summary>
    /// Resolves the route the app should land on at launch. Always returns a concrete route so
    /// <c>Home.razor</c> ("/") never renders a blank authenticated page (BUG-050): a valid token
    /// routes to its persona home, and a missing / expired / unreadable / unroutable-role token
    /// routes to <see cref="LoginRoute"/>.
    /// </summary>
    public async Task<string> ResolveStartRouteAsync()
    {
        string? token;
        try
        {
            token = await _tokenStore.GetTokenAsync();
        }
        catch
        {
            // If the token store is unavailable (e.g., iOS Keychain not ready on first launch),
            // treat it as no token and send the user to the login screen.
            return LoginRoute;
        }

        if (JwtExpiryReader.IsExpired(token, DateTimeOffset.UtcNow))
        {
            return LoginRoute;
        }

        // A live token must also name a routable persona; an unreadable/unknown role — or a valid
        // role with no frontend home (e.g. Simulator) — is an unusable session, so route to /login
        // rather than the persona shell over a blank body.
        var role = JwtRoleReader.ReadRole(token);
        if (role is null || !PersonaHomeRoutes.TryGetRoute(role.Value, out var homeRoute))
        {
            return LoginRoute;
        }

        return homeRoute;
    }
}
