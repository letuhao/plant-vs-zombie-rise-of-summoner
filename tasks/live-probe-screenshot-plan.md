# Implementation plan: `live-probe-screenshot` (module `lawn-screenshot`)

**Program:** `live-probe` · **Map:** [../.kilo/worktrees/lawn-screenshot-20260914/docs/architecture/live-probe-map.md](../.kilo/worktrees/lawn-screenshot-20260914/docs/architecture/live-probe-map.md) (amended on this branch: 4th row)
**Spec:** `docs/architecture/live-probe/spec-lawn-screenshot.md` (this branch)
**Task list:** `tasks/live-probe-screenshot-todo.md` (this branch)
**Session:** `lawn-screenshot-20260914` (worktree `worktree-lawn-screenshot-20260914`)

> Paths above are branch-relative. In the main tree these files do not exist yet; merge is
> owner-side after review. Do not confuse with `tasks/live-probe-plan.md` (solid-run's, untouched).

---

## Overview

Four slices in dependency order: prove the capture primitive on the real build first (the one
unverified assumption), then transport+store, then the minimal viewer, then the MCP adapter
(handoff to the `debug-mcp` session). Slices 1–3 are one agent, sequential — every slice's
verification needs the same live game+server and each builds on the previous slice's artifact.
Slice 4 is implemented by `debug-mcp-live-readiness-20260914` (owns `tools/debug-mcp/**`);
this plan defines its contract and acceptance, not its diff.

## Architecture decisions

- **`ScreenCapture` primary, camera-render fallback.** Locked by the ideal's prior-art
  reasoning: only the full-frame primitive sees UI overlays (the motivating misses). Task 1
  measures both on the real build; if `ScreenCapture` is absent from the interop, the fallback
  becomes primary with no spec rewrite (the seam — `ScreenshotCapture.Capture` — is identical).
- **Binary PUT, not events.** Images ride `RpgClient.UploadScreenshotAsync` (binary PUT, same
  shape as icon dumps), never the SignalR event queue and never the events table (poll bloat).
- **File side-store, not SQLite.** `artifacts/lawn-screenshots/` + `latest.json` pointer.
  Rationale: dev eyeball artifacts need no queryability; keeps SQL-in-Data untouched and the
  guard classification obvious.
- **Endpoint-first, viewer-second.** Slice 2 is testable with curl alone; slice 3 is UI-only
  and never blocks slice 2 from landing.

## Task list (index — detail in `tasks/live-probe-screenshot-todo.md`)

### Slice 1: Capture primitive (injector)

- [ ] Task 1: arm-in-Drain + end-of-frame runner + `debug.screenshot` case (Unity-mandated:
  mid-frame capture is undefined; GPU-Blit downscale, destroy temps, rate-limit), verified
  live: PNG opens, shows board + UI overlay; interop presence (`ScreenCapture`,
  `AsyncGPUReadback`) recorded.
- [ ] Checkpoint 1: capture works on the real build via one command; primitive choice
  (ScreenCapture vs fallback) recorded with measurement.

### Slice 2: Transport + store (server)

- [ ] Task 2: `RpgClient.UploadScreenshotAsync` + `POST /api/debug/screenshot`
  (Game-Injector-Debug banner) + file store + `GET latest`; guard green.
- [ ] Checkpoint 2: curl round-trip returns a fresh PNG; guard + scope tests green.

### Slice 3: Minimal viewer (web)

- [ ] Task 3: latest-frame panel in the control room next to existing debug reads
  (recipe + pieces, no new route); `npm test` + `npm run build` green.
- [ ] Checkpoint 3: operator sees the frame beside `debug.board-stats` without opening the
  game window.

### Slice 4: MCP adapter (built in this session, owner-authorized)

- [x] Task 4: `tools/debug-mcp/tools/debug_screenshot.py` adapter over the Task 2
  endpoints (trigger + `GET /events/tail` poll + fetch, scope-stamped envelope with
  base64 PNG), registered in `server.py` + README (11 tools); debug-mcp pytest 75/75;
  proven live (`mcplive3`, menu frame opened). Contract fulfilled; diff lands via
  owner-side merge with merge coordination noted below.
- [x] Checkpoint 4 (done): all spec success criteria met, including the MCP bullet;
  handoff lists exact regions for owner merge.

## Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| `ScreenCapture` missing from the game's interop assemblies | Primary primitive unavailable | Task 1 tries it first on the real build; camera-render fallback is pre-specified, same seam |
| 1080p capture stalls the main thread visibly | Hitch on every shot | Downscale-first (960px cap), rate-limit 5 s, PerfProbe measurement in Task 1; numbers recorded |
| `artifacts/` path differs between server CWDs (dev vs dist) | Latest-pointer 404s | Resolve store dir from the server content root with a logged absolute path; Task 2 asserts the file exists on disk |
| Live verification needs owner terminal (server-lifetime rule) | Slice verification stalls | Slices 1–2 specify curl-only checks the owner can run in one command; no agent-held server |
| Merge collision with solid-run (owns same src files on its branch) | Region overlap at merge | This branch touches narrow, named regions (two new files + one case-block + one method + two routes); record regions in the todo; owner merges |
| MCP adapter lives in another session's paths | This session cannot implement or verify Task 4's diff | Resolved owner-authorized 2026-09-14: main tree verified clean, Task 4 built + tested + proven live in this worktree; Checkpoint 4 done. Merge coordination with the `debug-mcp` session stays owner-side |

## Open questions

Carried from the spec: file store (recommended, Task 2 implements) and viewer-in-v1
(recommended yes-but-minimal, Task 3). Both reversible post-merge; neither gates Slice 1.
