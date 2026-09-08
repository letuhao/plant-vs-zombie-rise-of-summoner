# Spec: `structure-instantiate`

**Module 29 of 29 · level c3 · depends on `structure-catalog-import` · [base-defense-map.md](../base-defense-map.md)**
**Status:** spec, 2026-09-04. **Added by pass 3 (P3-3)** — the six structure modules covered seed →
catalog, which is **two layers of three**. This is the middle one.

---

## Objective

**Roll a concrete structure instance per player, using the SDK that already ships.**

`structure-seed-ideal.md` §1 law 1, stated as **binding for every generation feature**:

> *"**Seed → concrete → per-player. Three layers, and the middle one rolls.**
>
> ```text
> SEED       seedsmith, offline, enums only, no magnitudes      committed, diffable
>    |
>    v       the GAME RUNTIME resolves it - seeded per player, like Diablo loot
> CONCRETE   Instantiator.TryInstantiate(container, lookup, rollSeed, thetaContent, ...)
>    |
>    v
> STORED     that player's own tables. "each player play they own game"
> ```
>
> **Never design a second roll.**"*

The owner's words behind it: *"seedsmith generate seed, game generator in game runtime generate
concrete object, per player game store that object … every generator use this sdk baseline, so no need
to duplicated code for all."*

---

## ⭐ This is a wiring module. Almost none of it is new code.

`structure-seed-ideal.md` §2.2 says so directly:

> *"The concrete-roll layer has **no production caller** — `Instantiator.TryInstantiate`: **zero.**
> Every *'we need a runtime generator'* finding for structures is therefore a **wiring gap on a shipped
> SDK, not a new build.**"*

And the SDK is not merely shipped, it is **already reused**: `ActionSeeder.cs:19,45` uses
`Instantiator.Draw` *"unchanged, only its visibility widened."*

**So the whole module is: call it.** The red flag it exists to avoid is *"a new roll implementation
beside `Instantiator`"*, which is the first row of the seedsmith refuse-to-ship table.

---

## What already exists (verified at HEAD, 2026-09-04)

**Built.**

- `Instantiator.TryInstantiate` (`src/FusionRpg.Core/Effects/Atoms/Instantiator.cs:92`) — rolls a
  container into an `InstanceRow` with `RollSeed`, `CatalogRevision`, `ThetaContent` and
  `ContentFingerprint()`. Reproducible over `(container_id, catalog_revision, roll_seed)`.
- `ActionSeeder.cs:19,45` — the precedent, reusing `Instantiator.Draw` verbatim.
- `structure-catalog-import` — the catalog, read from the corpus.

**Wiring gap.** `TryInstantiate` has **zero production callers**, for any content type.

**Real gap.** Nothing rolls a structure instance, and nothing stores one per player.

---

## The contract

### 1. What is rolled, and what is not

This is the part a careless implementation gets wrong, because decision 32 already fixed some of it:

| Property | Rolled per instance? | Why |
|---|---|---|
| **HP, damage** | ❌ **No** | Decision 32: `P(Θ_development) × strengthBand`. **Deterministic** — a wall's toughness is its material and its city, not a die roll |
| **Cost, build turns, footprint, reach** | ❌ No | Ordinals → tuning intervals. Same reason |
| **Traits and actions** | ✅ **Yes** | The container roll. This is what `TryInstantiate` is *for*, and it is the whole reason a structure instance differs from its catalog row |
| **Cover radius / power** | ❌ No | Authored per kind (decision 39) |

> **So the roll is narrow, and that is correct rather than disappointing.** A structure's *numbers* come
> from a ladder; its *behaviour* comes from a container. Rolling the numbers too would put a die between
> the player and decision 32's `P(Θ)`, which is a private curve wearing a roll's clothes.

### 2. The call

```csharp
/// <summary>
/// Rolls one concrete structure instance for one player. A WIRING call — the roll itself is
/// Instantiator's, unchanged. Never a second roll (seedsmith Law 1).
///
/// <para>Reproducible over (containerId, catalogRevision, rollSeed), exactly as every other
/// InstanceRow is. Two players building "iron rampart" get different trait rolls; the same player
/// re-deriving their own save gets the same one.</para>
/// </summary>
public static bool TryInstantiateStructure(
    StructureDef def, ulong rollSeed, int thetaContent, out InstanceRow row);
```

**`rollSeed` derives from `(worldSeed, sectorId, slotIndex, buildTurn)`** through `SeededRng`'s existing
mixer — deterministic, unique per placement, and **replayable**, which the world's turn-report
re-derivation at `RpgStore.WorldTurns.cs:603` requires.

> ⛔ **Do not seed from a clock or a counter.** The re-derivation loop re-runs from turn zero; a
> counter-seeded roll would produce a different structure on replay than the one that was built.

### 3. Stored per player

*"that player's own tables. 'each player play they own game'"* — the instance is stored beside the
player's other `InstanceRow`s, not in the shared catalog. SQL stays inside `FusionRpg.Data`.

### 4. What this module does NOT do

- **No second roll implementation.** If a line here draws a random number, the module is wrong.
- **No magnitude rolling.** See §1.
- **No new SDK.** `Instantiator` is the SDK; this is its first production caller.
- **No catalog change.** `structure-catalog-import` owns the catalog.

---

## Tunables

**None.** The roll's inputs are the container and the seed; every interval it might touch already lives
in `structure-seed.v{n}.json` and is read by `structure-catalog-import`.

## Numeric types

`rollSeed` is **`ulong`**, matching `Instantiator`'s own signature and `WorldTemplateCatalog.Build`'s.
`thetaContent` is `int`. **No magnitudes are produced here** — that is `structure-catalog-import`'s
single ordinal→magnitude function.

## Boundaries

**Always:** call `Instantiator.TryInstantiate` · derive `rollSeed` deterministically from world state ·
store per player · SQL inside `FusionRpg.Data`.

**Ask first:** rolling anything beyond traits and actions.

**Never:** a second roll beside `Instantiator` · a clock- or counter-seeded roll · rolling HP, damage,
cost or any ordinal-derived magnitude · a new SDK.

---

## Testing

| Test | Asserts |
|---|---|
| `Instantiator_has_a_production_caller` | **the wiring gap, closed** — and a companion asserting it had none before |
| `No_second_roll_exists` | source scan for `Random`/`NextInt` in this module |
| `Same_seed_same_instance_10000_times` | reproducibility over `(container, revision, seed)` |
| `Two_placements_roll_differently` | the seed includes slot and turn |
| `Replay_reproduces_the_same_instance` | through the `:603` re-derivation loop — **the one that a counter-seed would break** |
| `Hp_is_not_rolled` | decision 32's `P(Θ)` is untouched by the roll |
| `Only_traits_and_actions_vary_between_instances` | §1's table, asserted |
| `Instance_is_stored_per_player_not_in_the_catalog` | |
| `guard_dal_passes` | SQL boundary |

## Success criteria

1. `Instantiator.TryInstantiate` has its **first production caller**.
2. No second roll exists anywhere in the module.
3. Instances reproduce byte-identically over 10,000 runs and through world replay.
4. Magnitudes are never rolled.
5. Instances are stored per player.

## Open questions

None. Law 1 fixes the shape; decision 32 fixes what is not rolled.
