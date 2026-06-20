using Microsoft.JSInterop;
using ServiceDelivery.Client.Core.Interfaces;

namespace ServiceDelivery.Client.Web.Services;

/// <summary>
/// Browser/WASM <see cref="ITokenStore"/> backed by <c>localStorage</c> so the JWT
/// survives page reloads. Honours the full contract (set / get / clear) with no no-ops.
/// </summary>
public class BrowserTokenStore : ITokenStore
{
    private const string TokenKey = "sd.auth.token";

    private readonly IJSRuntime _jsRuntime;

    public BrowserTokenStore(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task SetTokenAsync(string token) =>
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", TokenKey, token);

    public async Task<string?> GetTokenAsync() =>
        await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", TokenKey);

    public async Task ClearAsync() =>
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", TokenKey);
}
