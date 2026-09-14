# Spec: `lawn-screenshot`

**Program:** `live-probe` · **Map:** [../live-probe-map.md](../live-probe-map.md)
**Ideal:** [ideal-lawn-screenshot.md](ideal-lawn-screenshot.md) · **Standard:**
[../../contributing/live-probe-standard.md](../../contributing/live-probe-standard.md)

---

## Objective

Give a live-probe operator (human or agent) a mechanical eye on the Unity game window: a
`debug.screenshot` command that captures the rendered frame on demand and a server endpoint
that stores and serves the latest frame(s), so "what does the player actually see?" is answered
by an artifact instead of a person describing the screen — closing the exact gap
`live-probe-standard.md` §6 names (sim recovered, defeat overlay still rendered, nobody could
query which was true).

Who uses this: anyone running a live probe against a real game+server (today:
`actor-hub-live-proof`; tomorrow: every lawn-touching probe).

Success: from a terminal, `curl POST /api/debug/screenshot` → `GET latest` returns a PNG taken
from the live game within the last rate-limit window, with the game's own overlay (defeat /
seed-picker / pause) visible in it; `guard-debug-scope.ps1` still green; no per-frame cost
when idle.

**ASSUMPTIONS I'M MAKING:**

1. Unity version in the shipped game exposes `ScreenCapture.CaptureScreenshotAsTexture` through
   the BepInEx interop (ImageConversionModule is referenced; the class itself is unverified —
   Task 1 verifies, fallback is specified).
2. Control-room viewer is a minimal panel reusing existing pieces, not a new route (GG-1).
3. Images are dev artifacts: file store under `artifacts/`, never committed, never in SQLite
   events; retention is a count cap.
4. Capture runs at end-of-frame via a `WaitForEndOfFrame` coroutine (Unity-docs-mandated —
   mid-frame capture is undefined); `CheatCommandRunner.Drain` only arms the request.
→ Correct me now or implementation proceeds with these.

## Tech stack

- Injector: C# net6, Unity APIs available in-process (`UnityEngine.CoreModule`,
  `UnityEngine.ImageConversionModule` — both already referenced,
  `FusionRpg.Injector.BepInEx.csproj:93-123`).
- Server: net8 ASP.NET minimal API (`DebugEndpoints.cs` relay + static-file serve of stored PNG).
- No new packages. No new Unity assembly references expected (verify in Task 1).

## Commands

```powershell
.\scripts\guard-debug-scope.ps1                                            # scope banners still classify
dotnet test tests/FusionRpg.Guard.Tests --filter "FullyQualifiedName~DebugScope"
curl -X POST http://127.0.0.1:5088/api/debug/screenshot -H "Content-Type: application/json" -d '{"tag":"probe"}'
curl http://127.0.0.1:5088/api/debug/screenshot/latest -o latest.png
```

Injector build (needs legal game dir + interop, not in CI):

```powershell
$env:FUSIONRPG_GAME_DIR = "<game folder>"
dotnet build src/FusionRpg.Injector.BepInEx
```

## Project structure

| Path | Duty |
|---|---|
| `src/FusionRpg.Injector/ScreenshotCapture.cs` (new) | Arm/consume only: `debug.screenshot` case sets a pending flag with tag + rate-limit check; pure-Unity capture lives in the runner below. Never captures inline in Drain (mid-frame = undefined per Unity docs) |
| `src/FusionRpg.Injector/ScreenshotRunner.cs` (new, shared code) | Hidden `MonoBehaviour` on a lazily-created `DontDestroyOnLoad` `GameObject`: `WaitForEndOfFrame` coroutine → `ScreenCapture.CaptureScreenshotAsTexture()` primary, camera-render fallback (`TypeIconCapture` shape) → GPU `Blit` downscale → one `ReadPixels` → `EncodeToPNG` → destroy all temps → hand bytes to `RpgClient`. Host-agnostic: keeps both host shims thin (BepInEx `Plugin.cs:28-30` owns a `MonoBehaviour` already; `MelonFusionRpgMod.cs:28-59` owns none) |
| `src/FusionRpg.Injector/CheatCommandRunner.cs` | New `debug.screenshot` case → `ScreenshotCapture.Capture(tag)` → emit `debug.screenshot.ready` with fetch hint |
| `src/FusionRpg.Injector/RpgClient.cs` | `UploadScreenshotAsync(byte[] png, tag)` — HTTP POST binary to `/screenshot/upload`, same shape as `EnqueueIconDump` (`RpgClient.cs:276-322`) |
| `src/FusionRpg.Server/DebugEndpoints.cs` | `POST /api/debug/screenshot` trigger (Game Injector Debug banner + relay); `POST /api/debug/screenshot/upload` binary store + `GET .../latest` + `GET .../info` (Game Injector Debug banners; guard lists the non-relay three as ManualReview by design — file reads/writes, no domain state); `GET /api/debug/events/tail` newest-first kind poll (RPG Server Debug banner — reads the event store; the sibling `/events` reads forward from `afterId`, so its kinds filter only sees the oldest window on a long-lived server) |
| `artifacts/lawn-screenshots/` (new, gitignored) | Side store: `<scenarioId>-<timestamp>-<tag>.png` + `latest.json` pointer |
| `web/fusion-rpg-web/src/...` (TBD in Task 3) | Minimal latest-frame panel next to existing debug reads |
| `tools/debug-mcp/tools/debug_screenshot.py` (new — built in this session, owner-authorized) | MCP adapter over `POST /api/debug/screenshot` + `GET /events/tail` poll + `GET latest` fetch: trigger + fetch + scope-stamped envelope (`scope: "game-injector-debug"`) with base64 PNG, Playwright-`browser_screenshot` analogue. Adapter-only, same convention as `debug_restart_game.py` (adapter over `scripts/restart-game.ps1`). Registered in `server.py` + README walkthrough (11 tools); `test_debug_screenshot.py` (6 tests, stub transports). Merge coordination with the `debug-mcp` session is owner-side |

