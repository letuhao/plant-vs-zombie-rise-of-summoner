# Spec: `battle-allocation`

Module 10 in the [species-build capability map](../species-build-map.md). **Depends on
`demon-type-allocation` (5).**

**⛔ Added 2026-09-05 by the spec-coverage audit. Its absence was a real hole, not a refinement.**

## Objective

Make a species' allocation apply in **battle and expedition**, not only on the lawn.

### Why this had to exist

The first nine specs covered the lawn transport (`allocation-transport`, module 6) and assumed battle
followed. It does not. The battle seam is `WebMatchService.AptitudeChannelMods`, and it reads **only the
commander scope**:

```csharp
public static IReadOnlyList<BattleChannelMod> AptitudeChannelMods(int level, long playerId, RpgStore store)
{
    var allocation = store.LoadAllocation(
        AllocationScope.Commander, AptitudeEndpoints.ScopeKey(playerId));
    ...
}
```
— `src/FusionRpg.Server/WebMatchService.cs:415-418`

It takes a `playerId` and a level. **It has no species parameter at all.**

**The incoherence that follows is not cosmetic.** Decision 2 makes expeditions a source of species
build points, and decision 13 keeps expeditions as *the* game-closed source this program builds. So
without this module:

- a player earns a species' build points **by running expeditions**, and
- those points **never apply in expeditions** — only on the lawn.

That does not merely look odd. It **half-defeats standalone-first**: the feature becomes *earnable*
with the game closed but only *usable* with the game open, which is the invariant's own definition of
the injector gating a feature rather than enriching it.

## Design

### The species is already at the call site

No plumbing is needed to find it. `BuildSquad` already resolves the species per actor and stamps it on
the setup:

```csharp
var species = DemonSpeciesCatalog.Get(s.Profile.SpeciesId);
...
SpeciesId = species.SpeciesId,
```
— `WebMatchService.cs:338-343`

and `AptitudeChannelMods` is **already called inside that per-actor loop** (`:356`). So the shape is
right; only the argument and the read are missing.

### ⛔ Scopes must sum into ONE allocation — resolving twice and adding is wrong

This is the defect most likely to be written by accident, and the value type names it explicitly:

> **Scopes sum before share, never the reverse.** `Total` adds the four scopes for one aptitude;
> `Share` divides that sum by the grand total across all twelve. **A per-scope share, later combined, is
> a different (and wrong) number** — it would let a small scope's 100%-in-one-aptitude allocation
> outweigh a large scope's broad spread.
> — `AptitudeAllocation.cs:13-17`

So the implementation **merges the commander points and that actor's species points into a single
`AptitudeAllocation`** and calls `ResolveForBattle` **once**. Calling it twice — once per scope — and
concatenating the resulting `BattleChannelMod` lists produces a different, wrong number, and it would
look completely reasonable in review.

`AptitudeAllocation` already supports this: it is keyed `(scope, aptitudeId)` and `operator+` merges,
so the merge is the type's own intended use, not a workaround.

### Which `Θ` the battle read uses

`ResolveForBattle(allocation, tuning, ladder, theta, registry)` (`AptitudeResolver.cs:79-80`) takes a
single `theta`. The commander points and the species points belong to **one actor** resolving at **that
actor's** power index, which is what the call site already passes (`level`, `WebMatchService.cs:341`).
So the merge changes the allocation, not the `Θ` — and this module introduces **no second curve**.

### ⛔ There are FOUR read paths, and a missed one becomes a lying diagnostic

A second sweep for `LoadAllocation` callers found two more that are **not gameplay, and are worse for
being diagnostics** — a debugging surface that disagrees with the game is how an afternoon disappears:

| # | Path | Where | Owner |
|---|---|---|---|
| 1 | Lawn stat apply | `CheatState` → `AptitudeSubsystem` | module 6 `allocation-transport` |
| 2 | Battle setup | `WebMatchService.cs:415-418` | **this module** |
| 3 | **Battle report `aptitude.snapshot` event** | `WebMatchService.cs:264` | **this module** |
| 4 | **Derived-stat inspection endpoint** | `AuraDerivedEndpoints.cs:59` | **this module** |

Paths 3 and 4 both hard-code `AllocationScope.Commander` today. Left alone:

- a **battle report** would omit the species contribution, so the record of *why* a battle went that way
  is missing a term that actually decided it;
- the **derived inspection endpoint** would report channel values the lawn does not apply — a
  diagnostic that confidently lies.

