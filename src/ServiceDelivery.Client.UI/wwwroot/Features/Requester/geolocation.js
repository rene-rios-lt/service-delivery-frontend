// Browser geolocation interop module (FE-015 AC-1, "Use my current location"). Imported on demand by the
// Web host's BrowserGeolocationService via IJSRuntime. Wraps the callback-based
// navigator.geolocation.getCurrentPosition in a Promise that resolves to a { lat, lng } object the .NET
// side deserializes into a GpsPoint, or rejects on error/denial so the host service can map that to null
// (an explicit "no position" outcome — the submit form degrades gracefully rather than crashing).

export async function getCurrentPosition() {
  if (!("geolocation" in navigator)) {
    throw new Error("Geolocation is not supported by this browser.");
  }

  return await new Promise((resolve, reject) => {
    navigator.geolocation.getCurrentPosition(
      (position) => resolve({ lat: position.coords.latitude, lng: position.coords.longitude }),
      (error) => reject(new Error(error.message || "Unable to read the device location.")),
    );
  });
}
