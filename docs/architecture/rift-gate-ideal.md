# Rift Gate — the ideal

**Status:** idea phase **complete 2026-09-15** — all open questions answered, no blocker. Still not a
spec and no build authorized; the next step is `/spec`.
**Supersedes nothing**; it audits the direction recorded in [ideas/rift-gate.md](../ideas/rift-gate.md)
against shipped code and turns it into the input its own §Next extension asked for.
**Session boundary:** `tasks/sessions/rift-gate-idea-20260915-b7e4.json` (docs-only fence).

**This is an extension of a shipped, live-verified, release-packaged feature — not a new transport.**
The WebView2 overlay (launcher-hosted and injector-hosted) is built, owner-tested working, and already
carried by the player release; the packaging is probe-gated. The Gate adds a *menu affordance* on top
of that existing overlay and, separately, a first-open capture path. Treat the transport, the host
selection, the show/hide lifecycle, the release payload and its guards as **done and out of scope** —
this doc should never trigger a re-litigation of them.

This doc restates the principles it depends on inline, on purpose. A downstream session reads this
page, not its links.

---

## Which loop this extends

**None of its own — and that is the first thing to get right.** The Rift Gate adds no loop, no
resource, no reward, no progression. It is an **access affordance**: the in-game doorway to the
control room that already hosts the loops.

The loops it serves, read from [the-loops.md](../guide/the-loops.md) in this session:

- **Places 1 — Lawn (first core).** The only entry that exists today is in-match
  (`OverlaySwitchState.ButtonVisible` requires a live board). The Gate extends the lawn place by
  making the control room reachable from the lawn's *own menus*, not only mid-wave.
- **Spine A/B/C and Places 2–7** are all **web** features today. The Gate is their access surface,
  not a re-implementation of any of them.

Two rules from that page bind here:

- *"A later feature implements or extends one or more of these loops. It does not invent a parallel
  pitch."* — the Gate is transport/menu chrome, never a new pitch.
- *"Do not make the lawn the whole game."* — and, from
  [the-game.md:52](../guide/the-game.md), *"Unlocked web features stay playable with the lawn game
  closed once you have opened them — nothing essential stays lawn-only forever."* **The Gate must
  never become the only doorway.** Standalone browser play stays a first-class path; the Gate is
  additive convenience. This is `decisions.md` **Standalone-first (capability)** and DESIGN-GATE §2
  invariant 9, which is a capability and CI rule — not permission to re-pitch the product.

**Phase split (a real finding).** This program straddles two idea phases. The **system half** —
overlay transport, F10 ownership, menu anchors, first-open capture — is generic `/idea` material and
lives in this doc. The **FE half** — the "Leave the Rift" control and the first-open preparation
progress surface — is **player-facing chrome on a game surface**, so per DESIGN-GATE §1 and the
idea-phase skill it must go through **`/idea-ui`** (`docs/architecture/idea-ui-phase.md`), not a page
CSS pass. Do not carry this doc straight into a FE spec without that.

---

## What this is, in the player's language

On the PVZ main menu there is a **cracked tombstone** drawn in the menu's own art style. Click it (or
press **F10**) and the **web FE** opens over the game. Press Esc, F10, or the FE's own **Leave**
control and you are back in exactly the PVZ view you left. The web app was never reloaded and your
match never noticed.

**Naming rule (owner, 2026-09-15): the web FE never gets a name — it is "the web FE" in docs and in
player copy. The word "Rift" is deliberately reserved for a later mechanism and must not become the
FE's name.** This doc's filename keeps `rift-gate` for continuity with the idea it audits; that is a
path, not player copy. The bridge verb `rift.hide` (decided below) is internal protocol naming and
must never surface as a label the player reads.

**The first time**, the tombstone is a scene rather than a screen: the four-beat **story scene** plays
while behind it the game quietly reads its Almanac. See "The first-open fusion" below and the separate
`story-scene` sub-program it splits into.

