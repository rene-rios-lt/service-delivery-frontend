# Service Delivery — Frontend

.NET MAUI Blazor Hybrid client for the Service Delivery system, targeting Desktop, Mobile, and Web from a single shared codebase.

## Structure

```
src/
  ServiceDelivery.Client.Core/     # Pure C# — models, service interfaces, view models
  ServiceDelivery.Client.UI/       # Razor Class Library — all pages and components
  ServiceDelivery.Client.Desktop/  # MAUI Blazor Hybrid host (macOS, Windows)
  ServiceDelivery.Client.Mobile/   # MAUI Blazor Hybrid host (iOS, Android)
  ServiceDelivery.Client.Web/      # Blazor WASM host (browser)
tests/
  ServiceDelivery.Client.Tests/    # xUnit tests for Core and UI
```

## Dependency Flow

```
Core  ←  UI  ←  Desktop   (macOS, Windows)
                 Mobile    (iOS, Android)
                 Web       (Browser WASM)
```

All UI lives in `UI`. Host projects (`Desktop`, `Mobile`, `Web`) are thin bootstrappers — they contain no UI logic.

## Implementing Stories

Stories are implemented using the Master agent in the central repo. Invoke it with a frontend story ID:

```
/master FE-001
```

The agent runs the full TDD pipeline (evaluate → plan → implement → AI review → review → PR) with two human checkpoints. See [service-delivery-central](https://github.com/rene-rios-lt/service-delivery-central) for the full agent system documentation.

## Getting Started

See the [service-delivery-central](https://github.com/rene-rios-lt/service-delivery-central) repo for scripts to run the full system locally.

To run the web target directly:

```bash
dotnet run --project src/ServiceDelivery.Client.Web
```

## Platform Targets

| Project | Platforms | Min OS |
|---------|-----------|--------|
| Desktop | macOS (Catalyst), Windows | macOS 15.0+, Windows 10.0.17763.0+ |
| Mobile  | iOS, Android | iOS 15.0+, Android API 24+ |
| Web     | Browser (WASM) | — |
