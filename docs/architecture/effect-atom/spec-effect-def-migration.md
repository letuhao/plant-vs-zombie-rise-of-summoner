# Spec: effect-def-migration (E11)

Module **E11** in the [atom effect map](../effect-atom-map.md). Depends on **E7**, **E8**, **E14a** (it imports its seed rows).

> **Reads [definitions.md](definitions.md)** — the shared vocabulary pinned after the 2026-08-22 audit. Where this spec and the definitions disagree, **the definitions win**.

> **This is Checkpoint D.** All 16 defs are rows, the fixtures pass unchanged, `EffectSeedCatalog` is deleted. **The claim "a new effect costs one row" is either true here or the design failed.**

## Objective

Migrate the 16 hardcoded `EffectSeedCatalog` defs into `effect_atom` rows and prove the new path is **behaviourally identical** against the existing fixture corpus. This is the cheapest possible migration — it targets effects Foundation already executes — and it is the only place the schema gets falsified before content authoring begins.

## Design (locked on approval)

### The corpus is 49 fixtures, not 19

The program quoted "19 fixtures" for several rounds. The sweep found:

| Group | Count | Exercises |
|---|---|---|
| `effect-*.json` | 19 | FA1–FA9 opcodes, grant lifecycle, ICD, owner-key scoping |
| `combat-*.json` | 5 | FA10 targeting, Area/Row fan-out, Counter, DoT, heal |
| `status-*.json` | 25 | StatusRuntime apply, resist, immunity, all five contagion geometries |
| golden plan files | 15 | byte-comparison targets |

The parity gate is therefore **2.5× stronger** than advertised. Use all of it.

### Acceptance: byte-identical plans

For every fixture, running it through the atom path must produce an `IntentPlanDto` **byte-identical** to the one the current path produces. Not equivalent — identical. Any diff is either a schema bug or a deliberate, documented behaviour change, and there are no deliberate ones in this module.

Behaviour changes are **out of scope for E11 by design.** If a fixture cannot pass without one, that is the finding, and it stops the wave.

### The `subject` back-compat rule

E3 requires every `sideIs`/`typeIdIs` leaf to declare `subject: actor | target`, with no default — because on `OnDamageDealt`, legacy `filters.side`/`typeId` mean the **damaged** entity (`EffectProcAndOwner.cs:103–118`).

Migration therefore writes **`subject: target`** onto **every** leaf of every migrated `OnDamageDealt` predicate — not only side/type leaves. The inversion is a property of the event, so `hasStatus` and `hpBelowMilli` are equally affected, and leaving them defaulted would silently point them at the wrong entity and break the 25 status fixtures. That preserves today's inverted semantics exactly, keeps the fixtures byte-identical, and confines the legacy quirk to migrated rows. New content must state its subject and gets no inherited surprise. The drift is recorded in this spec, not carried silently forward.

### Multi-trigger defs — resolved

Two defs carry trigger **lists**. Both migrate without a behaviour change:

| Def | Triggers | Migrates to |
|---|---|---|
| `fx.shield_grant` | `OnDamageDealt`, `OnTimer`, `OnSpawn` | **3 atoms**, all `icd_key: shield-grant` — one shared clock, so hit-then-spawn still fires **once** |
| `fx.passive_atk_flat` | `OnGranted`, `OnRemoved` | **1 atom**, no trigger — it is a permanent modifier; `EffectBag` already injects `remove = true` itself |

See [definitions.md](definitions.md) §14. Without `icd_key` the first would have gained three independent
ICD clocks and fired twice where it fires once today — a behaviour change inside the module whose whole
acceptance is byte-identical plans.

### Multi-trigger defs — resolved

Two defs carry trigger **lists**. Both migrate without a behaviour change:

