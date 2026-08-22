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

- [~] **T4: Live verification** — **run 1 confirmed working 2026-08-22 by the owner**; remaining items pending — *owner-run, own terminal* (spec §Testing strategy, criteria 3–15)
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

- [x] **T7b-review: Self-review of the wave-2 code** (post-T7)
  - **Showstopper, fixed:** *the view could not be closed.* It covers the game, so the in-game button — the only source of `Toggle` — sits underneath it. Wave 1 has Esc, WPF chrome and a launcher-registered global hotkey; the in-game host inherits none, and `WS_EX_TOOLWINDOW` keeps it out of alt-tab too. Opening it would have meant killing the game. Esc/F10 now close it via `AcceleratorKeyPressed`.
  - **Important, fixed:** a topmost, non-alt-tabbable window left up while the player switches apps would cover *that* app unreachably — it now auto-hides when the game stops being the foreground process.
  - **Important, fixed:** the view navigated once at game start, which can beat the server to listening, leaving a permanent error page; a failed load now retries on next open while a loaded page is never re-navigated.
  - **Important, fixed:** `Shutdown()` was fire-and-forget on a background thread, so the process could exit before teardown — exactly how an orphaned `msedgewebview2.exe` happens. Now joins for up to 2 s.
  - **Minor, fixed:** the pump spun at 200 Hz regardless of visibility (now 5 ms visible / 50 ms hidden) and had no guard, so one throw killed the view for the session.
  - Rules that decide whether a player can get back to their game live in `FusionRpg.Core/Overlay/OverlayViewPolicy.cs` (11 tests) rather than buried in Win32 code, and a guard pins the escape path in place — proven by removing it and watching the guard go red.
  - Accept: Core overlay **142**, Launcher **155**, Guard **51** green; four boundary guards OK; both loader hosts build; redeployed.
  - Files: `src/FusionRpg.Core/Overlay/OverlayViewPolicy.cs`, `src/FusionRpg.Injector/Hud/{OverlayViewHost.cs,Win32.cs}`, `tests/FusionRpg.Core.Tests/Overlay/OverlayViewPolicyTests.cs`, `tests/FusionRpg.Guard.Tests/OverlayPipeContractGuardTests.cs`. Scope: M.

- [x] **T7c-test: Prove-It on the send path** (post-review)
  - **Defect:** `NamedPipeClientStream.Write` has no timeout. `Connect` was bounded at 250 ms but the write was not, so a launcher whose reader stalled mid-connection would park the injector's background thread forever — the completion `finally` never ran, `SendInFlight` never cleared, and the in-game button was dead for the rest of the session with no recovery short of restarting the game.
  - **Fixed at both levels:** the write now runs on an asynchronous pipe under a 1 s cancellation, so the thread cannot leak; and `OverlaySwitchState` treats a send older than `SendTimeoutMs` (3 s) as abandoned, so even an unforeseen stall cannot hold the gate shut. A late completion from the abandoned send cannot cancel a newer one.
  - Confirmed red before the fix by removing the recovery line and watching the test fail, then green after.
  - Accept: Core overlay **146** (4 new), Launcher **155**, Guard **51** green; four boundary guards OK; both loader hosts build.
  - Files: `src/FusionRpg.Core/Overlay/OverlaySwitchState.cs`, `src/FusionRpg.Injector/Hud/OverlaySwitch.cs`, `tests/FusionRpg.Core.Tests/Overlay/OverlaySwitchStateTests.cs`. Scope: S.

- [x] **T7d-review: Five-axis review of the wave-2 code** (post-test)
  - **Important, fixed — user-data folder collision.** The in-game view used `%LocalAppData%\FusionRpg\webview2`, byte-identical to the Launcher’s. A WebView2 user-data folder cannot be shared across processes, so with both hosts running (exactly the configuration on this dev box: launcher up from `deploy-play`, injector mode set) the second one fails to initialise. Now `webview2-game`.
  - **Important, fixed — no navigation boundary.** The view would load *any* URL the page navigated to, inside the game process, and an external link in the SPA would open there rather than in the player’s browser. `NavigationStarting` now cancels off-origin, `NewWindowRequested` never opens a second in-process window, and off-origin links go to the real browser — but only `http(s)` reaches `Process.Start`, since `UseShellExecute` would otherwise run the registered handler for `file:`, `javascript:` or an app scheme.
  - **Important, fixed — browser process for a disabled feature.** The view span up on the first tick regardless of the button toggle; with the button off there is no other way to open it in this mode, so it was a whole WebView2 process tree for nothing.
  - **Suggestion, not taken:** `Hud/Win32.cs` does not mirror `GameWindowInterop` in name despite the spec saying it does. Renaming touches every call site for no behaviour change; noted rather than churned.
  - Verified: 97 overlay tests green (run in isolation — see below), injector + Melon build, redeployed, four boundary guards OK.
  - **Tree note:** `FusionRpg.Core.Tests` and `FusionRpg.Guard.Tests` were both transiently uncompilable during this pass from other streams’ in-flight files (`World/Ai/PolicySeamTests.cs`, `Effects/Atoms/PredicateCompiler.cs`, `WorldDeterminismGuardTests.cs`). Overlay tests were therefore run through a throwaway project compiling only `src/FusionRpg.Core/Overlay/*.cs` + `tests/.../Overlay/*.cs`; Guard was last green in place at 51/51.
  - Files: `src/FusionRpg.Core/Overlay/OverlayViewPolicy.cs`, `src/FusionRpg.Injector/Hud/{OverlayViewHost.cs,OverlaySwitch.cs}`, `tests/FusionRpg.Core.Tests/Overlay/OverlayViewPolicyTests.cs`. Scope: M.

