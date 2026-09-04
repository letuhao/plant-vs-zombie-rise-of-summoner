# Spec: `allocation-transport`

Module 6 in the [species-build capability map](../species-build-map.md). **Depends on `resolver-memo`
(1) and `demon-type-allocation` (5).**

## Objective

Get a species' allocation from the server to a live lawn entity. This is the wiring gap that keeps the
whole feature off the board, and audit finding **A5** narrowed it considerably: the hard part — mapping
a live entity to a species — **is already built**.

| Piece | State |
|---|---|
| `(Side, GameTypeId) → DemonSpeciesDef` | **built** — `LawnElementIndex`, already hosted injector-side |
| `StatContext` carries `Side` and `TypeId` | **built** — `StatContext.cs:15-16`, stamped at `StatContextFactory.cs:22-25,50-53` |
| Per-entity derived contribution on the lawn | **built and normal** — `AtomDerivedSubsystem` already resolves per entity from `ctx` |
| The payload | **wiring gap** — `/api/aptitudes/{playerId}` returns a flat share map hard-coded to `Commander` (`RpgClient.cs:363-374`), with no species dimension |

**Success looks like:** placing a plant on the lawn applies that species' own allocation to that plant,
and the path is no slower than before this program existed.

## Design

### The payload gains a species dimension

The endpoint returns the commander shares **and** a species map. Adding a field is the free additive
path; the commander shape is unchanged so an older client keeps working.

```
GET /api/aptitudes/{playerId}
{
  "theta": ..., "budget": ..., "spent": ..., "withinBudget": ...,   // unchanged
  "shares": { "<aptitudeId>": <points>, ... },                      // unchanged — NOT renamed
  "species": { "<speciesId>": { "<aptitudeId>": <points>, ... } }   // the only addition
}
```

> ⛔ **Corrected by the spec review, 2026-09-05.** An earlier draft of this section proposed
> `{ "commander": {...}, "species": {...} }` while simultaneously claiming the commander shape was
> unchanged, listing "changing the commander payload shape" under **Never**, and asserting a success
> criterion that it stayed byte-identical. All three were contradicted by the draft's own payload:
> renaming `shares` → `commander` is a **breaking change**, and `RpgClient.cs:365` hard-requires
> `shares` — `if (!doc.RootElement.TryGetProperty("shares", …)) return;` — so an injector would have
> silently stopped applying every allocation. The shipped shape is
> `{ theta, budget, spent, withinBudget, shares }` (`AptitudeEndpoints.ProjectState`), and **`species`
> is added beside it**. Additive is the free path; a rename is not.

The species entries are the **effective** allocations from module 5 — baseline composed with any
override — so the injector never needs the plan, the level, or the budget rule. It receives points.

**Only species the player has actually levelled are sent.** The corpus is 829 rows; a player's levelled
subset is small, and an un-levelled species contributes nothing anyway (zero budget → zero points).

### Injector side

A cache keyed by `speciesId`, refreshed on **exactly the cadence the commander cache already uses** —
`StartAsync`, reconnect, the `AptitudesUpdated` push (`RpgClient.cs:353-375`), and the match edges
(`MatchHost.cs:169,194`). No new lifecycle, no polling, and **nothing on the Hot path ever awaits the
server**, which is a hard ban (`overlay-control-loops.md`).

Resolution at stat-apply time is two dictionary lookups:

```
(ctx.Side, ctx.TypeId) → LawnElementIndex → speciesId → cached allocation
```

Against the ~25,000 dictionary lookups a single `AptitudeResolver.Resolve` already performs, that is
noise — which is why `resolver-memo` (1) is the dependency that matters here. **With the memo, this path
is faster than today's commander-only path**, because the memo is keyed on exactly `(Side, TypeId)` and
is bounded by roster size rather than entity count.

### ⛔ Two hazards this codebase has already been bitten by

**1. The bootstrap window.** `LawnElementResolverHost` returns a **throwaway empty index** if
`Configure()` has not run. Routing allocation through it unguarded means early applies silently resolve
`Empty` — a species that looks correct and contributes nothing. This is the identical shape to the
documented defect where a 222-point allocation resolved and wrote nothing.

**The guard is explicit, not hopeful:** an un-configured index is distinguishable from a configured-empty
one, and resolving against an un-configured index is reported once rather than returning a silent zero.

**2. The key must keep its side.** `polevaulterzombie` and `wallnut` are both `GameTypeId 3`
(`LawnElementIndex.cs:11-13`). A bare type id hands a plant a zombie's build.

### ⛔ Scopes must sum into ONE allocation — added by the spec-coverage audit

The species points and the commander points are **merged into a single `AptitudeAllocation`** and
resolved once. Building a species allocation, resolving it separately, and adding the resulting
modifiers to the commander's is **wrong**, and the value type says so in its own words:

> **Scopes sum before share, never the reverse.** … **A per-scope share, later combined, is a different
> (and wrong) number** — it would let a small scope's 100%-in-one-aptitude allocation outweigh a large
> scope's broad spread.
> — `AptitudeAllocation.cs:13-17`

