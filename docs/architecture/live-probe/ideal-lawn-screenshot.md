# Lawn screenshot — the ideal

**Status:** idea phase, 2026-09-14. Not a spec. No build authorized.
**Program:** `live-probe` (module `lawn-screenshot`, 4th module) · **Session:** `lawn-screenshot-20260914` (worktree).

## Which loop this extends

**None directly — developer/verification infrastructure, not a player feature.** Same honest
answer as the parent `live-probe-ideal.md`: this exercises real loops to prove other features
work instead of extending one. It serves:

- **Place 1 (lawn)** — the screenshot is taken from a live lawn match; it is how an operator or
  agent checks what the lawn actually renders.
- **Spine A/B** — indirectly: every live probe that deploys a real `UniqueActor` and asserts the
  live-engine half currently ends at `debug.board-stats` telemetry (numbers), with no view of the
  rendered board.

No change to `the-loops.md`. No parallel pitch.

## What this is

A one-shot, on-demand capture of the **rendered Unity frame**, triggered from the existing debug
command path (`debug.screenshot`), uploaded to the server opaquely, and viewable from the control
room — so a live probe no longer needs a human eyeballing the game window to answer "what does
the player actually see?".

Load-bearing principles, stated inline (not linked):

- **Two async systems, record-then-drain.** The RPG and PvZ share no clock. The screenshot path
  must never join combat evaluation: no Server round-trip in capture, no hook-side work beyond
  enqueueing. Delay is the designed degradation (a screenshot arriving seconds late is fine; a
  frame hitch is not).
- **The RPG never reads PvZ's current state to decide anything.** A screenshot is an
  observation for a human/agent, never an input to domain logic. Nothing may parse pixels to
  compute damage, grant loot, or settle state — that would be guessing current game state, which
  `overlay-control-loops.md` forbids.
- **Game Injector Debug scope.** Capture fabricates nothing but proves only that the engine
  renders X — no domain logic, no persistence (`live-probe-standard.md` §1). A screenshot is
  evidence for the live-engine half of a probe, never the server half.
- **Perf is a main-thread problem.** `ReadPixels` + PNG encode stall the Unity thread. Capture
  is therefore one-shot per command, rate-limited, downscaled — never per-frame, never ambient.
- **Gameless-first untouched.** Screenshot enriches lawn testing; it gates nothing and changes
  no capability with the game closed.

## What already exists

### Built

| Finding | Evidence |
|---|---|
| In-process Unity texture readback works in this IL2CPP host: `Graphics.Blit` → `RenderTexture.active` → `Texture2D.ReadPixels` → `ImageConversion.EncodeToPNG` → `Object.Destroy`, all inside try/finally | `src/FusionRpg.Injector/TypeIconCapture.cs:168-198` |
| `ImageConversionModule` (EncodeToPNG) + `CoreModule` already referenced by the injector host project | `src/FusionRpg.Injector.BepInEx/FusionRpg.Injector.BepInEx.csproj:93-123` |
| `Camera.main` resolvable from injector code, with stale/disabled-camera re-resolve on scene switches | `src/FusionRpg.Injector/Fx/VfxDirector.cs:549` |
| Main-thread command drain exists: `InjectorLoop.Tick` (called from `Update`/`OnUpdate`) calls `CheatCommandRunner.Drain()` | `src/FusionRpg.Injector/Host/InjectorLoop.cs:37-57` |
| Debug-command → emit → poll pattern exists (`debug.snapshot`, `debug.board-stats` → `GET /api/debug/events?kinds=...`) | `src/FusionRpg.Injector/CheatCommandRunner.cs:322-343` |
| Binary PNG upload pattern exists (base64 layers via HTTP PUT, server-cache skip) | `src/FusionRpg.Injector/RpgClient.cs:276-322` |
| The exact gap is named: no passive query can see what is actually rendered; sim and screen can genuinely disagree (defeat overlay case 2026-09-14) | `docs/contributing/live-probe-standard.md:117-158` |

### Wiring gap

None — nothing exists that is one line away from a screenshot. The closest machinery
(`TypeIconCapture`) captures **sprites**, never the frame; reusing its encode/upload shape is a
pattern reuse, not flipping a toggle.

### Real gap

1. No frame-capture entry point: no `debug.screenshot` command, no `ScreenCapture` /
   camera-render call, no server store/serve endpoint for the image.
2. No viewer: the web control room has no surface showing the captured frame next to
   `debug.board-stats` telemetry.
3. No lifecycle: retention/size policy for captured images does not exist (events table must
   **not** carry MB-scale base64 — it is polled; images need a side store).

## Prior art (Unity docs, verified 2026-09-14 via websearch — quotes below are verbatim)

- **End-of-frame rule (both capture APIs — this constrains the design).**
  `ScreenCapture.CaptureScreenshotAsTexture` docs
  (`docs.unity3d.com/ScriptReference/ScreenCapture.CaptureScreenshotAsTexture.html`):
  *"To get a reliable output from this method you must make sure it is called once the frame
  rendering has ended, and not during the rendering process. … If you call this method during
  the rendering process you will get unpredictable and undefined results."* The documented
  pattern is a coroutine yielding `WaitForEndOfFrame`. The `CaptureScreenshotIntoRenderTexture`
  page carries the same Important-note. Consequence: capturing inline in
  `CheatCommandRunner.Drain` (which runs from `InjectorLoop.Tick` ← `RpgLoop.Update`,
  i.e. mid-frame) is **wrong** — Drain must only *arm* a pending request; a coroutine running
  at end-of-frame performs the capture. This was the spec's own pre-research error, caught by
  this search.
