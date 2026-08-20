# Spec: Game / web overlay (Launcher WebView2)

Status: **implemented, pending live verification** (see §Success criteria). This document is the contract for the feature — update it before changing behavior.

## Objective

Players flip between PVZ Fusion and the RPG web UI instantly, without alt-tabbing or a second monitor. One hotkey shows the web control room exactly covering the game window; the same hotkey (or Esc) returns to the game with focus restored — a Steam-overlay-style experience.

Why it lives in the Launcher: the game is Unity 2022.3 IL2CPP with no web view, and embedding a browser in-process via mods (e.g. UnityWebBrowser) requires the game's own Unity project plus a ~150 MB Chromium payload and carries high crash risk. The Launcher is already WPF, already supervises the game + server processes, and already knows the server URL. Decision recorded here; alternatives rejected in the 2026-08-20 investigation.

User story: *while defending the lawn I press F10, equip a specimen on `#/roster`, press F10, and I'm back in the game — the SPA never reloaded and the game never lost my match.*

## Tech stack

- `FusionRpg.Launcher` — net8.0-windows WPF (WPF-UI 3.0.5)
- `Microsoft.Web.WebView2` 1.0.2903.40 (Evergreen Runtime on the player machine; never bundled)
- Win32 via P/Invoke (`user32`): `RegisterHotKey`, `GetWindowRect`, `SetWindowPos`, `SetForegroundWindow`, `ShowWindow`, `IsIconic`

## Commands

```powershell
dotnet build src\FusionRpg.Launcher                 # build
dotnet test tests\FusionRpg.Launcher.Tests          # unit tests (includes hotkey parsing)
.\scripts\publish-player.ps1                        # player zip (self-contained)
.\scripts\deploy-play.ps1                           # dev loop: guards → build → server → game
```

## Project structure (files owned by this feature)

```
src/FusionRpg.Launcher/
  Services/GameWindowInterop.cs   → Win32 layer: hotkey register/parse, game window find/rect/focus
  OverlayWindow.xaml(.cs)         → borderless topmost WebView2 window (hide-not-destroy)
  MainWindow.xaml(.cs)            → hotkey hook (WM_HOTKEY), Overlay button, shutdown cleanup
  Services/LauncherSettings.cs    → overlayHotKey setting (WPF Key name, null = F10)
tests/FusionRpg.Launcher.Tests/
  GameWindowInteropTests.cs       → ParseOverlayKey table tests
docs/launcher/spec.md             → player-facing behavior summary (§Game / web overlay)
```

## Code style

Match the existing Launcher services: sealed/static service classes, `Try*` methods returning bool, swallow-and-log around process/window races, XML doc-comments only where the code can't say it. P/Invoke signatures stay private inside `GameWindowInterop`; the rest of the launcher never sees a raw HWND except via that class.

## Behavior contract

1. **Toggle** — hotkey (default `F10`, `MOD_NOREPEAT`, registered on the main window) or the **Overlay** button:
   - Overlay hidden → position over the game window rect (`GetWindowRect` → `SetWindowPos`, topmost), show, activate, focus WebView2. No game window found → maximized fallback.
   - Overlay visible → hide + `SetForegroundWindow(game)`.
2. **Session preservation** — toggling *hides*, never destroys, the WebView2 window: the SPA keeps its SignalR connection and page state. Alt+F4 on the overlay also hides. Real destruction only on launcher shutdown (`ForceClose`).
3. **URL** — active server URL from the play session; falls back to `LastPort` from settings; no URL → log "press Play first", no window.
4. **Hotkey config** — `overlayHotKey` in `%AppData%\FusionRpg\launcher.json` (any WPF `Key` name; unknown/modifier-only names fall back to F10). Registration failure (key owned by another app) logs and leaves the button as the path.
5. **WebView2 Runtime missing** — overlay shows install instructions (developer.microsoft.com link) instead of a blank window; "Open RPG UI" in a normal browser remains the fallback. User data dir: `%LocalAppData%\FusionRpg\webview2` (never next to the exe).
6. **Esc** and the in-overlay "Back to game" button behave exactly like the hotkey's hide path.
7. **Exclusive fullscreen fallback** — if the game is the foreground window in exclusive-fullscreen D3D mode (`SHQueryUserNotificationState` = `QUNS_RUNNING_D3D_FULL_SCREEN`), topmost windows can't cover it: the launcher minimizes the game and the overlay opens maximized as a normal window switch. Toggling back restores + refocuses the game (minimized-window rects are ignored by the positioner). Logged with a hint that borderless mode gives the seamless toggle.

## Testing strategy

- **Unit (CI):** pure logic only — hotkey-name parsing (`GameWindowInteropTests`, table-driven). Win32 and WebView2 paths are not unit-tested (no game window / runtime in CI).
- **Live checklist (dev machine, once per release):**
  1. `deploy-play.ps1` → Play → in-game press F10 → overlay covers the game window exactly; UI is the lawn control room.
  2. F10 / Esc → back in game, game has focus and input.
  3. Toggle twice more → SPA did not reload (Log page ring buffer keeps its entries).
  4. Close overlay with Alt+F4 → hides, game focused; F10 brings it back instantly.
  5. Stop all in launcher → no orphaned overlay window, hotkey unregistered (F10 does nothing).
  6. Negative: rename the WebView2 runtime check (or test on a machine without it) → instruction text shows, no crash.

## Boundaries

- **Always:** run launcher unit tests before handing back; keep all Win32 inside `GameWindowInterop`; keep the overlay hide-not-destroy invariant; log every failure path (no silent no-ops).
- **Ask first:** changing the default hotkey; adding a settings UI; bundling/auto-installing the WebView2 Runtime; making the overlay pause or send input to the game; new NuGet dependencies.
- **Never:** touch the game process/window beyond focus + rect reads; inject into the game for overlay purposes; ship an embedded browser inside the game folder; auto-close the game.

## Success criteria

| # | Criterion | Status |
|---|---|---|
| 1 | Build clean; all launcher tests pass (126, 11 new) | ✅ verified |
| 2 | Hotkey parse: F-keys accepted, junk/modifiers → F10 | ✅ verified (unit) |
| 3 | F10 in-game shows overlay over the game window ≤ ~1 s (after first init) | ⏳ live run |
| 4 | Toggle back restores game focus, game input works immediately | ⏳ live run |
| 5 | SPA session (SignalR, page state) survives ≥ 3 toggles | ⏳ live run |
| 6 | Missing WebView2 Runtime → instructions shown, no crash | ⏳ live run |
| 7 | Launcher shutdown leaves no overlay window or registered hotkey | ⏳ live run |
| 8 | Works with the game in borderless-fullscreen (Unity 2022 default) | ⏳ live run |
| 9 | Exclusive fullscreen → game minimized, overlay opens maximized, toggle-back restores the game | ⏳ live run |

## Resolved decisions (2026-08-20)

1. **Exclusive fullscreen** → fallback to window switch (behavior rule 7), not just a documented limitation.
2. **Default hotkey** → stays `F10` (JSON-configurable; F8 reserved by injector cheat conventions).
3. **WebView2 Runtime missing** → install link + instructions only; a one-click bootstrapper waits for a real player report.

## Out of scope v1

Hotkey settings UI, overlay opacity/click-through mode, remembering last overlay page, per-monitor DPI edge cases beyond `SetWindowPos` device pixels.
