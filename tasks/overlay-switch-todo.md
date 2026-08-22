# Tasks: Overlay switch — in-game button + pipe transport

Plan: [overlay-switch-plan.md](overlay-switch-plan.md) · Spec: [../docs/launcher/overlay-spec.md](../docs/launcher/overlay-spec.md)

Scope key: S = one sitting · M = a few · L = split it first.

## Wave 1 — button + pipe against the launcher host

- [x] **T0: Confirm code anchors** (read-only, no code change)
  - Pin file:line for: the host `OnGUI` chain and where `OverlaySettingsGui` is called; `OverlaySettings` toggle storage + how F7/F9 register; the Launcher's single toggle method behind `WM_HOTKEY` and the Overlay button; launcher shutdown/cleanup path (`ForceClose`); injector background-work convention (is there an existing worker/queue, or does this need its own task?).
  - Accept: every "Key facts" bullet in the plan is either confirmed with an anchor or corrected in the plan and spec. Any mismatch between docs and code is written down, not silently worked around.
  - **Done.** Three doc claims were wrong and are corrected in the spec: (a) `guard-secondary-no-unity` scans only `Core/Effects/Plugins` + `IEffectGrantPlugin` implementers, so it does **not** cover this code — the Unity-free state machine went to `FusionRpg.Core/Overlay/` instead, where the csproj has no UnityEngine reference; (b) there are **two** host `OnGUI` chains (BepInEx `RpgLoop.OnGUI`, Melon `OnGUI`), not one; (c) the injector had **no** prior background work (`Task.Run`/`Thread` absent), so the pipe worker is the first. Confirmed as documented: `OverlaySettingsGui` already admits Repaint+Layout+MouseDown+MouseUp (the interactive-IMGUI precedent), and `ToggleOverlayAsync()` / `HideOverlayToGame()` are the launcher's single toggle path.
  - Files: none changed. Scope: S.

- [x] **T1: Launcher pipe server** (spec §Transport)
  - `Services/OverlayPipeServer.cs` — async accept loop on `\\.\pipe\FusionRpg.Overlay`, one message per connection, reads one ASCII line, parses `toggle` / `show` / `hide` / `ping`, marshals to the UI thread, calls the **existing** toggle method. Unknown line → log + ignore. Start with the main window, stop in the same cleanup path that unregisters the hotkey.
  - `ping` answers availability only — it must not show, hide, or move the overlay.
  - Accept: `OverlayPipeServerTests` table-drives the parse (each verb, unknown verb, empty line, whitespace, oversize line, non-ASCII) with no throw; launcher suite green; launcher shutdown leaves no listening pipe.
  - **Done.** 22 parse tests + 3 real-pipe round-trip tests; launcher suite 126 -> 151 green. Pipe name is constructor-injectable so a round-trip test never collides with a live launcher. `Dispose` pokes the pipe with a local client because cancellation alone can leave `WaitForConnectionAsync` parked on Windows. Junk lines log their length only, never their content, so a sender cannot write into the launcher log.
  - Files: `src/FusionRpg.Launcher/Services/OverlayPipeServer.cs` (new), `MainWindow.xaml.cs`, `tests/FusionRpg.Launcher.Tests/OverlayPipeServerTests.cs` (new). Scope: M.

