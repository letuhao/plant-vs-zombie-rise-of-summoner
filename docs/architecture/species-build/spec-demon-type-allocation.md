# Spec: `demon-type-allocation`

Module 5 in the [species-build capability map](../species-build-map.md). **Depends on `budget-source`
(2), `species-xp` (3), `redistribution-plan` (4).**

## Objective

Make `AllocationScope.DemonType` real. The scope, the value type, the persistence table and the read
functions all ship already — `AllocationScope` has had four values since it was written
(`AptitudeAllocation.cs:8`), and `rpg_aptitude_allocation(scope, scope_key, aptitude_id, points)` is
scope-generic (`RpgStore.Aptitudes.cs:35-43`). **Only `Commander` is ever written**
(`AptitudeEndpoints.cs:76`). This module writes the second one.

**Success looks like:** a species' allocation exists, is per-player, is derived from the static plan and
that player's species level without being materialised, and can be overridden.

## Design

### Identity — decision 10

**Per-player, keyed by `speciesId`.** The existing table has no player column and encodes ownership in
`scope_key` (Commander uses `"player:{id}"`), so this module follows that convention rather than
migrating the schema:

```
scope     = 'demonType'
scope_key = 'player:{playerId}:species:{speciesId}'
```

Global (shared) allocation was **explicitly ruled out**: one player's respec must never change another
player's roster.

**`speciesId`, not the PvZ type id.** The `game_type_id` bridge is needed only at the lawn boundary,
where a spawn carries a type int — and `allocation-transport` (6) owns that translation through the
already-built `LawnElementIndex`. Keeping PvZ ids out of the RPG layer is the standing direction.

### The baseline is computed, not stored — audit finding A9

The plan is static shipped content (decision 7) and the budget is `speciesLevel × rate` (decision 14),
so the baseline allocation is a **pure function of two things the system already has**:

```
baseline(player, species) = plan.shares[species] ⊗ PointBudget.PointsFor(DemonType, speciesLevel, tuning)
```

Persisting it would be storing a computed total, which `stat-system.md` bans in as many words: *"Save
inputs (Y0 + bag / feature state), not computed totals."* So **only the override is persisted.**

⚠️ **This is a real seam change, and it is the module's central risk.** `AptitudeAllocation` is explicit
that **empty means all-zero shares, never an invented default** (`AptitudeAllocation.cs:19-22`), and
`LoadAllocation` returns only persisted rows (`RpgStore.Aptitudes.cs:109-133`). So a species with no override row today reads
**zero, not its baseline** — and a caller that reads `LoadAllocation` directly and forgets composition
gets a silently inert species. That is the same silent-zero shape this codebase has already been bitten
by once (a 222-point allocation that reached the writer as nothing).

**Mitigation, and it is structural rather than a convention:** composition lives behind a single named
entry point that returns the *effective* allocation, and `LoadAllocation` is not called directly by any
consumer of species allocation. A guard test asserts that.

### Two consequences of compose-at-read, added by the spec-coverage audit

**1. `LevelChangePipeline` needs no handler, and that is worth saying.** The ideal's §2 lists the empty
`LevelChangePipeline` (`RpgStore.Progression.cs:17`, no handlers registered) and the null
`progression.bonus.*` delegate as the wiring gaps "a level granting anything" would fill — so a reader
arriving from the ideal will look for this module to fill them. **It does not, and does not need to.**
Because the baseline is a pure function of `(plan, speciesLevel)` evaluated at read time, a level change
requires no handler to *push* anything: the next read simply computes a larger budget. Wiring a handler
would materialise state this design deliberately does not persist. Recorded so nobody adds one for
symmetry.

**2a. ⛔ A nonzero species baseline DILUTES every commander share — found by the spec review.**
`Share()` divides an aptitude's four-scope `Total` by `GrandTotal()` across all twelve
(`AptitudeAllocation.cs:81-85`, and "scopes sum before share" at `:13-17`). So a species baseline does
not merely *add* modifiers — it **changes the value of every commander-derived modifier for that
actor**, because the denominator grows. Any test asserting "commander behaviour is unchanged" is
asserting something false the moment a species baseline is nonzero, and `AptitudeChannelModsTests` plus
any Server/E2E golden routed through `BuildSquad` are exposed. This is the strongest reason the next
rule is not optional.

