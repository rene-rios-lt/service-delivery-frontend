// Google Maps map-instance interop module (FE-024). Imported on demand by GoogleMap.razor.cs via
// IJSObjectReference once MapsLoader (FE-025) reports the SDK is available. This module owns the
// google.maps.Map lifecycle and all overlay mutations — markers, polylines, panTo/setZoom, fitBounds —
// keyed by containerId so multiple GoogleMap components can coexist on one page. The C# side owns the
// Blazor lifecycle and the call surface; the DOM/SDK work lives here (single responsibility on each side).

// One entry per live map, keyed by the component's containerId. Each entry holds the google.maps.Map plus
// its overlay registries (markers / polylines keyed by the caller's id) so updates move an existing
// overlay rather than stacking duplicates, and removal/disposal can find and detach them cleanly.
const maps = new Map();

// Creates a google.maps.Map in the container div and registers it. mapId enables AdvancedMarkerElement,
// which is required for DOM-hosting markers (so each marker can carry a data-testid — see addOrUpdateMarker).
export function initMap(containerId, lat, lng, zoom) {
  const element = document.getElementById(containerId);
  if (element === null) {
    return;
  }

  const map = new google.maps.Map(element, {
    center: { lat, lng },
    zoom,
    mapId: "service-delivery-map",
    disableDefaultUI: false,
  });

  maps.set(containerId, { map, markers: new Map(), polylines: new Map() });
}

// Removes every overlay and drops the map entry so its handlers and DOM references are released — called
// from the component's DisposeAsync to avoid leaked handlers across navigation (AC-4).
export function disposeMap(containerId) {
  const entry = maps.get(containerId);
  if (entry === undefined) {
    return;
  }

  entry.markers.forEach((marker) => (marker.map = null));
  entry.polylines.forEach((polyline) => polyline.setMap(null));
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

// Adds or replaces a polyline overlay (keyed by id) drawn through the ordered points. The polyline's DOM
// element carries the data-testid hook (AC-5). Replacing an existing polyline detaches the old one first.
export function addOrUpdatePolyline(containerId, id, points, testId) {
  const entry = maps.get(containerId);
  if (entry === undefined) {
    return;
  }

  const path = points.map((p) => ({ lat: p.lat, lng: p.lng }));
  const existing = entry.polylines.get(id);
  if (existing !== undefined) {
    existing.setPath(path);
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
  polyline.set("testId", testId);
  entry.polylines.set(id, polyline);
}

// Removes the named polyline overlay from the map and the registry.
export function removePolyline(containerId, id) {
  const entry = maps.get(containerId);
  if (entry === undefined) {
    return;
  }

  const polyline = entry.polylines.get(id);
  if (polyline !== undefined) {
    polyline.setMap(null);
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