- [x] **T2: Injector switch core (Unity-free)** (spec §Transport, §In-game switch button)
  - `Overlay/OverlaySwitch.cs` — no Unity types. Owns: intent flag set by the click, 300 ms debounce, off-thread send (connect 250 ms timeout, write one line, dispose), `ping` probe on match start and every 30 s, cached reachability driving button visibility, one log line per reachability transition.
  - Clock and pipe I/O injected behind small interfaces so the state machine is testable without a real pipe or a real clock.
  - Accept: `OverlaySwitchTests` pins — two clicks inside 300 ms send once; a click after the window sends again; probe result cached until expiry; unreachable host hides the button; reachability flip logs exactly once per transition; no send path touches the calling thread with blocking I/O. `guard-secondary-no-unity` green.
  - **Done.** 16 tests green. Split as T0 required: `FusionRpg.Core/Overlay/OverlaySwitchState.cs` holds every decision (clock injected, no I/O); `FusionRpg.Injector/Hud/OverlaySwitch.cs` does only pipe I/O. Probe results come back through two volatile flags and are applied on the Unity thread, so the state object stays single-threaded. A failed toggle send also counts as a reachability answer, so the button hides without waiting out the 30 s interval.
  - Files: `src/FusionRpg.Core/Overlay/OverlaySwitchState.cs` (new), `src/FusionRpg.Injector/Hud/OverlaySwitch.cs` (new), `tests/FusionRpg.Core.Tests/Overlay/OverlaySwitchStateTests.cs` (new). Scope: M.

