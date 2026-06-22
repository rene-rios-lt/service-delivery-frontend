using ServiceDelivery.Client.Core.Interfaces;

namespace ServiceDelivery.Client.Core.Authentication;

/// <summary>
/// Reads the stored token via <see cref="ITokenStore"/> and delegates the <c>exp</c> claim
/// inspection to <see cref="JwtExpiryReader"/>. A missing token is treated as expired.
/// </summary>
public class SessionState : ISessionState
{
    private readonly ITokenStore _tokenStore;

    public SessionState(ITokenStore tokenStore)
    {
        _tokenStore = tokenStore;
    }

    public async Task<bool> IsTokenExpiredAsync()
    {
        var token = await _tokenStore.GetTokenAsync();
        return JwtExpiryReader.IsExpired(token, DateTimeOffset.UtcNow);
    }
}