- [x] **T8: Pause while away** (owner request, 2026-08-22 — closes the spec's open question)
  - Opening the control room mid-wave was costing runs. A live board now holds still while the player is not looking at it.
  - **Signal:** the game window is not the foreground process, or (wave 2) the in-game view is visible. The launcher's F10 never reaches the injector, so foreground is the only signal covering every entry point — hotkey, in-game button, launcher button — and it covers plain alt-tab too, which is the same situation.
  - **Single writer preserved.** `CheatActions.TickContinuous` already asserts `Time.timeScale` every frame for G-TIMEFREEZE / G-TIMESCALE, so a naive `Time.timeScale = 0` here would have been overwritten on the next frame whenever a speed setting was active. `OverlayPause` decides and remembers; `CheatActions` applies it with priority and hands back the captured scale on resume. Guard-pinned.
  - Resume restores the *captured* scale, not a hardcoded 1.0, so a player's own timescale survives; a captured 0/NaN resumes at 1.0 rather than leaving the game stuck. Cleared on board end and shutdown.
  - Player toggle "Pause while away" in the F7 panel, default on.
  - **Guard bug found and fixed while writing it:** the first version of the single-writer guard was vacuous — the shell had turned `` into a literal backspace byte inside the C# regex, so it matched nothing and passed with a second writer deliberately injected. Rewritten via direct file write, then proven by re-injecting the writer and watching it go red. The guard now also asserts it scanned >50 files and still sees the owner's writes, so it cannot silently watch nothing again.
  - Accept: 109 overlay tests green (12 new pause-policy), Guard **54**, both loader hosts build, injector redeployed.
  - Files: `src/FusionRpg.Core/Overlay/OverlayPausePolicy.cs`, `src/FusionRpg.Injector/Hud/{OverlayPause.cs,OverlaySwitch.cs,OverlaySettings.cs,OverlaySettingsGui.cs}`, `src/FusionRpg.Injector/CheatActions.cs`, `tests/FusionRpg.Core.Tests/Overlay/OverlayPausePolicyTests.cs`, `tests/FusionRpg.Guard.Tests/OverlayPipeContractGuardTests.cs`. Scope: M.

- [x] **T9: Deploy to the 3.9 MelonLoader cell** (owner request, 2026-08-22)
  - Deployed to `H:\Games\PVZ-Fusion-3.9_MelonLoader\Mods` — profile `pvzrh-3.9` (GameAssembly matches the documented 57717248 signature), MelonLoader only, no BepInEx present so no dual-load risk.
  - **Gap this exposed:** the WebView2 package and trim targets had been added to the BepInEx and 3.8.1 MelonLoader hosts but **not** to `FusionRpg.Injector.MelonLoader.39`, which compiles the same shared sources. That cell would have failed to compile the moment anyone built it. Fixed, and the guard now **discovers** injector hosts by their shared-source glob rather than listing them, so the next matrix cell cannot be missed the same way.
  - **The guard was vacuous twice before it worked.** First it asserted `Contains("Microsoft.Web.WebView2")`, which stays true with the package removed because the trim targets name the WPF/WinForms assemblies. Now it matches the `PackageReference` itself; proven by removing the package and watching it go red.
  - Accept: 3.9 host builds clean; `Mods\` carries `Microsoft.Web.WebView2.Core.dll` + flat `WebView2Loader.dll`, no `runtimes\` tree; Guard **54** green.
  - Files: `src/FusionRpg.Injector.MelonLoader.39/FusionRpg.Injector.MelonLoader.39.csproj`, `tests/FusionRpg.Guard.Tests/OverlayPipeContractGuardTests.cs`. Scope: S.

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

- [x] **T7: Wave 2 build** — **opening verified live 2026-08-22**: the in-game button showed the web view over the game on 3.9 MelonLoader with no launcher, so z-order holds and the spike’s Q2 is answered. Alt-tab focus and crash teardown still unverified.
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
