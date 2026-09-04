# Spec: `resolver-memo`

Module 1 in the [species-build capability map](../species-build-map.md). **No dependencies.** Build
first — it is a correction to shipped code, it is semantically neutral, and every later module is
cheaper to prove once it exists.

## Objective

`AptitudeResolver.Resolve` recomputes from scratch on **every entity, every apply**. It loops every
tuning edge (**526** real edges in the shipped tree) and calls `allocation.Share(edge.Source)` per edge, each of
which is a `GrandTotal()` over 12 aptitudes × 4 scopes — **roughly 25,000 dictionary lookups per entity
resolve** (`AptitudeSubsystem.cs:51-57` → `AptitudeResolver.cs:35-62` → `AptitudeAllocation.cs:81-85`).
It runs on the status/hit path as well as the apply path (`InjectorStatusBridge.cs:58`,
`EntityApply.cs:88,217,354`).

Memoize it. **This changes no number** — it changes how often the same number is computed.

**Why it is a module and not a line in a later one:** the cost predates this program entirely, and it
benefits the shipped commander path immediately. Bundled with a species-aptitude change, a regression
in either would be attributable to neither, and "species aptitudes made the game slow" would become the
story of a cost that was already there.

**Success looks like:** identical output for identical inputs, proven by test rather than asserted;
zero goldens moved; and a resolve count that falls from *per entity* to *per distinct `(Side, TypeId, Theta)`
per generation*.

## Design

A memo on the subsystem instance, keyed by **every input `Resolve` actually reads**.

```
key        = (StatSide Side, int TypeId, long Theta)
value      = IReadOnlyList<DerivedModifier>       // exactly what Resolve returns today
generation = long, bumped by Invalidate()
```

> ### ⛔ `Theta` is in the key, and an earlier draft of this spec left it out
>
> That draft keyed on `(Side, TypeId)` alone and was **unbuildable**. `ContributeDerived` computes
> `var theta = _powerIndex.ActorIndex(ctx);` and passes it into `Resolve`
> (`AptitudeSubsystem.cs:53-56`) — Θ is a **per-actor** value, hydrated per `ctx`. Two entities of the
> same `(Side, TypeId)` at different power indices would have been served the first one's modifiers:
> a silently stale build, which is the exact failure this module claims to be designed against. The
> draft's own equivalence test would have passed, because a fixture holding Θ constant cannot see it.
>
> Recorded rather than quietly fixed, because the near-miss is the point: **a memo is only as correct
> as its key is complete, and the way to get that right is to enumerate what the memoized function
> reads rather than what the caller happens to vary.**

**Why Θ belongs in the key rather than in the invalidation table.** A changed Θ is simply a different
key, so no bump is needed and no staleness window exists. Bounded growth still holds: entities of one
type on one board share a type-level Θ, so the entry count is roster-sized, not entity-sized.

**Why not `EntityKey`.** Keying on the entity makes the memo grow with entity count, which is the thing
being optimised away.

**Side must stay in the key.** `LawnElementIndex.cs:11-13` records that `polevaulterzombie` and
`wallnut` are both `GameTypeId 3`; a bare type id collides across sides.

⚠️ **The key is *compatible* with `demon-type-allocation`'s needs, not identical to any existing one.**
An earlier draft claimed it was "exactly the key `demon-type-allocation` will need". `LawnElementIndex`
is keyed `(string Side, int GameTypeId)` (`LawnElementIndex.cs`), while `StatContext.Side` is a
`StatSide` — so module 6 needs a small, explicit translation between the two, and this spec does not
get to promise it away.

**Invalidation is a generation stamp, not a clear.** Entries carry the generation they were computed
under; a stale entry is ignored and replaced rather than requiring a synchronous purge. Bumping is
cheap and cannot race with a read.

**Every path that can change the answer must bump the generation.** These are the ones that exist
today, and the spec's own test matrix is this list:

| Path | Where |
|---|---|
| Allocation replaced (session start, reconnect, `AptitudesUpdated` push) | `RpgClient.cs:353-375` → `CheatState.ApplyCommanderAllocation` (`:75-85`) |
| Match edges | `MatchHost.cs:169`, `:194` |
| Any `StatSystem.Invalidate()` | `stat-system.md` §Invalidate |
| *(a changed `Θ` needs no bump — it is part of the key)* | see Design |
| Tuning reconfigured | `AptitudeTuningHub.Configure` |

**A missed bump is the failure mode**, and it is silent — a stale build that looks correct. The test
strategy below is built around that rather than around hit rates.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter Aptitude
dotnet test tests\FusionRpg.Core.Tests
dotnet test tests\FusionRpg.Guard.Tests
.\scripts\guard-single-writer.ps1
python scripts\audit-overflow.py
```

## Project structure

```
src/FusionRpg.Core/Stats/Derived/Subsystems/AptitudeSubsystem.cs   the memo lives here
src/FusionRpg.Injector/CheatState.cs                              bumps on allocation apply
src/FusionRpg.Injector/Match/MatchHost.cs                         bumps at match edges
tests/FusionRpg.Core.Tests/Stats/Aptitudes/AptitudeMemoTests.cs   new
```

No new file is required in `FusionRpg.Core` beyond the subsystem itself. **No tuning key** — a cache is
structural, not a balance number, and gets a comment saying so.

## Code style

- The memo is instance state on a registered subsystem, not a static — a static would leak between
  scoped test hosts, which is the exact `AptitudeTuningHub` race this repo already fixed once
  (`PointBudget.cs:6-10` records it).
- `long` generation counter, `checked` increment.
- The comment on the memo states **why it is not a tunable** (structural, per `tunables-ssot.md` §1).
- No `float` anywhere; the memo stores what `Resolve` already returns and computes nothing.

## Testing strategy

The important test is equivalence, not performance.

1. **Identical output, cached vs uncached.** Resolve the same context twice with the memo enabled and
   once with it disabled; assert the modifier lists are equal element-for-element. This is the test
   that makes the module safe.
2. **One test per invalidation path** in the table above: change the input, assert the *new* value is
   returned rather than the memoized one. A missing bump fails here.
3. **Distinct keys do not collide** — same `TypeId`, different `Side`, different allocation → different
   results (the `polevaulterzombie`/`wallnut` case, as a named test).
4. **Bounded growth** — resolving N entities of the same `(Side, TypeId, Theta)` produces one entry.
6. **⛔ Θ is honoured:** two contexts identical but for `Θ` resolve to **different** modifiers. This is the
   test the first draft of this spec would have failed.
7. **Zero goldens.** Full Core suite green with no re-bless. If any golden moves, the memo is not
   semantically neutral and the module is wrong.

## Boundaries

- **Always:** bump the generation on every path that can change an allocation or the tuning; keep the
  memo instance-scoped; prove equivalence before performance.
- **Ask first:** changing the key shape — **removing any input `Resolve` reads is the defect this spec already made once**; caching anything
  *above* `Resolve` (e.g. memoizing the whole derived snapshot), which has a much larger blast radius.
- **Never:** cache across players; make the memo static; introduce a tunable for cache size or TTL —
  an eviction policy is a new mechanism, and the bound here is roster size, which does not need one.

## Success criteria

1. Equivalence test passes: memoized and non-memoized resolves are element-wise identical.
2. Every invalidation path in the table has a test that fails if its bump is removed.
3. Full Core + Guard suites green; **zero goldens re-blessed**.
4. Θ is in the key and proven honoured; the key covers every input `Resolve` reads.
