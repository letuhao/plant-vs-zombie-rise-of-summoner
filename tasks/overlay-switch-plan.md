# Plan: Overlay switch — in-game button + pipe transport

Spec: [../docs/launcher/overlay-spec.md](../docs/launcher/overlay-spec.md) (the contract; update it before changing behavior)
Related: [../docs/launcher/spec.md](../docs/launcher/spec.md) §Game / web overlay · [../docs/architecture/vfx-ssot.md](../docs/architecture/vfx-ssot.md) §8.3 (IMGUI rules) · [../docs/architecture/decisions.md](../docs/architecture/decisions.md) (cheats SSOT row bounds what the button may do)
Parallel rounds: `plan.md` / `todo.md` = perf; `effect-atom-*`, `world-map-*`, `battle-timeline-*` untouched by this stream.

## Context

The Launcher-hosted overlay shipped on 2026-08-20 and has sat at "pending live verification" since — criteria 3–9 have never been run. The only ways to reach it are a global F10 hotkey nobody discovers and a button in a launcher window the player isn't looking at.

This round adds the discoverable path: a small button drawn inside the game that toggles the same overlay, signalling the Launcher over a local named pipe. It also finally closes the live-verification debt, because a button is worthless if the overlay it opens was never proven to cover the game window.

Wave 2 — moving the view into the game process so the overlay works however the game was started — is scoped here but gated behind a spike. Nothing in wave 1 presumes wave 2 will pass.

## Dependency graph

```
T0 confirm anchors (read-only)
     ├──> T1 launcher pipe server ────┐
     └──> T2 injector switch core ────┤
                    └──> T3 in-game button
                                      ▼
                                     CP1 (unit + guards + smoke)
                                      ▼
                            T4 live verification (owner-run)
                                      ▼
                                T5 docs sync ──> CP2 = wave 1 done
                                      ▼
                            T6 wave 2 spike (report only)
                                      ▼
                        [gate: owner sign-off + decisions row]
                                      ▼
                                T7 wave 2 build
```

T1 and T2 are independent once T0 lands. T3 needs T2. T4 needs all three plus a running game — it is the owner's, not the assistant's (see Risks).

## Key facts — doc-sourced, confirm in T0

These come from the architecture docs, not from reading the code. T0 exists to turn each into a file:line anchor before anything is written.

- An in-game IMGUI surface already exists: host `OnGUI` runs `VfxDirector.Draw()` then `OverlaySettingsGui`, with `OverlaySettings` holding the F7/F9 presentation toggles (vfx-ssot §8.3). The button is a third item in that chain — **no new host hook**.
- `decisions.md` (cheats SSOT row) explicitly permits "a lightweight overlay settings panel for presentation toggles/hotkeys" while banning a second in-game cheats surface. A one-action show/hide button is inside that allowance; anything more is not.
- The Launcher's toggle already has one implementation behind `WM_HOTKEY` and the Overlay button (`MainWindow.xaml.cs`, `OverlayWindow`, `Services/GameWindowInterop.cs`). The pipe handler must call it, not re-implement it.
- The Launcher is **not** a SignalR client today — only the SPA is. That is why the transport is a pipe rather than a server relay.
- Injector shared host facade is `RpgHost` / `InjectorBootstrap` / `InjectorLoop` under `src/FusionRpg.Injector/Host/`, and hooks must not reference either loader. Both loader hosts compile the same sources, so the button and pipe client must be loader-agnostic.
- Main-thread stalls are the known lag source (2026-08 perf audit): per-hit `FindObjectsOfType` and uncached resolves. A blocking pipe connect on the Unity thread would be the same mistake in a new place.

## Risks

| Risk | Mitigation |
|---|---|
| Pipe connect blocks a frame when the launcher is absent | Connect off-thread with a 250 ms timeout; the click only sets a flag. Probe result cached, never per-frame. T2 unit-pins the state machine |
| Interactive IMGUI can't be Repaint-gated, so it costs every event pass | Exactly one `GUI.Button`, cached `GUIStyle` + `Rect`, no allocation. Criterion 13 measures it against button-hidden |
| Button eats a gameplay click near the card tray or shovel | Fixed corner placement chosen away from both; live checklist item 7 plays a real match with it on |
| Live verification never happens again | T4 is its own task with the full 11-item checklist, and CP2 does not close without it |
| Assistant-started server dies with its tool call (two prior incidents) | T4 is **owner-run** from their own terminal. The assistant may prepare, but does not launch the play session |
| Wave 2 looks like reversing the 2026-08-20 rejection | Spec states the difference explicitly: separate top-level window in the game process ≠ web UI rendered inside Unity as a texture. Spike answers five questions before any build |
| Button drifts into a second cheats menu | Spec boundary "never"; any second action is ask-first |

## Out of this round

Hotkey settings UI, overlay opacity / click-through, remembering the last overlay page, pausing the match while the overlay is up (open question in the spec — needs an answer before it can be a task), per-monitor DPI work.
