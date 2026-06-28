namespace ServiceDelivery.Client.Core.Models;

/// <summary>
/// A latitude/longitude coordinate (FE-024). The shared shape for polyline points and the
/// <c>fitBounds</c> point set on the <c>GoogleMap</c> component's public API; serialised to the JS
/// module as a <c>{ lat, lng }</c> object. Lives in Core (no UI dependency) so future map consumers
/// (FE-026, FE-027, FE-003) can use it without referencing the UI layer.
/// </summary>
public record GpsPoint(double Lat, double Lng);
