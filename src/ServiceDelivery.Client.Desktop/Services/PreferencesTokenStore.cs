using ServiceDelivery.Client.Core.Interfaces;

namespace ServiceDelivery.Client.Desktop.Services;

/// <summary>
/// Desktop (MAUI) <see cref="ITokenStore"/> backed by MAUI <see cref="Preferences"/> rather than the
/// platform Keychain (<c>SecureStorage</c>). <c>SecureStorage</c> needs a Keychain-access-group
/// entitlement that an unsigned local Mac Catalyst build does not carry, so <c>GetAsync</c> threw and
/// crashed Dispatcher login before any UI rendered (FE-003 AC-1). <see cref="Preferences"/> requires no
/// entitlement and works correctly under an unsigned build. Honours the full contract (set / get / clear)
/// with no no-ops — <c>GetTokenAsync</c> returns <c>null</c> when no token is stored, matching the
/// nullable contract the callers (SignalR access-token provider, session bootstrap) already handle.
/// </summary>
public class PreferencesTokenStore : ITokenStore
{
    private const string TokenKey = "sd.auth.token";

    public Task SetTokenAsync(string token)
    {
        Preferences.Default.Set(TokenKey, token);
        return Task.CompletedTask;
    }

    public Task<string?> GetTokenAsync()
    {
        var token = Preferences.Default.Get(TokenKey, string.Empty);
        return Task.FromResult(string.IsNullOrEmpty(token) ? null : token);
    }

    public Task ClearAsync()
    {
        Preferences.Default.Remove(TokenKey);
        return Task.CompletedTask;
    }
}