| Def | Triggers | Migrates to |
|---|---|---|
| `fx.shield_grant` | `OnDamageDealt`, `OnTimer`, `OnSpawn` | **3 atoms**, all `icd_key: shield-grant` — one shared clock, so hit-then-spawn still fires **once** |
| `fx.passive_atk_flat` | `OnGranted`, `OnRemoved` | **1 atom**, no trigger — it is a permanent modifier; `EffectBag` already injects `remove = true` itself |

See [definitions.md](definitions.md) §14. Without `icd_key` the first would have gained three independent
ICD clocks and fired twice where it fires once today — a behaviour change inside the module whose whole
acceptance is byte-identical plans.

### The `mods_json` grant migration — inherited from E6

`rpg_unique_stat_mods.mods_json` holds `{ absolutes, grants }` per instance.

- **`grants` move** into `effect_binding`, one row each. Only possible **here**: a binding points at an
  instance of a container, and a legacy grant names an `effectId` that has no container until this module
  creates one. E6's spec assigned this to E6, which could not do it.
- **`absolutes` stay where they are.** They are Tab B/C `Override` writes on a hand-built channel map, and
  effects cannot emit `Override` at all (E1). Moving them would smuggle a fourth write path into this program.

One-way and **idempotent**: re-running it on an already-migrated instance is a no-op.

### The 16 defs, plus two irregulars

| Def | Kind it becomes |
|---|---|
| `fx.butter_on_hit`, `fx.freeze_on_hit`, `fx.cold_on_hit`, `fx.poison_on_hit` | `status.apply` |
| `fx.clear_butter` | `status.clear` |
| `fx.passive_atk_flat` | `stat.modify` |
| `fx.spawn_zombie_ondeath`, `fx.spawn_plant_bullet` | `spawn.entity` |
| `fx.board_cherry` | `board.action` |
| `fx.grid_item_cycle` | `grid.spawn` + `grid.clear` (two atoms, one container) |
| `fx.set_dirt_box` | `box.set` |
| `fx.economy_sun` | `resource.economy` |
| `fx.icd_butter`, `fx.spawn_butter` | `status.apply` |
| `fx.overlay_damage` | `resource.delta` |
| `fx.shield_grant` | `shield.grant` |

**Irregular 1 — `fx.patron_aura`.** A 17th def, defined but deliberately **not** in `CreateAll()`: a Passive with **no triggers and no actions**, whose magnitudes live in `PatronRuntimeState` and apply as a compose-time overlay. It migrates as a **marker container with zero atoms**, preserving exactly that: the grant is the lifecycle anchor, nothing more. Do not invent atoms for it — that is the patron spec's call.

**Irregular 2 — `fx.shield_grant`.** Its def carries **empty params**; every magnitude is overlay. And `GrantShield` is not in `InjectorEffectActionSink` — it executes bag-side in Core. Migration records that irregularity; normalising the execution path is not this module's job.

### ~~Deleting `EffectSeedCatalog` breaks the VFX mirror~~ — **phantom, retired 2026-08-22**

`VfxCatalog.cs:67` really does say *"C#-seeded catalog, mirroring `EffectSeedCatalog`"* — but **verification showed that comment is stale prose, not a coupling**. It is the only occurrence of `EffectSeedCatalog` in the file, and the file contains **zero `fx.*` ids**: every row keys on a **statusId**. `VfxCatalog` mirrors the *status* catalog.

So there is no cue → effect-def coupling to break, no cross-stream gate, and **the deletion is unblocked**. The lesson worth keeping: a comment is not evidence.

While here, correct the comment so the next reader is not sent down the same path.

### ⚠️ The "both paths" seam does not exist yet — build it first

Step 2 assumes the fixture runner can be pointed at an alternate catalog. It cannot: `EffectScenarioRunner.Run` hardcodes `new SimEffectHost(...)`, and `SimEffectHost`'s constructor hardcodes `catalog.ReplaceAll(EffectSeedCatalog.CreateAll())`. `WithCatalog` exists on both `SimEffectHost` and `FoundationHarness` but is **unreachable from `RunFile`**.

