# Todo: `live-probe-screenshot` (module `lawn-screenshot`)

**Plan:** [live-probe-screenshot-plan.md](live-probe-screenshot-plan.md) · **Spec:**
[../.kilo/worktrees/lawn-screenshot-20260914/docs/architecture/live-probe/spec-lawn-screenshot.md](../.kilo/worktrees/lawn-screenshot-20260914/docs/architecture/live-probe/spec-lawn-screenshot.md) · **Map:**
[../.kilo/worktrees/lawn-screenshot-20260914/docs/architecture/live-probe-map.md](../.kilo/worktrees/lawn-screenshot-20260914/docs/architecture/live-probe-map.md)

> Branch-relative paths. Builds on `worktree-lawn-screenshot-20260914`. Merge is owner-side.

---

## Slice 1 — Capture primitive (injector)

### Task 1: Arm in Drain + end-of-frame runner + `debug.screenshot` case

**Description:** `src/FusionRpg.Injector/ScreenshotCapture.cs` (arm/consume + rate-limit as
commented structural constants) plus `src/FusionRpg.Injector/ScreenshotRunner.cs` (hidden
`DontDestroyOnLoad` `MonoBehaviour`, `WaitForEndOfFrame` coroutine — Unity-docs-mandated,
mid-frame capture is undefined) plus the `debug.screenshot` arm-only case in
`CheatCommandRunner.Drain` emitting `debug.screenshot.ready`. Capture:
`ScreenCapture.CaptureScreenshotAsTexture()` primary with camera-render fallback
(`TypeIconCapture` shape), GPU `Blit` downscale to ≤960px, one `ReadPixels`, `EncodeToPNG`,
destroy all temps. Compiling against the real interop also settles whether `ScreenCapture`
(and, opportunistically, `AsyncGPUReadback` for a named v2) exists in this game's Unity
version. No transport yet — the emit carries size + timestamp so the live check can assert
freshness without a viewer.

**Acceptance criteria:**

- [ ] On the real game build, one `debug.screenshot` command produces a PNG that opens and
  shows the live board including UI chrome (verified once against a defeat/pause overlay).
- [ ] Interop presence recorded: `ScreenCapture` present/absent (fallback promoted if
  absent), `AsyncGPUReadback` present/absent (v2 only), with PerfProbe delta noted in the
  task report (numbers recorded, never pinned in tests).
- [ ] No per-frame work added to `InjectorLoop.Tick` (arm is a flag set; coroutine runs only
  when armed); temps destroyed (no RT/texture leak across repeated shots); runner
  `GameObject` created once per process.
- [ ] Both host projects still build (`FusionRpg.Injector.BepInEx`, `MelonLoader` variants
  with game dir set); host shims untouched (runner lives in shared code); no new packages.

**Verification:**

- [ ] Live: trigger command, pull bytes, open PNG, confirm board + overlay visible.
- [ ] Build: injector host builds green against the real interop.
- [ ] Substrate: no test-side temp store; no new committed binary.

**Dependencies:** None (spec approved).

**Files likely touched:**

- `src/FusionRpg.Injector/ScreenshotCapture.cs` (new)
- `src/FusionRpg.Injector/ScreenshotRunner.cs` (new)
- `src/FusionRpg.Injector/CheatCommandRunner.cs` (one arm-only case-block)

**Estimated scope:** M.

### Checkpoint 1 (code done 2026-09-14; live + interop-compile owner-side)

- [x] Code written on this branch (`ScreenshotCapture.cs`, `ScreenshotRunner.cs`, Drain case,
  `RpgClient.EnqueueScreenshot`); primitive + fallback pre-specified with measurement slot.
- [ ] Live: capture works on the real build via one command (owner terminal — needs game dir).
- [ ] Interop compile green (owner — needs `FUSIONRPG_GAME_DIR`).

---

## Slice 2 — Transport + store (server)

### Task 2: Upload + `POST/GET` + file store + guard

**Description:** `RpgClient.UploadScreenshotAsync` (binary PUT, icon-dump shape);
`POST /api/debug/screenshot` with `// Game Injector Debug` banner relaying
`debug.screenshot` and writing `artifacts/lawn-screenshots/<scenario>-<ts>-<tag>.png` +
`latest.json`; `GET /api/debug/screenshot/latest` serving the file with an accurate
non-relay banner; `artifacts/lawn-screenshots/` gitignored.

**Acceptance criteria:**

- [ ] curl round-trip: POST on a live game → GET latest returns a PNG newer than the POST.
- [ ] `.\scripts\guard-debug-scope.ps1` green; `DebugScope` guard tests green.
- [ ] Events table carries no image bytes (size asserted by reading the stored row kinds).
- [ ] No committed PNG; gitignore entry present.

**Verification:**

