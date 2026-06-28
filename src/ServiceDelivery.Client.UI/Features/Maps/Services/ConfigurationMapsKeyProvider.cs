using Microsoft.Extensions.Configuration;
using ServiceDelivery.Client.Core.Interfaces;

namespace ServiceDelivery.Client.UI.Features.Maps.Services;

/// <summary>
/// Reads the Google Maps API key from <see cref="IConfiguration"/> under the <c>GoogleMaps:ApiKey</c> key
/// (FE-025). Host-agnostic: every host (Web, Mobile, Desktop) populates <see cref="IConfiguration"/> from
/// its own source — Web from <c>wwwroot/appsettings.json</c> + env, the MAUI hosts from an embedded
/// <c>appsettings.json</c> (real key in a gitignored <c>appsettings.Local.json</c>) — then registers this
/// single reader. The key is never hardcoded; this class only reads the configured value (single
/// responsibility — the SDK loading concern lives in <see cref="MapsLoader"/>).
/// </summary>
public class ConfigurationMapsKeyProvider : IMapsKeyProvider
{
    private readonly IConfiguration _configuration;

    public ConfigurationMapsKeyProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string? GetMapsApiKey() => _configuration["GoogleMaps:ApiKey"];
}
