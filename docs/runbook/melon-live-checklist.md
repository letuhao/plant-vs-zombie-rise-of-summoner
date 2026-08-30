# MelonLoader LIVE prove checklist

Ordered operator + script checklist for proving the **MelonLoader** injector on Blooms 3.8.1.
API reference: [`debug-pipeline.md`](debug-pipeline.md).  
BepInEx LIVE record (do not overwrite): [`debug-live-checklist.md`](debug-live-checklist.md).  
P0 type dump: [`../research/melonloader-assembly-csharp-p0.md`](../research/melonloader-assembly-csharp-p0.md).

**Not** EffectBag. Fill Pass/Fail during Melon play only. Leave SIM and `FUSIONRPG_MELON_SKIP_HARMONY` unset.

**Session note:** **pvzrh-3.9** Melon pack (`H:\Games\PVZ-Fusion-3.9_MelonLoader`). See [game-versioning.md](../architecture/game-versioning.md).

## 0. Pre-launch (before first Melon boot)

```powershell
$env:FUSIONRPG_ML_GAMEDIR = "H:\Games\PVZ-Fusion-3.9_MelonLoader"   # or Blooms 3.8.1 Game Files
# Optional force: $env:FUSIONRPG_GAME_PROFILE = "pvzrh-3.9"
# Do not set FUSIONRPG_SIM or FUSIONRPG_MELON_SKIP_HARMONY for LIVE proof.
.\scripts\deploy-play.ps1 -LoaderHost MelonLoader
```

Confirm after deploy:

| Check | Expect |
|---|---|
| `Mods\FusionRpg.Injector.MelonLoader.39.dll` (3.9) or `.dll` (3.8.1) | Present |
| `Mods\fusionrpg.cfg` | `ServerUrl=http://127.0.0.1:5088` (or launcher port) |
| Foreign mods in `Mods\` | Untouched (owned-only uninstall) |
| Dual-load | No `winhttp.dll` / Bep markers on this pack |

After lawn is open:

```powershell
.\scripts\smoke-melon-live.ps1
```

## 1. Preconditions

| # | Check | Pass? |
|---|---|---|
| 1 | `deploy-play.ps1 -LoaderHost MelonLoader`; game + Melon injector running | **PASS** |
| 2 | Any level open; lawn running (not paused menu) | **PASS** |
| 3 | Simulator **off** (`FUSIONRPG_SIM` unset) | **PASS** |
| 4 | Skip-harmony **off** (`FUSIONRPG_MELON_SKIP_HARMONY` unset) | **PASS** |
| 5 | `GET /health` → `injectorConnected: true`, `source: "injector"`, `simEnabled: false` | **PASS** |
| 6 | Base URL `http://127.0.0.1:5088` (or launcher port) | **PASS** |
| 7 | TypeIds: Pea≈0, WallNut≈3, BasicZ≈0 | **PASS** |

## 2. Hello gate (P3)

Melon log: `MelonLoader\Latest.log` or `MelonLoader\Logs\Latest.log`.

| # | Check | Pass? | Notes |
|---|---|---|---|
| H1 | MelonMod FusionRpg started (LoggerInstance / MelonRpgLog) | **PASS** | MelonLoader.39 |
| H2 | Harmony SafePatchAll ran (not stub-only) | **PASS** | HitPlant skipped via `HarmonyDontPatchAll` |
| H3 | `CreateZombie.SetZombie` `patch.failed` (if any) | **PASS** | X1/X2 still acked via extra spawn path |
| H4 | Heartbeat reaches server (`injectorConnected`) | **PASS** | |

## 3. Capture baseline `afterId`

`ListEvents(afterId=0)` returns **oldest** rows. Always page from a high watermark (`smoke-melon-live.ps1` does this), e.g.:

```powershell
# Prefer smoke script; manual: use /api/events?afterId=<knownMax>&limit=...
Invoke-RestMethod -Method POST http://127.0.0.1:5088/api/debug/session/start -ContentType application/json -Body '{}'
```

