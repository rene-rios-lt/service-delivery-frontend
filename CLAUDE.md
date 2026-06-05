# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

This is the frontend repository for the Service Delivery system. It uses .NET MAUI Blazor Hybrid to share a single Razor UI codebase across desktop (Windows/macOS), mobile (iOS/Android), and web (Blazor WASM).

## Commands

```bash
# Build all
dotnet build

# Run the web target (browser)
dotnet run --project src/ServiceDelivery.Client.Web

# Run the MAUI app (macOS desktop)
dotnet run --project src/ServiceDelivery.Client.Maui -f net10.0-maccatalyst

# Run the MAUI app (iOS simulator)
dotnet run --project src/ServiceDelivery.Client.Maui -f net10.0-ios

# Run the MAUI app (Android emulator)
dotnet run --project src/ServiceDelivery.Client.Maui -f net10.0-android

# Run tests
dotnet test

# Run a single test by name
dotnet test --filter "FullyQualifiedName~YourTestName"
```

## Architecture

All UI lives in `ServiceDelivery.Client.Shared` (Razor Class Library). The MAUI and Web projects are thin hosts that reference Shared and provide platform-specific bootstrapping only.

```
ServiceDelivery.Client.Shared  ← all components, pages, view models, service interfaces
        ↑                              ↑
ServiceDelivery.Client.Maui    ServiceDelivery.Client.Web
(desktop + mobile host)        (browser WASM host)
```

- **Shared** (`src/ServiceDelivery.Client.Shared`) — Razor components, pages, view models, and service interfaces. All new UI work goes here.
- **Maui** (`src/ServiceDelivery.Client.Maui`) — MAUI Blazor Hybrid shell. Platform-specific DI registration and native integrations only. Do not add UI logic here.
- **Web** (`src/ServiceDelivery.Client.Web`) — Blazor WASM shell. Mirrors Maui host for the browser. Do not add UI logic here.

## Key Conventions

- New pages and components go in `Shared/Pages/` and `Shared/Components/` respectively.
- Service interfaces are defined in `Shared/Services/` and injected via DI in each host project.
- View models go in `Shared/ViewModels/` and are registered as scoped services.
- Platform-specific service implementations (camera, file system, push notifications) live in the `Maui` project and are not referenced by `Shared`.
