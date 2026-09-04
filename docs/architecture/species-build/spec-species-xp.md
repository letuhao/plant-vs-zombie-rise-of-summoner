# Spec: `species-xp`

Module 3 in the [species-build capability map](../species-build-map.md). **No dependencies.** Supplies
the level that `budget-source` turns into points.

## Objective

A demon species must have a **per-player level**. Today the repo levels a *PvZ type* per player — real,
shipped, and lawn-only — and levels a *specimen* per player. Neither is a species.

This module supplies the species level, from **two sources**, because standalone-first requires the
feature to work with the game closed: the injector may *enrich* a feature, never *gate* one.

**Success looks like:** a species has a level a player can raise by playing, with the game open or
closed, and nothing about that level is a private curve.

## Design

### 1. The identity question — decide it, do not assume it

`rpg_actor_progression(player_id, kind, type_id)` already levels `plant`/`zombie` types
(`RpgStore.cs:355-368`), and `LawnElementIndex` already maps `(Side, GameTypeId) → DemonSpeciesDef`
(`src/FusionRpg.Core/Demons/LawnElementIndex.cs`). So a species level *could* be a **join** onto the
existing type level rather than new state.

**It cannot be only a join, for two reasons that must be checked against code before building:**

- **The non-lawn sources have no PvZ type at all.** An expedition battle awards to a specimen and a
  species, never to a `PlantType` int. A join has nothing to join on.
- **`(Side, GameTypeId)` is not guaranteed unique.** `LawnElementIndex`'s own comment records that
  `Validate` enforces unique `SpeciesId`/`DemonTypeId` but **not** unique `(Side, GameTypeId)`, and
  resolves collisions by dropping the loser. A dropped species could never level through a join.

**Therefore: species progression is its own row, and the lawn's existing type XP is *projected* onto
it.** The existing `plant`/`zombie` type rows are **left untouched** — other things read them, and
this module has no mandate to migrate them.

**Storage follows the repo's own precedent, not a new pattern.** `rpg_actor_progression` is keyed on an
`int type_id`, and a `speciesId` is a string. The module picks **one** of these and records why:

| Option | Shape | Cost |
|---|---|---|
| **A — new `kind='species'` + a text key column** | `EnsureColumn` adds a nullable `scope_key TEXT`; species rows use it, existing rows leave it null | Follows the `EnsureColumn` migration precedent (T3.4); one table, one `Reset()` pipeline; a nullable column that only one kind uses |
| **B — a dedicated `rpg_species_progression` table** | Its own partial `RpgStore` slice | Cleaner typing; a second progression store to keep in sync with the ledger, retention and compaction paths |

**Recommendation: A**, because progression already has a ledger, retention, compaction and a
`LevelChangePipeline` bound to it, and B would fork all four. The spec's implementer confirms this
against `RpgStore.Progression.cs` before committing.

### 2. The two sources

| Source | Where | Status today |
|---|---|---|
| **Lawn** | `PlantPlaced`/`ZombieSpawned` already award type XP (`RpgXpAwardMap.FromActivity`'s `PlantPlaced`/`ZombieSpawned` cases) | **built** — this module projects the same fact onto the species row via `LawnElementIndex` |
| **Expedition** | `ExpeditionResolver` already grants specimen XP per battle won (`:32`, `:214`), applied at `RpgStore.Expeditions.cs:313-317` | **wiring gap** — add a species award alongside the specimen award, in the same transaction |

**The web-battle source is explicitly not this module's** (decision 13): it is another program's
endpoint, consumed here when it exists. Expeditions are what satisfies standalone-first *today*.

⛔ **The lawn-only conditional must be handled, not worked around.** `RpgStore.Progression.cs:32-35`
reads `if (!pvzGame && award.Kind != RpgActorKinds.Player) continue;` — *"Web-mode runs never level PvZ
almanac type actors."* That rule is about **PvZ almanac types**, and it stays true for them. A species
row is not a PvZ almanac type, so the condition must be re-expressed to say what it means rather than
widened by accident. Getting this wrong in either direction is a real defect: too narrow and
standalone-first breaks; too wide and web runs start levelling PvZ types, which that line exists to
prevent.

### 3. ✅ The faucet shape — decided, not inherited

`PlantPlaced` awards **+8 per placement, uncapped** — *"every place/spawn awards (not
once-per-type-per-run)"* (`rpg-progression.md`). Under this program that converts directly into
permanent aptitude points, and it rewards **volume of placement rather than engagement**: the cheapest
plant spammed in a safe corner is the optimal way to raise a species. This is precisely the failure the
ideal's §9 prior art documents — Final Fantasy II's players attacked each other because the growth
signal was an *action*, not an *outcome*.

