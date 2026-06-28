// Google Maps JavaScript SDK loader (FE-025). Imported on demand by MapsLoader.cs via IJSRuntime;
// MapsLoader only calls loadSdk when a non-blank API key is present, so the SDK is never requested
// without a key. This module owns all DOM work (creating and appending the <script> tag) — the C#
// loader owns the key validation and diagnostics. Single responsibility on each side of the boundary.

const SCRIPT_ID = "google-maps-sdk";

// Builds the SDK <script> tag and appends it to <head>. The URL requests the `maps` and `marker`
// libraries (the two FE-024's map needs) and uses loading=async per Google's current loading guidance.
// Idempotent: if the tag already exists the SDK is left in place rather than re-injected.
export function loadSdk(apiKey) {
  if (isSdkLoaded()) {
    return;
  }

  const script = document.createElement("script");
  script.id = SCRIPT_ID;
  script.async = true;
  script.src =
    "https://maps.googleapis.com/maps/api/js" +
    "?key=" + encodeURIComponent(apiKey) +
    "&libraries=maps,marker" +
    "&loading=async";
  document.head.appendChild(script);
}

// True once the SDK <script> tag has been injected. FE-024 can poll this before using google.maps.
export function isSdkLoaded() {
  return document.getElementById(SCRIPT_ID) !== null;
}
