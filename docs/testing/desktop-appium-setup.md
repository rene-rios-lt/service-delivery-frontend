# Desktop (Mac Catalyst) Appium setup

The Desktop end-to-end suite (`tests/ServiceDelivery.Client.Appium.Mac`,
`DispatcherFleetMapDesktopTests`) drives the MAUI Blazor Hybrid **Desktop** host
(`ServiceDelivery.Client.Desktop`, Mac Catalyst) as a black box through Appium's **mac2**
driver, asserting on the **native macOS accessibility (AX) tree** — the mac2 driver has no
WebView context, so there are no CSS/`data-testid` selectors here (unlike the iOS suite).

It runs via `scripts/local/test-appium-mac.sh` (standalone), and — when the prerequisites below
are present — automatically as the Desktop phase of `scripts/local/test-e2e.sh` and
`scripts/local/test-all.sh`.

## One-time prerequisites

1. **Appium + the mac2 driver** (macOS only):
   ```bash
   npm install -g appium
   appium driver install mac2
   ```
2. **macOS privacy grants** (System Settings → Privacy & Security):
   - **Accessibility** → enable the app that launches the tests (e.g. **Terminal**, or your IDE).
     Required — mac2 automates the AX tree through the Accessibility API.
   - **Screen Recording** → enable **Terminal** — required **only** if you capture screenshots
     via `SD_SHOT_DIR`; the tests themselves do not need it.

No iOS simulator is involved. The runner builds the Desktop app itself
(`dotnet build src/ServiceDelivery.Client.Desktop -f net10.0-maccatalyst -c Debug`) and mac2
launches the resulting `.app` bundle (`com.companyname.servicedelivery.client.desktop`).

## Running it

```bash
# Desktop suite alone (brings backend up backend-only, builds Desktop, starts Appium, runs the suite)
./scripts/local/test-appium-mac.sh

# As part of the E2E suite (Playwright web + iOS Appium + Desktop)
./scripts/local/test-e2e.sh

# As part of the complete suite (unit + integration + all E2E)
./scripts/local/test-all.sh
```

In `test-e2e.sh` / `test-all.sh` the Desktop phase is **gated** by `sd_desktop_enabled`
(`scripts/utils/test-report.sh`): it runs only when the `mac2` driver is installed and
`SD_SKIP_DESKTOP` is unset. When it is skipped, the results table shows a dimmed **`n/a`**
Desktop row rather than a failure, so machines without the Desktop toolchain (web/mobile-only)
still get a green suite. A genuine Desktop test failure still reds the row and fails the run.

## Environment knobs

| Variable | Default | Purpose |
|----------|---------|---------|
| `SD_SKIP_DESKTOP` | unset | Set to `1` to force-skip the Desktop phase in `test-e2e.sh` / `test-all.sh` even when mac2 is installed (renders the `n/a` row). |
| `APPIUM_BASE_URL` | `http://localhost:5180` | Backend base URL the suite seeds against. |
| `APPIUM_SERVER_URL` | `http://localhost:4723` | Appium server URL (the runner starts one on this port if needed). |
| `APPIUM_DISPATCHER_PASSWORD` | `Password123!` | Seeded dispatcher (`alex@dealer.com`) password. |
| `SD_AX_DUMP` | unset | Set to `1` to dump the macOS AX tree on failure for debugging. |
| `SD_SHOT_DIR` | unset | Directory to save full-window screenshots (needs the Screen Recording grant). |

## Notes

- The runner brings the backend up **backend-only** (`SD_SKIP_SIMULATOR=1`) so the test's
  `BackendApiHelper` is the sole position source; it reuses an already-running backend and only
  tears down what it started.
- Before each run it wipes persisted app preferences
  (`defaults delete com.companyname.servicedelivery.client.desktop`) so the app cold-starts
  unauthenticated — a persisted valid token used to land on a blank `/` page (BUG-050, fixed).
- The suite opens **one** shared mac2 session per fixture (launching the Desktop app is slow) and
  pins the implicit wait to zero, driving every lookup through an explicit bounded poll — see the
  class comments in `tests/ServiceDelivery.Client.Appium.Mac/MacDesktopTestBase.cs`.
