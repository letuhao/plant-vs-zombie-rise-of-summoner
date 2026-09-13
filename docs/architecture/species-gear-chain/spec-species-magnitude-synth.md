# Spec: Species magnitude synthesizer (`species-magnitude-synth`)

**Initiative:** `species-gear-chain` ([map](../species-gear-chain-map.md)) · **Module:** `species-magnitude-synth`
**Owning program:** `creature-seed`
**Depends on:** `threat-band-fill` (it rewrites every magnitude)
**Status:** spec, 2026-09-13. Awaiting owner approval. No build authorized.
**Source ideal:** [tier-system-ideal.md](../tier-system-ideal.md) § AUDIT A7, DO #3, § The shape 3

---

## Objective

**Turn a 904-row content program into an importer change.**

⭐ **This is the highest value-per-effort item in the whole initiative, and the reason is one
measurement:** the numbers already exist. Every one of them.

Until this lands, **the entire `threatBand → Θ → P(Θ)` bake — the one propagation path this whole
design rests on — reaches no gameplay at all**, and no tier work downstream is observable. The call
site is live and correct on every deploy; it fails closed only because the corpus is empty.

---

## What exists today — measured this session

### Built — all of it except the last hop

**Measured over `data/generated/creatures/*.json`:**

> **904 of 904 species carry magnitudes. 26 distinct channels.** Ten of them are present on **all
> 904** — `resource.max.{hp, hunger, qi, spirit, stamina}` and `resource.regen.{…}` — with
> `combat.power.omni` on 457 and `combat.block.shred.omni` on 404.

**They are already persisted.** `RpgStore.Species.cs:155-159` writes them row by row:

```sql
DELETE FROM creature_species_magnitude WHERE species_id = $id;
INSERT INTO creature_species_magnitude (species_id, channel, value) VALUES ($id, $ch, $v);
```

**The consumer is live, correct, and already reached on every deploy.**
`ReconcileCreatureMagnitudeBindingsUnlocked` (`RpgStore.UniqueActors.cs:1588`):

⚠ **Line cites in this list were re-checked and corrected — an earlier draft was uniformly −1.**

- reads the creature profile and returns early for a non-creature specimen (`:1595`);
- computes `wantedContainerId = SpeciesMagnitudeContainerId(profile.SpeciesId)` when the species has
  magnitudes (`:1600-1604`);
- **withdraws** any binding whose slot no longer matches (`:1611-1613`) — so re-running is safe;
- honours the **double-grant invariant**, returning early if already bound (`:1616-1617`);
- derives a stable roll seed from `(player.WorldSeed, source, instanceId:speciesId)` (`:1622-1623`);
- binds through `ProduceAndBind(container, DomainMembers, rollSeed, CreatureMagnitudeAtomPinTheta,
  PowerTuningHub.Tuning, …)`.

⛔ **And the one line that is the whole module:**

```csharp
var container = GetContainer(wantedContainerId);
if (container is null) return;   // seeded species-magnitude container missing; fail closed
```

`RpgStore.UniqueActors.cs:1619-1620`. **It fails closed, correctly, 904 times out of 904.**

### Real gap

**Zero `trait.species-magnitude-*` containers exist in `data/seed/`.** One container is wanted **per
species** — 904 of them.

### ⭐ Why this is an importer change, not a 904-row content program

The container's content is a **projection of data that already exists**: 26 channels, per species,
already in SQLite, already written by the importer that would host the synthesizer.

**So: synthesize the container in `ImportCreatureSpecies` at import time, from the rows it is already
writing.** That is A7's finding, and it converts the work to roughly one file with **zero committed
corpus** — no generated tree to regenerate, no seed files to review, no drift for CI to catch.

⚠ **This must follow `threat-band-fill`**, which rewrites every magnitude: 719 species move off the
default rung, their `Θ` changes, and `P(Θ)` is quadratic. Synthesizing first means synthesizing values
that are about to change.

---

## Design

### 1. Synthesize at import, never as a committed corpus

`ImportCreatureSpecies` already writes `creature_species_magnitude`. In the same transaction, for each
species with at least one magnitude, upsert a container at `SpeciesMagnitudeContainerId(speciesId)`
whose members are that species' `(channel, value)` pairs.

**Why not a committed corpus of 904 seed files:**