After each scenario, wait **0.5–2s** (longer for combat). Prefer `/api/events?afterId=...` or `/api/debug/events?afterId=...&kinds=...`.

## 4. Priority A — Core (do first)

| # | Scenario | Wait | Script assert | Pass | Fail | Notes |
|---|---|---|---|---|---|---|
| F1 | `p1-baseline` | 1–2s + shots | `zombie.damage` ≈ **20** | **PASS** | | dmg 20×3; smoke PASS |
| F2 | `p1-plant` | 1–2s | Damage → **100** (ATK×5) | **PASS** | | saw 100 after baseline peas |
| F5 | `status-butter` | 1s | `debug.status.applied` method=true | **PASS** | | |
| F6 | `status-freeze` | 1s | method freeze | **PASS** | | method=true |
| F7 | `status-cold` | 1s | method cold | **PASS** | | method=true |
| F8 | `status-poison` | 1s | method poison | **PASS** | | method=true |
| F9 | `status-float-butter` | 1s | `method=false` float CC | **PASS** | | |
| F10 | `status-clear` | 1s | clear runs | **PASS** | | `debug.status.cleared` |
| F11 | `def-plant` | 12–14s | `plant.damage` before/after | **PASS** | | bite **50→10** @ DEF×5 (needs ~20s if melee late) |
| F12 | `def-zombie` | 7s | `zombie.damage` scaled | **PASS** | | pea hit **4** (20/5) |
| F18 | `kill-signal` | 1s | `zombie.die` | **PASS** | | |
| F19 | `kill-plant` | 1s | `plant.die` | **PASS** | | |
| F20 | `spawn-matrix` | 1s | atk≈77 hp≈300; z hp≈888 | **PASS** | | plant atk 77 / hp 300; z hp 888 |
| X3 | `GET /api/debug/session` | — | sessionActive | **PASS** | | |
| X4 | `GET /api/debug/snapshot` | — | `debug.snapshot` | **PASS** | | |

## 5. Priority B — SetZombie risk (P0 arity delta)

Melon `CreateZombie.SetZombie` has **5** params; shared Harmony may miss this processor. TakeDamage arity matches — hit paths are separate.

| # | Call | Assert | Pass | Fail | Notes |
|---|---|---|---|---|---|
| X1 | `POST /api/debug/fire-spawn-extra` `{}` | `pvz.spawn.extra.ack` | **PASS** | | ack + zombie.spawn |
| X2 | `POST /api/debug/spawn-extra` `{ "typeId": 0 }` | same | **PASS** | | |

## 6. Priority C — Hit capture

| # | Scenario | Wait | Script assert | Pass | Fail | Notes |
|---|---|---|---|---|---|---|
| F4 | `hit-capture` | 6s | `combat.hit` fields | **PASS** | | side=zombie source=takeDamage dmg=20 |
| F4b | `hit-capture-plant` | 14s | plant `combat.hit` | **PASS** | | side=plant source=attackPlant |

## 7. Cheat pack smoke

```powershell
Invoke-RestMethod -Method POST http://127.0.0.1:5088/api/cheats/probe `
  -ContentType application/json -Body '{"packId":"pack.smoke-core"}'
# Watch cheat.inject / stat.applied; then:
Invoke-RestMethod -Method POST http://127.0.0.1:5088/api/cheats/probe/end `
  -ContentType application/json -Body '{"probeId":"<id>","reason":"done"}'
