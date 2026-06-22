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
        var token = await _tokenStore.GetTokenAsync();

        var sessionInvalid = JwtExpiryReader.IsExpired(token, DateTimeOffset.UtcNow);

        return sessionInvalid ? LoginRoute : null;
    }
}