- [x] **T3: In-game button** (spec §In-game switch button)
  - `Overlay/OverlaySwitchGui.cs` — one `GUI.Button` with cached `GUIStyle` and cached `Rect`, drawn from the existing host `OnGUI` chain after `VfxDirector.Draw()` / beside `OverlaySettingsGui`. **No new host hook.** Runs on all event passes (interactive control — see spec on why the Repaint-only gate doesn't apply), allocates nothing per pass, resolves nothing per pass. Click → `OverlaySwitch` intent flag only.
  - Visibility: `OverlaySettings` toggle (default on) AND `OverlaySwitch` reachability. Corner placement clear of the card tray and shovel slot. No throw escapes `OnGUI`.
  - Accept: both loader hosts compile; core/guard suites green; button hidden with no launcher and when the settings toggle is off.
  - **Done.** Bottom-right corner, default button skin — IL2CPP does not expose the `GUIStyle` copy constructor (`new GUIStyle(GUI.skin.button)` fails to compile), so the cached-style plan became "no style at all", which is cheaper anyway. Rect recomputed only on resolution change. Both loader hosts compile.
  - Files: `src/FusionRpg.Injector/Hud/OverlaySwitchGui.cs` (new), `Hud/OverlaySettings.cs`, `Hud/OverlaySettingsGui.cs`, `Host/InjectorLoop.cs`, `GameHooks.cs`, `Injector.BepInEx/Plugin.cs`, `Injector.MelonLoader/MelonFusionRpgMod.cs`. Scope: S.

### Checkpoint 1
- [x] Core / Guard / Launcher suites green; `guard-secondary-no-unity` + `guard-single-writer` + `guard-funnel-delta` + `guard-dal` green; both loader hosts compile; `deploy-play.ps1 -NoServer` deploys clean.
- Measured 2026-08-22: Core **1840** (+16), Launcher **151** (+25), Guard **44**, all four guards OK, BepInEx + Melon builds clean, injector deployed to the game plugins folder.
- Two reds in the tree belong to **other streams**, not this one: `CheatCore DebugScenariosTests.No_unknown_step_names` (shield `debug.shield.demo-all` allowlist drift) and `Data WorldWaveOneAcceptanceTests.The_scenario_hashes_to_its_golden` (world-map wave-1 golden). Neither file is in this program's diff. Core also failed once transiently on a timing-sensitive test and passed 1840/1840 on two re-runs.

- [x] **T3b: Defect pass on the wave-1 code** (Prove-It, post-CP1)
  - Two real defects found by reviewing the shipped code and reproduced with failing tests before fixing:
    - **Second launcher spun the accept loop.** The pipe allows one server instance, so a second launcher's `NamedPipeServerStream` ctor throws — which the single generic catch treated as a connection error: log, wait 500 ms, retry, forever. Roughly two log lines a second for the whole session. Fix separates claiming the name from serving a connection: claim failure logs once, re-checks every 5 s quietly, and logs again only on takeover. Confirmed red by temporarily restoring the old behavior.
    - **A click during a slow send vanished and moved the window.** `TryClick` recorded the send time before the caller checked its in-flight flag, so a click arriving after the 300 ms window but during a send (the connect alone allows 250 ms) was dropped *and* reset the debounce — locking the player out for another 300 ms. Fix moves the in-flight gate into `OverlaySwitchState` ahead of the debounce record, with `MarkSendComplete()` driven from the injector tick.
  - Three existing tests were updated, not weakened: they chained clicks without completing a send, which the corrected model forbids.
  - Accept: Core **1864** green (20 overlay-state tests), Launcher **153** green (28 pipe tests), Guard 44, all four guards OK, both loader hosts compile.
  - Files: `src/FusionRpg.Core/Overlay/OverlaySwitchState.cs`, `src/FusionRpg.Launcher/Services/OverlayPipeServer.cs`, `src/FusionRpg.Injector/Hud/OverlaySwitch.cs`, both test files. Scope: S.

- [x] **T3c: Five-axis review of the wave-1 code** (post-CP1)
  - **Critical, fixed:** `StartOverlayPipe()` sat after the `if (_hwndSource == null) return;` early return in `InitOverlayHotKey`, so the in-game button was silently dead whenever the window handle was unavailable — precisely the case where the hotkey has also failed and the button is the only way in. Two unrelated concerns were sequenced together; the pipe now starts first.
  - **Important, fixed:** a client that connected and never wrote parked the single-instance listener forever, killing the button for the session (2 s read timeout added; confirmed red before the fix). `Start()` was not idempotent — a second call raced its own listener and reported it as a rival launcher (now `Interlocked`-guarded).
  - **Important, fixed:** the pipe name and verbs are duplicated across two assemblies that share no code, with nothing linking them — drift would silently hide the button with no error. `tests/FusionRpg.Guard.Tests/OverlayPipeContractGuardTests.cs` now pins both sides against each other and against the spec; proven by drifting the injector literal and watching it go red.
  - **Suggestion, spec corrected:** the ACL row claimed "creating user"; the Windows default also grants Everyone *read*. Not exploitable (the pipe is `PipeDirection.In`, so reading cannot send a command), but the doc was wrong.
  - **Suggestions:** three behaviour calls were raised here and resolved in T3d below.
  - Accept: Core **1865**, Launcher **155**, Guard **47** green; both loader hosts compile.
  - Files: `MainWindow.xaml.cs`, `Services/OverlayPipeServer.cs`, `tests/FusionRpg.Guard.Tests/OverlayPipeContractGuardTests.cs`, launcher tests, spec. Scope: S.

- [x] **T3d: Resolve the three open review suggestions** (post-review)
  - **Button gated to a live board.** `OverlaySwitchState.MatchActive` joins host-reachability and the player preference in `ButtonVisible`; `OnMatchStart` sets it, a new `OnMatchEnd` clears it from `GameHooks` where `MatchKey` is nulled. The corner was picked against the seed bank and shovel and was never checked against menu screens, so drawing it there risked stealing a menu click. Nothing is lost: the global hotkey still works everywhere.
  - **Button scales with the display.** Geometry moved to a pure `FusionRpg.Core/Overlay/OverlaySwitchLayout.cs` — 1× at 1080p, capped at 3×, never below 1×, always fully on screen even for a degenerate resolution. IMGUI works in device pixels, so the original fixed 72×28 was half the physical size on 4K. 12 tests.
  - **`show` / `hide` dropped.** Nothing ever sent them; a wire protocol with unreachable verbs is untested surface. Protocol is now `toggle` + `ping`; wave 2 adds what it actually calls. Spec and the `decisions.md` row follow.
  - Accept: overlay tests **115** green (Core), Launcher **155**, Guard **47**; all three projects build; injector redeployed.
  - Files: `src/FusionRpg.Core/Overlay/{OverlaySwitchState.cs,OverlaySwitchLayout.cs}`, `src/FusionRpg.Injector/Hud/{OverlaySwitch.cs,OverlaySwitchGui.cs}`, `src/FusionRpg.Injector/GameHooks.cs`, `src/FusionRpg.Launcher/{Services/OverlayPipeServer.cs,MainWindow.xaml.cs}`, Core + Launcher tests, spec, decisions. Scope: S.

- [ ] **T4: Live verification** — *owner-run, own terminal* (spec §Testing strategy, criteria 3–15)
  - Run all 15 checklist items (11 wave-1, 4 wave-2), including the 6 that have been outstanding since 2026-08-20 (overlay covers the game window, focus restore, session survives 3 toggles, Alt+F4 hides, shutdown leaves nothing, missing-runtime instructions) plus the 5 new ones (button toggles, no-launcher hides the button, launcher killed mid-match, debounce, frame-time parity).
  - Assistant does not start the play session: a server started from an assistant tool call dies with the tool call's process tree.
  - Accept: spec success criteria 3–15 flip from ⏳/⏹ to ✅ or to a recorded failure with the observed behavior. Frame-time parity backed by a perf probe capture, not by eye.
  - Files: `docs/launcher/overlay-spec.md` (status column). Scope: M.

- [x] **T5: Docs sync**
  - `docs/launcher/spec.md` §Game / web overlay — add the in-game button and the pipe to the player-facing summary.
  - `docs/README.md` — overlay row description picks up the button.
  - `docs/architecture/decisions.md` — the overlay switch row (added 2026-08-22) reads true against what shipped; correct it if T0–T4 changed anything.
  - Accept: no doc claims a status the code doesn't have; every new file in the spec's project-structure block exists at the stated path.
  - **Done.** `launcher/spec.md` gained the in-game button bullet and the pipe server in its code list; `README.md` row refreshed; the `decisions.md` row still reads true. Spec status moved to "wave 1 built, pending live verification"; criteria 10 and 11 are ✅ (unit), 12 and 13 still need the live run.
  - Files: as listed. Scope: S.

### Checkpoint 2 — wave 1 done
- [ ] Spec criteria 1–15 all ✅; docs sync green; button + hotkey + launcher button all reach the same toggle method.

## Wave 2 — injector-hosted view (gated)

- [~] **T6: Spike — WebView2 in the game process** (report only, no shipped code) — **desk half done, 3 questions need the game**
  - Answer all five spec questions: does the WebView2 loader initialize in an IL2CPP process; can a borderless window owned by the game HWND hold z-order and focus; does input routing survive alt-tab; does teardown run cleanly on game exit **and** on crash; behavior with no Evergreen runtime.
  - Accept: a findings note under `docs/research/` with a go / no-go per question. A single no-go on teardown or focus is a no-go overall — wave 1 stands and the spec records why.
  - **Done so far** — [../docs/research/2026-08-22-overlay-injector-host.md](../docs/research/2026-08-22-overlay-injector-host.md). A harness built against the injector's exact TFM (`net6.0`, not `-windows`) creates a real borderless `WS_POPUP` window via `user32` and attaches a `CoreWebView2Controller` to it, no WPF, no visible window. Q1 **partial go** (proven outside IL2CPP; BepInEx assembly + native loader resolution still open), Q4 **partial go** (clean `Close()` proven, crash path not), Q5 **go** (`WebView2RuntimeNotFoundException`, catchable). Q2/Q3 need the game. Cost: ~270–315 ms to stand up, so the view must be created once in the background, never lazily on the click.
  - **Design constraint found:** the WebView2 objects are apartment-bound. Reading `.Result` from a pool `ContinueWith` fails every call with `NotImplementedException: Unable to cast to ...ICoreWebView2Environment`, whose message blames version skew and sends you to the versioning docs — a false trail. Wave 2 must create *and* consume environment/controller/CoreWebView2 on one thread it owns and pumps, and that pump is a second one alongside Unity's.
  - **False alarm cleared:** the first failure looked like the Launcher's pinned SDK being incompatible with runtime 151. It was the harness. SDK **1.0.2903.40** re-tested against runtime **151.0.4129.93** passes all five steps — wave 1 is unaffected.
  - **Left for the owner:** five in-game observations listed at the end of the findings note, foldable into the T4 session. An orphaned `msedgewebview2.exe` after a game crash is a no-go.
  - Files: `docs/research/2026-08-22-overlay-injector-host.md` (new). Scope: M.

### Gate
- [x] Owner signed off 2026-08-22 ("start it") after being shown that 3 of 5 spike questions were unanswered; `decisions.md` row locking `overlayHost` semantics written first. The NuGet dependency in the game process — ask-first per the spec — is covered by that same instruction.

- [x] **T7: Wave 2 build** — code complete, **live-unverified**
  - `overlayHost` = `launcher` | `injector` (host config + `FUSIONRPG_OVERLAY_HOST`, env wins). With `injector`: local borderless window + WebView2 in the game process, button calls it directly, pipe client bypassed. Behavior contract rules 1–7 unchanged from the player's side. Default stays `launcher` until a live run says otherwise.
  - Accept: both host modes pass the same live checklist; flipping the default is a separate ask-first decision.
  - Split into five slices, each built and compiled green:
    - **T7a host selection** — `OverlayHostSelection` in Core, env beats config, unusable values fall through so an env typo cannot discard a config choice, default `launcher`. 16 tests.
    - **T7b config plumbing** — `OverlayHost` on `IRpgConfig`, bound in the BepInEx config and parsed from `fusionrpg.cfg` for Melon, resolved once in `RpgHost.Initialize` beside `ServerUrl`.
    - **T7c the view host** — `Hud/OverlayViewHost.cs` (owned STA thread + pump, environment/controller created and consumed in-apartment per the spike) and `Hud/Win32.cs` (all P/Invoke, mirroring the Launcher’s `GameWindowInterop` rule). Borderless `WS_POPUP` + `WS_EX_TOOLWINDOW`, positioned over the largest visible top-level window this process owns. Pre-loads `WebView2Loader.dll` by absolute path because the SDK imports it by bare name and the search path is rooted at the game exe, not our plugin folder.
    - **T7d button routing** — under `overlayHost=injector` there is no pipe, probe or wire debounce: a click is a queue push, and reachability becomes "the view came up". Leaving a match also hides the view so the web UI never sits over the menu.
    - **T7e teardown** — `OnApplicationQuit` in both hosts calls `OverlaySwitch.Shutdown()`.
  - **Payload trimmed:** the package wanted to ship WPF/WinForms wrappers, 815 KB of XML docs and arm64/x86 natives into the player’s game folder. Two MSBuild targets cut it to `Microsoft.Web.WebView2.Core.dll` + the x64 loader (~752 KB). Guard-pinned, as is `launcher` staying the default.
  - **What is NOT proven:** z-order over borderless-fullscreen, focus/input across alt-tab, and teardown after a *crash*. All three need the game; an orphaned `msedgewebview2.exe` is a no-go and would mean reverting to wave 1.
  - Accept (met): Core overlay **131**, Launcher **155**, Guard **50** green; four boundary guards OK; both loader hosts build; trimmed payload verified in the deployed plugin folder.
  - Files: `src/FusionRpg.Core/Overlay/OverlayHostSelection.cs`, `src/FusionRpg.Injector/Hud/{OverlayViewHost.cs,Win32.cs,OverlaySwitch.cs}`, `src/FusionRpg.Injector/Host/{IRpgConfig.cs,RpgHost.cs,FileRpgConfig.cs}`, both host projects + entry points, `tests/FusionRpg.Core.Tests/Overlay/OverlayHostSelectionTests.cs`, `tests/FusionRpg.Guard.Tests/OverlayPipeContractGuardTests.cs`. Scope: L.
