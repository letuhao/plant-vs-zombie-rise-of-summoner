# Spec: compose-channel-cache (E25)

**Status: BUILT 2026-08-23, retrospective spec written 2026-09-03.** Module **E25** in the
[effect-atom map](../effect-atom-map.md) §3, Wave 6, Checkpoint F. This document records what shipped;
it is not a plan. Acceptance evidence: [tasks/effect-atom-todo.md](../../../tasks/effect-atom-todo.md)
(search `E25: compose-channel-cache`). Scoped from [completeness-audit.md](completeness-audit.md)
finding B3.

> Reads [definitions.md](definitions.md), which wins where it and this document disagree.

## What it owns

One cache slot inside `DerivedStatChannels`, holding the generated overlay combat channel set together
with the `ElementTable` instance it was generated from, plus the O(1) membership read that the cache
makes possible. Everything else about channel generation — the family list, the naming, the roster
source — is E18's and unchanged.

## What it closed

`AllCombatChannelIds` generated its whole set from scratch on every read: 84 interpolated strings at
the time, from `families × (omni + roster)`. `BattleStatComposer.Compose` read it once **per actor
composed**, and `StatusStatPayload.IsKnownChannel` did a linear `.Contains` over a freshly allocated
list for **every channel it parsed**. The stated reason for never caching was that the roster is loaded
after startup — which stopped being a reason the moment E20 shipped a loader with a defined swap point.

## The contract as shipped

**`src/FusionRpg.Core/Stats/Derived/DerivedStatChannels.cs:363-409`.**

- `AllCombatChannelIds` (`:363`), `AllCombatChannelEntries` (`:369`) and `IsCombatChannel` (`:376`) all
  read `EnsureCache()`, so the flat list, the `(family, slot)` entries and the membership set are built
  in one pass and can never desync.
- `CacheSlot` is a `readonly record struct` of `(ElementTable Source, IReadOnlyList<string> List,
  HashSet<string> Set, IReadOnlyList<CombatChannelEntry> Entries)` (`:393-394`).
- `EnsureCache()` (`:396-409`) compares `ElementTable.Current` to the slot's `Source` by
  **reference**, and rebuilds only on a mismatch. That is sound because `ElementTable` is immutable and
  `Use`/`UseScoped` always assign a *new* instance rather than mutating one in place — so reference
  equality is exactly as fresh as a version counter, with no edit to `ElementTable.cs` at all. The
  planned version counter was not needed and was not built.
- Generation filters to enabled elements: `current.Elements.Where(e => e.Enabled)` (`:403`).
- `IsCombatChannel(channel)` is a `HashSet` lookup over the same generation
  (`StringComparer.Ordinal`), and `StatusStatPayload.IsKnownChannel`
  (`src/FusionRpg.Core/Status/StatusStatPayload.cs:123-128`) calls it instead of scanning a fresh list.

**The cache slot is `AsyncLocal`, not `static`** (`:391`). It shipped on 2026-08-23 as a single
lock-guarded static slot; that was corrected on **2026-08-25**, and the reason is written into the code
at `:377-389`. `ElementTable.Current` is itself `AsyncLocal`-scoped, so two tests scoped to different
rosters can legitimately see different values — but one shared cache slot let them thrash each other,
breaking the same-instance guarantee non-deterministically (reproduced once in roughly four full-suite
runs, passing in isolation — the signature of a race). The cache now scopes exactly the way the pointer
it is keyed on does.

**The freshness guarantee is per call, not per scope.** There is one slot per async context, so
restoring an outer `UseScoped` re-derives the outer roster's channel set by value rather than returning
the identical instance. Correctness holds; instance identity across a nested swap does not, and the
tests assert it that way.

## What it does NOT do

- **It does not change what is generated.** Same families, same ids, same order — the cached output is
  asserted byte-identical to an uncached `BuildAllCombatChannelIds` rebuild.
- **It does not cache anything else.** `BuildAllCombatChannelIds(elementIds)` (`:411`) still builds for
  an explicit roster on demand — that is how a test adds a seventh element — and is deliberately
  uncached.
- **It does not invalidate on anything but a roster instance change.** Nothing else can change the
  generated set, because the set is a pure function of the enabled roster and the code-held family list.
- **It does not touch `DerivedStatRegistry` or the derived catalog.** Status derived channels stay a
  separate literal `HashSet` (`StatusStatPayload.cs:130-140`).

## How it is verified today

- **Unit** — `tests/FusionRpg.Core.Tests/Stats/ChannelCacheTests.cs`, 5 tests: repeated reads with no
  roster change return the same list instance (`Assert.Same`); a roster swap invalidates and the output
  changes; the cached output is byte-identical to an uncached rebuild; `IsCombatChannel` agrees with
  `AllCombatChannelIds` for every generated id; `IsCombatChannel` also invalidates on a swap.
- **Seam** — `tests/FusionRpg.Core.Tests/Stats/ChannelCacheSeamTests.cs`, 2 tests against the real
  consumer, `BattleStatComposer.Compose`: across a real seventh-element roster swap, and across repeated
  composes with no swap.
- **Regression guard** — `tests/FusionRpg.Core.Tests/Stats/ChannelCacheBudgetGuardTests.cs`, 2 tests:
  10,000 warm reads of `AllCombatChannelIds` and of `IsCombatChannel` must average under 64 and 16
  bytes per call respectively (`:45`, `:68`). Generous headroom over "should be roughly zero", chosen so
  the guard fails loudly if the cache is removed (back to a full rebuild per call) without going yellow
  on ordinary GC noise.

## Known residuals

- **The set is no longer 84.** The derived-stats program's H.1 (2026-08-24) took the families to 28, so
  the generation is `28 × 7 = 196` today (`DerivedStatChannels.cs:348`). Every "84" in the map, the todo
  and the audit is the pre-2026-08-24 figure; the cache is unaffected, and the saving is larger than it
  was measured to be.
- **The allocation guard is a heuristic, not a budget.** It measures bytes per call on the current
  thread over a warm loop; a runtime or GC change could move it without any regression in this module.
  It is a tripwire for cache removal, and it is the only performance evidence this module has — no
  probe, no before/after timing, was captured.
- **The same-instance guarantee is per async scope**, so a nested `UseScoped` restore rebuilds. Recorded
  in the code (`:385-389`) and asserted that way in `ChannelCacheTests`; a reader expecting
  process-wide memoisation would be wrong.