- **Full-frame, not per-camera.**
  `CaptureScreenshot` docs (6000.x): *"This is a screenshot of the final frame presented to
  the user, not a capture from a specific Camera. If multiple cameras render to the screen,
  their combined result is captured."* `WaitForEndOfFrame` docs: suspends *"until the end of
  the frame after Unity has rendered every Camera and GUI, just before displaying the frame
  on screen"* — GUI explicitly included. This confirms the primitive covers the motivating
  misses (defeat overlay, seed-picker, pause menu), which a `Camera.main`-only render would
  miss. Camera-render stays as fallback only.
- **ReadPixels slowness is official, not folklore.**
  `Texture2D.ReadPixels` docs (6000.x): *"ReadPixels is usually slow, because the method waits
  for the GPU to complete all previous work first."* Hence GPU-side downscale (Blit into a
  smaller RT, then ReadPixels once) + rate-limit are load-bearing, not polish.
- **Async upgrade path, pre-named.**
  `CaptureScreenshotIntoRenderTexture` docs: *"makes it possible to read pixels asynchronously
  using AsyncGPUReadback, making the process consume less time on the main thread."*
  Availability of `AsyncGPUReadback` in this game's IL2CPP interop is **unverified** — v1
  ships sync ReadPixels (the proven `TypeIconCapture` shape); async is a named v2, not a v1
  dependency.
- **Playwright screenshots** (this repo's own web e2e): every world-map checkpoint is
  Playwright + agent CV, never owner eyeball (`DESIGN-GATE.md` world-map row). Lawn screenshot
  is the same doctrine applied to the Unity half: a mechanical eye instead of a human one.
- **Interop presence still unverified:** neither `ScreenCapture` nor `AsyncGPUReadback` has
  been confirmed in the game's `BepInEx/interop` assemblies (no game dir in this session).
  Task 1 verifies by compiling against the real interop; both fallbacks are pre-specified.

## The shape

One new `live-probe` module, `lawn-screenshot`, in three slices:

1. **Capture (injector):** `debug.screenshot` in `CheatCommandRunner.cs` **arms** a pending
   request only (Drain runs mid-frame from `Update` — capturing there is undefined per Unity
   docs). A shared-code hidden `MonoBehaviour` runner (one `GameObject`,
   `DontDestroyOnLoad`, created lazily — host-agnostic so both BepInEx `RpgLoop` and the
   Melon `MelonMod`, which owns no `MonoBehaviour`, stay thin per the injector-host lock)
   runs a `WaitForEndOfFrame` coroutine: `ScreenCapture.CaptureScreenshotAsTexture()`
   primary, camera-render fallback; GPU downscale via `Blit` into a ≤960px-wide RT, single
   `ReadPixels`, `EncodeToPNG`, `Destroy` all temps; rate-limit (one capture per N seconds,
   structural constant with comment).
2. **Transport + store (server):** new `POST /api/debug/screenshot` relay (Game Injector
   Debug banner) + side-store write (file under `artifacts/` or media DB — spec decides, **not**
   the events table); `GET` latest by scenario/match for the viewer. Guard
   `guard-debug-scope.ps1` classifies it Game-Injector-Debug by construction (relay present).
3. **Viewer (web, minimal):** control-room panel showing latest frame beside the existing
   `debug.board-stats` read — recipe + existing pieces, not a new page (game-gui GG-1: open
   over where the operator already is).

Rejected: per-frame streaming (violates main-thread budget); pixel-parsing for assertions
(violates "never guess current state"); stuffing images into the events table (poll bloat);
a second debug surface/MCP toolset duplicating `DebugEndpoints.cs` (banned by AGENTS.md debug
rule — adapter-wrap only).

## Tunables

Screenshot is **developer tooling, not the balance surface** — no `data/tuning` file. The
numbers it introduces are structural constants, each exempt with a stated reason in a comment:

- max capture width (e.g. 960) — structural: bounds main-thread + payload cost;
- min interval between captures (e.g. 5 s) — structural: rate limit, not gameplay;
- retention (count/age cap on stored frames) — structural: disk bound.
- PNG vs JPEG: spec decides; JPEG smaller but lossy (fine for eyeball checks, never for
  pixel assertions — which are out of scope anyway).

No `long`/`float` magnitude, no power-ladder number, no cap on a progression magnitude —
`ssot-power-scale.md` §11 does not apply. No actor magnitude — ActorHub gate does not apply.

## What this deliberately does not decide

- Exact store location (file vs media DB) — spec decides after checking `data-architecture.md`.
- Exact viewer placement in the control room — spec decides with `game-gui-principles.md`.
- Whether `ProveLiveProbe` (live-probe-tool) auto-attaches a screenshot per run — left to that
  module's owner; this module only provides the capability.

## Open questions (owner decisions only)

1. Store location: `artifacts/` files (uncommitted, simplest) vs media SQLite (queryable)?
   Recommendation: files first; queryability is not needed for an eyeball check.
2. Do we need the viewer in v1, or is "capture + GET endpoint + control-room link" enough and
   the viewer a follow-up? Recommendation: endpoint first, minimal viewer second (vertical
   slice still testable without the viewer via curl).