`operator+` on `AptitudeAllocation` is the intended merge; this is the type's own use, not a workaround.
The failure mode matters because two separate resolves would look entirely reasonable in review and
would quietly change every actor's build.

### What this module does not do — and a correction to how the output actually lands

It does not change `EntityApply`, `EntityStatWriter`, or the compose phases. A species allocation
reaches the game the same way a commander allocation already does.

⛔ **Correction (spec-coverage audit, 2026-09-05).** An earlier draft of this section said the output
reaches the writer *"as `progression.bonus.*`-shaped combat flats"*. That is imprecise enough to send an
implementer at the wrong seam. Measured against the shipped tuning: of the aptitude edge set's distinct
channels, **only the `progression.bonus.*` handful** (`atk`, `defense`, `arm1`, `arm2`, `maxHp`) merge
into `AppliedCombat` for the writer. The large majority — `combat.*`, `resource.*`, `status.*`,
`skill.*`, `move.*` — are read from the **derived snapshot** by their own consumers and never pass
through `AppliedCombat` at all.

Both paths already exist and neither is this module's to change. What matters for acceptance is that a
species allocation contributes on the **same channels a commander allocation already contributes on** —
so the test asserts channel-set equivalence rather than a particular delivery mechanism. The ban that
does apply unchanged: progression never writes bare `hp`/`maxHp`/`atk` (`actor-hub-ssot.md:63`).

## Commands

```powershell
dotnet test tests\FusionRpg.Server.Tests --filter Aptitude
dotnet test tests\FusionRpg.Core.Tests
.\scripts\guard-single-writer.ps1
.\scripts\guard-secondary-no-unity.ps1
.\scripts\deploy-play.ps1 -NoServer      # then the live check below
```

## Project structure

```
src/FusionRpg.Contracts/AptitudeDtos.cs                     the species map
src/FusionRpg.Server/AptitudeEndpoints.cs                   emit species entries
src/FusionRpg.Injector/RpgClient.cs                         fetch + cache + refresh
src/FusionRpg.Injector/CheatState.cs                        species allocation source
src/FusionRpg.Core/Stats/Aptitudes/SpeciesAllocationSource.cs   ctx → allocation, Core-testable
tests/FusionRpg.Core.Tests/Stats/Aptitudes/SpeciesAllocationSourceTests.cs
tests/FusionRpg.Server.Tests/AptitudeEndpointsTests.cs      extended
```

**The resolution logic lives in Core with an injected lookup**, mirroring `MechanicalOwnSideOracle`'s and
`SpecimenOwnershipOracle`'s established shape — so it is fully provable in `Core.Tests` with a fake
resolver and no running game. Only the cache write is injector-side and unverifiable outside a live host,
which is this repo's accepted precedent for injector-only edits.

## Code style

- Adding a contract field is additive and free; **narrowing or renaming one is a version bump** and is
  not done here.
- The injector cache is a plain dictionary refreshed at edges — never a TTL, never a poll.
- No `await` on the Hot path; the cache read is synchronous by construction.
- Side is part of every key, always.

## Testing strategy

1. **End-to-end in Core:** a fake `(Side, TypeId) → speciesId` resolver plus a fake allocation cache
   produces the expected derived modifiers for a plant, and different modifiers for a zombie with the
   same `TypeId`. The `polevaulterzombie`/`wallnut` case is a named test.
2. **Un-configured index:** resolving before `Configure()` is **reported**, not silently zero. Fails if
   the guard is removed.
3. **Refresh cadence:** each of the four refresh paths updates the cache; a stale cache after an
   `AptitudesUpdated` push is a failure.
4. **Endpoint shape:** the commander half is byte-unchanged for a player with no species allocations —
   an older client is unaffected.
5. **No await on Hot:** a guard test that the species source performs no I/O.
6. **Zero goldens** — nothing here changes a number for an actor with no species allocation.
7. **Live check (owner-run):** a plant whose species has a real allocation shows changed stats on a live
   lawn, and the same plant with the allocation cleared returns to baseline. This is the module's real
   acceptance, and it cannot be proven offline.

## Boundaries

- **Always:** keep `Side` in the key; refresh on the existing cadence; keep resolution logic in Core
  behind an injected lookup; guard the un-configured index.
- **Ask first:** sending the whole corpus rather than the levelled subset; any new refresh trigger;
  changing the commander payload shape.
- **Never:** await the server on the Hot path; resolve a species by scanning the board; write a Unity
  combat field outside `EntityStatWriter`; let an un-configured index return a silent zero.

## Success criteria

1. A species' allocation demonstrably changes that species' stats on a live lawn (owner-run check).
2. `polevaulterzombie`/`wallnut` resolve to different species — proven by test.
3. An un-configured index reports rather than silently zeroing.
4. The commander payload is unchanged for existing clients.
5. All four guards green; zero goldens moved; the path is no slower than before (module 1's memo).
