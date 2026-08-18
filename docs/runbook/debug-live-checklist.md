# Debug pipeline — LIVE prove checklist

Ordered operator + script checklist for proving debug scenarios in a running game (**BepInEx** host).
MelonLoader twin (Blooms): [`melon-live-checklist.md`](melon-live-checklist.md) — do not overwrite these Bep Pass/Fail rows.
API reference: [`debug-pipeline.md`](debug-pipeline.md).

**Not** EffectBag. Fill Pass/Fail during play; copy notable results into [`../research/effect-runtime/04-proof-results.md`](../research/effect-runtime/04-proof-results.md).

**Session note (2026-08-16):** `combat.hit` restored via **TakeDamage** when `damageFrom` is a `Bullet` (`source=takeDamage`, pea→zombie). Plant **melee** needs **`Zombie.AttackPlant`** (deferred Harmony) — TakeDamage `damageFrom` is often the plant itself (`damageFromClass=WallNut`). Scenario `hit-capture-plant`. Base `HitZombie`/`HitPlant` Harmony stays **off**. **HitLand** deferred (stable) but `combat.hitland` rare (~134 overrides). Details: [`02-hit-pipeline-candidates.md`](../research/effect-runtime/02-hit-pipeline-candidates.md), [`_checklist-hit-plant-live.json`](../research/effect-runtime/_checklist-hit-plant-live.json).

Raw run dumps: [`_checklist-f4plus-live.json`](../research/effect-runtime/_checklist-f4plus-live.json), [`_checklist-reprobe-live.json`](../research/effect-runtime/_checklist-reprobe-live.json).

## 1. Preconditions

| # | Check | Pass? |
|---|---|---|
| 1 | `.\scripts\deploy-play.ps1` deployed; game + BepInEx injector running | **PASS** |
| 2 | Any level open; lawn running (not paused menu) | **PASS** |
| 3 | Simulator **off** (`FUSIONRPG_SIM` unset / sim tab idle) | **PASS** (`simEnabled=false`) |
| 4 | Injector connected (`GET /health` → `injectorConnected`) | **PASS** |
| 5 | Base URL `http://127.0.0.1:5088` | **PASS** |
| 6 | Confirm typeIds: Pea≈0, WallNut≈3, BasicZ≈0 | **PASS** |

## 2. Capture baseline `afterId`

```powershell
$base = Invoke-RestMethod 'http://127.0.0.1:5088/api/debug/events?limit=1'
$afterId = if ($base.items.Count) { $base.items[-1].id } else { 0 }
Invoke-RestMethod -Method POST http://127.0.0.1:5088/api/debug/session/start -ContentType application/json -Body '{}'
```

After each scenario, wait **0.5–2s** (longer for combat). Prefer **`/api/events`** (global; oldest-first — paginate to max id before a probe window).

## 3. Ordered scenario rows (F1–F23)

| # | Scenario | Wait | Script assert | Pass | Fail | Notes |
|---|---|---|---|---|---|---|
| F1 | `p1-baseline` | 1–2s + shots | `zombie.damage` ≈ **20** | **PASS** | | |
| F2 | `p1-plant` | 1–2s | Damage → **100** (ATK×5) | **PASS** | | Plant ATK path |
| F3 | `p1-bullet` | 1–2s | Hit still **20**; `bullet.init` **999** | | **FAIL*** | *Expected: pea ignores Bullet.Damage for hit |
| F4 | `hit-capture` | 6s | `combat.hit` fields | **PASS** | | zombie-side via TakeDamage (`source=takeDamage`, Peashooter) |
| F4b | `hit-capture-plant` | 14s | plant `combat.hit` | **PASS** | | `source=attackPlant`, NormalZombie→WallNut; TakeDamage damageFrom=plant self |
| F5 | `status-butter` | 1s | `debug.status.applied` method=true | **PASS** | | **Visual:** butter stops zombie (with butter look) |
| F6 | `status-freeze` | 1s | method freeze | **PASS** | | |
| F7 | `status-cold` | 1s | method cold | **PASS** | | |
| F8 | `status-poison` | 1s | method poison | **PASS** | | |
| F9 | `status-float-butter` | 1s | `method=false` float CC | **PASS** | | **Visual:** stops **without** butter look; clear → walks again |
| F10 | `status-clear` | 1s | clear runs | **PASS** | | |
| F11 | `def-plant` | 12–14s | `plant.damage` before/after | **PASS** | | bite **50→10** @ DEF×5; spawn col=6 / x≈7.8 |
| F12 | `def-zombie` | 7s | `zombie.damage` scaled | **PASS** | | pea hit **4** (20/5) |
| F13 | `def-alt-paths` | 7s | path real/body/apply | **PASS** | | saw `take`/`body`/`apply` emits |
| F14 | `onkilled-extra` | 2s | `debug.onkill.extra` / ack | **PASS** | | |
| F15 | `onhit-extra` | 7s | `debug.onhit.extra` | **PASS** | | Needs TakeDamage bridge (redeployed) |
| F16 | `onhit-status` | 7s | `debug.onhit.status` | **PASS** | | Same |
| F17 | `onkill-status` | 2s | die + status | **PASS** | | |
| F18 | `kill-signal` | 1s | `zombie.die` | **PASS** | | |
| F19 | `kill-plant` | 1s | `plant.die` | **PASS** | | |
| F20 | `spawn-matrix` | 1s | atk≈77 hp≈300; z hp≈888 | **PASS** | | |
| F21 | `spawn-bullet-hit` | 2s | damage / bullet.init | **PASS** | | + `combat.hit` when TakeDamage sees Bullet |
| F22 | `wave-freeze-check` | 1s | freeze enabled | **PASS** | | |
| F23 | `hitland-butter` | 2s | `combat.hitland` | | **FAIL** | HitLand Harmony **applied** deferred (no AV); no `combat.hitland` events — ~134 HitLand overrides miss base patch |

