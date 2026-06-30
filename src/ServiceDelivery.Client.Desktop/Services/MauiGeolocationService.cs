using Microsoft.Maui.Devices.Sensors;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Desktop.Services;

/// <summary>
/// Desktop (MAUI) <see cref="IGeolocationService"/> (FE-015 AC-1). Reads the device position via the
/// MAUI <see cref="Geolocation"/> sensor API. Returns a <see cref="GpsPoint"/> on success; returns
/// <c>null</c> when the platform cannot supply a position (permission denied, location off, or
/// unsupported) — an explicit "no position" outcome that fully honours the contract (no silent no-op).
/// </summary>
public class MauiGeolocationService : IGeolocationService
{
    public async Task<GpsPoint?> GetCurrentLocationAsync()
    {
        try
        {
            var location = await Geolocation.GetLocationAsync(
                new GeolocationRequest(GeolocationAccuracy.Medium));

            return location is null ? null : new GpsPoint(location.Latitude, location.Longitude);
        }
        catch (Exception)
        {
            // Permission denied / location disabled / unsupported on this device. Surface "no position"
            // so the submit form keeps working — the requester can still drop a pin on the map (AC-1).
            return null;
        }
    }
}
