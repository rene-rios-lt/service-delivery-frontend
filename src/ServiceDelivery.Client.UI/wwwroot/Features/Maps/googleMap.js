// Google Maps map-instance interop module (FE-024). Imported on demand by GoogleMap.razor.cs via
// IJSObjectReference once MapsLoader (FE-025) reports the SDK is available. This module owns the
// google.maps.Map lifecycle and all overlay mutations — markers, polylines, panTo/setZoom, fitBounds —
// keyed by containerId so multiple GoogleMap components can coexist on one page. The C# side owns the
// Blazor lifecycle and the call surface; the DOM/SDK work lives here (single responsibility on each side).

// One entry per live map, keyed by the component's containerId. Each entry holds the google.maps.Map plus
// its overlay registries (markers / polylines keyed by the caller's id) so updates move an existing
// overlay rather than stacking duplicates, and removal/disposal can find and detach them cleanly.
const maps = new Map();

// Resolves once google.maps.importLibrary is actually wired up, polling up to ~5 s. MapsLoader.loadSdk
// resolves on the SDK bootstrap <script>'s onload, but with loading=async that onload can fire a tick
// BEFORE the bootstrap finishes defining google.maps.importLibrary. Calling importLibrary in that gap
// throws "google.maps.importLibrary is not a function" (a TypeError) — which surfaced INTERMITTENTLY as an
// unhandled Blazor circuit error and a blank map (FE-027 cycle-1/2 finding). The race is timing-sensitive,
// so it bit the read-only job-offer map more often than the active-job map, but it is not specific to
// gestureHandling. Polling for readiness here closes the gap deterministically for every consumer without
// swallowing a genuine failure: if the SDK never becomes ready within the budget we throw, so a real
// load/key problem is still reported rather than masked.
function whenImportLibraryReady() {
  return new Promise((resolve, reject) => {
    const start = Date.now();
    const tick = () => {
      if (typeof google !== "undefined" && google.maps &&
          typeof google.maps.importLibrary === "function") {
        resolve();
      } else if (Date.now() - start > 5000) {
        reject(new Error("google.maps.importLibrary did not become available within 5s."));
      } else {
        setTimeout(tick, 50);
      }
    };
    tick();
  });
}

// Creates a google.maps.Map in the container div and registers it. mapId enables AdvancedMarkerElement,
// which is required for DOM-hosting markers (so each marker can carry a data-testid — see addOrUpdateMarker).
// gestureHandling (FE-027) is the google.maps option that governs interactivity: 'none' makes the map
// read-only (the job-offer screen passes Interactive=false → 'none' so the rep cannot pan/zoom away from the
// requester pin during the countdown); 'auto' (the default for interactive maps) keeps full pan/zoom.
export async function initMap(containerId, lat, lng, zoom, gestureHandling) {
  const element = document.getElementById(containerId);
  if (element === null) {
    return;
  }

  // Close the loading=async readiness gap (FE-027) BEFORE touching google.maps.importLibrary, then await
  // both libraries. With loading=async the SDK bootstrap's script onload does NOT guarantee the `maps` /
  // `marker` classes are constructible yet — only awaiting google.maps.importLibrary(...) does. Awaiting
  // both libraries here before `new google.maps.Map(...)` is what stops the map being constructed against
  // an undefined class (which threw an unhandled Blazor error and left a collapsed grey box — FE-026).
  await whenImportLibraryReady();
  await google.maps.importLibrary("maps");
  await google.maps.importLibrary("marker");

  // FE-027 read-only: gestureHandling 'none' makes the map read-only (no pan / pinch-zoom / scroll-zoom /
  // double-click-zoom), which is exactly the approved checkpoint-#1 intent — the rep can't drag away from
  // the requester pin during the countdown. We keep disableDefaultUI:false (matching the interactive
  // active-job path) so gestureHandling is the only behavioural delta between the two maps; gestureHandling
  // alone fully delivers the read-only requirement.
  const map = new google.maps.Map(element, {
    center: { lat, lng },
    zoom,
    mapId: "service-delivery-map",
    gestureHandling: gestureHandling ?? "auto",
    disableDefaultUI: false,
  });

  maps.set(containerId, { map, markers: new Map(), polylines: new Map(), clickListener: null });
}

// FE-015 (tap-to-place-pin): registers a google.maps 'click' listener on the named map. Each click
// forwards the tapped lat/lng back into .NET via dotNetRef.invokeMethodAsync('OnMapClickedAsync', ...),
// where the GoogleMap component raises its OnMapClicked EventCallback. The listener handle is kept on the
// map entry so removeClickListener / disposeMap can detach it (no leaked handler across navigation).
export function addClickListener(containerId, dotNetRef) {
  const entry = maps.get(containerId);
  if (entry === undefined) {
    return;
  }

  entry.clickListener = entry.map.addListener("click", (event) => {
    dotNetRef.invokeMethodAsync("OnMapClickedAsync", event.latLng.lat(), event.latLng.lng());
  });
}

// FE-015: detaches the map-click listener registered by addClickListener, if any.
export function removeClickListener(containerId) {
  const entry = maps.get(containerId);
  if (entry === undefined || entry.clickListener === null) {
    return;
  }

  google.maps.event.removeListener(entry.clickListener);
  entry.clickListener = null;
}