The Gate is a **show/hide surface, not a browser lifecycle** — which is already true of the shipped
overlay (`docs/launcher/overlay-spec.md` §Behavior contract rule 2, "toggling *hides*, never
destroys"). The Gate changes **where the affordance lives** (the PVZ menu, not only the match) and
**what the first open prepares** (Almanac capture) — it does not change how the overlay shows or
hides.

---

## What already exists

Sorted into **built / wiring gap / real gap**. A default-off toggle, a null delegate, a debug-only
entry point, or a missing argument at a call site is a **wiring gap** — not an architectural limit.

### Built

| Thing | Evidence | What proves it |
|---|---|---|
| **The overlay itself: built, live-verified by the owner, and shipped in the release** | `src/FusionRpg.Launcher/OverlayWindow.xaml.cs`; `src/FusionRpg.Injector/Hud/OverlayViewHost.cs`; packaging probe `src/FusionRpg.Launcher/Services/PlayerPackProbe.cs:95-159` | WebView2 works in both host modes (owner-tested). The release is **probe-gated**: `ProbeOverlayPayload` fails a release when the launcher lacks its own WebView2, when any injector drop is missing WebView2 beside it, when an injector carries no overlay types (stale drop), or when no MelonLoader drop exists. `release.yml:126-128` ships the overlay payload and states the MelonLoader/dual-host position |
| Overlay show/hide with hide-not-destroy, covering the game window, focus restore | `src/FusionRpg.Launcher/OverlayWindow.xaml.cs:29-48,57-88` | Shipped, live-verified (overlay-spec header: "wave 1 built and live-verified 2026-08-22") |
| Injector-hosted view (`overlayHost=injector`), separate top-level HWND, own STA pump, same-origin lock, auto-hide on focus loss | `src/FusionRpg.Injector/Hud/OverlayViewHost.cs:82-209,229-256` | Built; z-order live-verified 2026-08-22 (overlay-spec criterion 19) |
| **F10 opens the overlay from the main menu** — launcher mode | `src/FusionRpg.Launcher/MainWindow.xaml.cs:270` (global `RegisterHotKey`), handler `:301-308`, and `ToggleOverlayAsync` `:313-352` gates only on *a server URL* (`:322-327`) — never on a board | **Built.** The hotkey is global on the launcher HWND; the toggle has no match gate |
| **F10 opens the overlay from the main menu** — injector mode | `OverlaySwitch.cs:113-117` starts the view on the first tick, board-independent; `OverlayViewHost.cs:186` registers its own F10; `:339-341` handles `WM_HOTKEY` | **Built.** Registered before any board exists |
| Main-menu Rift *artwork* (portal + mini icon, aspect-safe, responsive, cached texture) | `src/FusionRpg.Injector/Hud/RiftMenuOverlay.cs:20-43,97-133`; layout `src/FusionRpg.Core/Overlay/RiftMenuOverlayLayout.cs:44-67` | Built and unit-covered (`tests/FusionRpg.Core.Tests/Overlay/RiftMenuOverlayLayoutTests.cs`) |
| One convergence point for every show/hide entry (hotkey, pipe, button) | `MainWindow.xaml.cs:286-299` (pipe → `ToggleOverlayAsync`, "Same toggle path as the hotkey — no second behavior" `:277`), `:301-308`; `OverlayViewHost.cs:65-67,235-241` | Built. "No entry point gets its own behavior" holds today |
| The in-match corner entry | `src/FusionRpg.Injector/Hud/OverlaySwitchGui.cs:19-44` | Built; guard-pinned as one-action in-match chrome |
| Durable, non-reward story ledger + the onboarding prologue that reuses this art | `src/FusionRpg.Data/Sqlite/RpgStore.Onboarding.cs`, `OnboardingEndpoints.cs`, `web/.../features/onboarding/RiftPrologueDialog.tsx` | Built (commit `71a28b28`) — a **separate program**; see "What this is not" below |
| Media provenance registry `rift_asset_sources` (schema + upsert/get) | `src/FusionRpg.Data/Sqlite/RpgStore.cs:926-939`; `RpgStore.Icons.cs:26,40,60-68,99` | Schema and accessors exist and validate `pvz_dump` keys against `type_icon_layers` |
| Present-only dump listings and per-item cache probes | `src/FusionRpg.Server/Program.cs:1131,1134,1141,1148`; `RpgStore.Icons.cs:126,195` | Built |
| Cache-aware upload (skip if the server already has the row) | `src/FusionRpg.Injector/RpgClient.cs:286-292` (icons), `:251-256` (text) | Built — this is the resume mechanism that already exists |

**The load-bearing correction.** The overlay is **shipped, working and release-packaged**; the Rift
Gate's *doorway* is ~80% shipped and its *art* is shipped. The idea's own framing ("a first-open
cached capture path that replaces player-facing debug/almanac-dump workarounds") is the part that is
mostly missing — **not** the overlay, and **not** F10. Nothing in this doc is a reason to touch the
overlay transport, the host selection, the release payload or its probes.

### Wiring gap (machinery exists and is inert)

| Gap | The inert line | Why it is not a wall |
|---|---|---|
| The main-menu Rift is **paint only** — no click path | `RiftMenuOverlay.cs:25` gates on `EventType.Repaint` only; the file contains no `GUI.Button`, no `MouseDown/MouseUp`, no hit-test (its own doc, `:7-9`: *"has no input"*; layout doc `RiftMenuOverlayLayout.cs:3`: *"non-interactive"*) | The interactive idiom already exists in the same `OnGUI` chain: `OverlaySwitchGui.cs:26-42` accepts exactly `Repaint/Layout/MouseDown/MouseUp` and calls `GUI.Button`. The tombstone needs that event filter and a hit rect, not a new draw surface |
| **No FE → host message channel of any kind** | Exhaustive negative: no `WebMessageReceived` in `src/`; no `postMessage`/`window.chrome`/host object in `web/fusion-rpg-web/src`; the only WebView2 subscriptions are `AcceleratorKeyPressed`/`NavigationStarting`/`NewWindowRequested`/`NavigationCompleted` (`OverlayViewHost.cs:140,153,164,174`) and the launcher subscribes none (`OverlayWindow.xaml.cs:69-70,88`) | WebView2 ships the primitive (`WebMessageReceived` + `postMessage`). This is a wiring gap: the host-side handler and a small validated verb vocabulary do not exist yet |
| The **only** existing entry is board-gated | `OverlaySwitchState.cs:51` — `ButtonVisible => SettingsEnabled && HostReachable && MatchActive` | The *gate*, not the *transport*. `MatchActive` is the overlay's own edge flag (`:49`, set by `OnMatchStart`/`OnMatchEnd`). A menu affordance needs a different gate, not a different mechanism |
| The **safe text sweep** exists but is a debug-only entry point | `GameHooks.cs:377-395` (`EnqueueFullAlmanacText`, enum-driven, reads loaded dictionaries, no Almanac UI) — its **only** caller is `CheatActions.cs:757` via the `almanac-dump-all` cheat command (`CheatCommandRunner.cs` case) | Real, working, safe machinery held back by a debug caller. Per the debug-scope rule it cannot ship as release behaviour as-is; the wiring gap is "give it a non-debug trigger", not "build the sweep" |
| `rift_asset_sources` has **no producer or consumer** outside Data/Tests | `RpgStore.Icons.cs:40,99` — grep for `UpsertRiftAssetSource`/`GetRiftAssetSource` under `src/FusionRpg.Server` and `web/` returns nothing | Schema + validation are done; nothing writes or reads it |
| The four Core VFX recipes are declared but unwired | `src/FusionRpg.Core/Vfx/VfxCatalog.cs:65-68,442-517`; `VfxDirector.cs` has **zero** rift references; `prove-vfx.ps1` has no rift assertion; `SanctumStage.tsx:260-265` never passes the dialog's `onCue` | Belongs to the **onboarding** program's open T13/T14, listed here so it is not rediscovered as a Gate task |
| Injector-mode open is behind a player setting | `OverlaySwitch.cs:113` — `if (!_viewStarted && State.SettingsEnabled)`; the code comment is explicit that with the button off there is no other way in | A preference-vs-guarantee decision, not a technical limit |

### Real gap (no mechanism exists anywhere)

| Gap | Why it is real | What would have to be built |
|---|---|---|
| **A trustworthy "the player is on a safe menu" signal** | There is no hook that knows the *main menu*. `RiftMenuOverlay.cs:25` infers "menu" from `GameHooks.Board == null`, which is also seed-picker, post-loss and init states. `menu.enter` comes from `UIMgr.BackToMenu` (`GameCaptureHooks.cs:812-818`) which is documented as landing on the *previous* layer, not the true main menu (`DebugActions.cs:1462-1464`); `UIMgr.EnterMainMenu` is called **only** by the debug nav switch (`DebugActions.cs:1478`), with no patch and no emitted signal. No `SceneManager`/scene-name reference exists in the injector at all | A new reviewed menu-state signal (a patch or a state read) and a **reviewed allow-list** of surfaces. **Painting may stay broad; clicking may not.** A clickable rect drawn on "no board" risks eating a host-game menu click — the exact outcome the existing button avoided by being board-gated (`OverlaySwitchState.cs:44-48`) |
| **Non-interactive portrait capture** | Portrait capture needs a live `AlmanacCardUI`: `TypeIconCapture.cs:20-22` returns if `card == null`, and layers are scraped from the card's own sprites/Images (`:56-124`). The only triggers are the two card-select hooks (`GameCaptureHooks.cs:549-560,571-582`). There is no enum sweep for icons (the text sweep has no icon analogue) | Either (a) a safe non-interactive source for every card's layers, or (b) a **bounded, host-owned card traversal** that leaves the player where they started. The idea names both; neither exists. This is the idea's own "technical spike" |
| **Capture completeness / manifest** | Nothing reports "capture is complete for revision N". Only per-item presence exists: `HasTypeIconDump` (`RpgStore.Icons.cs:126`), `ListTypeIconDumps` (`:195`), `ListAlmanacTextDumps` (`RpgStore.Almanac.cs:103`), present-only routes `Program.cs:1131,1202` | A manifest that distinguishes fresh / stale / partial / failed for a game-content revision. Note the guardrail rule: the manifest must not treat a *row count* as the completion contract (DESIGN-GATE §3 rule 7) |
| **Durable capture-job state** | The in-process "already sent" sets are lost on restart (`TypeIconCapture.cs:18,25`; `AlmanacTextCapture.cs:21`). Resume today is accidental — re-running the enum sweep re-probes each item via the cache check (`RpgClient.cs:286-292`) | Either accept "resume = re-enumerate and skip cached" as the mechanism (cheap, honest) or add durable job state. The first-open **progress** surface needs *some* durable or reconstructable status either way |
| **A first-open preparation UI** | There is no FE surface for "preparing" and no host→FE channel to feed it | FE chrome; belongs to the `/idea-ui` half. Must never block the Rift from opening (idea's own contract, and the debug-scope/live-probe honesty rules) |
| **Clickable hit-target geometry contract** | The art layout is pure and tested, but there is no *hit-target* concept: minimum device-pixel size, and the rule "never cover a host-game primary action" | A derived minimum target size and an overlap policy. Geometry belongs in Core beside `OverlaySwitchLayout` (`src/FusionRpg.Core/Overlay/OverlaySwitchLayout.cs:39-62` is the precedent) |

### Verified F10 / host facts that constrain the design

- **F10 is already correctly owned.** Native capture is global in launcher mode
  (`MainWindow.xaml.cs:270`, default `Key.F10` at `GameWindowInterop.cs:17`) and in injector mode
  (`OverlayViewHost.cs:186`). This satisfies the idea's "F10 must be captured natively by the host"
  requirement **today, on the main menu, board or not.**
- **The FE must not bind F10.** `web/fusion-rpg-web/src/shell/keymap.ts:18` —
  `FORBIDDEN_KEYS = new Set(["F10"])`; the guard `keymapGuard.ts:51-63` allows the literal only in
  `keymap.ts`, `keymapGuard.ts` and one read-only display file, and
  `keymapGuard.test.ts:10-11` asserts nothing else mentions it. So the idea's *"the FE also captures
  F10 while it has focus and sends the typed bridge message"* **contradicts a guard-pinned rule**
  and would require an ask-first amendment to that guard and its test. The native host path makes
  the FE half unnecessary for F10 — this is a design simplification the idea missed.
- **Two processes can both want F10.** Only one can hold a `RegisterHotKey`; the loser logs and
  falls back to its button (`OverlayViewHost.cs:186-190`; the "cannot start" note is
  `Win32.cs:62-63`). Handled, but it means behaviour differs by which host claimed the key — a
  live-test item, not a defect.
- **Unverified, flagged:** with the injector view focused, `AcceleratorKeyPressed` also maps F10 to
  *hide* (`OverlayViewHost.cs:140-150`, `OverlayViewPolicy.cs:22-23`) while the global hotkey maps
  F10 to *toggle*. If the global registration succeeded, the key is consumed and the accelerator
  path is likely dead (a sensible fallback); if it failed, the accelerator is the only path. I have
  **not** run this live. Treat "F10 behaves identically whether or not the hotkey registration
  succeeded" as an acceptance item, not an assumed fact.

---

## Prior art (genre research, with numbers and failure modes)

**1. A game-hosted WebView2 overlay that a page can message is a solved, common pattern.**
`maschine34675/WebOverlay` (Tarkov mod, WebView2-over-Unity) exposes exactly the shape this feature
needs: `IWebOverlay.Post(message)` / `ExecuteScript(script)` with a **bounded outbox** that buffers
until the page has loaded, and a `MessageReceived` event for page→host. It also documents the two
failure modes to copy a fix for: (a) the overlay thread is **not** the Unity thread, so handlers
must be treated as "any thread" and queue state for `Update()`; (b) `WebOverlays.Create` returns
`null` when overlays are known unavailable, and later failures (no runtime, dead browser) arrive on
a latched `Failed` event. It ships `CloseKeys` for exactly the "the view covers the button that
opened it" problem this repo solved differently (`OverlayViewPolicy.IsCloseKey`).
Source: https://github.com/maschine34675/WebOverlay

**2. Do not chase Unity-texture embedding — the repo already rejected it, and the ecosystem agrees
it is a different hosting model.** Per DESIGN-GATE §1 and `overlay-spec.md:20`, in-Unity rendering
stays rejected. The modern library that *does* it (`cantetfelix/WebViewToolkit`, DX11/DX12,
DirectComposition) documents the cost honestly: Windows-only, x64-only, DX11/12-only, and
**~30 FPS texture updates**, with input forwarded manually. That is a worse contract than a
separate HWND and confirms the existing decision.
Source: https://github.com/cantetfelix/WebViewToolkit

**3. Focus-stealing is the documented failure mode of a WebView2 child window.** `Chrome_WidgetWin_0`
takes keyboard focus on click, so the host's `WndProc` stops seeing `WM_KEYDOWN` until focus is
returned (`fwflunky` migration gist, 2025-09-08). This repo already dodges it by keeping the view a
**top-level** window and re-focusing the game on hide (`OverlayViewHost.cs:308-319`), but it is the
reason "click the tombstone, then press Esc" must be tested explicitly.
Source: https://gist.github.com/fwflunky/2f6979def791d6bacfd5259c99c0a7c9

**4. Keyboard forwarding into the webview is genuinely awkward; prefer native host capture.**
Microsoft's `AllowHostInputProcessing` (WebView2 SDK `1.0.3351` / runtime `1.0.1901.177`) was added
because accelerators like `Esc`/`Tab` did not reach the host, and enabling it *then* overrides keys
the page needs (Rick Strahl, 2025-08-20: `Tab` starts moving WPF focus; `ESC` bubbles out). This is
independent evidence for this repo's existing choice: capture the close key **natively at the
window**, not in the page. It also warns against adding a FE key binding as the primary mechanism.
Source: https://weblog.west-wind.com/posts/2025/Aug/20/Using-the-new-WebView2-AllowHostInputProcessing-Keyboard-Mapping-Feature

**5. First-run capture should be manifest-driven, chunked, resumable, cache-first, and never block
play — with concrete numbers.** The consistent shape across shipped launchers and content systems:

- **Interrupted work resumes, it does not restart.** CAVS's hardened path resumed a **232 MiB install
  killed at 57 MiB**, fetching only the missing **~166 MiB**; re-downloads cost **~0 bytes** against a
  persistent cache; the binary manifest is **~75–77% smaller** than JSON. Stable error codes
  (`CAVS-E-*`) let the client choose retry/repair/give-up. Source:
  https://github.com/orelvis15/cavs and https://lib.rs/crates/cavs-client
- **Verify each unit on arrival; write atomically; repair only the bad units.** PrismLauncher's
  `HttpMetaCache` keeps `etag`/`md5sum` per entry and a `MetaCacheSink` to skip unchanged files;
  `NetJob` caps concurrency, retries non-404 up to **3** times, then asks the user.
  Sources: https://deepwiki.com/PrismLauncher/PrismLauncher/5.4-resource-download-system
- **Stream, do not gate.** Xbox's Streaming Installation / Intelligent Delivery exists precisely so a
  title *runs before it is fully installed*, with chunks marked by language/console class, and
  installation continues in the background while the player plays. Source:
  https://learn.microsoft.com/en-us/gaming/gdk/docs/features/common/packaging/overviews/streaming_install-intelligent_delivery
- **The documented failure modes:** coupling downloads to installs creates phase stalls, installers
  can advance before deployment is confirmed (missed/partial installs), and heavy progress dispatch
  stutters the UI — Vortex's 2025 refactor decoupled them and **batched progress updates** to fix it.
  Source: https://github.com/Nexus-Mods/Vortex/issues/18211
- **A first-run verify must be cheap by default.** The pattern is quick-verify (changed chunks only)
  by default and full-verify on demand; never a blind full scan at every launch.
  Source: https://app.studyraid.com/en/read/99448/4464021/implementing-auto-repair-for-corrupted-files

**Mapping to this feature:** the repo is already aligned on the important half — the per-item cache
probe (`RpgClient.cs:286-292`) is chunk-level "have-set" skipping, and the media DB rows *are* the
durable completeness record. What the prior art adds is (a) a **manifest that reports state** rather
than making the client re-enumerate everything to find out, (b) **batched/progress** signalling,
(c) **never gating play** — which the idea already states, and (d) **stable reason codes**, matching
the repo's existing `onboarding.story-*` refusal-reason style.

---

## The shape being proposed

The honest minimal shape, and it is smaller than the idea doc implies:

1. **Separate "paint" from "click."** Keep `RiftMenuOverlay` as the paint layer under its current
   broad gate (`Board == null`). Add an **interactive** sibling, gated on a **new, narrow, reviewed
   menu-safety signal** — never on "no board". This is the one genuinely new piece the door needs.
2. **Click routes through the existing convergence point**, exactly as the pipe and hotkey do
   (`MainWindow.xaml.cs:286-299`, `:313`): menu click → the same toggle the launcher already owns.
3. **Native F10 stays the FE-independent open/close path.** No FE key binding. The idea's "FE
   captures F10" is dropped (it fights `keymapGuard`).
4. **"Leave the Rift" is an explicit hide, and only if the FE half earns it.** The host already has
   a one-way `Hide` (`OverlayViewHost.cs:67,239-241`; launcher `HideOverlayToGame`). The bridge, if
   built, carries a *tiny validated vocabulary* (`rift.hide`, and nothing else until something calls
   it) — mirroring the pipe's deliberate two-verb surface (`OverlayPipeServer.cs:14-20,81-86`) and
   the "unreachable verbs are untested surface" rule there.
5. **First-open capture ships as its own, separate program** (see Open questions). Its mechanism
   should be: manifest reports state → if stale/partial, run the **enum-driven text sweep** (exists,
   needs a non-debug trigger) and a **bounded card traversal** for portraits (does not exist) →
   per-item cache probe already dedupes → media DB rows are the durable resume state → FE shows
   preparation, and **the Rift opens regardless**.

**Alternatives rejected:**

- **Reuse the in-match button's gate for the menu tombstone.** Rejected: that gate is `MatchActive`
  (`OverlaySwitchState.cs:51`) — a menu affordance by definition has no match.
- **Make the tombstone clickable whenever `Board == null`.** Rejected: `Board == null` is
  seed-picker / post-loss / init too; a click there can eat a host menu action. This is the specific
  mistake the existing button's board gate was written to avoid (`OverlaySwitchState.cs:44-48`).
- **Have the FE bind F10 and post a bridge message.** Rejected: `keymap.ts:18` +
  `keymapGuard.ts:51-63` forbid it, the native path already covers it, and Microsoft's own
  keyboard-forwarding caveats argue against page-level key capture.
- **Render the control room as a Unity texture.** Rejected by `overlay-spec.md:20`; unchanged.
- **Put capture progress behind a blocking full-screen spinner.** Rejected by the idea's own contract
  and by the streaming-install prior art; also it would make the Rift's *first* moment its slowest.
- **Replace the existing in-match "RPG" button with the tombstone icon in this program.** Deferred,
  not rejected; the button is guard-pinned and swapping its identity is a separate reviewed change.

### Owner decisions (2026-09-15)

These are settled; the sections above are what they change.

| # | Decision | Consequence |
|---|---|---|
| 1 | **This program is only the tombstone's job: a PVZ-game sprite that opens the web FE.** | No story, no capture mandate inside this map. Capture is *triggered* here; its mechanism is a separate program. |
| 2 | **The web FE is never named; "Rift" is reserved for a later mechanism.** | Rename player-facing copy to "web FE". Do not call the overlay "the Rift". |
| 3 | **The tombstone is clickable.** | Q1/Q2 resolved toward the reviewed menu signal. |
| 4 | **The story becomes a reusable `story-scene` sub-feature**, enriched through `/idea-ui` (module-first: UI pieces broken into small components), with an agent deployed to enrich it. Genre research supports this. | A second program, not part of this map. Needs its own ideal doc. |
| 5 | **v1 capture = text only** (~900 entries) + an honest fallback for missing portraits. | Portrait harvest stays behind the spike. |
| 6 | **Actors render a real sprite; a missing sprite falls back to a labelled shape** (rectangle/square carrying the actor's name). | Mirrors Ren'Py's own `Placeholder` and Dialogic's default-portrait scene — see the story-scene program. |
| 7 | **Primary actors: Penny and Crazy Dave** (Zomboss is a story question, deferred). Dave remains the first commander, but **his display name becomes the player's name** — no second identity, no new actor. | See conflict 1. It is a one-line extension plus two cross-program consequences (a locked spec row and pinned tests) |
| 8 | **After the story, open the FE and land on the default page**: auto-create the player if needed, then go to page 2 (today's create-or-continue behaviour). Other pages stay locked until unlocked by play. **The player's first commander takes the player's own name** — extend the existing first-commander code rather than adding a second identity (owner, 2026-09-15). | Changes the FE landing contract, and extends `commander-surface` (see conflict 1 — resolved, small: one hardcoded line + its consumers) |
| 9 | **"Leave" uses a real `rift.hide` bridge command.** | New FE→host contract to build and guard. |
| 10 | **`commander-surface` owns the commander-name change** (Q13). | rift-gate's spec *depends on* the seam; it does not edit `spec-commander-list-api.md` or its tests. |
| 11 | **Clickable surfaces: the full tombstone on the PVZ main menu, plus a compact icon on the live-lawn entry and one reviewed non-gameplay menu surface** (Q11a) — the idea's own shape (`rift-gate.md:44,46,132-133`). | The allow-list is small, named, and owner-reviewed. |
| 12 | **A narrow reviewed menu-state signal gates the hit target** (Q11b); paint may stay broad, clicking may not. | The one genuinely new mechanism the door needs. |
| 13 | **The two programs run in parallel and their specs are written together** (Q12), so the landing contract (decision 8) is agreed once. | Sequenced, not serial. |
| 14 | **`rift.hide` is one verb with two implementations, matching the dual-host split** (owner, 2026-09-15). | The launcher adds a `hide` verb to its pipe (`OverlayPipeServer.cs:14-19`, today only `toggle`/`ping`); the injector host calls the in-process `OverlayViewHost.Hide()` (`OverlaySwitch.cs:55`). One vocabulary, two transports — the FE does not care which host owns it. |
| 15 | **The live-lawn affordance is the existing guard-pinned "RPG" button, restyled** (owner, 2026-09-15; supersedes decision 11's "compact icon" wording). | One lawn affordance. The tombstone program owns that button's icon/label as a reviewed change to the guard's scope; the guard/test is updated, not bypassed — the button still carries one action. |
| 16 | **Both hosts set an embed marker at open**, so the page knows it is embedded and that **Leave** can work (owner, 2026-09-15). | Verified same-origin-safe (`OverlayViewPolicy.cs:53-55` compares only scheme/host/port). The page must still behave correctly with **no** marker (plain browser visit), where **Leave** is not offered. |

**This dissolves the earlier Q7/Q8/Q9 and creates two new conflicts.** Q7 is answered — two programs,
not one map. Q8 is superseded by decision 8. Q9 is answered — text only.

**Conflict 1 — resolved before it was a conflict: the first commander simply does not take the player's
name yet, and that is a one-line extension.** I initially recorded this as a collision between
"auto-create a Crazy Dave player" and Dave-as-Commander. That was wrong, and the correction is worth
keeping because it removes a whole open question. Verified in this session:

- **Dave's display name is hardcoded at exactly one line**: `PlayerEmpireCommanders.cs:14` —
  `CommanderId.Dave => "Crazy Dave"`. There is no per-player naming anywhere in that path.
- **The commander is already player-scoped, deliberately.** `CommanderId.cs` (`AllocationScopeKey`)
  maps Dave to `"player:{playerId}"` and states in its own doc that "he IS the player's own
  commander — no new convention needed, no data migration for existing saves". So the identity is
  already the player's; only the *label* is still a constant.
- **The name is already reachable at the exact call site.** `CommanderEndpoints.ProjectList(store,
  playerId)` (`CommanderEndpoints.cs:52-102`) already holds `playerId`, and the player's real name is
  one call away (`PlayerDto.Name`, `Dtos.cs:130-140`; read by `GetPlayerUnlocked`, `RpgStore.cs:3845-3852`).
  The player picks that name themselves — the FE literally asks ("Choose a summoner",
  `SaveSelect.tsx:45`; `CreatePlayer`, `RpgStore.cs:1124-1137`).
- **The id/scope work is done.** `PlayerEmpireCommanders.ForPlayer` (`:9-10`), the `commander:dave`
  stable id, and the `rpg_player_commander` row (`RpgStore.PlayerCommander.cs:26-42`) are all already
  player-keyed. Nothing about persistence changes.

**So the fix is: make Dave's display name the player's name, at the seam that already has both.** The
sites that consume the constant today are exactly five, and only one of them is authoritative:

| Site | Role | What changes |
|---|---|---|
| `CommanderEndpoints.cs:87` | **The authoritative API row** (`/api/commanders/{id}`) | Reads the player's name and emits it, instead of the constant |
| `RpgClient.cs:710` | Injector fallback when the row is absent | Follows the seam (offline/unknown case) |
| `MatchCommanderSessionCache.cs:14,43,83,95` | Core fallback default + `DaveFallback` | Degenerate "we don't know" case; a neutral fallback is correct here, not a name |

Two consequences the spec must carry, and both are **cross-program**, not this map's:

1. **It edits another program's locked spec.** `docs/architecture/commander-surface/spec-commander-list-api.md:94`
   currently says `| displayName | Fixed map v1: Dave → "Crazy Dave" |`. Extending the name **supersedes
   that row**, so it belongs to `commander-surface` (or a small acknowledged follow-up), and the
   rift-gate spec should *depend on* it rather than silently editing another program's spec.
2. **It moves pinned tests.** `tests/FusionRpg.Core.Tests/Commanders/MatchCommanderSnapshotTests.cs`
   asserts the literal `"Crazy Dave"` at ~7 places (`:58,66,92,108,118,134,219`) while `:20` reads it
   through `PlayerEmpireCommanders.DisplayName`. Those assertions follow the source and must be updated
   with it. The FE `"Crazy Dave"` hits (`adapt.test.ts`, `LawnHud.test.tsx`, `lawnProjectorFold.test.ts`,
   `CommandersLayer.test.tsx`, `bus/commanders.test.tsx`) are **fixture data**, not source pins — they
   inject a value and stay valid.

**A pleasant side effect that confirms the design:** `PlayerEmpireCommanders.ForPlayer` returns Dave for
*any* `playerId > 0`, including Zomboss's own player row (`EnsureZombossPlayer`, `RpgStore.ZombossDeploy.cs:25-29`,
name `"Zomboss"`). Player-scoping the name makes that row's commander read "Zomboss" — which is exactly
right, where the constant made it wrong.

**Conflict 2 — "Leave" vs the `DialogShell` dismiss contract.** Decision 9 adds `rift.hide`. The
prologue's dialog currently treats dismissal as `finish("skipped")` — a *durable story
acknowledgement* written on `onOpenChange(false)` and `onEscapeKeyDown`
(`RiftPrologueDialog.tsx:131-133`). If "Leave" hides the overlay but the dialog is open, the two
meanings collide: does leaving the scene abandon the story, or just hide the window? On a tombstone-open
presentation **hide must not acknowledge** — the story is not skipped, the window is simply closed,
and the scene resumes at beat 1 next open (which the existing "interrupted beat" rule already
supports, `onboarding-gnome-teaser.md:123-125`). The spec must separate "hide the window" from
"acknowledge the story", or a single Esc silently burns the prologue.

---

## The first-open fusion — strengthened

The owner's proposal: **the first Rift open does two jobs in one action** — it starts the Almanac
capture (which takes time) and it shows the prologue (which the player reads during that time). This
is the right instinct and it has strong precedent. It also currently rests on a false premise and
contains one trap that would break the prologue's own locked contract. Both are fixable.

### Prior art: the pattern is well-documented, and the numbers favour it

- **God of War (2018) has literally zero loading screens.** The crawl-through-crack, squeeze-under-log
  and cliff-climb beats *are* the load, dressed as narrative — and reviewers praised them as making
  the world feel continuous. The studio's rule: "never ask the player to stop… if the game needs five
  seconds to load, find five seconds worth of story to tell." Three jobs, one moment, zero waiting.
  This is exactly the fusion being proposed, and it is a proven, celebrated design.
  ([Adithya Shourie, 2026-05-25](https://www.linkedin.com/posts/adithyashourie_productmanagement-gamingindustry-uxdesign-activity-7464557621707059200-Mcfn))
- **Interactive waiting measurably shortens perceived wait, and works *better* the longer the wait.**
  A 2025 study (n=58) found interactive loading interfaces produced significantly lower time
  perception than non-interactive ones at **15 s and 60 s** (p<0.05), maintaining positive emotion —
  the effect grows with wait length. A second study (n=21, 38 s mean wait) found passive vs
  interactive significant at **F(1,20)=32.24, p<0.01**, and that interactive *2D* screens match 3D for
  engagement (**F(1,20)=2.909, p>0.05**) at lower cost.
  ([Frontiers in VR, 2025](https://doi.org/10.3389/frvir.2025.1540406) ·
  [ACM, 2024](https://dl.acm.org/doi/fullHtml/10.1145/3691573.3691585))
- **But an *undisguised* wait is not accepted just because a bar is honest, and a disguised wait that
  takes too long gets panned.** Mass Effect's elevator (transparent loading) became notorious for
  ~26 s rides; Mass Effect 2's explicit loading screen was *accepted at the same or longer duration*
  (~35 s) because it stopped pretending. The lesson cuts both ways: **do not sell the player a fake
  seamlessness, and do not gate the thing they came to do behind the wait.**
  ([DiGRA loading-interface taxonomy](https://dl.digra.org/index.php/dl/article/download/1928/1927))
- **The FTUE literature is unambiguous about the failure mode to avoid.** Unskippable tutorials and a
  core loop that arrives late are among the most-cited refund triggers; "if your FTUE hasn't let the
  player actually play within the first five minutes, you've told them you don't trust them"; modal
  tutorial walls have the *worst* completion rates and players "mash through them, retain nothing."
  ([recognizingpatterns](https://recognizingpatterns.substack.com/p/first-time-user-experience-your-player) ·
  [Solana Garden](https://solana.garden/guides/game-tutorial-onboarding-explained/))

Net: **the fusion is right; making the prologue *be* the loading gate is the trap.**

### The premise is currently false — where the Rift actually lands

The proposal assumes the prologue already appears when the Rift opens. It does not:

| Claim | Reality | Evidence |
|---|---|---|
| "When we open rift the first time… show prologue" | Opening the Rift loads the server root. `PlaySession.ActiveUrl => $"http://127.0.0.1:{p}"` (`PlaySession.cs:38`) — no path, no hash — and the SPA uses `HashRouter` (`app/App.tsx:13`), so the landing route is `/` = **`TitleScreen`**, not Sanctum. The prologue mounts only inside `SanctumStage.tsx:260-265` | `PlaySession.cs:38`, `app/App.tsx:13`, `app/routes.tsx:44-46`, `app/TitleScreen.tsx:22` |
| "it will capture the almanac images" | **Nothing triggers any capture on Rift open.** Portrait capture needs a *live* `AlmanacCardUI` (`TypeIconCapture.cs:20-22`, `:56-124`) and fires only from the two card-select hooks (`GameCaptureHooks.cs:549-560,571-582`). The text sweep exists but its **only** caller is the `almanac-dump-all` debug cheat (`CheatActions.cs:753-761`) | wiring gap (text) + real gap (portraits) |
| "it take time" | True and quantified: the sweep logs **"full sweep queued 900 entries"** (`tasks/almanac-todo.md:19`) — ~900 text entries, each doing a cache-probe GET then a PUT (`RpgClient.cs:241-263`). Portraits would be on top | `GameHooks.cs:377-397` |

So the two jobs are **not** currently in one action. Today the first Rift open shows a title screen and
captures nothing. That is the gap to close — not a behaviour to preserve.

### The trap: 20–40 s of reading cannot cover a multi-minute capture

Four beats is maybe **20–40 seconds** of reading. ~900 text captures plus a portrait traversal is
**longer, and unbounded** (it can fail, resume, and depend on a spike that does not exist yet). Any
design that couples them produces one of two failures, both already documented above:

- **Capture finishes first** → the prologue becomes a gratuitous gate the player must click through
  before touching the thing they opened. That is the #1 FTUE sin ("refusing to let a paying player
  play"), and it is *worse* here because the player explicitly asked to enter the control room.
- **Capture finishes last** → after the fourth beat the player sits in a non-interactive dialog with
  nothing left to read. That is the Mass Effect elevator, and it got panned.

There is also a **state collision**: the prologue's outcome is a durable `completed | skipped`
acknowledgement (`OnboardingStory.cs:17-22`); capture state is `fresh | stale | partial | failed`
(`rift-gate.md:123-124`). Coupling them makes "skip" ambiguous — does it abort the capture? — and
makes ack-failure interact with capture-progress. Two contracts, two tables, two failure modes; they
must stay decoupled.

**And the prologue's own locked contract already forbids the coupling.** It states: *"The Rift still
opens. Missing art uses an honest fallback state; preparation may not strand the player behind a
blocking full-screen spinner"* (`rift-gate.md:80-81`), and *"First-open capture is bounded, resumable,
cache-aware, and does not block opening the control room"* (`rift-gate.md:127-128`). The fusion must
run *concurrently with* the prologue, never *behind* it.

### The strengthened shape

**Make the first Rift open the diegetic cause of the prologue, and run capture concurrently behind an
always-dismissible scene.**

1. **The prologue becomes "the Rift's first activation", not "a Sanctum entry interrupt."** Today it
   is motivated by an invisible rule ("unseen AND no settled victory AND entering Sanctum" —
   `RpgStore.Onboarding.cs:110-134`). If the player *clicks the tombstone and opens the Rift*, the
   four beats are literally what they see inside — the crack they touched. That is a stronger, more
   honest motivation than a dialog that appears on arrival, and it is the GoW crack in the rock
   applied correctly.
2. **Eligibility fires at the first eligible moment, not a fixed surface.** Once per player, at
   whichever comes first: first eligible Rift open **or** first eligible Sanctum entry. Never
   in-match (a live board), never mid-run — which preserves the existing rule's *intent* while
   removing its dependence on Sanctum. The durable ledger and the ack endpoint are unchanged.
3. **The capture starts on that same open, but is not the prologue's clock.** The prologue ends when
   the *player* finishes reading. Capture continues after the dialog closes — while the player browses
   the control room and while they play the lawn, exactly like the Xbox streaming-install model where
   installation continues in the background during play
   ([Microsoft GDK](https://learn.microsoft.com/en-us/gaming/gdk/docs/features/common/packaging/overviews/streaming_install-intelligent_delivery)).
4. **The two jobs are shown as one scene, not as a spinner over a story.** During the beats, the
   capture's progress is the *scene's own* framing — the Rift reading the world's memory as the player
   watches it happen. After the dialog closes it degrades to a small non-modal chip
   (`Preparing the Rift — 214 / 912`), never a blocking overlay. This is the "interactive loading
   interface" from the studies above, and it is what makes the fusion honest rather than fake.
5. **Both jobs fail soft, independently.** Capture failing does not touch the prologue; the prologue's
   ack failing does not touch capture (it already has a `Continue` bypass, `RiftPrologueDialog.tsx:113-119`).
   Missing art uses the designed placeholder; missing capture resumes next Rift open.
6. **Scope honesty for v1: the fusion ships the *text* capture; portraits stay a spike.** The premise
   "capture the almanac images for all zombie/plant" is not buildable yet — no non-interactive portrait
   source exists. v1 fuses the prologue with the **~900-entry text sweep** (which, once given a
   non-debug trigger, is real and safe) plus the honest placeholder for portraits. Promising "all
   images" before the spike is the same overclaim the debug-scope rule exists to prevent.

### Story strengthening (the narrative itself)

The four beats are good and stay. Three concrete strengthenings when this graduates:

- **The final action must stop presuming lawn-first.** Beat 4 ends `Anchor the lawn`
  (`onboarding-gnome-teaser.md:32`, implemented `RiftPrologueDialog.tsx:152`) and
  `onContinueToLawn` navigates `/lawn` (`SanctumStage.tsx:264`). Opened from the Rift, that pushes a
  player who asked for the control room *out* to the lawn — the trap in miniature. On a Rift-open
  presentation the primary action should be **Enter the Rift** (dismiss, stay in the control room)
  with lawn as the secondary. Two entry surfaces, one story, two honest destinations.
- **Say why the Rift is speaking now.** The beats explain the *crisis* but not the *fact of the
  scene*. One added half-line — the tombstone answering the player's touch — converts the prologue
  from "a story that happens to you" into "the thing you just did", which is what makes it diegetic
  rather than a modal wall.
- **Keep the canon boundary exactly as-is.** Gnome/Gnomiverse stay Garden Warfare inspiration;
  Rift/Fracture/Void/quarantine stay project fiction
  (`canon-boundary-and-onboarding.md:53-64`). Nothing in the fusion touches that.

What this does **not** change: the three reward checkpoints (`first-win-dave` →
`level-3-general-species` → `level-4-dave-equipment`), the story's non-reward ledger, the
`rpg_onboarding_story` schema, or the ack endpoint.

---

## Tunables

Nothing here touches power, `Θ`, `P(Θ)`, damage, or any magnitude — the one-power-ladder invariant
is untouched, and this feature introduces **no** level-derived number. The numbers it *does* add are
feel/geometry, and the repo already has a home for exactly that class.

| Number | Belongs in | Why |
|---|---|---|
| Tombstone placement/size percentages (`RiftMenuOverlayLayout.cs:30-42`, today `const` in Core) | `data/tuning/overlay.v{n}.json`, under a new `riftMenu` group | Precedent: `switchLayout` was extracted from `OverlaySwitchLayout.cs` into `overlay.v1.json` as *"same UI/feel-tuning class"* (`data/tuning/overlay.v1.json:4-6,18-24`). A balance-feel pass would change these, so they are the balance surface (`tunables-ssot.md`), not `const`s |
| Hit-target minimum size (device px) and its scale-vs-screen rule | same file, beside `switchLayout` | Same class; the derive-from-height precedent is `OverlaySwitchLayout.ScaleFor` (`:39-45`) |
| First-open capture pacing | **structural per-frame cap, hardcoded, with a comment** | Precedent: the drain budget is `Math.Clamp(frameSec*0.10, 0.0002, 0.002)` and is explicitly *hardcoded and not configurable* because "changing it changes whether the drain fits in a frame, never how the game feels" (`event-pipeline-v2-ssot.md:68-76`). Capture pacing is the same shape and must say so |
| Probe interval / debounce if a bridge adds any | `overlay.v1.json` `switchState` (already has `debounceMs`, `probeIntervalMs`, `sendTimeoutMs`) | Existing group |
| No new caps on magnitudes; a bounded card-traversal count is a structural limit | n/a — must carry a comment | `ssot-power-scale.md` §11 exempts per-frame caps and bounded ratios **only when they say so** |

---

## What this deliberately does not decide

- Anything about the overlay transport, host selection, show/hide lifecycle, release payload or its
  probes — **all shipped and out of scope**. A change here is not a Gate task.
- Whether first-open capture is in v1 at all (see Open questions 1 and 4).
- The card-traversal mechanism (patch the Almanac UI vs a non-interactive source) — that is the
  spike the idea itself demanded, and it should not be pre-empted by a spec.
- The manifest's exact shape and where it lives (Data vs Server vs both).
- Whether the FE gains "Leave the Rift" as a bridge command or as documented Esc/F10 affordance.
- Any final art, VFX, animation, or the tombstone's shipped look — the existing PNGs stay the v1
  stand-ins, and per `spec-onboarding-rift-assets.md` a replacement must not change behaviour.
- Any telemetry (observational only, and not a gate).

## What this is not

The **onboarding Rift prologue** (`docs/ideas/onboarding-gnome-teaser.md`,
`tasks/onboarding-rift-plan.md`, commit `71a28b28`) is today a *separate* feature that reuses the same
rift art: a four-beat dialog **inside the web FE** on first Sanctum entry, with its own durable story
ledger, no menu affordance and no overlay show path.

**The first-open fusion above deliberately couples them** — the prologue becomes the Rift's
first-activation scene — which is precisely why they now belong in **one capability map with two
modules** (see open question 7). What stays separate regardless: the prologue's non-reward
acknowledgement, its `rpg_onboarding_story` ledger, the ack endpoint, the three reward checkpoints,
and the capture's own `fresh | stale | partial | failed` state. Its other open items (live VFX proof,
the four `rift.*` cues not reaching `VfxDirector`, accessibility sweep) are still its own work, listed
here only so a later session does not conflate them with the Gate.

---

## Open questions (owner decisions only)

**All answered 2026-09-15. No open question blocks `/spec`.** The audit produced nine, then two more
(the commander name and the click surface), then one more (sequencing); every one is now settled in
the decisions table above.

- Q1/Q2 — clickable tombstone, narrow reviewed menu signal (decisions 11–12).
- Q3/Q4 — v1 capture is **text only**; portraits stay behind the spike (decision 5).
- Q5 — injector mode with the button preference off: treated as a bug, not a preference (see the
  wiring-gap table).
- Q6 — **`rift.hide`** is a real bridge command (decision 9).
- Q7 — **two programs**, not one map (decisions 1, 4).
- Q8/Q9 — superseded by decisions 8 and 5.
- Q10 — the first commander **takes the player's name** by extending the existing hardcoded seam; no
  second identity, no second player row (decisions 7–8, conflict 1).
- Q11a/Q11b — full tombstone on the main menu + a compact icon on the live-lawn entry and one
  reviewed non-gameplay menu; gated by a narrow reviewed menu-state signal (decisions 11–12).
- Q12 — the two programs run **in parallel and their specs are written together** (decision 13).
- Q13 — **`commander-surface` owns** the commander-name change; rift-gate depends on it (decision 10).

## Handoff

Path written: `docs/architecture/rift-gate-ideal.md`. Scope after the 2026-09-15 decisions: **this
map is the tombstone (a PVZ-game sprite that opens the web FE) plus the landing contract, plus making
the first commander carry the player's name.** The story scene is a **separate sub-program** for
`/idea-ui`, with its own ideal doc (`docs/architecture/story-scene-ideal.md`, enrichment agent
deployed) and it is not a module of this map.

**Next step: `/spec`** — a capability map plus module specs, with prefixed
`tasks/rift-gate-plan.md` / `tasks/rift-gate-todo.md`. Write the rift-gate and story-scene specs
together so the landing contract (decision 8) is agreed once. The `commander-surface` name change
lands as its own small follow-up that this program depends on, not as an edit inside the rift-gate
spec.

**The capability map exists and is approved:** [rift-gate-map.md](rift-gate-map.md) (Phase 0 complete
2026-09-15, five modules: `menu-anchor` · `tombstone` · `overlay-hide` · `first-open-signal` ·
`entry-landing`). **Read the map's Audit section before the module specs** — the map was audited
against code and the first draft was wrong in three places (the `overlay-hide` path, where
`first-open-signal` is observable, and how large `menu-anchor` really is) plus seven recorded gaps.
Those corrections are load-bearing; re-deriving the first draft's shape costs the same hour twice.

**Cross-program boundary (verified 2026-09-15, after the story-scene program specced).** The
`story-scene` program **ceded this bridge to rift-gate** in its own capability map, three times, in
its Out table: "The FE↔host bridge mechanism itself" → *"Sibling `rift-gate-ideal` (decision 9) —
this program **consumes** it, does not redefine it"*; "Unity-side `rift.*` VFX wiring" →
story-scene T27a/b, with the explicit error to avoid being *"Build a second FE→host channel"*; and
"Rift Gate mechanism (overlay transport, host selection, first-open capture)" → sibling. Its task
list restates it (`tasks/story-scene-todo.md:964`: building a second channel *"would violate
one-owner-per-mechanism"*). So the seam is already agreed: **rift-gate owns the bridge and the
`rift.hide` verb; story-scene consumes them and stops at the seam if absent.** Do not build a second
channel, and do not let the story-scene program absorb this one.

Two things the spec must carry that are easy to lose:

1. **Hide ≠ acknowledge.** `rift.hide` must close the window without writing the story's
   `completed | skipped` acknowledgement, or one Esc burns the prologue
   (`RiftPrologueDialog.tsx:131-133`, conflict 2).
2. **Pacing is structural, not tuning** — capture pacing is a hardcoded per-frame cap with a comment,
   matching the drain-budget precedent (`event-pipeline-v2-ssot.md:68-76`), never a `data/tuning`
   number and never a cap on a magnitude.

No spec, plan, or code from this phase. The ideal doc is where this phase stops.