### Extra plant matrix (manual)

| Plant | typeId | Base | ATK×5 | D-DMG=999 |
|---|---|---|---|---|
| Cabbage-pult | 26 | 40 | **200** | 40 (init 999) |
| Kernel-pult | 28 | 20 | 100/120 | 20/40 |
| Melon-pult | 32 | 80 | 400 (+26) | 80/132 |
| Fume-shroom | 7 | 20 | **100** | 20 |
| Threepeater | 14 | 20 | **100** | 20 (init 999) |

### Extra (not named scenarios)

| # | Call | Assert | Pass | Notes |
|---|---|---|---|---|
| X1 | `POST /api/debug/fire-spawn-extra` `{}` | `pvz.spawn.extra.ack` | **PASS** | Default row=2 after fix |
| X2 | `POST /api/debug/spawn-extra` `{ "typeId": 0 }` | same | **PASS** | |
| X3 | `GET /api/debug/session` | sessionActive | **PASS** | |
| X4 | `GET /api/debug/snapshot` | `debug.snapshot` | **PASS** | |

## 11. StatusRuntime LIVE (F52+)

Prove L2 instances, resisted telemetry, and contagion. **Do not** mix with F5–F10 (`status-butter` / `debug.apply-status`) — those are Unity CC bypass (visual only).

Pin profiles on spawn (`derivedProfile`). After the fire-synthetic harness fix, `debug.effect.synthetic.actorPtr` and `debug.status.instances[].attackerPtr` must be the **plant** at col 2 / row 2, not the seed zombie (`attackerPtr != hostPtr`).

Automated prove: [`../../scripts/prove-status-full.ps1`](../../scripts/prove-status-full.ps1) (default skips F5–F10; pass `-IncludeUnityBypass` for those). Raw dump: [`../research/effect-runtime/_prove-status-full.json`](../research/effect-runtime/_prove-status-full.json).

**Polling:** do **not** use `GET /api/debug/events?limit=1` as `afterId` — that page is the oldest events. Capture max id (binary search on `/api/events`), run **one** scenario, wait for `debug.run-steps.done`, then page `afterId` for `debug.status`, `debug.status.resisted`, `debug.actor-derived`, `debug.effect.synthetic`.

