# Google Maps API Key Configuration

This document describes how the Google Maps JavaScript SDK key is configured for the Service Delivery
frontend (FE-025). It covers the config key name, where to obtain a key, how the key is kept out of source
control, and the API-key restriction posture for local development and (future) production.

## Config key name

The key is read from host configuration under:

```
GoogleMaps:ApiKey
```

All three hosts (Web, Mobile, Desktop) register the same reader (`ConfigurationMapsKeyProvider` in the UI
project), which pulls `GoogleMaps:ApiKey` from `IConfiguration`. The key is **never** hardcoded in source.

## Where to obtain a key

1. Open the [Google Cloud Console](https://console.cloud.google.com/).
2. Create (or select) a project.
3. Enable the **Maps JavaScript API** for the project (APIs & Services → Library → Maps JavaScript API).
4. Enable **billing** on the project (the Maps JavaScript API requires an active billing account, even
   within the free monthly usage allowance).
5. Go to **APIs & Services → Credentials → Create credentials → API key** and copy the generated key.

## Where the key lives (and what is committed)

| File | Committed? | Contents |
|------|-----------|----------|
| `src/ServiceDelivery.Client.Web/wwwroot/appsettings.json` | Yes | Placeholder only — `GoogleMaps:ApiKey` is an **empty string** |
| `appsettings.Local.json` (each host) | **No — gitignored** | The real key: `{ "GoogleMaps": { "ApiKey": "AIza..." } }` |

`appsettings.Local.json` is listed in `.gitignore`, so the real key never enters source control. A clean
checkout starts with the empty placeholder; the map degrades gracefully to its "map unavailable"
placeholder (FE-024) and logs a clear diagnostic until a real key is supplied locally (AC-3).

The MAUI hosts (Mobile, Desktop) do not load `appsettings.json` by default; FE-025 adds an embedded
`appsettings.json` (with `appsettings.Local.json` layered on top when present) to `builder.Configuration`
so `ConfigurationMapsKeyProvider` can read `GoogleMaps:ApiKey` on those hosts too.

## API key restriction posture

### Local development

- **API restrictions:** restrict the key to the **Maps JavaScript API** only.
- **Application restrictions:** **None.**

  This is deliberate for the POC: with no application restriction the same key works in both the Web host
  (browser origin) and the iOS `BlazorWebView` (which presents an `app://`-style origin, not a normal HTTP
  referrer — see ADR-0010, "Google Maps for map visualization", in the central governance repo). It
  avoids the BlazorWebView origin caveat entirely during local development.

### Production hardening (future)

For a hardened production deployment, tighten the key:

- **iOS (Mobile host):** apply an **iOS app** application restriction using the bundle identifier
  `com.companyname.servicedelivery.client.mobile` (the `<ApplicationId>` from
  `src/ServiceDelivery.Client.Mobile`). This is the restriction recorded for the MAUI `BlazorWebView` host.
- **Web host:** apply an **HTTP referrer** application restriction scoped to the web host's deployed origin.
- Keep the **Maps JavaScript API**-only API restriction in all environments.

## Verifying the live iOS render (AC-4 live-verify)

Confirming the map actually renders inside the iOS `BlazorWebView` requires a real key and a running map
screen. There is no map screen yet in FE-025 (the `GoogleMap` component arrives in FE-024/FE-026), so the
on-device render confirmation is deferred to whichever of those stories first puts a map on screen. At that
point, place a real key in a gitignored `appsettings.Local.json`, run `scripts/local/startInPhone.sh`, and
confirm the map tiles render inside the iOS Mobile host.
