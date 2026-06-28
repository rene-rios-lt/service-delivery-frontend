using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.Interfaces;

/// <summary>
/// Loads the Google Maps JavaScript SDK at runtime and reports whether it is available (FE-025).
/// Extracted from the concrete <c>MapsLoader</c> (FE-024) so the <c>GoogleMap</c> component depends on
/// this abstraction rather than the concrete UI service (Dependency Inversion). The abstraction also lets
/// bUnit tests mock the unavailable branch (AC-6) without touching real configuration or the JS module.
/// </summary>
public interface IMapsLoader
{
    Task<MapsAvailability> LoadAsync();
}
