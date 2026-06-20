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

    public Task<string?> GetTokenAsync() =>
        SecureStorage.Default.GetAsync(TokenKey);

    public Task ClearAsync()
    {
        SecureStorage.Default.Remove(TokenKey);
        return Task.CompletedTask;
    }
}
