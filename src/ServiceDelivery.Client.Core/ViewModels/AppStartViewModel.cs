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

        return string.IsNullOrWhiteSpace(token) ? LoginRoute : null;
    }
}
