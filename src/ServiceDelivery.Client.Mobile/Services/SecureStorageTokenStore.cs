using ServiceDelivery.Client.Core.Interfaces;

namespace ServiceDelivery.Client.Mobile.Services;

/// <summary>
/// Mobile (MAUI) <see cref="ITokenStore"/> backed by the platform <see cref="SecureStorage"/>.
/// Honours the full contract (set / get / clear) with no no-ops.
/// </summary>
public class SecureStorageTokenStore : ITokenStore
{
    private const string TokenKey = "sd.auth.token";

    public Task SetTokenAsync(string token) =>
        SecureStorage.Default.SetAsync(TokenKey, token);

    public async Task<string?> GetTokenAsync()
    {
        try
        {
            return await SecureStorage.Default.GetAsync(TokenKey);
        }
        catch
        {
            // SecureStorage can throw on first launch in the iOS simulator before the Keychain
            // is ready (timing race). Treat any failure as "no token" → redirect to login.
            return null;
        }
    }

    public Task ClearAsync()
    {
        SecureStorage.Default.Remove(TokenKey);
        return Task.CompletedTask;
    }
}