**2. The budget must be zero at level 1**, which `budget-source` (2) owns as `max(0, level − 1) × rate`.
An unrecorded actor defaults to `Level = 1` (`RpgStore.Progression.cs:280`), so a `level × rate` budget
would give **every species everywhere** a non-empty baseline — including every golden fixture. This
module's compose-at-read is what makes that reachable, so the constraint is named here as well as there.

### Override semantics

An override is **per-species and whole-vector**: it replaces that species' entire distribution, rather
than layering per-aptitude deltas on the baseline.

Two reasons: a respec is a whole-build action, so a whole-vector override is what the player is actually
doing; and per-aptitude deltas would require distinguishing "explicitly zero" from "not set", which is
exactly the ambiguity the "empty means all-zero, never a default" rule exists to avoid.

**Reverting to the baseline is deleting the override row**, and it is free — `species-respec` (7) prices
*changing* a build, not *returning to the shipped one*.

### Budget enforcement

`PointBudget.CheckScope` already exists and compares scope-local spend against that scope's own budget
(`PointBudget.cs:50-54`). An override is refused if it spends more than
`PointsFor(DemonType, speciesLevel, tuning)`. **Scope-local**: overspending `DemonType` can never be
covered by surplus in `Commander`, which is the shipped contract and stays.

**No cap on the allocation itself** (PS-8) — a species that earns more points gets more points.

## Commands

```powershell
dotnet test tests\FusionRpg.Data.Tests --filter Allocation
dotnet test tests\FusionRpg.Core.Tests --filter Aptitude
dotnet test tests\FusionRpg.Server.Tests
.\scripts\guard-dal.ps1
```

## Project structure

```
src/FusionRpg.Core/Stats/Aptitudes/SpeciesAllocation.cs      the compose-at-read entry point
src/FusionRpg.Data/Sqlite/RpgStore.Aptitudes.cs             demonType scope_key encoding
src/FusionRpg.Server/AptitudeEndpoints.cs                   species read/write
tests/FusionRpg.Core.Tests/Stats/Aptitudes/SpeciesAllocationTests.cs
tests/FusionRpg.Data.Tests/AllocationStoreTests.cs          extended for the new scope
tests/FusionRpg.Guard.Tests/SpeciesAllocationSeamTests.cs   the "no direct LoadAllocation" guard
```

No schema migration: the table is already scope-generic.

## Code style

- One named entry point returns the **effective** allocation; nothing else composes it. The name says
  effective, so a reader cannot mistake it for the raw row read.
- `long` points, `checked`; shares are bounded ratios and say so.
- `scope_key` encoding lives in one place with the Commander encoding, not scattered at call sites.
- Scopes **sum before share** — unchanged, and this module adds a second summand rather than a second
  formula (`AptitudeAllocation.cs:13-17`).

## Testing strategy

1. **Baseline without any row:** a species with a level and no override resolves to the plan's shares
   scaled by its budget — **not** to zero. This is the test that catches the silent-zero risk.
2. **Override replaces, not layers:** after an override, the effective allocation is the override, and
   deleting the row returns exactly the baseline.
3. **Per-player isolation:** two players with the same species at the same level, one of whom overrides,
   read different effective allocations. Decision 10, asserted.
4. **Budget refusal:** an override spending more than `PointsFor(DemonType, level)` is refused, and the
   refusal is scope-local — a large `Commander` budget does not fund it.
5. **Scopes sum:** an actor with both a Commander and a DemonType allocation reads the sum, and `share`
   is taken on the sum. A regression here would silently re-introduce per-scope shares.
6. **Seam guard:** no production consumer of species allocation calls `LoadAllocation` directly.
7. **PS-8:** a very large species level produces a proportionally large budget; overflow throws.

## Boundaries

- **Always:** compose the baseline at read time; persist only the override; keep budget checks
  scope-local; keep the `scope_key` encoding in one place.
- **Ask first:** changing the `scope_key` format (it is a stored key — a change is a migration);
  materialising baselines (reverses A9's decision and needs a reason).
- **Never:** persist a computed total; let a species allocation read another player's rows; cap the
  allocation; call `LoadAllocation` directly from a species-allocation consumer.

## Success criteria

1. A species with no override resolves to its planned baseline, proven by test.
2. Overrides are per-player, whole-vector, budget-checked, and revertible for free.
3. The seam guard passes — no direct `LoadAllocation` in a species path.
4. Data + Core + Server suites green; `guard-dal` green. **Goldens hold only because the budget is zero
   at level 1** — if one moves, that rule has been broken, and the dilution in §2a is why.
