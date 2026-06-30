using Microsoft.JSInterop;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Web.Services;

/// <summary>
/// Browser/WASM <see cref="IGeolocationService"/> (FE-015 AC-1). Imports the UI-shipped
/// <c>geolocation.js</c> module and calls its <c>getCurrentPosition</c>, which wraps
/// <c>navigator.geolocation.getCurrentPosition</c> in a Promise. Returns a <see cref="GpsPoint"/> on
/// success; returns <c>null</c> when the browser cannot supply a position (permission denied or
/// unsupported) — an explicit "no position" outcome that fully honours the contract (no silent no-op).
/// </summary>
public class BrowserGeolocationService : IGeolocationService
{
    private const string ModulePath =
        "./_content/ServiceDelivery.Client.UI/Features/Requester/geolocation.js";

    private readonly IJSRuntime _jsRuntime;

    public BrowserGeolocationService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<GpsPoint?> GetCurrentLocationAsync()
    {
        try
        {
            await using var module =
                await _jsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath);
            return await module.InvokeAsync<GpsPoint>("getCurrentPosition");
        }
        catch (JSException)
        {
            // The browser rejected the request (permission denied or unsupported). Surface "no position"
            // so the submit form keeps working — the requester can still drop a pin on the map (AC-1).
            return null;
        }
    }
}