| # | Call | Wait | Script assert | Pass | Fail | Notes |
|---|---|---|---|---|---|---|
| F52 | `POST /api/debug/scenario/status-l2-wither` | 1s | instance `wither`; plant `attackerPtr` | | | plant `neutral`, zombie `glass` |
| F53 | `POST /api/debug/scenario/status-l2-snapshot` or `GET /api/debug/status` | 0.5s | `debug.status` instances + `resisted[]` | | | alias: `POST /api/debug/effect/dots` |
| F54 | `POST /api/debug/scenario/status-l2-resist` | 1s | `debug.status.resisted` `reason=PotencyFloor` | | | zombie `iron-dot`; no wither instance |
| F55 | `POST /api/debug/scenario/status-l2-blight-row` | 2s | seed + row neighbor `blight`; row-3 control empty | | | Z1 x=7.5 r2, Z2 x=8.2 r2, Z3 r3 |
| F56 | `status-l2-leech` | 1s | instance `leech`; plant attacker | | | OverTime |
| F57 | `status-l2-rally` | 1s | instance `rally` | | | Buff |
| F58 | `status-l2-expose` | 1s | instance `expose` | | | Debuff |
| F59 | `status-l2-command` | 1s | instance `command` | | | Meter |
| F60 | `status-l2-shatter` | 1s | instance `shatter` | | | Debuff |
| F61 | `status-l2-bond` | 1.5s | instance `bond` + `debug.combat.packet` fa10 after 5 hits | | | Counter everyHits=5 |
| F62 | `status-l2-rot` | 2s | 2+ `rot` hosts (column) | | | Contagion |
| F63 | `status-l2-spark` | 2s | 2+ `spark` hosts (square) | | | Contagion |
| F64 | `status-l2-pact-mark` | 2s | `pact_mark` instance | | | Random spread |
| F65 | `status-l2-spore` | 2s | 2+ `spore` hosts (rect) | | | Contagion |
| F66 | `status-l2-butter` | 1s | instance + `synthetic.actions` > 0 | | | UnityCc L2; not F5 |
| F67 | `status-l2-freeze` | 1s | instance + synthetic actions | | | UnityCc |
| F68 | `status-l2-cold` | 1s | instance + synthetic actions | | | UnityCc |
| F69 | `status-l2-poison` | 1s | instance + synthetic actions | | | UnityCc |
| F70 | `status-l2-hypno` | 1s | instance + synthetic actions | | | UnityCc |
| F71 | `status-l2-ember` | 1s | instance + synthetic actions | | | UnityCc |
| F72 | `status-l2-jala` | 1s | instance + synthetic actions | | | UnityCc |
| F73 | `status-l2-kelp` | 1s | instance + synthetic actions | | | UnityCc |
| F74 | `status-l2-charm-pulse` | 1s | instance `charm_pulse` | | | CrowdControl |
| F75 | `status-l2-resist-cc` | 1s | `PotencyFloor` on butter; no instance | | | zombie `iron-cc`, plant `caster` |
| F76 | `status-l2-resist-contagion` | 2s | seed blight; neighbor `PotencyFloor` | | | neighbor `iron-contagion` |
| F77 | `status-l2-poison-immune` | 1s | `Immunity`; no poison instance | | | zombie `immune-poison` |
| F78 | `status-l2-actor-derived` | 1s | `GET /api/debug/actor-derived?ptr=` plant matches caster pin | | | `status.power.omni` ≥ 100 |

Optional operator visual (Unity bypass, F5–F10): butter/freeze/cold/poison look-and-feel. Distinct from F66–F69.

```powershell
# Capture the newest event id (not limit=1).
function Get-MaxEventId([string]$url = 'http://127.0.0.1:5088') {
    function Has-After([long]$id) {
        $page = Invoke-RestMethod -Uri "$url/api/events?afterId=$id&limit=1"
        return @($page.items).Count -gt 0
    }
    if (-not (Has-After 0)) { return 0L }
    $lo = 0L; $hi = 1L
    while (Has-After $hi) { $lo = $hi; $hi = $hi * 2L }
    while (($hi - $lo) -gt 1L) {
        $mid = $lo + (($hi - $lo) / 2L)
        if (Has-After $mid) { $lo = $mid } else { $hi = $mid }
    }
    $tail = Invoke-RestMethod -Uri "$url/api/events?afterId=$lo&limit=500"
    return [long]@($tail.items)[-1].id
}

$after = Get-MaxEventId
Invoke-RestMethod -Method POST http://127.0.0.1:5088/api/debug/scenario/status-l2-wither -ContentType application/json -Body '{}'
# Wait for debug.run-steps.done, then:
Invoke-RestMethod "http://127.0.0.1:5088/api/debug/events?afterId=$after&kinds=debug.status,debug.status.resisted,debug.actor-derived,debug.effect.synthetic,debug.run-steps.done&limit=200"
```

## 4. Final P1 verdict

| Case | Typical damage | vs baseline | Pass? |
|---|---|---|---|
| F1 baseline | **20** | — | **PASS** |
| F2 plant ATK×5 | **100** | ≫ | **PASS** |
| F3 bullet set 999 | hit **20** / init **999** | ≈ | **FAIL** (expected) |

P1 product verdict: **plant ATK drives hit damage** (pea + cabbage/fume/threepeater). **`Bullet.Damage` writable but not hit path** for those shooters. Hit ≠ interval ≠ DPS.