> ### ✅ Verdict, owner, 2026-09-05 — BOTH signals, weighted so the outcome dominates
>
> *"let's make a run more bonus and make place/spawn less bonus, tunable config, so the spawn bonus
> still have they variable"*
>
> **Two award terms, both tunable, sized so the run dominates:**
>
> | Term | Fires | Sized |
> |---|---|---|
> | `runCompletionAward` | once per **resolved match** in which the species was fielded | the **larger** term |
> | `placementAward` | per place/spawn, as today | the **smaller** term |
>
> **Why this is better than either option offered.** Removing the placement award entirely would have
> deleted a signal that genuinely means *"I used this species"* and flattened the variance that makes
> two runs feel different. Keeping it alone rewards volume over engagement. Keeping both and making the
> **outcome the dominant term** kills the grind vector by *ratio* rather than by *ban* — a species
> spammed in a safe corner still earns, it just earns badly compared to playing the match. That is this
> repo's own "priced, never banned" instinct applied to a faucet.
>
> **The ratio is the balance surface**, not a structural constant: both terms live in
> `data/tuning/species-progression.v1.json`, and a balance pass moves them without a rebuild.
>
> **No new capture is needed.** "Was this species fielded in this run" is already derivable from the
> run-scoped place/spawn facts the ingest records today — the run award reads facts that exist, it does
> not ask the injector for anything new.
>
> **SaGa's scaling counter is NOT adopted** — it was the third option and it stays available if the
> ratio alone proves insufficient. It is a second mechanism, and the ratio is one number; try the
> cheaper lever first.

### 4. The curve

Species level reads the **same arithmetic curve shape already shipped** (`RpgXpCurve`,
`XpToNext = first + (level−1) × step`, `RpgProgression.cs:24-61`), with its own `first`/`step` as
tunables. **Unlimited levels** — PS-8; a clamp is a progression ceiling. Overflow throws.

**No private `f(level)`:** this module produces a level, and `budget-source` reads it as a linear index.
Nothing here computes a magnitude from a level.

## Commands

```powershell
dotnet test tests\FusionRpg.Data.Tests --filter Progression
dotnet test tests\FusionRpg.Data.Tests
dotnet test tests\FusionRpg.Core.Tests
.\scripts\guard-dal.ps1
```

## Project structure

```
src/FusionRpg.Core/Progression/RpgProgression.cs         species kind + curve tunables
src/FusionRpg.Core/Progression/RpgXpAwardMap.cs          the species award
src/FusionRpg.Data/Sqlite/RpgStore.Progression.cs        projection, EnsureColumn migration
src/FusionRpg.Data/Sqlite/RpgStore.Expeditions.cs        species award in the reward transaction
data/tuning/species-progression.v1.json                  first/step, runCompletionAward, placementAward
tests/FusionRpg.Data.Tests/SpeciesProgressionTests.cs    new
```

## Code style

- SQL only in `FusionRpg.Data` — `guard-dal.ps1` enforces it.
- Migration via `EnsureColumn`, following T3.4's precedent: a pre-migration database keeps working and
  reads a default.
- Awards are idempotent on the existing dedupe key; a replayed fact never double-levels.
- `long` XP and level; `checked`; no clamp.
- Every tunable in `data/tuning/species-progression.v1.json`; a missing key is a **load rejection naming
  it**, never a default.

## Testing strategy

1. **Lawn projection:** a `PlantPlaced` fact levels the species row, and the species resolved matches
   `LawnElementIndex`'s own answer for that `(Side, TypeId)`.
1a. **The run term dominates the placement term** at the shipped tuning — a single resolved match
   out-earns a plausible number of placements. This is the assertion that keeps the grind vector closed;
   if a balance pass inverts the ratio, this test says so.
1b. **A species fielded in a run earns the run award exactly once**, however many times it was placed.
2. **Game-closed proof (the standalone-first test):** an expedition battle win levels a species with
   **no lawn run in the test at all**. This is the test that proves the invariant, and it must fail if
   the award is removed.
3. **The conditional, both directions:** a web-mode run still does **not** level a PvZ almanac type
   (the existing rule holds), *and* does level a species. Two tests, because widening that condition by
   accident is the likely defect.
4. **Collision safety:** a species that loses a `(Side, GameTypeId)` collision can still be levelled
   through the non-lawn source — i.e. it is not unreachable.
5. **Idempotence:** the same fact ingested twice levels once.
6. **Unlimited levels:** a very large XP award produces a proportionally large level, never a clamp
   (PS-8), and overflow throws rather than wraps.

## Boundaries

- **Always:** leave the existing `plant`/`zombie` type rows untouched; keep the ledger/retention/
  compaction path shared; state the faucet verdict explicitly in this spec.
- **Ask first:** the RATIO between `runCompletionAward` and `placementAward` if a balance pass wants the run to stop dominating (§3 is decided; its sizing is not);
  changing the existing `!pvzGame` rule's meaning for PvZ types; adding a second progression store
  (option B) instead of the recommended column.
- **Never:** cap species level; award species XP from anything but a recorded Activity fact or an
  expedition resolution; compute a magnitude from species level in this module.

## Success criteria

1. A species has a per-player level, raisable from the lawn **and** from an expedition.
2. A test proves the game-closed path with no lawn involvement.
3. The `!pvzGame` rule still prevents web runs from levelling PvZ almanac types — proven by test.
4. The faucet verdict is recorded, with its rate as a tunable.
5. `guard-dal` green; Data + Core suites green; a pre-migration database still opens.
