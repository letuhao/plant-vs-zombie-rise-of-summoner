# Live-test SSOT

**Operator + script contract** for in-game LIVE proves. Preferred driver: Python package [`tools/live_test`](../../tools/live_test/). PowerShell under `scripts/` remains as legacy recipes.

Related: [debug-pipeline.md](debug-pipeline.md) (API recipes), [level-entry.md](../research/level-entry.md) (enter-level gate), [debug-live-checklist.md](debug-live-checklist.md) / [melon-live-checklist.md](melon-live-checklist.md) (host checklists), [live-test-maintain.md](../contributing/live-test-maintain.md) (enrich/maintain rule). Code route map: [`DebugEndpoints.cs`](../../src/FusionRpg.Server/DebugEndpoints.cs). Protocol table: [rest.md](../protocol/rest.md).

## 1. Preflight

| Check | Expect |
|---|---|
| Server | `GET http://127.0.0.1:5088/health` → `ok=true` |
| Injector | `injectorConnected=true` (heartbeat ≤5s) |
| SIM | `simEnabled=false` for LIVE |
| Melon env | `$env:FUSIONRPG_ML_GAMEDIR` = Melon pack (3.9) |
| Game | Adventure/Challenge **day** lawn live — not Explore / Travel / Idle |

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
| `POST /api/debug/enter-level` | Gated `UIMgr.EnterGame` | Gate on: cheat `DEBUG-LEVEL-ENTRY` or env `FUSIONRPG_LEVEL_ENTRY=1`. Assert `debug.level.enter ok=true` then `board.start` — HTTP queued ≠ entered |
| `POST /api/debug/scenario/{id}` | Expands named steps on **current** board | Mid-match lab only — does **not** open a level |
| `POST /api/debug/wave-freeze` | `{ "enabled": true }` | Almost every lab starts here |

Named labs (see `DebugScenarios`): `lab-overlay`, `lab-empty`, `lab-shield-bar`.

Refuse lab when latest live `board.start` has `levelType` in `Explore`, `Travel*`, `IZ`.

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
| `status.apply` | apply-status | status present |
| `status.clear` | clear-status | cleared |
| `status.catalog` | small id subset | each apply+clear or skip |

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
| `prove-status-full.ps1` | `run status.catalog` | **mis-mapped** — L2 F52–F78 vs Unity CC smoke subset |
| `prove-vfx.ps1` | `run vfx.play` / `vfx.shaders` | **PS1 stronger** (shown/mute/rate/state) |
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
| **Python smoke** | Event ack / soft `check` / explicit SKIP | `shield.toggle` (F9 manual), `combat.probe` payload soft, `status.*` Unity CC only |
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
