using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.Interfaces;

/// <summary>
/// Reads the device's current GPS position (FE-015 AC-1, "Use my current location"). A focused,
/// platform-specific capability (Interface Segregation): the Web host wraps the browser geolocation
/// API; the MAUI hosts wrap <c>Microsoft.Maui.Devices.Sensors.Geolocation</c>. Returns <c>null</c> when
/// the device cannot supply a position (permission denied or hardware unavailable) so the caller can
/// degrade gracefully rather than crash — an explicit "no position" outcome, not a thrown exception.
/// </summary>
public interface IGeolocationService
{
    Task<GpsPoint?> GetCurrentLocationAsync();
}
