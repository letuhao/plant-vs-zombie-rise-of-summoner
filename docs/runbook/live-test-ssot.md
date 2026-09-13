# Live-test SSOT

**Operator + script contract** for in-game LIVE proves. Preferred driver: Python package [`tools/live_test`](../../tools/live_test/). PowerShell under `scripts/` remains as legacy recipes.

Related: [debug-pipeline.md](debug-pipeline.md) (API recipes), [level-entry.md](../research/level-entry.md) (enter-level gate), [debug-live-checklist.md](debug-live-checklist.md) / [melon-live-checklist.md](melon-live-checklist.md) (host checklists), [live-test-maintain.md](../contributing/live-test-maintain.md) (enrich/maintain rule). Code route map: [`DebugEndpoints.cs`](../../src/FusionRpg.Server/DebugEndpoints.cs). Protocol table: [rest.md](../protocol/rest.md).

## 0. Golden path — one full Live Probe run, start to finish

**Read this section before running or scripting a Live Probe.** It is the mandatory sequence, in
order. Each numbered step names the exact API/command, why it exists, and the failure it prevents.
Sections 1–8 below are reference detail for the pieces this sequence calls; this section is the
procedure itself.

**Governing rule (unchanged by this section):** a debug call may trigger a real operation but must
never fabricate its result — see [live-probe-standard.md](../contributing/live-probe-standard.md).
Nothing here licenses treating a queued HTTP response as proof; every step below is proven by
polling the resulting event, not by the `{ "ok": true, "queued": 1 }` envelope.

### Step 1 — Redeploy before every new test instance

A stale injector build or a stale running game/server from a prior session is not a valid starting
state — never reuse one across test instances. Kill it and redeploy clean:

```powershell
# Server: survives tool-tree cleanup (an assistant tool call's own process does not)
Start-Process dist\FusionRpg.Server\FusionRpg.Server.exe
# Injector only — never let deploy-play also restart a server from an assistant session
.\scripts\deploy-play.ps1 -NoServer
# Confirm before touching anything else
Invoke-RestMethod http://127.0.0.1:5088/health
# Expect: ok=true, injectorConnected=true, simEnabled=false
Invoke-RestMethod http://127.0.0.1:5088/api/debug/lawn/state
# Read state/asOf/sinceMs before assuming a clean slate -- a prior instance's board can still be
# Cycling/Defeated/InMatch. See Step 6 and live-probe-standard.md §6.
```

