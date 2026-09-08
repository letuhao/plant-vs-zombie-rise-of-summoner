# Spec: `world-seed`

**Module id:** `world-seed` · **Program:** [effect-pipeline](../effect-pipeline-map.md) · **Build order:** 7 of 10
**Depends on:** `resolution-order` (module 2)

## Objective

A per-player world seed: created once at player creation, shown in the UI, and composed with a stream
name and a target id to produce every per-player roll this program and `demon-seed` make.

```text
hash(worldSeed, streamName, targetId)
       |            |           |
    the save    which layer   what is rolled
```

Owner, Q7: *"the whole save"* — every per-player generator derives from this one seed, not a
per-feature seed each.

## Design

### Why this exists as its own module and not a field on the player row

The seed alone is not the module; the **derivation contract** is. Every downstream consumer (module 2's
resolver, `demon-seed` module 16's `player-materialise`) must derive the SAME way, or two runtimes
disagree on the same seed — the exact failure `resolution-order`'s named-stream design exists to
prevent one layer down. This module is the one place that owns `hash(worldSeed, streamName, targetId)`,
so every consumer calls it rather than reimplementing it.

```csharp
// SeededRng.DeriveStream returns a stateful PRNG, not a scalar (SeededRng.cs:9-27) — Instantiator's
// own rollSeed parameter is a long, so this module draws exactly one deterministic ulong from the
// freshly-derived stream rather than inventing a second hash function.
public static long DeriveRollSeed(long worldSeed, string streamName, string targetId) =>
    unchecked((long)SeededRng.DeriveStream((ulong)worldSeed, $"{streamName}|{targetId}").NextULong());
```

Reuses `SeededRng.DeriveStream` (`SeededRng.cs:26`) exactly as it already runs in production
(`FusionRoller.cs:27`) — not a new hash function. `Fnv1a64` itself (`SeededRng.cs:75`) is `private` and
stays that way; this module composes `streamName`/`targetId` into one string and lets `DeriveStream`'s
existing public entry point do the hashing.

### Creation, once, shown

Created at player creation (`RpgStore`'s existing player-creation path), stored on the player row,
**never regenerated** for that player (Q5: "existing rolls frozen forever; new species appended"). The
seed is surfaced in the UI — Q7's *"the whole save"* framing implies a player can see and, per §3.5's
release-shape note, potentially share it (accepted consequence, A5: *"a player can compute their whole
roster before playing... accepted, not a bug to be found later"*).

### The seed is release content's key, not a database

`effect-pipeline-ideal.md` §3.5: the import stage runs on the player's machine at first run / version
change, writing the catalog with a `catalog_revision`. The world seed is the OTHER half of what a roll
needs — `(worldSeed, catalog_revision)` together make the whole derived roster reproducible (§3.6), so
a lost or corrupted per-player table is rebuildable from two retained numbers, not the only copy of a
fact.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~WorldSeed"
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~WorldSeed"
```

## Project structure

```text
src/FusionRpg.Core/Effects/Atoms/WorldSeed.cs        new — DeriveRollSeed, the one derivation contract
src/FusionRpg.Data/Sqlite/RpgStore.cs                edit — world_seed column on the player row,
                                                       created once at player creation
tests/FusionRpg.Core.Tests/Atoms/WorldSeedTests.cs   new
```

## Code style

```csharp
// The ONE place hash(worldSeed, streamName, targetId) is computed. Every per-player generator
// (this program's resolver, demon-seed's player-materialise) calls this - never reimplements it,
// or two runtimes derive the same seed two different ways.
```

## Testing strategy

| Test | Asserts |
|---|---|
| `world_seed_is_created_once_at_player_creation` | never regenerated for an existing player |
| `derive_roll_seed_is_pure_and_deterministic` | same three inputs, same output, every call |
| `different_stream_names_never_collide` | two layers rolling the same targetId get different seeds |
| `different_target_ids_never_collide` | two targets in the same stream get different seeds |
| `a_lost_roster_table_reconstructs_from_worldseed_and_catalogrevision_alone` | §3.6's own reproducibility property, proven not asserted |

## Boundaries

**Always:** derive through this module's one function; treat the seed as create-once, never
regenerated.

**Ask first:** exposing a way to CHANGE an existing player's world seed post-creation (that is the
"reforge" surface — module 10, dev-only, never for players per A4).

**Never:** let a feature compute its own per-player seed independently of `DeriveRollSeed`; use a clock
or any non-seed input anywhere in the derivation chain.

## Success criteria

- [ ] Every per-player roll in this program derives through `DeriveRollSeed`.
- [ ] The seed is created once, shown in the UI, and never silently regenerated.
- [ ] `(worldSeed, catalog_revision)` alone reconstructs a lost roster, proven by test.
