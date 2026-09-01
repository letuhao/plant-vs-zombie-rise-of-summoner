# Spec: `player-materialise`

**Module id:** `player-materialise` · **Program:** [demon-seed](../demon-seed-map.md) · **Build order:** 16 of 16
**Model calls:** none, ever. This runs on a player's machine.

## Objective

At profile creation, roll every species container against that player's **world seed** and write the
result to their own tables — frozen for the life of the save.

Owner, Q7: *"the in game runtime will read the seed, run in game generate, and generate map table and
concrete species for use **when player is created**."*
Owner, Q5: *"base specie (around 900) is generate by read seedsmith generated seed, it only generate
when game start and frozen, **generate by deterministic function, so very fast**."*

## Design

### 1. The two layers, and which one this module writes

Owner's decision: **shared definitions, per-player materialisation.**

| Layer | Holds | Written by |
|---|---|---|
| shared | species definition + derived stats + the container definition | `species-import` (module 12), one copy |
| **per-player** | **the rolled effect instance** for each species | **this module**, once per player |

`WaveCatalog`, `DemonRecipeCatalog`, `DemonMaterialCatalog` and `LaneCost` keep reading the **shared**
layer and need no player context — that is what the two-layer answer bought, and `catalog-runtime`
(module 13) depends on it staying true.

**Stats are not rolled.** They derive deterministically from the anchor through one `P(Θ)`, so they are
identical for every player and belong in the shared layer. **Only effects roll.**

### 2. The seed

```text
player.worldSeed                                   rolled once at profile creation, shown in the UI
species roll seed = hash(worldSeed, "species", speciesId)
```

Two independent namespacing axes that cannot disturb each other: **which save** (`worldSeed`) and
**which layer of the resolver** (the per-layer RNG stream names). Adding a resolution layer later
therefore does not shift any existing roll.

`InstanceRow.RollSeed` already exists as a stored `long`, so the column is not new — only what fills it.

### 3. Frozen forever, append-only — Q5

| Event | Behaviour |
|---|---|
| profile creation | every species in the catalog is rolled and written |
| a later `catalog_revision` **adds** species | the new ones are rolled on next load and **appended**, from the same world seed |
| a later `catalog_revision` **retunes** an affix | **existing rolls are untouched.** The retune reaches new rolls only |
| a species is removed upstream | its rows stay; a player never loses what they had |

### 4. ⭐ Derived, not merely stored

`(worldSeed, catalog_revision)` reproduces the whole roster, so the player's table is a **cache of a
derivation, not the only copy of a fact**:

- a lost or corrupted roster table **rebuilds**
- *"why does my conezombie have this?"* **replays exactly** — a support question with an answer
- storage is an **optimisation**, so 904 rolls happen once rather than per session

**The cost is purity, and it is absolute.** No clock, no unseeded `Random()`, **no dictionary or hash-set
iteration order** anywhere in the derivation. A single impure input destroys every property above, and
it will not fail loudly — it will just produce a different roster on the next machine.

### 5. Performance — measured, not asserted

The owner's *"very fast"* is probably right, and this spec says why rather than repeating it.

**On the path:** per value, one `ContentScale.Apply` (a widened multiply and a divide) and one RNG draw.
**Not on the path:** power. `PowerJson` is nullable and *"E9 owns power, lands later, and backfills."*

**The unmeasured cost is the write** — roughly 904 instance rows plus ~5,400 instance-atom rows in one
transaction. That is well inside SQLite's capability, but it has never been run, and the acceptance
criterion is a number, not a belief.

> ⚠️ **Standing warning.** `PowerReads.IntegerFifthRoot` is a **binary search over `BigInteger`**,
> required because *"five categories near 6000 each already overflow Int64."* It is correctly off this
> path. Moving power onto profile creation would put ~5,400 BigInteger binary searches inside it.
> **Keep power backfilled.**

### 6. The dev reforge — A4

Freezing rolls means **a retuned affix cannot be observed without a new profile**, which taxes the exact
loop this repo protects everywhere else by moving numbers into `data/tuning/`.

`POST /api/debug/reforge-world` re-derives the roster from the current catalog against the same world
seed. It sits behind the existing debug surface, never reaches players, and costs one endpoint precisely
because §4 made the roster derivable. The player-facing "offer a reforge" option was considered and
rejected — this is not that.

### 7. All-or-nothing

One transaction for the whole roster. A partial materialisation leaves a player with some species
carrying effects and some not, and the game starts anyway — failing later, somewhere unrelated. A
refused creation fails where someone is watching.

## Commands

```powershell
dotnet test tests/FusionRpg.Data.Tests --filter PlayerMaterialise
dotnet test tests/FusionRpg.Core.Tests --filter Materialise
curl -X POST http://127.0.0.1:5088/api/debug/reforge-world   # dev only
.\scripts\guard-dal.ps1
```

## Project structure

```text
src/FusionRpg.Core/Demons/Materialise/SpeciesMaterialiser.cs   the derivation, pure and testable
src/FusionRpg.Data/Sqlite/RpgStore.PlayerSpecies.cs            the transaction and every statement
src/FusionRpg.Server/DebugEndpoints.cs                         the reforge endpoint
tests/FusionRpg.Core.Tests/Demons/MaterialiseTests.cs
tests/FusionRpg.Data.Tests/PlayerMaterialiseTests.cs
```

`SpeciesMaterialiser` is pure — seed and catalog in, rows out, no I/O — so every determinism test runs
without a database.

## Code style

```csharp
// The roster is a cache of a derivation, not the only copy of a fact: (worldSeed,
// catalog_revision) reproduces it. That property dies to a single impure input, so
// nothing here reads a clock, an unseeded Random, or a hash-set's iteration order.
```

## Testing strategy

| Test | Asserts |
|---|---|
| `same_world_seed_and_catalog_reproduce_the_roster_exactly` | the derivation property, by fingerprint |
| `two_world_seeds_produce_different_rosters` | the roll is real |
| `no_impure_input_reaches_the_derivation` | a guard test over the materialiser's source |
| `enumeration_order_does_not_affect_output` | shuffle the catalog, same result |
| `added_species_are_appended_not_rerolled` | Q5, existing rows byte-identical |
| `retuned_affix_does_not_touch_existing_rolls` | Q5 |
| `partial_failure_writes_nothing` | all-or-nothing |
| `power_json_is_null_after_materialisation` | the standing warning, as a test |
| `full_roster_materialises_within_budget` | a real timing assertion with a stated number |
| `reforge_reproduces_the_same_roster_when_the_catalog_is_unchanged` | the dev command is safe |

## Boundaries

**Always:** derive from `hash(worldSeed, stream, targetId)`; keep the materialiser pure; one
transaction; append rather than re-roll; keep SQL in `FusionRpg.Data`.

**Ask first:** changing the hash composition (it re-rolls every existing save); adding a per-player
table.

**Never:** read a clock or an unseeded RNG; compute power here; roll per spawn; expose reforge outside
the debug surface; let a partial materialisation commit.

## Success criteria

- [ ] The same `(worldSeed, catalog_revision)` reproduces the roster byte-for-byte, proven by test.
- [ ] A guard test forbids impure inputs in the materialiser.
- [ ] Adding species to the catalog appends without disturbing one existing row.
- [ ] Full-roster materialisation has a **measured** time, stated in the test.
- [ ] `PowerJson` is null on every materialised row.
- [ ] The reforge endpoint exists, is debug-only, and is idempotent against an unchanged catalog.
