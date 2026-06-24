# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

This is the frontend repository for the Service Delivery system. It uses .NET MAUI Blazor Hybrid and Blazor WASM to share a single Razor UI codebase across Desktop, Mobile, and Web from distinct host projects.

## System Context

This frontend serves three personas for a fleet dispatch system — "Uber for service reps." The user's **role** (from their JWT) determines which view they see. The **platform** (Desktop, Web, Mobile) determines the layout. **Each persona is supported only on a subset of platforms** — see the Supported Platforms column below.

| Role | Supported Platforms | View |
|------|--------------------|------|
| Dispatcher | Desktop, Web | Fleet command center — live map, request queue, redirect controls |
| ServiceRep | Mobile | Take over an idle vehicle (pick from dropdown, supersede the simulator), job offers (accept/decline), active job, mark complete |
| Requester | Desktop, Web, Mobile | Submit request, Uber-like rep tracking, redirect notifications |

The backend communicates over REST and SignalR. Vehicle positions update every 3 seconds. Google Maps is used for all map views.

## UI Framework

**MudBlazor** is the component library for all Razor components (see ADR-0007 in `service-delivery-central`).

- Add the `MudBlazor` NuGet package to `ServiceDelivery.Client.UI` and `ServiceDelivery.Client.Web`
- Register MudBlazor services in `MauiProgram.cs` (Desktop/Mobile) and `Program.cs` (Web)
- **Load MudBlazor's static assets in *every* host's `wwwroot/index.html`** — Web, Desktop, *and* Mobile each ship their own host page. Each must `<link>` `_content/MudBlazor/MudBlazor.min.css` (+ the Roboto font) and `<script src="_content/MudBlazor/MudBlazor.min.js">`. Registering services and adding `<MudThemeProvider>` is **not** enough — without the stylesheet every `Mud*` component renders as unstyled HTML. (This gap caused BUG-020 on Web and BUG-022 on Desktop/Mobile — fix one host, fix all three.)
- Use MudBlazor primitives (`MudCard`, `MudChip`, `MudBadge`, `MudDataGrid`, etc.) in preference to plain HTML elements
- Define a `MudTheme` that maps domain colours to theme tokens:
  - **Rep-state markers:** Green = Available, Blue = En Route, Yellow = Within 15 Miles, Red = On Site, Grey = Offline
  - **Tier badges:** Bronze, Silver, Gold
- Express all styling through MudBlazor theme tokens and component parameters first; add raw CSS only when no component parameter covers the need
- Never introduce a second component library alongside MudBlazor

## Required Reading Before Implementing

Read these docs before building any view or component. They are the authoritative specification for what each persona sees and how each flow works.

- [`docs/persona-views.md`](docs/persona-views.md) — exact screen-by-screen spec for all 3 personas across all states
- [`docs/ux-flows.md`](docs/ux-flows.md) — all 5 end-to-end flows written from each persona's perspective

For the backend contract (API endpoints, SignalR hub events, and data shapes), refer to `docs/api-design.md` in the backend repo.

## Implementing Stories

Stories for this repo (`FE-001` through `FE-023`) are implemented using the Master agent in `service-delivery-central`. Invoke it with the story ID:

```
/master FE-007
```

The agent creates a feature branch, runs the full TDD pipeline (evaluate → plan → implement → AI review → PR), and pauses at two human checkpoints. Never implement a story by writing code directly without the agent — TDD discipline and SOLID checks are enforced through that pipeline.

### Audit Files (`.stories/`)

During story execution the agent writes ephemeral working files to `.stories/<STORY-ID>/` in this repo. These files are gitignored and deleted at the start of each new run — they are session-scoped working memory for the pipeline, not source files. Do not create or commit anything under `.stories/`.

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
| `Client.E2E` | Web/Desktop persona flows against a live system (Dispatcher, Requester, ServiceRep-web) | Playwright — run via `scripts/local/test-e2e.sh` |
| `Client.Appium` | Mobile ServiceRep flows on an iOS simulator against a live system | Appium/XCUITest — run via `scripts/local/test-appium.sh` |

All unit/bUnit tests live in `tests/ServiceDelivery.Client.Tests`. E2E projects (`Client.E2E`, `Client.Appium`) require a running backend (`start.sh`) and, for Appium, a booted iOS simulator. Host projects (Desktop, Mobile, Web) are bootstrapping only — they contain no logic and require no tests.

### Test Naming

Use the `Given_When_Then` convention for all test method names:

```csharp
public void GivenACounter_WhenIncrementIsCalled_ThenCountIncreasesBy1()
public void GivenADispatcherView_WhenFleetMapLoads_ThenVehicleMarkersAreVisible()
public void GivenAJobOffer_WhenCountdownExpires_ThenOfferStateIsExpired()
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

### L — Liskov Substitution
- Every service implementation in a host's `Services/` folder must fully honour the contract defined in the `Core/Interfaces/` interface it implements — no silent no-ops, no partial implementations, no methods that throw `NotImplementedException`.
- If a platform cannot support a capability, model that explicitly (return a typed `Unsupported` result or a null-object) rather than silently breaking the contract.

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