- [ ] Live curl round-trip + open-the-bytes check.
- [ ] `.\scripts\guard-debug-scope.ps1` → exit 0.
- [ ] `dotnet test tests/FusionRpg.Guard.Tests --filter "FullyQualifiedName~DebugScope"` green.

**Dependencies:** Task 1.

**Files likely touched:**

- `src/FusionRpg.Injector/RpgClient.cs` (one method)
- `src/FusionRpg.Server/DebugEndpoints.cs` (two routes + banners)
- `.gitignore` (one dir)

**Estimated scope:** M.

### Checkpoint 2 (code done + verified 2026-09-14; live round-trip owner-side)

- [x] Server builds green; `guard-debug-scope.ps1` green (87 routes, 0 mismatches;
  trigger = GameInjectorDebug, store/reads = ManualReview by design with banner notes).
- [x] `DebugScope` guard tests 6/6 green; Server.Tests 438/440 (2 failures pre-existing,
  identical on main).
- [ ] Live curl round-trip returns a fresh PNG (owner terminal).

---

## Slice 3 — Minimal viewer (web)

### Task 3: Latest-frame panel in the control room

**Description:** Minimal panel showing `GET latest` beside the existing debug reads
(recipe + existing pieces, no new top-level route per GG-1); follows
`gui-lego`/`game-gui-principles` for placement; `Phaser`/recharts stay off the entry chunk
(bundle check green).

**Acceptance criteria:**

- [ ] Operator sees the latest frame next to `debug.board-stats` without opening the game.
- [ ] `npm test` green; `npm run build` green (type errors fail); `npm run check:bundle` green.

**Verification:**

- [ ] `npm test`, `npm run build`, `npm run check:bundle` in `web/fusion-rpg-web`.
- [ ] Manual: screenshot visible beside telemetry on a live session.

**Dependencies:** Task 2.

**Files likely touched:**

- `web/fusion-rpg-web/src/...` (panel only, exact file in Task 3 report)

**Estimated scope:** S–M.

### Checkpoint 3 (code done + verified 2026-09-14; live view owner-side)

- [x] Panel + 3/3 vitest green; `tsc` + `vite build` green; full suite 2566/2574 with the
  8 failures pre-existing (identical files on main; zero new violations from these files —
  verified by diffing guard output).
- [x] Bundle check fails identically on main (pre-existing entry-chunk budget breach).
- [ ] Manual: screenshot visible beside telemetry on a live session (owner).

---

## Slice 4 — MCP adapter (built in this session, owner-authorized 2026-09-14)

### Task 4: `debug_screenshot.py` adapter + registration (done, proven live)

**Description:** Thin adapter `tools/debug-mcp/tools/debug_screenshot.py` over the Task 2
endpoints — `screenshot(tag)` triggers `POST /api/debug/screenshot`, polls the ready event
via the new `GET /api/debug/events/tail` (the sibling `/events` reads forward from
`afterId`, so its kinds filter only sees the oldest window — found live), fetches
`GET latest` bytes, returns base64 PNG + optional `save_to` in a scope-stamped envelope
(`scope: "game-injector-debug"`). Failures return the envelope, never raise except on
caller misuse (same shape as `debug_restart_game.py`). Registered in `server.py` + README
walkthrough (11 tools); `test_debug_screenshot.py` (6 stub tests).

**Acceptance criteria:**

- [x] `screenshot(tag)` on a live game returns a real PNG (valid signature) taken after the
  call — proven live 2026-09-14 (`mcplive3`, 960×538, 597KB, opened and confirmed the menu
  frame), or an envelope explaining which half failed (trigger vs poll vs fetch — distinct).
- [x] Scope label present in metadata and every response; budgets honored (bounded poll,
  bounded fetch, caller-side timeout).
- [x] `tools/debug-mcp` pytest 75/75 green (incl. README-walks-every-tool at 11).

**Verification:**

- [x] `python -m pytest tools/debug-mcp/tests -q` → 75 passed.
- [x] Live: MCP call returned the envelope with a viewable PNG (opened).

**Dependencies:** Task 2 (endpoints must exist).

**Files touched (owner-authorized on the `debug-mcp` session's paths; main tree verified
clean first):**

- `tools/debug-mcp/tools/debug_screenshot.py` (new)
- `tools/debug-mcp/tests/test_debug_screenshot.py` (new)
- `tools/debug-mcp/server.py` (registration block)
- `tools/debug-mcp/README.md` (walkthrough item 11)
- `tools/debug-mcp/tests/test_http_mode.py` (count 10→11)

**Estimated scope:** S (actual).

### Checkpoint 4 (done)

- [x] All success criteria in the spec met, including the MCP bullet; handoff lists exact
  regions for owner merge. `debug_call` is the documented bounded equivalent of a
  Playwright-evaluate (no arbitrary-execution tool by design — allowlist refusal is the
  posture, recorded in the spec's Never list).