If a game process from an earlier test instance is still running, treat it as **the wrong frame**:
close it and start over from a fresh `deploy-play.ps1` deploy, rather than attaching a new test run
to old process state (a stale `G-TIMEFREEZE`/`G-TIMESCALE`/cheat-toggle carryover from a previous
session's run is a real, observed failure mode — see the 2026-09-13/14 incident in Step 2).

### Step 2 — Enter the level, then close the seed-picker screen (mandatory UI gate)

`UIMgr.EnterGame` (`POST /api/debug/enter-level`) opens the level, but the vanilla **"Choose Your
Plants"** seed-picker screen (the panel with the "一起摇滚吧!" / "Let's Rock!" button) stays on
screen afterward. **While it is open, the match has not started: waves do not spawn, and plants and
zombies do not act.** This is not cosmetic — it is a hard precondition, and skipping it silently
produces a "board" that will never generate a single real hit.

Close it with `debug.skip-setup` (`InitBoard.QuickInGame()`), gated behind `DEBUG-SETUP-SKIP`:

```powershell
Invoke-RestMethod -Method POST http://127.0.0.1:5088/api/cheats/toggle `
  -ContentType application/json -Body '{"id":"DEBUG-SETUP-SKIP","enabled":true}'
Invoke-RestMethod -Method POST http://127.0.0.1:5088/api/debug/setup/skip `
  -ContentType application/json -Body '{"method":"quick","timeoutSec":15}'
# Success: debug.setup.skip { ok: true, board: true, ui: true }
```

**As of 2026-09-14, `POST /api/debug/lawn/quick-start` does this step for you automatically** — it
self-enables `DEBUG-SETUP-SKIP` and calls `debug.skip-setup` right after entering the level, before
freezing waves or running any scenario (`DebugEndpoints.cs`, `/lawn/quick-start` handler). Prefer
quick-start for a new test instance; call `/setup/skip` directly only when driving `/enter-level`
by hand or reusing an already-open board outside quick-start.

*Incident this step exists because of (2026-09-13/14):* a live run of `lawn-combat-observer` (T0)
could not capture a single real vanilla hit against a freshly entered board. The operator's own
screenshot showed the seed-picker screen still open — `/lawn/quick-start` had entered the level but
never dismissed it, so the "run" had never actually started. The fix landed in `/lawn/quick-start`
itself (commit `8224dae4`); this doc section and the standalone `/setup/skip` path remain for any
flow that does not go through quick-start.

### Step 3 — Freeze waves immediately, before any scenario or spawn work

Vanilla zombie waves must never be allowed to spawn during a controlled test — freeze them the
moment the run starts, before doing anything else on the board:

```powershell
Invoke-RestMethod -Method POST http://127.0.0.1:5088/api/debug/wave-freeze `
  -ContentType application/json -Body '{"enabled":true}'
# Assert: debug.wave.freeze { enabled: true }
```

`/lawn/quick-start` already sequences this immediately after Step 2 and before expanding any
scenario. If driving the API by hand, do not reorder this after a spawn or scenario call — an
un-frozen wave can spawn and attack concurrently with a controlled test actor, corrupting attribution.

### Step 4 — Spawn test actors into specific lanes, then verify before executing

Set the target cell before each spawn (`debug.spawn-cell` caches `col`/`row` for the *next* spawn
call only — set it again before every subsequent spawn into a different lane):

```powershell
Invoke-RestMethod -Method POST http://127.0.0.1:5088/api/debug/spawn-cell `
  -ContentType application/json -Body '{"col":1,"row":2}'
Invoke-RestMethod -Method POST http://127.0.0.1:5088/api/debug/spawn-plant `
  -ContentType application/json -Body '{"type":0}'   # Peashooter at col 1 / row 2

Invoke-RestMethod -Method POST http://127.0.0.1:5088/api/debug/spawn-cell `
  -ContentType application/json -Body '{"col":8,"row":2}'
Invoke-RestMethod -Method POST http://127.0.0.1:5088/api/debug/spawn-zombie `
  -ContentType application/json -Body '{"type":0,"mindControl":false}'   # BasicZ at col 8 / row 2
```

**Do not proceed to the test proper until every spawned actor's identity and location are confirmed**
— never assume a spawn call succeeded silently. **`board-stats` is `POST`-only and does not return
the stats in its HTTP body** — like every other `debug.*` relay, it queues a command and answers
`{ "ok": true, "queued": N }`; the real data arrives as a `debug.board-stats` **event** (verified
live 2026-09-14 — a `GET` to this path falls through to the web UI's SPA route and returns
`index.html`, not stats):

```powershell
$tip = (Invoke-RestMethod "http://127.0.0.1:5088/api/events?afterId=0&limit=1").items[-1].id
Invoke-RestMethod -Method POST http://127.0.0.1:5088/api/debug/board-stats
Start-Sleep -Milliseconds 500
$page = Invoke-RestMethod "http://127.0.0.1:5088/api/events?afterId=$tip&limit=50"
($page.items | Where-Object kind -eq 'debug.board-stats')[-1].payload
# plants[]/zombies[] each carry: ptr, typeId, col, row, attack/attackDamage, hp, maxHp
```

Cross-check every entry against the intended lane and type before executing the test. A spawn that
landed in the wrong lane, at the wrong type, or not at all (empty `plants`/`zombies` array) is a
setup failure, not a test result — stop and re-spawn rather than running the test against an
unverified board.

### Step 5 — Run the test, then read results only from the real event/telemetry path

Never read a "did it work" verdict from an HTTP response body alone. Poll `GET /api/events` (or the
narrower `GET /api/debug/events`) after recording the pre-test max event id, per §2's cursor pattern
below — or, for `lawn-combat-wire` specifically, read the `lawn-combat-observer` run file (an
always-on, non-perturbing instrument; see `tools/LawnCombatObserver`), never console prose and never
a worker's summary.

### Step 6 — A cycling or defeated board is not a mystery: it is now a metric

Two real, live-observed board states that used to produce a confusing silent timeout with no clue to
the real cause — both now self-report instead of requiring a manual event-log diff:

- **Cycling board** (a "quick" setup-skip with no real plants placed loses every wave in seconds, and
  the game auto-retries in a tight loop). `/lawn/quick-start` counts `board.end` events in the last
  10s and refuses immediately (`recentBoardEnds`, `waitedMs`) instead of polling `debug.enter-level`
  into a timeout that can never resolve while the board keeps churning.
- **Defeated board** (`match.result` payload `result:"defeat"` — `GameHooks.cs`'s
  `BoardStatistics.GameOver` hook). Spawn commands still *queue* successfully but land against a dead
  board. `/lawn/quick-start` now checks the latest `match.result` and proactively runs
  `debug.reset-board` when it says defeat, reporting `defeatReset: true`. **Proven live 2026-09-14**:
  after a real defeat, a manual `debug.reset-board` + `spawn-plant` produced a real
  `plant.spawn`/`debug.spawn.plant` event immediately after.
- **What this does NOT fix**: the game's own visual "重新开始" (restart) / defeat overlay stays on
  screen — `reset-board` restores API-level spawn capability, but there is no sanctioned debug command
  today to dismiss that overlay (confirmed by inspection: no restart/replay/back-to-menu case exists
  in `CheatCommandRunner.cs`). A real player or operator still needs to click through it or return to
  the main menu for a visually clean board. Do not report a defeat-recovered board as "clean" — it is
  API-usable, not player-presentable.

Both checks run automatically inside `/lawn/quick-start` (`DebugEndpoints.cs`) — driving the API by
hand still needs its own `board.end`/`match.result` check before trusting a poll to resolve.

**Query it directly instead of re-deriving it by hand:**

```powershell
Invoke-RestMethod http://127.0.0.1:5088/api/debug/lawn/state
# { state: "Cycling"|"Defeated"|"Victorious"|"InMatch"|"LevelEntryPending"|"Unknown",
#   asOf, sinceMs, recentBoardEnds, latestMatchResult, note }
```

Call this **before** reporting any live-probe outcome, not just before a spawn/scenario call. It is
read-only (no injector relay, no side effects) — safe to call as often as needed. Full state machine —
every signal behind these six states, its reliability, and every known blind spot:
[lawn-run-state-machine.md](../architecture/live-probe/lawn-run-state-machine.md). See
[live-probe-standard.md](../contributing/live-probe-standard.md) §6 for the full rule and the incident
that produced it: an `{ ok: true }` response and an operator's own eyes disagreed, and there was no
single query either side could check instead. `state: "Unknown"` cannot tell the main menu, the
seed-picker screen, or a paused match apart (no passive telemetry fires for any of them), and no state
read can see what is actually rendered — a defeat overlay can outlive `debug.reset-board` clearing the
entities behind it. When the question is what a human sees on screen, ask the human; this answers what
the simulation recorded.

### Step 7 — Tear down before the next test instance

End the debug session, then return to Step 1 for the next instance — never layer a new test onto a
board a previous instance already mutated:

```powershell
Invoke-RestMethod -Method POST http://127.0.0.1:5088/api/debug/session/end
```

---

## 1. Preflight

| Check | Expect |
|---|---|
| Server | `GET http://127.0.0.1:5088/health` → `ok=true` |
| Injector | `injectorConnected=true` (heartbeat ≤5s) |
| SIM | `simEnabled=false` for LIVE |
| Melon env | `$env:FUSIONRPG_ML_GAMEDIR` = Melon pack (3.9) |
| Game | Injector connected — **no manual lawn** when using `Ensure-LiveLabBoard` / `audit -Live` |

All-in-one board setup (preferred): [`scripts/lib/LiveLawnSetup.ps1`](../../scripts/lib/LiveLawnSetup.ps1) / Python `ensure_lab_board()` / skill [live-lawn-quick-start](../../.claude/skills/live-lawn-quick-start/SKILL.md). Legacy mid-match: operator in Adventure day + `setup-lab-run.ps1`.

Deploy (Melon, reuse server):

```powershell
$env:FUSIONRPG_ML_GAMEDIR = "H:\Games\PVZ-Fusion-3.9_MelonLoader"
.\scripts\deploy-play.ps1 -LoaderHost MelonLoader -NoServer
# or: python -m live_test deploy --launch
```

From assistant sessions: start server with `Start-Process dist\FusionRpg.Server\FusionRpg.Server.exe` (tool-tree `deploy-play` can kill the server).

## 2. Command / event model

Every `POST /api/debug/*` returns `{ "ok": true, "queued": 1 }` (or similar). That only means the command was enqueued.

**Truth** = poll events after Unity drain:

- `GET /api/events?afterId={tip}&limit=100` (scripts/harness default)
- or `GET /api/debug/events?afterId=&kinds=&scenarioId=`

Pattern:

1. Record tip = max event id
2. POST command
3. Wait for expected `kind` with `id > tip`
4. Parse `payload` (string JSON or object)

Trap: `afterId=0` + `kinds=` returns the **oldest** matching page — use tip cursor for live asserts.

## 3. Level / run setup

| Path | What it does | Use when |
|---|---|---|
| **Manual** | Operator: main menu → Adventure → day; leave lawn running | **Default reliable** |
| `POST /api/debug/lawn/quick-start` | One-call orchestrator: enter-level → **skip-setup (2026-09-14+)** → wave-freeze → scenario expand → board-snapshot poll | **Preferred for a new test instance** — see §0's golden path |
| `POST /api/debug/enter-level` | Gated `UIMgr.EnterGame` | Gate on: cheat `DEBUG-LEVEL-ENTRY` or env `FUSIONRPG_LEVEL_ENTRY=1`. Assert `debug.level.enter ok=true` then `board.start` — HTTP queued ≠ entered. Leaves the seed-picker screen open — follow with setup-skip (below) before anything else |
| `POST /api/debug/scenario/{id}` | Expands named steps on **current** board | Mid-match lab only — does **not** open a level |
| `POST /api/debug/wave-freeze` | `{ "enabled": true }` | Almost every lab starts here |

Named labs (see `DebugScenarios`): `lab-overlay`, `lab-empty`, `lab-shield-bar`.

Refuse lab when latest live `board.start` has `levelType` in `Explore`, `Travel*`, `IZ`.

### Setup panel skip

**As of 2026-09-14, `/lawn/quick-start` performs this step automatically** (self-enables
`DEBUG-SETUP-SKIP` and calls `debug.skip-setup` right after entering the level, before wave-freeze —
see §0 Step 2 and `DebugEndpoints.cs`). The manual sequence below is still the correct path when
driving `/enter-level` directly instead of through quick-start, or when reusing an already-open
board through the standalone `/setup/skip` probe.

The setup panel can be completed through the host game's own IL2CPP handler. The injector command
is gated so ordinary player runs do not invoke it. Set the gate before launching the game, then use
the proof script or endpoint:

```powershell
$env:FUSIONRPG_SETUP_SKIP = "1"
.\scripts\prove-live-setup-skip.ps1 -Method quick
# If the current profile does not advance with QuickInGame:
.\scripts\prove-live-setup-skip.ps1 -Method button
```

The endpoint is `POST /api/debug/setup/skip` with `{ "method": "quick", "timeoutSec": 15 }`.
Success means the injector emitted `debug.setup.skip` with `ok=true`; the HTTP enqueue alone is not
proof. `quick` calls `InitBoard.QuickInGame()`. `button` calls
`InGameUI.OnStartBattleButtonClick()`. Never replace either call with a panel visibility change.

LIVE proof record (2026-09-09, `pvzrh-3.9`, MelonLoader): the first call from the main menu
returned the expected honest refusal, `InitBoard.Instance is null`. After the existing gated level
entry path created an Adventure board (`board.start` event 12267 and `debug.level.enter` event
12273), `-Method quick` returned `ok=true` with `board=true` and `ui=true`. A follow-up
`lab-overlay` run produced live target and plant pointers, including `targetPtr=282FAD2F960` and
`plantPtr=282F898C900`, proving the board continued past setup without a manual click. The
acknowledgement payload reported `ready=false`; that field is diagnostic only, not a success gate.
The direct `button` method remains an explicit comparison probe; `quick` is the proven default for
this profile.

## 4. Debug API encyclopedia (live)

Base: `/api/debug`. Success event kinds usually mirror the command name (`debug.shield.bar-status`, etc.) unless noted.

### Session / observe

| Method | Path | Purpose |
|---|---|---|
| POST | `/session/start` | Start debug session (optional `scenarioId`) |
| POST | `/session/end` | End session / clear arms |
| GET | `/session` | Server mirror: active, scenarioId, arms |
| GET | `/snapshot` | Queue `debug.snapshot` |
| GET | `/events` | Filter events (`afterId`, `kinds`, `scenarioId`, `limit`) |
| GET | `/scenarios` | List named scenario ids |
| POST | `/scenario/{id}` | Expand → `debug.run-steps` |

### Board / spawn / economy

| Method | Path | Purpose |
|---|---|---|
| POST | `/reset-board` | Clear fixtures |
| POST | `/clear-plants` / `/clear-zombies` | Side clear |
| POST | `/spawn-plant` / `/spawn-zombie` / `/spawn-bullet` / `/spawn-cell` | Place entities |
| POST | `/spawn-extra` / `/fire-spawn-extra` | Extra-spawn intent |
| POST | `/wave-freeze` | Freeze/unfreeze waves |
| POST | `/ensure-sun` / `/economy` / `/board-config` / `/board-action` | Sun / board knobs |
| POST | `/enter-level` | Gated level enter probe |
| POST | `/select` / `/kill` / `/kill-plant` | Selection + kill |
| POST | `/set-mods` / `/reset-mods` / `/reapply` / `/board-stats` | Mods + census |
| POST | `/stress-fill` / `/stress-clear` | Mass spawn stress |
| POST | `/spawn-grid` / `/clear-grid` / `/set-box` / `/grid-query` / `/ice-road` | Grid helpers |
| POST | `/arm/{kind}` / `/disarm` | onkill/onhit arms |

### Status / effects / combat / shield / FX

| Method | Path | Purpose |
|---|---|---|
| POST | `/apply-status` / `/apply-status-float` / `/clear-status` | CC / status |
| GET/POST | `/status` / POST `/status/apply` | Status snapshot / apply |
| GET/POST | `/actor-derived` | Derived combat profile |
| POST | `/effect/grant\|withdraw\|clear\|list\|fire-synthetic\|enqueue-delta\|board-snapshot\|dots\|counters` | Effect session |
| GET | `/effects/session-grants` / `/effects/contract` | Grants + FT* contract |
| POST | `/effects/reload` | Reload effects |
| POST | `/combat/pin-element` / `/silence-vanilla` / `/probe` / `/snapshot` | Overlay combat |
| POST | `/shield/grant` / `/clear` / `/demo` / `/demo-all` / `/snapshot` / `/bar-status` | RPG shield + HUD audit |
| POST | `/fx/probe-shaders` / `/world-flash` / `/play` / `/list` / `/mute` / `/unmute` / `/state` | VFX prove |

### Outside `/api/debug` (live harness also uses)

| Method | Path | Purpose |
|---|---|---|
| GET | `/health` | injectorConnected / simEnabled |
| GET/POST | `/api/events` | Global event stream |
| POST | `/api/cheats/toggle` | e.g. `OVERLAY-COMBAT`, `DEBUG-LEVEL-ENTRY` |
| GET/POST | `/api/perf` / `/api/perf/recent` | Perf windows |

## 5. Scenario matrix

Run: `python -m live_test run <id>` (from `tools/live_test`).

### Pack `shield`

| Id | Prove | Setup | Auto assert | Manual |
|---|---|---|---|---|
| `shield.lab` | Pea+zombie 3 stacks | `scenario/lab-shield-bar` | ownerCount≥1, stacks=3 | bars under units |
| `shield.bar` | World VFX path | after lab | dataOwners>0, shaderOk, worldBars>0 (+ resolvedBodies match), early=ok | no top-left IMGUI chip |
| `shield.absorb` | Probe spends shield | OVERLAY-COMBAT + combat.probe | shieldAbsorbed>0, hp drop | — |
| `shield.decade` | 10% display steps | mid-bucket HP | displayRatio == floor(true*10)/10 (min 0.1 if hp>0) | length matches display |
| `shield.hide` | Empty hides bar | clear or drain 0 | worldBars=0 (or no dataOwners) | — |
| `shield.cascade` | Outer→inner | repeat probe | fire→ice→earth order in snapshot | shield.broken optional |
| `shield.toggle` | F9 | bars visible | skip if not exposable | F9 hide/show; F7 settings only |
| `shield.all` | Ordered suite | — | all auto rows | — |

### Pack `lab`

| Id | Prove | Auto assert |
|---|---|---|
| `lab.overlay` | lab-overlay freeze+spawn | living pea+zombie via board-stats/snapshot |
| `lab.empty` | clear board | empty plants/zombies |
| `lab.freeze` | wave-freeze | ack event; freeze enabled |

### Pack `combat`

| Id | Prove | Auto assert |
|---|---|---|
| `combat.probe` | overlay combat probe | hit + probe event |
| `combat.silence` | silence-vanilla | ack |
| `combat.pin-element` | pin-element | ack + element on follow-up probe |

### Pack `status`

| Id | Prove | Auto assert |
|---|---|---|
| `status.apply` | Unity CC `/apply-status` | `debug.apply-status` (no custom VFX) |
| `status.clear` | clear-status | cleared |
| `status.catalog` | Unity CC subset (butter/freeze/cold) | each apply ack |
| `status.l2.apply` | `/status/apply` wither | `debug.fx.state.started` + retry |
| `status.l2.catalog` | 5 custom ids via L2 path | each sustained start |
| `status.l2.organic` | scenario `status-l2-wither` | apply or fx started after fire-synthetic |

### Pack `vfx`

| Id | Prove | Auto assert |
|---|---|---|
| `vfx.shaders` | fx/probe-shaders | probe event ok |
| `vfx.play` | fx/play | play event |
| `vfx.list` | fx/list | cue list non-empty |

### Pack `stress`

| Id | Prove | Auto assert |
|---|---|---|
| `stress.fill` | stress-fill | ack / no disconnect |
| `stress.clear` | stress-clear | ack |
| `stress.noshield` | zero shields stress | run completes |

**Backlog (documented only):** vanilla non-absorb, web `rpgShield*` live, broken-cue art, enter-level L1 green, stress perf share vs baseline.

## 6. Python CLI

```text
cd tools/live_test
python -m live_test doctor
python -m live_test deploy --launch
python -m live_test list
python -m live_test run shield.all
python -m live_test run lab.overlay
python -m live_test run combat.probe
python -m live_test monitor bar-status --interval 1
```

Flags: `--base-url`, `--enter-level`, `--force-setup`, `--amount` (shield absorb).

## 7. PowerShell → Python map (parity)

| Legacy script | Scenario / command | Parity |
|---|---|---|
| `deploy-play.ps1 -LoaderHost MelonLoader` | `live_test deploy --launch` | ≈ |
| `setup-shield-bar-lab.ps1` | `run shield.lab` | ≈ |
| `probe-live-shield-bar.ps1` | `run shield.bar` | ≈ |
| `probe-shield-damage.ps1` | `run shield.absorb` / `shield.decade` | ≈ (decade stronger in Python) |
| `setup-lab-run.ps1` | `run lab.overlay` / `lab.empty` | ≈ (PS1 has richer lawn refuse / `-ThenProve`) |
| `prove-overlay-combat.ps1` | `run combat.probe` | **PS1 stronger** (C1–C10 matrix) |
| `prove-status-full.ps1` | `run status.l2.catalog` / organic | **PS1 stronger** (full status-l2-* matrix) |
| `prove-status-l2-one.ps1` | `run status.l2.organic` | ≈ single scenario |
| `prove-vfx.ps1` | `run vfx.play` / `status.l2.apply` | **PS1 stronger** (shown/mute/rate/state + organic) |
| `stress-test.ps1` | `run stress.fill` | **PS1 stronger** (census settle + perf window) |
| `smoke-melon-live.ps1` | *(no Python pack)* | checklist / Melon host smoke |
| `smoke-effect-scoped-atk.ps1` | *(no Python pack)* | effect scope S1–S5 |
| `probe-perf.ps1` | *(backlog)* | perf capture, not scenario assert |

Prefer Python for **new shield/lab smoke**. Do not delete PS1 until a Python pack has hard-assert parity and this table says ≈.

Maintain rule: [live-test-maintain.md](../contributing/live-test-maintain.md) (agents: `.cursor/rules/live-test-maintain.mdc`).

## 8. Coverage honesty

Three tiers — a matrix row alone is **not** covered until product fields use `require`:

| Tier | Meaning | Examples |
|---|---|---|
| **Python hard** | `Report.require` on product fields | `shield.bar`, `shield.absorb`, `shield.decade`, `shield.hide` |
| **Python smoke** | Event ack / soft `check` / explicit SKIP | `shield.toggle` (F9 manual), `combat.probe` payload soft, `status.apply` Unity CC only |
| **Python L2 hard** | `Report.require` on sustained VFX | `status.l2.apply`, `status.l2.catalog`, `status.l2.organic` |
| **PS1 / checklist only** | Full regression surface | F1–F78, C1–C10, Melon host X/H/S, effect L1–L14, `prove-*.ps1` |

**Python encodes well:** tip cursor, Adventure lawn gate, `--enter-level`, Melon deploy, shield lab→bar→absorb→decade→hide, per-target clear → emit kind `debug.shield.cleared`.

**Still learn elsewhere:**

| Need | Source |
|---|---|
| F-row / C-row regression | [debug-live-checklist.md](debug-live-checklist.md), [melon-live-checklist.md](melon-live-checklist.md) |
| Overlay matchup / miss / crit | `scripts/prove-overlay-combat.ps1` |
| StatusRuntime L2 | `scripts/prove-status-full.ps1` (not `status.catalog`) |
| VFX lifecycle | `scripts/prove-vfx.ps1` |
| Assistant server lifetime | §1 — `Start-Process` server exe |

**Command vs emit kind (trap):** `POST /shield/clear` needs `targetPtr`; success event is `debug.shield.cleared` (not `debug.shield.clear`). Stress fill/clear emit `debug.stress.fill` / `debug.stress.clear` (dots, not hyphens).

**Next expansion (not this harness yet):** overlay.matrix, status.l2.*, effect.scope.*, econ/env/tile packs — add only with hard asserts + SSOT update (see maintain rule).