## Code style

Same as `TypeIconCapture.cs`: all Unity objects created and destroyed in one method, temps in
`try/finally`, every failure path is `CheatState.Error(...)` + `return null/false`, never throw
out of the drain:

```csharp
// Structural (not tunable): bounds main-thread stall + payload. Not the balance surface.
const int MaxCaptureWidth = 960;
const int MinIntervalSeconds = 5;

// Drain-side: arm only. Mid-frame capture is undefined (Unity docs), so Drain never
// touches ScreenCapture — the runner coroutine below owns all Unity capture work.
public static bool TryArm(string tag)
{
    if (!RateLimiting.TryEnter("screenshot", MinIntervalSeconds)) return false;
    ScreenshotRunner.Arm(tag);
    return true;
}

// Runner-side (WaitForEndOfFrame coroutine — runs after every Camera + GUI rendered):
IEnumerator CaptureAtEndOfFrame(string tag)
{
    yield return new WaitForEndOfFrame();
    Texture2D? tex = null;
    RenderTexture? scaled = null;
    try
    {
        tex = ScreenCapture.CaptureScreenshotAsTexture();
        scaled = DownscaleBlit(tex, MaxCaptureWidth);   // GPU-side; one ReadPixels after
        yield return null;                              // let the Blit settle a frame
        var png = ReadAndEncode(scaled);
        RpgHost.Client?.UploadScreenshot(png, tag);     // binary PUT, icon-dump shape
    }
    catch (Exception ex) { CheatState.Error("debug.screenshot: " + ex.Message); }
    finally { if (tex != null) Object.Destroy(tex); if (scaled != null) RenderTexture.ReleaseTemporary(scaled); }
}
```

## Testing strategy

- **Guard (CI):** `guard-debug-scope.ps1` green — new POST route carries `// Game Injector
  Debug` banner and relays (classified Game-Injector-Debug by construction); new GET-latest
  route carries an accurate banner for its shape (no relay → RPG-Server-Debug-shaped read of a
  dev-artifact side store, stated in the banner so the next reader is not misled).
- **Unit (no game):** downscale/encode helper tested on synthetic `byte[]` level only —
  no Unity, no temp store (testing-standard substrate rule).
- **Live (real game+server, owner terminal):** POST → latest returns a PNG that opens, shows
  the live board, and is newer than the request; defeat-overlay case from the 2026-09-14
  incident reproduces visibly. Response-only is not proof: the check opens the bytes.
- **Perf:** frame-time delta around a capture measured via existing `PerfProbe` sections;
  acceptance is "no idle cost, bounded on-demand cost" (numbers recorded, not asserted as
  literals in tests).

## Boundaries

- Always: run on the main-thread drain only; destroy every temp texture/RT; `// Game Injector
  Debug` / accurate banners on every touched route; `guard-debug-scope.ps1` green before done.
- Ask first: new server file layout under `artifacts/`; any web control-room surface beyond the
  minimal panel; changing `RpgClient` upload shape for other dump types; the MCP adapter
  contract (Task 4) — built in this session under owner authorization on the `debug-mcp`
  session's paths (main tree verified clean first); merge coordination stays owner-side.
- Never: parse pixels into domain decisions; write combat/derived state from capture;
  per-frame or ambient capture; images into `RpgStore` events table; second debug surface
  duplicating `DebugEndpoints.cs`; arbitrary in-game code execution (the Playwright-evaluate
  analogue is deliberately absent — `debug_call`'s allowlist is the bounded equivalent, and
  the allowlist refusing is the security posture, not a gap); commit captured PNGs; touch
  `solid-run` branch files (this builds on `worktree-lawn-screenshot-20260914`, merge is
  owner-side).