## 6. Expansion LIVE (F24+) — speed / economy / spawn-death / produce / board / MC

Prove remaining Effect surfaces. Raw dump (after run): [`../research/effect-runtime/_checklist-expand-live.json`](../research/effect-runtime/_checklist-expand-live.json).  
Opportunity map: [`../research/effect-runtime/07-effect-opportunities.md`](../research/effect-runtime/07-effect-opportunities.md).

**Economy note:** sun/money/points are Effect-capable (`economy` tag). Assert via HUD + `board.economy` / `debug.economy`. Product Effects must ICD/cap — not banned at API level.

| # | Scenario | Wait | Operator visual | Script assert | Pass | Fail | Notes |
|---|---|---|---|---|---|---|---|
| F24 | `econ-sun-set` | 0.5s | Sun shows **777** | `board.economy` / `debug.economy` which=sun | **PASS** | | `debug.economy` value=777 (HUD may drift with sun.gain) |
| F25 | `econ-sun-add` | 0.5s | Sun **150** (100+50) | economy events | **PASS** | | suns 100→150 |
| F26 | `econ-money-set` | 0.5s | Money **888** | economy money | **PASS** | | |
| F27 | `econ-money-add` | 0.5s | Money **225** | economy | **PASS** | | 200+25 |
| F28 | `econ-points-set` | 0.5s | Points **42** | economy points | **PASS** | | |
| F29 | `zombie-speed-slow` | 2s | Zombie **crawls** | `debug.spawn.zombie` uniqueSpeed≈0.3 | **PASS** | | **Visual:** very slow (operator) |
| F30 | `zombie-speed-fast` | 2s | Zombie **rushes** | uniqueSpeed≈2 (+ x10 live) | **PASS** | | **Visual:** very fast |
| F31 | `onspawn-inspect` | 0.5s | Pea + zombie on board | plant atk≈55 hp≈400; zombie hp≈1234 | **PASS** | | exact |
| F32 | `ondeath-inspect` | 0.5s | Both gone | `plant.die` + `zombie.die` | **PASS** | | |
| F33 | `zombie-atk-bite` | 12–14s | Wall-nut takes big bites | `plant.damage` ≫ baseline | **PASS** | | bite **250** (×5 of 50) |
| F34 | `plant-produce` | 2–5s | Sunflower; faster sun drops? | `thePlantProduceInterval`≈1 | **PASS** | | **Visual:** generates faster |
| F35 | `board-config-speed` | 2s | Zombies feel slow (E-ZS=0.4/0.25) | `board.modifiers` / `debug.board.config` | **PASS** | | modifiers E-ZS=0.4 LIVE; `theOriginSpeed` scaled (~0.53 vs ~1.33) |
| F36 | `spawn-mc` | 1s | Hypno / mind-controlled look | `isMindControlled=true` | **PASS** | | **Visual:** hypno works |

Raw: [`../research/effect-runtime/_checklist-expand-live.json`](../research/effect-runtime/_checklist-expand-live.json).

### Leftover probes (2026-08-16)

| Probe | Result | Ship in v1 Effects? |
|---|---|---|
| F35 `Board.config` E-ZS | **PASS** — modifiers + originSpeed scale on env LIVE pass | **Yes** (match affix) |
| `Z-TAKEMULT` / takeDmgMultiplier | Writer exists; LIVE **inconclusive** (no clear dmg change / field on dump) | **No** for now |
| Forced `spawn-bullet` + Damage=999 | Many bulletType ints hit for **999** (**operator confirm**) | **Yes** — Effect **action** + **onHit trigger** when spawned bullet lands; not plant-fired pea |
| Plant-fired pea + D-DMG | Still ≠ hit (known) | Keep plant ATK for shooters |
| Jala / kelp CC | Not wired in `ApplyStatus` | **No** |
| `combat.hit` | TakeDamage Bullet + AttackPlant | **Yes** (pea + plant bite LIVE) |
| `combat.hitland` / base HitLand | Harmony applied; overrides miss | **No** event yet — NOT SHIPPED for Effects |
| HitZombie/HitPlant Harmony | Still skipped (unsafe config off) | **No** |

Raw leftovers: [`../research/effect-runtime/_checklist-leftovers-live.json`](../research/effect-runtime/_checklist-leftovers-live.json).

```powershell
# Example: run one expansion scenario
Invoke-RestMethod -Method POST http://127.0.0.1:5088/api/debug/scenario/zombie-speed-slow -ContentType application/json -Body '{"scenarioId":"exp-slow"}'
```