So E11's Structure gains `EffectScenarioRunner.cs` and `SimEffectHost.cs`, and step 0 is threading the catalog source through. Without it the module cannot execute its own acceptance.

### Order of work

0. Thread a catalog source through `EffectScenarioRunner` → `SimEffectHost` (above).
1. Author the 16 defs as seed rows; import them with **E14a**; load alongside the existing catalog.
2. Run every fixture through **both** paths; diff plans.
3. Close diffs until zero.
4. Delete `EffectSeedCatalog`; switch **all five** call sites to the row catalog — `BattleEffects.cs`, `SimEffectHost.cs`, `EffectRuntime.cs`, `CheatCommandRunner.cs`, and `FoundationHarness.cs` itself; fix the stale `VfxCatalog` comment.
5. Re-run E13's benchmark against the migrated catalog and confirm the chosen encoding still wins.

**Stamping `contentHash` into fixture reports moved to E12.** Adding a new stamped field to a report *is* a golden diff — E11 cannot both add it and promise the 15 golden files are byte-identical. E12 already has Checkpoint E for exactly that re-bless.

Step 3 is the module. Steps 1–2 are setup and 4–5 are cleanup.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~EffectScenario|Status"
dotnet test tests\FusionRpg.Data.Tests
.\scripts\guard-funnel-delta.ps1
.\scripts\guard-dal.ps1
```

## Structure

```
data/seed/atoms/fx-*.json                                 (new — the 16 as rows)
src/FusionRpg.Core/Effects/FoundationHarness.cs           (EffectSeedCatalog deleted at step 4)
src/FusionRpg.Core/Battle/BattleEffects.cs                (catalog source seam — the fifth call site)
src/FusionRpg.Core/Effects/SimEffectHost.cs               (catalog source seam — step 0)
src/FusionRpg.Core/Vfx/VfxCatalog.cs                      (fix the stale comment only)
src/FusionRpg.Injector/Effects/EffectRuntime.cs           (catalog source swap)
src/FusionRpg.Injector/CheatCommandRunner.cs              (`effects.reload` source swap)
tests/FusionRpg.Core.Tests/Atoms/MigrationParityTests.cs  (new — the gate)
tests/FusionRpg.Core.Tests/Atoms/OneRowClaimTests.cs      (new — Checkpoint D's claim, tested where it is claimed)
tests/FusionRpg.Core.Tests/Atoms/OneRowClaimTests.cs      (new — Checkpoint D's claim, tested where it is claimed)
```

## Testing strategy

| Case | Expect |
|---|---|
| All 49 fixtures, both paths | **byte-identical** `IntentPlanDto` |
| All 15 golden plans | unchanged files |
| `effect-icd-butter` (no `icd_ms` override) | 250 ms default preserved exactly |
| `effect-butter-filter` (`plant:7` scoping) | same skip/fire pattern |
| `effect-withdraw-on-die` | `entity:` grant auto-withdraw preserved |
| 25 status fixtures | resist reasons and contagion geometries identical |
| `fx.patron_aura` | migrates as a zero-atom marker; no atoms invented |
| `VfxCatalog` | untouched by the deletion — it keys on statusIds and never referenced an `fx.*` id |
| E13 benchmark on real content | chosen encoding still wins |
| `EffectSeedCatalog` after step 5 | absent from `src/`; nothing references it |
| Adding a 17th effect afterwards | **one row, no build** — the Checkpoint D claim, tested |
| Guards | all pass |

## Boundaries

**Always:** diff plans byte-for-byte; write `subject: target` on migrated `OnDamageDealt` filters; keep both paths runnable until step 3 closes.

**Ask first:** any behaviour change to pass a fixture.

**Never:** re-bless a golden in this module — goldens move in **E12**, not here; invent atoms for `fx.patron_aura`; leave a hand-maintained mirror pointing at a deleted source.