- Never: parse pixels into domain decisions; write combat/derived state from capture;
  per-frame or ambient capture; images into `RpgStore` events table; second debug surface
  duplicating `DebugEndpoints.cs`; commit captured PNGs; touch `solid-run` branch files
  (this builds on `worktree-lawn-screenshot-20260914`, merge is owner-side).

## Success criteria

- [ ] `POST /api/debug/screenshot` on a live game stores a PNG < 60 s old served by `GET latest`.
- [ ] The PNG shows UI overlays (verified once against the defeat-overlay case).
- [ ] Idle game+server show zero capture cost (no per-frame work added to `InjectorLoop.Tick`).
- [ ] `guard-debug-scope.ps1` + `DebugScope` guard tests green.
- [ ] No captured image committed; `artifacts/lawn-screenshots/` gitignored.
- [ ] MCP `debug_screenshot` adapter honors the contract below (Task 4, built + proven
  live 2026-09-14): trigger via `POST`, poll via `GET /events/tail`, fetch via `GET latest`,
  every response carries `scope: "game-injector-debug"`, failures return the envelope
  (never raise except caller misuse), registered in `server.py` + README walkthrough.
- [ ] DESIGN-GATE §5 checklist complete (below).

## Open questions

1. File store vs media DB — recommended files (see ideal Q1). Owner confirms at plan review.
2. Viewer in v1 or follow-up — recommended endpoint-first (see ideal Q2).

## DESIGN-GATE §5 checklist

- [x] Subsystem identified: injector→game comms + live-probe/debug-API. Reread this session:
  `software-architecture.md`, `decisions.md` (locks rows), `event-pipeline-v2-ssot.md` (G1/G5),
  `overlay-control-loops.md` (Hot/Cold), `live-probe-standard.md` (full), `live-probe-ideal.md`,
  `live-probe-map.md`, `spec-debug-scope-guard.md` (shape), `the-game.md`/`the-loops.md`
  (vision — infra, no loop extended).
- [x] Boundary recorded: `tasks/sessions/lawn-screenshot-20260914.json`, worktree mode,
  `session-boundary-check.ps1` run (pre-existing drift noted, none from this session).
- [x] `decisions.md` checked: injector host (BepInEx+MelonLoader, no dual-load), SignalR+HTTP
  fallback, Cheats SSOT (no duplicate cheat menu — this adds no cheat), debug-scope rule
  (adapter-wrap, scope-labeled). No lock contradicted.
- [x] Claims cite file:line (see ideal buckets + structure table).
- [x] Verified against code, not comments (opened `TypeIconCapture`, `RpgClient`,
  `VfxDirector`, `InjectorLoop`, `CheatCommandRunner`, host csproj, BepInEx `Plugin.cs`
  (`RpgLoop : MonoBehaviour`), Melon `MelonFusionRpgMod.cs` (no `MonoBehaviour` — hence the
  shared-code runner)).
- [x] Rules quoted with sections (event-pipeline G1/G5 §2; overlay Hot §3; live-probe §1/§6).
- [x] Constraints tested, not assumed: readback proven by shipped `TypeIconCapture`;
  `ScreenCapture` + `StartCoroutine(string)` presence compile-verified 2026-09-14 against
  the real Melon 3.9 interop (build green); runtime drive of the string coroutine + which
  primitive fires is Task 1's live check (`primitive` in `debug.screenshot.ready`).
  BepInEx host: `ScreenCaptureModule` reference not added there (its interop dir on this
  machine carries no UnityEngine modules) — its build will fail loudly on the shared
  `ScreenCapture` type until the one-line reference lands; Melon is the deploy target.
- [x] No §2 invariant contradiction (two-systems, record-then-drain, deltas, single writer,
  Funnel-only path, SQL-in-Data, standalone-first, main-thread perf, no ceilings, data
  balance surface, long magnitudes, one ladder, ActorHub gate, SOLID). Screenshot touches
  none of the write paths.
- [x] Corrections propagated: map + spec + plan + todo written together in this phase.
- [x] No population-count/text literal assertions (perf numbers recorded, never pinned).
- [x] No event-refreshed cache introduced (latest-pointer is a dev-artifact file write, not a
  keyed game-state cache; no trigger set to enumerate).
- [x] No ordering-fixed acceptance criteria (single-shot command, no sequence).
- [x] No actor magnitude produced/consumed — ActorHub gate N/A, stated.
- [x] No SOLID-violating parallel path (reuses `CheatCommandRunner`/`RpgClient`/`DebugEndpoints`
  seams; no second debug surface).