- It would be a **derived population** committed to disk — 904 files whose every value is computable
  from rows in the same import. Any drift between them is a defect with no detector.
- CI already fails on generated-tree drift; adding a 904-file tree that mirrors a table adds a drift
  surface for no information.
- The synthesizer is deterministic and idempotent, so the container can be rebuilt at any time.

### 2. Idempotency and the withdraw path

The consumer already withdraws stale bindings and refuses double-grants. The synthesizer must match
that discipline: **upsert, never append**, keyed on the container id, so a re-import replaces rather
than duplicates — the same shape as the `DELETE … INSERT` the magnitude rows already use.

### 3. Fail closed stays fail closed

⛔ **Do not change `:1619-1620`.** Its early return is correct and must remain: a missing container is
a real condition (a species with no magnitudes), and turning it into a throw would convert honest
incompleteness into a boot failure. The module's job is to make the case rare, not to make it fatal.

---

## Tech stack

C# .NET 8 (`FusionRpg.Data` — the importer), xUnit. **SQL only inside `FusionRpg.Data`**;
`guard-dal.ps1` enforces it. No new dependency.

## Commands

```powershell
dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~Species"
dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~UniqueActor"
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Container"
.\scripts\guard-dal.ps1
.\scripts\guard-actor-hub.ps1
.\scripts\guard-test-substrate.ps1
python scripts/audit-overflow.py
```

## Project structure

| Path | Role |
|---|---|
| `src/FusionRpg.Data/Sqlite/RpgStore.Species.cs:155-159` | Where the magnitudes are written — the synthesizer's host |
| `src/FusionRpg.Data/Sqlite/RpgStore.UniqueActors.cs:1588-1625` | The consumer — **read, not modified** |
| `tests/FusionRpg.Data.Tests/` | Import + reconcile round-trip |

## Code style

```csharp
// Synthesized from the rows this importer is already writing — NOT a committed corpus. 904 seed
// files mirroring a table would be a derived population on disk, with drift nothing detects. The
// synthesis is deterministic and idempotent, so the container can be rebuilt at any time.
// UPSERT, never append: a re-import replaces, matching the DELETE+INSERT the magnitude rows use.
UpsertContainer(db, tx, SpeciesMagnitudeContainerId(s.SpeciesId), s.Magnitudes);
```

---

## Tunables

**This module introduces no balance number.** Every value it projects is already authored upstream
and already persisted. Its output is a container, not a curve.

| Value | Meaning | Owner |
|---|---|---|
| `CreatureMagnitudeAtomPinTheta` | The `Θ` the container's atoms pin at | Already shipped, read from `PowerTuningHub.Tuning` — **not authored here** |

**Structural (stays `const`, with a comment):** none introduced.

## Numeric types

⛔ **This is the module where the overflow rules bite hardest**, because it is the one that carries
species magnitudes into the power ladder.

- **`long` for every magnitude.** `creature_species_magnitude.value` is an `INTEGER` column;
  SQLite `INTEGER` is 64-bit, and the C# side must read it as `long`, never `int`.
- **Never `float`.** `P(Θ)` is quadratic; a `float` magnitude stops being integer-exact at `Θ` = 232,
  **inside normal play**, and it is non-deterministic across runtimes.
- **Widen before multiplying** — `(long)a * b`, never `(long)(a * b)`.
- **Divide by 1000 last, exactly once.** Per-mille intermediates are 1000× closer to the ceiling.
- **Overflow throws, never wraps.** No `unchecked` on this path.
- A per-mille `int` would break at `Θ` = 3,213; `long` reaches `Θ` = 214,748,300 (`CLAUDE.md`).

⚠ Run `python scripts/audit-overflow.py` before finishing — this module's whole subject matter is an
A3 target class.

## ActorHub gate

⭐ **Contributes via `ActorHub` as a registered atom reader — it does not compose and does not fold.**

The species-magnitude corpus reaches the Hub through `AtomDerivedSubsystem` (`ActorHub.cs:168`). This
module produces the container that reader consumes; the composition happens once, in `ActorHub`,
exactly as today.

⛔ **No new `*Composer*`, no private fold of the same actor combat numbers.**
`guard-actor-hub.ps1` must stay green. `BattleStatComposer` is grandfathered **debt until fused** and
is **not** a template.

