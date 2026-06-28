namespace ServiceDelivery.Client.Core.Interfaces;

/// <summary>
/// Supplies the Google Maps JavaScript API key for the current host (FE-025). Each host
/// (Web, Mobile, Desktop) provides its own implementation that reads <c>GoogleMaps:ApiKey</c>
/// from <c>IConfiguration</c> — the key is never hardcoded or committed (the real value lives in a
/// gitignored <c>appsettings.Local.json</c>; the committed <c>appsettings.json</c> carries an empty
/// placeholder). The UI <c>MapsLoader</c> depends on this abstraction, never on a concrete host class.
/// Synchronous because both MAUI and WebAssembly <c>IConfiguration</c> resolve values synchronously.
/// </summary>
public interface IMapsKeyProvider
{
    string? GetMapsApiKey();
}
