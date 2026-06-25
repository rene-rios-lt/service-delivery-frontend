using ServiceDelivery.Client.Core.Authentication;
using ServiceDelivery.Client.Core.Interfaces;

namespace ServiceDelivery.Client.Core.ViewModels;

public class AppStartViewModel
{
    private readonly ITokenStore _tokenStore;

    public AppStartViewModel(ITokenStore tokenStore)
    {
        _tokenStore = tokenStore;
    }

    public const string LoginRoute = "/login";

    public async Task<string?> ResolveStartRouteAsync()
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

        var sessionInvalid = JwtExpiryReader.IsExpired(token, DateTimeOffset.UtcNow);

        return sessionInvalid ? LoginRoute : null;
    }
}
