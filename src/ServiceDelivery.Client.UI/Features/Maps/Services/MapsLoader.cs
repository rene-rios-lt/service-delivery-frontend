using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.UI.Features.Maps.Services;

/// <summary>
/// Loads the Google Maps JavaScript SDK at runtime (FE-025). Reads the API key from
/// <see cref="IMapsKeyProvider"/>; on a non-blank key it injects the SDK <c>&lt;script&gt;</c> tag via the
/// <c>mapsLoader.js</c> module (with the <c>maps,marker</c> libraries) and returns an available result.
/// On a missing/blank key it injects nothing, logs a clear diagnostic, and returns an unavailable result —
/// so the app never crashes and FE-024's <c>GoogleMap</c> can render its placeholder. The loader does not
/// touch the DOM directly; all script injection lives in the JS module (single responsibility).
/// Implements <see cref="IMapsLoader"/> (FE-024) so the <c>GoogleMap</c> component depends on the
/// abstraction; the existing <see cref="LoadAsync"/> implementation is unchanged.
/// </summary>
public class MapsLoader : IMapsLoader
{
    private const string ModulePath = "./_content/ServiceDelivery.Client.UI/Features/Maps/mapsLoader.js";

    private const string MissingKeyDiagnostic =
        "Google Maps API key is missing or blank. Set GoogleMaps:ApiKey in appsettings.Local.json " +
        "(see docs/maps-api-key.md). The map will not load; the 'map unavailable' placeholder is shown instead.";

    private const string LoadFailedDiagnostic =
        "The Google Maps SDK script failed to load (check the API key is valid and the network is " +
        "reachable). The 'map unavailable' placeholder is shown instead.";

    private readonly IMapsKeyProvider _keyProvider;
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<MapsLoader> _logger;

    public MapsLoader(IMapsKeyProvider keyProvider, IJSRuntime jsRuntime, ILogger<MapsLoader> logger)
    {
        _keyProvider = keyProvider;
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    public async Task<MapsAvailability> LoadAsync()
    {
        var apiKey = _keyProvider.GetMapsApiKey();

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning(MissingKeyDiagnostic);
            return new MapsAvailability(false, MissingKeyDiagnostic);
        }

        try
        {
            var module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath);
            // loadSdk resolves on the SDK script's onload and rejects on onerror, so awaiting it here means
            // we only report the SDK available once it is genuinely loaded (FE-026 BLOCKED finding). A
            // rejection (bad/failed key, network failure) is surfaced as an unavailable result so GoogleMap
            // shows the placeholder rather than letting the exception crash the page.
            await module.InvokeVoidAsync("loadSdk", apiKey);
            return new MapsAvailability(true, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, LoadFailedDiagnostic);
            return new MapsAvailability(false, LoadFailedDiagnostic);
        }
    }
}