// Removes every overlay and drops the map entry so its handlers and DOM references are released — called
// from the component's DisposeAsync to avoid leaked handlers across navigation (AC-4).
export function disposeMap(containerId) {
  const entry = maps.get(containerId);
  if (entry === undefined) {
    return;
  }

  // FE-015: detach the map-click listener too (if one was registered) so its handler is released.
  if (entry.clickListener !== null && entry.clickListener !== undefined) {
    google.maps.event.removeListener(entry.clickListener);
    entry.clickListener = null;
  }

  entry.markers.forEach((marker) => (marker.map = null));
  entry.polylines.forEach((overlay) => {
    overlay.polyline.setMap(null);
    overlay.testIdElement.remove();
  });
  entry.markers.clear();
  entry.polylines.clear();
  maps.delete(containerId);
}

// Adds a new pin or moves/recolours an existing one (keyed by id). Uses AdvancedMarkerElement with a custom
// teardrop element whose colour comes from the supplied design-system token; the element carries the
// data-testid hook so Playwright/Appium can locate the marker without touching the tile layer (AC-5).
export function addOrUpdateMarker(containerId, id, lat, lng, colour, testId) {
  const entry = maps.get(containerId);
  if (entry === undefined) {
    return;
  }

  const position = { lat, lng };
  const existing = entry.markers.get(id);
  if (existing !== undefined) {
    existing.position = position;
    existing.content = buildMarkerContent(colour, testId);
    return;
  }

  const marker = new google.maps.marker.AdvancedMarkerElement({
    map: entry.map,
    position,
    content: buildMarkerContent(colour, testId),
  });
  entry.markers.set(id, marker);
}

// Builds the marker's DOM content: a coloured pin carrying the data-testid attribute (AC-5).
function buildMarkerContent(colour, testId) {
  const pin = document.createElement("div");
  pin.className = "sd-map-marker";
  pin.style.width = "22px";
  pin.style.height = "22px";
  pin.style.borderRadius = "50% 50% 50% 0";
  pin.style.transform = "rotate(-45deg)";
  pin.style.background = colour;
  pin.style.border = "2px solid #fff";
  pin.style.boxShadow = "0 2px 6px rgba(20, 22, 40, .3)";
  pin.setAttribute("data-testid", testId);
  return pin;
}

// Removes the named marker from the map and the registry.
export function removeMarker(containerId, id) {
  const entry = maps.get(containerId);
  if (entry === undefined) {
    return;
  }

  const marker = entry.markers.get(id);
  if (marker !== undefined) {
    marker.map = null;
    entry.markers.delete(id);
  }
}

// Adds or replaces a polyline overlay (keyed by id) drawn through the ordered points. A google.maps.Polyline
// cannot host a DOM element the way an AdvancedMarkerElement can, so — unlike markers — its testId cannot
// live on the polyline itself. Instead an invisible <div> carrying the data-testid attribute is attached to
// the map container so Playwright/Appium CSS selectors (e.g. [data-testid='route-line']) can find the route
// without touching the SVG tile layer (FE-026 AC-6). The div is paired with the polyline in the registry so
// removePolyline / disposeMap can detach both. Replacing an existing polyline reuses its div and just
// repaths the line.
export function addOrUpdatePolyline(containerId, id, points, testId) {
  const entry = maps.get(containerId);
  if (entry === undefined) {
    return;
  }

  const path = points.map((p) => ({ lat: p.lat, lng: p.lng }));
  const existing = entry.polylines.get(id);
  if (existing !== undefined) {
    existing.polyline.setPath(path);
    return;
  }

  const polyline = new google.maps.Polyline({
    map: entry.map,
    path,
    geodesic: true,
    strokeColor: "#1E88E5",
    strokeOpacity: 0.85,
    strokeWeight: 4,
  });

  const testIdElement = document.createElement("div");
  testIdElement.setAttribute("data-testid", testId);
  testIdElement.style.display = "none";
  const container = document.getElementById(containerId);
  if (container !== null) {
    container.appendChild(testIdElement);
  }

  entry.polylines.set(id, { polyline, testIdElement });
}

// Removes the named polyline overlay from the map and the registry, detaching its testId div too.
export function removePolyline(containerId, id) {
  const entry = maps.get(containerId);
  if (entry === undefined) {
    return;
  }

  const overlay = entry.polylines.get(id);
  if (overlay !== undefined) {
    overlay.polyline.setMap(null);
    overlay.testIdElement.remove();
    entry.polylines.delete(id);
  }
}

// Pans the map centre to the supplied coordinates.
export function panTo(containerId, lat, lng) {
  const entry = maps.get(containerId);
  if (entry !== undefined) {
    entry.map.panTo({ lat, lng });
  }
}

// Sets the map zoom level.
export function setZoom(containerId, zoom) {
  const entry = maps.get(containerId);
  if (entry !== undefined) {
    entry.map.setZoom(zoom);
  }
}

// Frames all supplied points by extending a LatLngBounds and calling map.fitBounds.
export function fitBounds(containerId, points) {
  const entry = maps.get(containerId);
  if (entry === undefined || points.length === 0) {
    return;
  }

  const bounds = new google.maps.LatLngBounds();
  points.forEach((p) => bounds.extend({ lat: p.lat, lng: p.lng }));
  entry.map.fitBounds(bounds);
}