## 8. Environment / field LIVE (F37+)

Prove mid-match AOE + graves. Inventory: [`../research/effect-runtime/08-environment-field-surface.md`](../research/effect-runtime/08-environment-field-surface.md).  
Raw: [`../research/effect-runtime/_checklist-env-live.json`](../research/effect-runtime/_checklist-env-live.json).

| # | Scenario | Wait | Operator visual | Script assert | Pass | Fail | Notes |
|---|---|---|---|---|---|---|---|
| F37 | `env-freeze` | 2s | Ice AOE / CC on zombie | `debug.board.action` op=freeze | **PASS** | | event + spawn; prefer zombie-pos for VFX |
| F38 | `env-doom` | 2s | Doom crater | `debug.board.action` doom + `grid.place` CraterDay | **PASS** | | setPit crater @5,2 |
| F39 | `env-fireline` | 3s | Row fire | fireline + `zombie.damage` / die | **PASS** | | kill observed |
| F40 | `env-cherry` | 2s | Cherry blast | cherry action + die | **PASS** | | |
| F41 | `env-grave` | 1s | Grave on tile | `grid.place` type=Grave(7) | **PASS** | | `debug.spawn.grid` |
| — | Scene weather | — | fog/snow/pool mid-match | `sceneType` / `theBoardType` on snapshot | **N/A** | | Day lawn: sceneType=0, theBoardType=0 — **NOT SHIPPED** as Effect (LEVEL-BOUND) |

```powershell
Invoke-RestMethod -Method POST http://127.0.0.1:5088/api/debug/scenario/env-freeze -ContentType application/json -Body '{"scenarioId":"env-1"}'
Invoke-RestMethod http://127.0.0.1:5088/api/debug/snapshot
```

## 10. Tile / grid LIVE (F42+)

Spawn/clear graves & ice, paint `BoxType`, on-kill arms. Raw: [`../research/effect-runtime/_checklist-tile-live.json`](../research/effect-runtime/_checklist-tile-live.json).

| # | Scenario | Wait | Operator visual | Script assert | Pass | Fail | Notes |
|---|---|---|---|---|---|---|---|
| F42 | `tile-grave` | 1s | Grave @4,2 | `grid.place` + `hasGrave` | **PASS** | | |
| F43 | `tile-grave-clear` | 1s | Grave gone | `debug.grid.clear` + `grid.die` | **PASS** | | `hasGrave` false after |
| F44 | `tile-iceblock` | 1s | Ice block | `grid.place` IceBlock(8) | **PASS** | | |
| F45 | `tile-box-water` | 1s | Water tile look? | `debug.box.set` Grass→Water | **PASS** | | via `BoardGrid.set_boxType` |
| F46 | `tile-box-grass` | 1s | Back to grass | Water→Grass | **PASS** | | |
| F47 | `tile-box-lava` | 1s | Lava tile? | Grass→Lava | **PASS** | | gameplay TBD |
| F48 | `onkill-grave` | 1s | Grave on kill | `debug.onkill.grave` + `grid.place` | **PASS** | | zombie curse |
| F49 | `onkill-clear-grave` | 1s | One grave removed | `debug.onkill.clear-grave` | **PASS** | | plant-side clear |
| F50 | `tile-box-dirt` | 1s | Scorched / dirt tile (+ crater) | Grass→Dirt + hasPit | **PASS** | | nuclear alias; Effect: destroy grass |
| F51 | `tile-ice-road` | 2s | Ice trail on row | `debug.ice.road` | | **FAIL** | Spawns **Sledge/Driver zombie**; no usable ice-trail Effect — **NOT SHIPPED** |

`Board.roadType` alone is **not** a full lawn map (len≈12) — do not use for Effects.

## 9. Sign-off

| Field | Value |
|---|---|
| Date | 2026-08-16 |
| Game / pack version | 3.8.1 / FusionRpg |
| Pea / Sunflower / Wall-nut / BasicZ / Grave / IceBlock | 0 / 1 / 3 / 0 / 7 / 8 |
| Operator | NeneScarlet |
| Script / assert run | F1–F41 + F42–F50 tile/grid; F51 ice-road fail |
| Notes | Grave/Dirt/BoxType READY; ice-road **NOT SHIPPED** (spawns Sledge); scene weather NOT SHIPPED |

```powershell
Invoke-RestMethod -Method POST http://127.0.0.1:5088/api/debug/session/end
```
