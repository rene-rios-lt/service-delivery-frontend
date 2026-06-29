// Google Maps JavaScript SDK loader (FE-025). Imported on demand by MapsLoader.cs via IJSRuntime;
// MapsLoader only calls loadSdk when a non-blank API key is present, so the SDK is never requested
// without a key. This module owns all DOM work (creating and appending the <script> tag) — the C#
// loader owns the key validation and diagnostics. Single responsibility on each side of the boundary.

const SCRIPT_ID = "google-maps-sdk";

// Builds the SDK <script> tag, appends it to <head>, and resolves only once the script has actually
// loaded. The URL requests the `maps` and `marker` libraries (the two FE-024's map needs) and uses
// loading=async per Google's current loading guidance. Returning a Promise that resolves on the script
// tag's `onload` (and rejects on `onerror`) is the contract that keeps the awaiting MapsLoader.LoadAsync
// from reporting the SDK available before `google.maps` exists — without this the GoogleMap component
// would call `new google.maps.Map(...)` against an undefined SDK and throw (FE-026 BLOCKED finding).
// Idempotent: if the tag already exists the SDK is left in place and the Promise resolves immediately.
export function loadSdk(apiKey) {
  if (isSdkLoaded()) {
    return Promise.resolve();
  }

  return new Promise((resolve, reject) => {
    const script = document.createElement("script");
    script.id = SCRIPT_ID;
    script.async = true;
    script.src =
      "https://maps.googleapis.com/maps/api/js" +
      "?key=" + encodeURIComponent(apiKey) +
      "&libraries=maps,marker" +
      "&loading=async";
    script.onload = () => resolve();
    script.onerror = () => reject(new Error("Failed to load the Google Maps SDK script."));
    document.head.appendChild(script);
  });
}

// True once the SDK <script> tag has been injected. FE-024 can poll this before using google.maps.
export function isSdkLoaded() {
  return document.getElementById(SCRIPT_ID) !== null;
}
