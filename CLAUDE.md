# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

This is the frontend repository for the Service Delivery system. It uses .NET MAUI Blazor Hybrid and Blazor WASM to share a single Razor UI codebase across Desktop, Mobile, and Web from distinct host projects.

## Commands

```bash
# Build individual projects
dotnet build src/ServiceDelivery.Client.Core
dotnet build src/ServiceDelivery.Client.UI
dotnet build src/ServiceDelivery.Client.Web

# Run the web target (browser)
dotnet run --project src/ServiceDelivery.Client.Web

# Run the desktop app (macOS)
dotnet run --project src/ServiceDelivery.Client.Desktop -f net10.0-maccatalyst

# Run the mobile app (Android emulator)
dotnet run --project src/ServiceDelivery.Client.Mobile -f net10.0-android

# Run the mobile app (iOS simulator)
dotnet run --project src/ServiceDelivery.Client.Mobile -f net10.0-ios

# Run all tests
dotnet test

# Run a single test by name
dotnet test --filter "FullyQualifiedName~YourTestName"
```

## Architecture

```
Core  ←  UI  ←  Desktop   (macOS, Windows)
                 Mobile    (iOS, Android)
                 Web       (Browser WASM)
```

- **Core** (`src/ServiceDelivery.Client.Core`) — `net10.0` class library. No Razor, no UI framework dependency. Contains `Models/`, `Interfaces/` (service contracts), and `ViewModels/`. This project has zero project references — it depends on nothing.
- **UI** (`src/ServiceDelivery.Client.UI`) — `net10.0` Razor Class Library. All pages and components live here, organized by feature under `Features/<FeatureName>/Pages/` and `Features/<FeatureName>/Components/`. Cross-feature components go in `Shared/Components/`. Layout in `Layout/`. Exposes a single `Routes.razor` entry point consumed by all hosts. References Core only.
- **Desktop** (`src/ServiceDelivery.Client.Desktop`) — MAUI Blazor Hybrid host for macOS and Windows. Targets `net10.0-maccatalyst` and `net10.0-windows10.0.19041.0`. Contains bootstrapping (`MauiProgram.cs`) and macOS/Windows-specific native service implementations only.
- **Mobile** (`src/ServiceDelivery.Client.Mobile`) — MAUI Blazor Hybrid host for iOS and Android. Targets `net10.0-android` and `net10.0-ios`. Contains bootstrapping and iOS/Android-specific native service implementations (camera, GPS, push notifications) only.
- **Web** (`src/ServiceDelivery.Client.Web`) — Blazor WASM host. `App.razor` delegates entirely to `<ServiceDelivery.Client.UI.Routes />`. Contains bootstrapping and browser-specific service implementations only.

## Platform Targets

| Project | Platforms | Min OS |
|---------|-----------|--------|
| Desktop | macOS (Catalyst), Windows | macOS 15.0+, Windows 10.0.17763.0+ |
| Mobile  | iOS, Android | iOS 15.0+, Android API 24+ |
| Web     | Browser (WASM) | — |

## Test-Driven Development

TDD is mandatory in this repo. No production code is written without a failing test first.

### The Cycle

```
Red   → Write a failing test that describes the behaviour you want
Green → Write the minimum production code to make it pass
Refactor → Clean up without breaking the tests
```

Never write production code speculatively. If there is no failing test, there is no code to write.

### Test Projects and What They Cover

| Project | What to test | Tools |
|---------|-------------|-------|
| `Client.Tests` | Component behaviour, view model logic, service interface contracts | xUnit, bUnit |

All tests live in `tests/ServiceDelivery.Client.Tests`. Host projects (Desktop, Mobile, Web) are bootstrapping only — they contain no logic and require no tests.

### Test Naming

Use the `Given_When_Then` convention:

```csharp
// Good
public void GivenACounter_WhenIncrementIsCalled_ThenCountIncreasesBy1()

// Also acceptable for simpler cases
public void Counter_AfterOneClick_CountIsOne()
```

### Test Structure — Arrange / Act / Assert

Every test must have clearly separated sections:

```csharp
[Fact]
public void GivenACounter_WhenButtonIsClicked_ThenDisplayUpdates()
{
    // Arrange
    var cut = Render<Counter>();

    // Act
    cut.Find("button").Click();

    // Assert
    cut.Find("p[role=status]").MarkupMatches("<p role=\"status\">Current count: 1</p>");
}
```

### Layer-Specific TDD Rules

- **Core (ViewModels)** — Write the view model test first. View models are pure C# — no bUnit needed, just xUnit.
- **Core (Interfaces)** — Interfaces are contracts. Write a test against the interface before implementing it in a host's `Services/` folder.
- **UI (Components)** — Write the bUnit component test before writing the Razor markup. The test defines what the component should render and how it should respond to interaction. Use `Render<T>()` from `BunitContext`.
- **Services** — Mock service interfaces in component tests. Never let a component test reach a real HTTP client or native API.

### What Not to Test

- MAUI bootstrapping (`MauiProgram.cs`, `App.xaml.cs`) — framework wiring, not your logic
- Razor layout structure (`MainLayout.razor`) unless it contains conditional logic
- Static markup with no state or interaction

## SOLID Principles

All additions and modifications to this repo must follow these principles. They are mapped directly to the project structure so there is no ambiguity about where code belongs.

### S — Single Responsibility
Each project has exactly one job:
- **Core** = contracts and logic
- **UI** = Razor markup and components
- **Hosts** (Desktop, Mobile, Web) = bootstrapping only

Each Razor component should do one thing. If a component fetches data **and** renders it, split the data access into a service interface in `Core/Interfaces/` and keep the component focused on display.

### O — Open/Closed
- Add new features by creating new files under `UI/Features/<FeatureName>/` — never by modifying existing unrelated feature folders.
- Extend service behavior by adding new implementations in host `Services/` folders, not by modifying existing interfaces in Core.

### I — Interface Segregation
- Define small, focused interfaces in `Core/Interfaces/` — one interface per logical capability (e.g. `IAuthService`, `ITicketService`).
- Avoid catch-all interfaces. A component that only needs to read tickets should not depend on an interface that also creates and deletes them.

### D — Dependency Inversion
- UI components depend on interfaces from Core, never on concrete implementations.
- Concrete service implementations live in each host's `Services/` folder and are registered in `MauiProgram.cs` (Desktop/Mobile) or `Program.cs` (Web).
- Core and UI never reference host projects — the dependency always flows inward.

### Dependency Direction (enforced by project references)
```
Core          → no references
UI            → Core only
Desktop       → UI, Core
Mobile        → UI, Core
Web           → UI, Core
Tests         → UI, Core
```
Any code that would require violating this graph belongs in a different layer.

## Key Conventions

- New pages → `UI/Features/<FeatureName>/Pages/`
- New components → `UI/Features/<FeatureName>/Components/` (or `UI/Shared/Components/` if cross-feature)
- Service interfaces → `Core/Interfaces/`
- View models → `Core/ViewModels/`
- Native service implementations → `Desktop/Services/`, `Mobile/Services/`, or `Web/Services/`
- Register services in `MauiProgram.cs` (Desktop/Mobile) or `Program.cs` (Web) — never in Core or UI