Both are in scope here rather than deferred, because they are the same one-line scope addition and
because a diagnostic that disagrees with the game is worth less than no diagnostic at all.

Path 4 already has a `StatContext` in hand (`c.PlayerId` is read from it at `:59`), so it has the same
`Side`/`TypeId` the lawn path resolves species from — no new plumbing.

### A small efficiency correction that rides along

`AptitudeChannelMods` is called once per squad actor (`:356`) and performs a `LoadAllocation` **inside
the loop**, so a squad of N does N commander reads that all return the same rows. Adding the species
read naively makes that 2N. The commander allocation is loaded **once per squad build** and passed in;
only the species read is per actor. This is a straight improvement over today and it is in scope
because this module is already changing that signature.

## Commands

```powershell
dotnet test tests\FusionRpg.Server.Tests --filter Aptitude
dotnet test tests\FusionRpg.Server.Tests
dotnet test tests\FusionRpg.Core.Tests --filter Battle
dotnet test tests\FusionRpg.Core.Tests
```

## Project structure

```
src/FusionRpg.Server/WebMatchService.cs                      AptitudeChannelMods gains species + a hoisted commander read
tests/FusionRpg.Server.Tests/AptitudeChannelModsTests.cs     extended — it already covers this seam
```

Nothing in `FusionRpg.Core` changes: `AptitudeAllocation.operator+` and `ResolveForBattle` already do
what this needs.

## Code style

- One merged allocation, one `ResolveForBattle` call, per actor.
- The commander read is hoisted out of the per-actor loop; the species read stays in it.
- The existing method keeps its name and its doc comment's structure — that comment already explains
  the seam's contract (*"aptitudes reach battle the same way stars/loyalty do: ordinary ChannelMods in
  the setup, adapted at this one seam, never an engine or composer change"*), and that stays true.

## Testing strategy

1. **The coherence test, and it is this module's reason to exist:** an actor whose species has an
   allocation resolves **different** channel mods than the same actor whose species has none. If this
   passes only for the commander scope, the module has not landed.
2. **Scopes sum before share:** an actor with points in *both* scopes resolves to the mods produced by
   the **merged** allocation — and explicitly **not** to the concatenation of two per-scope resolves.
   Assert the two differ, so a future refactor into two calls fails loudly rather than silently
   changing every battle.
3. **Inertness preserved:** a player with no allocation in either scope still resolves to empty — the
   existing `AptitudeChannelModsTests` assertion, unchanged and still passing.
4. **Per-actor, not per-squad:** two actors of different species in the same squad resolve to different
   mods. This is the test that would fail if someone hoisted the *species* read out of the loop too.
5. **Commander read hoisting is behaviour-neutral:** the mods for a squad are identical before and after
   the hoist.
6. **Goldens:** see the constraint below — with species budgets zero at level 1, every existing battle
   golden must be **byte-identical**. If one moves, the budget is not zero at level 1 and that is the
   bug, not the golden.

## ⛔ The golden constraint this module makes load-bearing

An unrecorded actor's progression defaults to **`Level = 1`** (`RpgStore.Progression.cs:280`). Under
`demon-type-allocation`'s compose-at-read baseline, a level-1 species would therefore carry a *non-empty*
allocation everywhere — including in every battle golden fixture, whose actors would silently gain a
build they never had.

**Therefore the species budget must be zero at level 1**, which `budget-source` (module 2) owns:
`PointsFor(DemonType, level)` reads `(level − 1) × rate`, not `level × rate`.

That is not a workaround for the goldens. **It is what the owner actually described** — *"they will earn
bonus when specie level up"* — a species that has never levelled has earned nothing. The golden safety
falls out of stating the rule correctly.

## Boundaries

- **Always:** merge scopes into one allocation before resolving; keep the species read per actor; keep
  the seam a `ChannelMods` adaptation, never an engine or composer change.
- **Ask first:** changing `AptitudeChannelMods`'s public signature in a way other than adding the
  species (it is exercised by shipped tests); reading any scope beyond Commander and DemonType (Aspect
  is not authorized to build).
- **Never:** resolve per scope and concatenate; introduce a second `Θ`; let a battle read a species
  allocation belonging to another player.

## Success criteria

1. A species with a real allocation changes that actor's battle outcome — the incoherence is closed.
2. Merged-scope resolution is asserted to differ from concatenated per-scope resolution.
3. Two different species in one squad resolve differently.
4. Existing `AptitudeChannelModsTests` still pass unchanged.
5. **Every battle and expedition golden is byte-identical** — proving the level-1-is-zero rule holds.