⚠ **This module owes a `ContributionSourceIds` (GG-49 FULL) grammar id** for that reader
(`actor-hub-ssot.md` §8.1). **It is an ask, filed against `actor-hub`, not an assumption** — and it is
the same ask `threat-band-fill` carries, so the two should file it once.

## Testing strategy

**Store tests run in memory**; disk only when the disk is the thing under test; a failed temp-delete
is a **failure**, never `catch { }`. `guard-test-substrate.ps1` enforces it.

| Level | What it asserts |
|---|---|
| Unit | Importing a species with magnitudes produces exactly one container at `SpeciesMagnitudeContainerId(speciesId)` |
| Unit | Importing a species with **no** magnitudes produces **no** container — and the consumer's fail-closed early return still holds |
| Unit | **Idempotency** — importing twice leaves one container with identical members, never two |
| Unit | The container's members match the `creature_species_magnitude` rows **exactly**, channel for channel and value for value |
| Unit | Values round-trip as `long` through the full path, proven with a value that would overflow `int` |
| Integration | ⭐ **The full round trip** — import a species, deploy a specimen, and assert `ReconcileCreatureMagnitudeBindingsUnlocked` **binds** rather than taking the fail-closed return. This is the acceptance test for the whole module |
| Integration | Re-deploying does not double-grant (the existing invariant), and changing a species' magnitudes **withdraws** the stale binding |
| Guard | `guard-dal.ps1`, `guard-actor-hub.ps1`, `guard-test-substrate.ps1` green |

⛔ **No test asserts "904 containers exist."** That is a reading and it grows when a species ships.
Assert the **reconciliation property**: `containers == species-with-magnitudes`, computed from the
same source on both sides.

## Boundaries

**Always**
- Synthesize in the importer's own transaction, beside the rows it is already writing.
- Upsert, never append.
- `long` everywhere a magnitude flows; widen before multiplying; divide last; let overflow throw.
- Keep SQL inside `FusionRpg.Data`.
- Run store tests in memory.
- Commit via MCP `repo-git.commit` with explicit `paths`.

**Ask first**
- Committing any species-magnitude corpus to disk (see § Design 1 for why not).
- The GG-49 SourceId grammar — coordinate with `threat-band-fill` so it is filed once.
- Changing `CreatureMagnitudeAtomPinTheta` or anything in `PowerTuningHub.Tuning`.

**Never**
- ⛔ Change the fail-closed early return at `RpgStore.UniqueActors.cs:1619-1620`. Honest
  incompleteness is the designed behaviour; a throw would make a legitimate state fatal.
- ⛔ Add a second composer or a private ChannelMods combat writer.
- Use `float` for any magnitude on this path.
- Ship before `threat-band-fill` — 719 species' `Θ` is about to move.
- Write a new `f(level)` curve. Contests read `Θ`; magnitudes read `P(Θ)`.
- Assert a population count.

## Success criteria

1. ⭐ **A deployed creature specimen binds its species-magnitude container** instead of taking the
   fail-closed return — proven end to end, which is the one thing that has never been true.
2. Every species with magnitudes has exactly one container; every species without has none.
3. Container members reconcile exactly against `creature_species_magnitude`, computed on both sides.
4. Import is idempotent; a second run produces no duplicate and no change.
5. A magnitude change withdraws the stale binding and rebinds — the existing machinery still holds.
6. Zero species-magnitude files committed to `data/`.
7. Values are `long` end to end; `audit-overflow.py` shows no new critical.
8. `guard-dal.ps1`, `guard-actor-hub.ps1`, `guard-test-substrate.ps1` green; Data and Core suites green.

## Open questions

1. **Does the synthesizer run for *all* species, or only those a player can reach?** **Recommendation:
   all.** It is a projection of rows already written; gating it would add a condition with no benefit
   and make "why is this species inert?" a two-cause question.
2. **Does the container carry all 26 channels or a curated subset?** **Recommendation: all of them.**
   The importer has no basis for curating, and dropping channels here would be an invisible balance
   decision made in an import path.
3. *(Not a question — moved out.)* A species rename orphans its container, which the consumer's
   existing withdraw path already handles. **That is a test, not an owner decision**, and it is listed
   in § Testing strategy.