```

| # | Pack | Assert | Pass | Fail | Notes |
|---|---|---|---|---|---|
| C1 | `pack.smoke-core` | `cheat.inject` / `stat.applied` (or expectedKinds) | **PASS** | | |

## 8. Capacity — remaining F rows

Same scenario ids as Bep checklist. Session **2026-08-16** Melon 3.9 LIVE.

### HitPlant / HitZombie (both hosts)

| Hook | BepInEx | Melon 3.9 | Product path |
|---|---|---|---|
| `Bullet.HitZombie` / `HitPlant` | **Off** (`EnableUnsafeHitPatches=false`) | **Off** same + `[HarmonyDontPatchAll]` so Melon auto-`PatchAll` cannot apply them at load (IL2CPP NRE) | **`TakeDamage` → `combat.hit`** (pea); **`Zombie.AttackPlant`** (melee) |
| `Bullet.HitLand` | Deferred apply; ~134 overrides miss | Same | `combat.hitland` still rare / not shipped |

**No Melon-only Hit* workaround required** — same policy as Bep. Do not flip `EnableUnsafeHitPatches=true` unless researching subtype-safe hooks.

### Band results

| # | Scenario | Pass | Fail | Notes |
|---|---|---|---|---|
| F3 | `p1-bullet` | | **FAIL*** / capability **PASS** | *Product:* hit **20**, `bullet.init` **999** (pea ignores Bullet.Damage). Melon write+hit proven 2026-08-16 redeploy |
| F13 | `def-alt-paths` | **PASS** | | `zombie.damage` + `combat.hit` |
| F14 | `onkilled-extra` | **PASS** | | `debug.onkill.extra` + ack |
| F15 | `onhit-extra` | **PASS** | | |
| F16 | `onhit-status` | **PASS** | | |
| F17 | `onkill-status` | **PASS** | | |
| F21 | `spawn-bullet-hit` | **PASS** | | Forced bullet **Damage=50 → hit 50** + `debug.spawn.bullet` / `bullet.init` / `combat.hit` |
| F22 | `wave-freeze-check` | **PASS** | | |
| F23 | `hitland-butter` | | **FAIL** | Match Bep — no `combat.hitland` (overrides) — **not an Effect foundation blocker** |
| F24–F28 | econ-* | **PASS** | | |
| F29–F30 | zombie-speed-* | **PASS** | | uniqueSpeed 0.3 / 2 |
| F31–F32 | onspawn/ondeath | **PASS** | | |
| F33 | `zombie-atk-bite` | **PASS** | | bite **250** |
| F34–F36 | produce / board / MC | **PASS** | | |
| F37–F41 | env-* | **PASS** | | |
| F42–F47, F50 | tile-* | **PASS** | | |
| F48 | `onkill-grave` | **PASS** | | Retest after `DeadZombies.Clear` in `DeleteAllZombies` |
| F49 | `onkill-clear-grave` | **PASS** | | Same |
| F51 | `tile-ice-road` | **PASS** | | `debug.ice.road` emitted (Effect trail still not shipped) |

### Effect foundation L1–L14 (Melon 3.9)

All **PASS** 2026-08-16 redeploy — results: [`../research/effect-runtime/_prove-melon39-foundation-live.json`](../research/effect-runtime/_prove-melon39-foundation-live.json).

**Verdict: Melon 3.9 unblocks Effect foundation work** (same surface as Bep). Known non-blockers: HitZombie/HitPlant off, `combat.hitland` rare.

### Scoped FA1 ATK (match / plant:N / entity)

After deploy, with lawn open:

```powershell
.\scripts\smoke-effect-scoped-atk.ps1
```

Asserts via `debug.board-stats` (living `plants[].attack` + `sessionMods`). Results: [`../research/effect-runtime/_prove-melon39-scoped-atk.json`](../research/effect-runtime/_prove-melon39-scoped-atk.json).

| # | Scenario | Expect |
|---|---|---|
| S1 | `effect-entity-atk` | col1 ATK > col3 (entity grant then sibling spawn) | **PASS** 2026-08-16 |
| S2 | `effect-plant-type-atk` | pea ATK > wall-nut (`plant:0`) | **PASS** |
| S3 | `effect-match-midspawn` | both peas equal after match grant + mid-spawn | **PASS** |
| S4 | `effect-spawn-then-grant` | col1 > col3 (select A then entity grant) | **PASS** |
| S5 | `effect-entity-midspawn` | only A buffed; withdraw restores | **PASS** |

## 8b. Overlay combat prove (C1–C13) — T8, Melon 3.9

**T8 (`tasks/aura-skill-todo.md`) closed here, not on the Bep checklist** — this run was against
`H:\Games\PVZ-Fusion-3.9_MelonLoader`, so the results belong on this page, not
[`debug-live-checklist.md`](debug-live-checklist.md)'s own Bep-only C1–C10 table (which stays
unfilled — it was never re-run on that host and this page's rule against overwriting Bep rows cuts
both ways).

Setup: `POST /api/debug/lawn/quick-start` (`.claude/skills/live-lawn-quick-start/`) opened level 1,
froze the wave, fired `lab-overlay`, and returned `targetPtr=22D78434960` / `plantPtr=22D77EF5240`.
Proof: `.\scripts\prove-overlay-combat.ps1 -TargetPtr 22D78434960 -ActorPtr 22D77EF5240`.

| # | Scenario | Pass | Notes |
|---|---|---|---|
| C1 | `overlay-fire-vs-ice` | **PASS** | `matchupBonus=25` |
| C2 | `overlay-fire-vs-air` | **PASS** | `matchupBonus=-25` |
| C3 | `overlay-hybrid-vs-ice` | **PASS** | `matchupBonus=17.5` |
| C4 | `overlay-miss` | **PASS** | `hit=false finalSignedDelta=0` |
| C5 | `overlay-heal` | **PASS** | no overlay breakdown; heal pass-through |
| C6 | `overlay-flag-off` | **PASS** | pass-through -100; no overlay emit |
| C7 | `overlay-ice-vs-fire` | **PASS** | `matchupBonus=-25` |
| C8 | `overlay-air-vs-earth` | **PASS** | `matchupBonus=-25` |
| C9 | `overlay-earth-vs-air` | **PASS** | `matchupBonus=25` |
| C10 | `overlay-force-crit` | **PASS** | `crit=true critMultiplierFinal=1.99330714907572` |
| C11 | `overlay-heal-with-payload-scales-with-heal-power` | **PASS** | `healed=50` (expected ~50) |
| C12 | `overlay-heal-with-no-payload-still-reads-heal-power` | **PASS** | `healed=50` — proves `FinalizeHeal` ran despite no payload |
| C13 | `overlay-full-mitigation-resolves-to-zero-no-chip-floor` | **PASS** | `finalSignedDelta=0`, no exception |

13/13 PASS, 2026-08-30. Raw: [`../research/effect-runtime/_prove-overlay-combat.json`](../research/effect-runtime/_prove-overlay-combat.json).
`OVERLAY-COMBAT` promoted to default-on in all three cheat registries (`CheatRegistry.cs`,
`CheatSchema.cs`, `CheatState.cs`) immediately after this run, per `spec-overlay-combat-enable.md` §7's
"only after the proof" rule. No golden moved — full 6-suite .NET re-run after the flip:
`Core.Tests` 4663/4663 (1 pre-existing, order-dependent allocation-benchmark flake unrelated to this
change, confirmed clean in isolation), `Data.Tests` 539/539, `Server.Tests` 60/60, `Guard.Tests`
116/116, `Launcher.Tests` 162/162, `CheatCore.Tests` 40/40, `E2E.Tests` 194/194 (this last one also
fixed a real, unrelated pre-existing build break in its own `ContractTuningTestBootstrap.cs`, found
during this pass — see `tasks/aura-skill-todo.md`'s T22 entry).

## 9. Sign-off

| Field | Value |
|---|---|
| Date | 2026-08-16 |
| Pack | **pvzrh-3.9** Melon (`H:\Games\PVZ-Fusion-3.9_MelonLoader`) |
| Hello (H1–H4) | **PASS** |
| Core (Priority A) | **PASS** |
| SetZombie X1/X2 | **PASS** |
| Hit F4/F4b | **PASS** |
| Cheat C1 | **PASS** |
| Capacity | **PASS** (F3*/F23 expected like Bep) |
| Effect L1–L14 | **PASS** |
| Bullet spawn+hit | **PASS** (`spawn-bullet-hit` dmg 50) |
| Operator | script + operator |
| Notes | DeadZombies fix deployed. Hit* Harmony off both hosts. Ready to continue Effect foundation / Secondary. |

```powershell
Invoke-RestMethod -Method POST http://127.0.0.1:5088/api/debug/session/end
```
