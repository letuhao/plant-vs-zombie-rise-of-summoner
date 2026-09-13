# Spec: action-choice-rung-tiebreak (A31)

Module **A31** in the [action map](../action-map.md) §17. Reads
[action-choice-ideal.md](../action-choice-ideal.md) Fix 1. Depends on nothing — reuses data already
computed on `CompiledAction`.

## Objective

`ActionTagPreference.Compare` (`ActionTagPreference.cs:44-48`) ties same-tag-rank actions on
`action_id`, alphabetically. Concretely: a rung-9 unlock-ladder combo skill can be permanently shadowed
by a rung-1 basic of the same tag whose id string sorts first — **provably**, every decision, regardless
of level or build. This module fixes the tiebreak to use `Rung` (already computed, already priced,
action-ideal.md §4.1) before falling back to `action_id`.

## Design

1. `ActionTagPreference.Compare` changes from `(tagRank, action_id)` to `(tagRank, -Rung, action_id)`.
   `action_id` stays as the **final** tiebreak (needed for replay determinism when two held actions
   somehow share a rung) — it simply stops being the decisive one.
2. `RankOf` (tag ranking itself) is **unchanged** — this module touches only the tiebreak clause.
3. No new field on `CompiledAction` — `Rung` already exists (`ActionCompiler.cs:54,64`).
4. Update `ActionTagPreference.cs`'s own doc comment (lines 3-8) to record that the tiebreak is no
   longer purely alphabetical, keeping the existing "content to rebalance" framing for the tag-rank part
   that is unchanged.

```csharp
public static int Compare(CompiledAction a, CompiledAction b)
{
    var byRank = RankOf(a).CompareTo(RankOf(b));
    if (byRank != 0) return byRank;
    var byRung = b.Rung.CompareTo(a.Rung); // descending: higher rung first
    return byRung != 0 ? byRung : string.CompareOrdinal(a.ActionId, b.ActionId);
}
```

## ActorHub gate

Not applicable — this reads `CompiledAction.Rung` (a content-authored, already-compiled field), never an
actor combat/derived magnitude, and does not touch `ActorHub` compose in any direction.

## Numeric types

`Rung` is already an authored `int` field (small, bounded by the rung table's row count, action-ideal.md
§3.2 — cap 10 today). No new magnitude, no overflow surface.

## Tunables

None new. `Rung` itself is authored content (the rung table, A12, already tunable). This module adds no
new number.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~ActionTagPreference"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~BasicAttackAdoption"   # goldens
```

## Project Structure

```
src/FusionRpg.Core/Actions/ActionTagPreference.cs      (Compare's tiebreak clause)
tests/FusionRpg.Core.Tests/Actions/ActionTagPreferenceTests.cs   (new/extended)
```

## Code Style

One function, one clause changed. No new class, no new file structure.

## Testing Strategy

| Case | Expect |
|---|---|
| Two same-tag actions, rung 1 (`"aaa_basic"`) vs rung 9 (`"zzz_combo"`) | rung-9 sorts first, reversing the old alphabetical result — **the planted regression test this module exists to add** |
| Two same-tag, same-rung actions | falls back to `action_id` ordinal, unchanged from today |
| Different-tag actions | tag rank still decides, `Rung` never consulted — unchanged |
| **Two untagged actions, different rung** | now ordered by rung, not just `action_id` — `ActionTagPreference.cs:30-32`'s own doc comment ("an untagged action ranks last of all, tied only by `action_id`") describes the OLD tiebreak; this test proves the new one applies uniformly, including to the untagged case, not just tagged ones |
| `SiegeAiIntentSource` (`SiegeAiIntentSource.cs:112-123`), not just `StubIntentSource` | **also** picks the higher-rung action for a same-tag pair — it pre-sorts `heldActions` by the same `ActionTagPreference.Compare`, so this fix propagates to siege's AI automatically; assert it explicitly rather than only testing `StubIntentSource`, since it is a separate call site that could silently diverge later |
| Full `Core.Tests` + all 8 goldens | **zero movers**, proven not assumed — no real shipped content holds two same-tag actions today (same reasoning A17 used), so this should be golden-neutral; run the suite to confirm rather than predict |

## Boundaries

**Always:** run the full test suite and all goldens after the change — this function is read by every
`IIntentSource` implementation (`StubIntentSource`, `SiegeAiIntentSource`), so a mistake here is
wide-blast-radius even though the change itself is one line.

**Ask first:** nothing — small, deterministic, reuses an existing field, no new tunable, no new
mechanism.

**Never:** touch `RankOf`/the tag-rank ordering itself in this module — that's a separate, larger
balance question (which tag beats which), not this module's scope.

## Success Criteria

1. A rung-9 action of a given tag always outranks a rung-1 action of the same tag, regardless of
   `action_id`.
2. `action_id` remains the deterministic final tiebreak.
3. Zero goldens move.
