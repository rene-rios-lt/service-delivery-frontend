# Service Delivery — Frontend

.NET MAUI Blazor Hybrid client for the Service Delivery system, targeting desktop, mobile, and web from a single codebase.

## Structure

```
src/
  ServiceDelivery.Client.Shared/   # Razor components, pages, view models — shared across all targets
  ServiceDelivery.Client.Maui/     # MAUI Blazor Hybrid host (desktop: Windows/macOS, mobile: iOS/Android)
  ServiceDelivery.Client.Web/      # Blazor WASM host (browser)
tests/
  ServiceDelivery.Client.Tests/    # Component and view model tests
```

## Getting Started

See the [service-delivery-central](https://github.com/rene-rios-lt/service-delivery-central) repo for scripts to run the full system locally.

## Targets

| Target | Project | Platforms |
|--------|---------|-----------|
| Desktop & Mobile | `ServiceDelivery.Client.Maui` | Windows, macOS, iOS, Android |
| Web | `ServiceDelivery.Client.Web` | Browser (WASM) |
