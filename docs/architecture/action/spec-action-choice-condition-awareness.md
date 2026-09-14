# Spec: action-choice-condition-awareness (A32)

Module **A32** in the [action map](../action-map.md) §17. Reads
[action-choice-ideal.md](../action-choice-ideal.md) Fix 2. Depends on **A31** (the `(tagRank, Rung)` tied
runs this module scans only exist meaningfully once A31 orders by rung — before A31, ties were mostly
arbitrary alphabetical adjacency) and **A18a** `action-container-binding` (built — the container a live
action is bound to already exists per-actor per-action).

## Objective

A rung-and-tag-ordered choice (A31) still can't tell that *right now*, a same-rung action's conditional
payoff atom (e.g. "double damage if Chilled") would actually fire, while a plain unconditional peer
would not get that bonus. This module surfaces that one boolean fact to the selector, so a live combo
outranks a same-rung, same-tag peer with no live bonus — without inventing scoring, and without changing
the ordered-priority-list shape A31 already established (`docs/architecture/action-choice-ideal.md`'s
prior-art section: this repo's shipped shape already matches WoW SimC's Action Priority List; the fix is
one more, richer condition check, not a new decision model).

## Design

### ⚠️ Audit correction (2026-09-13): this cannot be folded into `ActionTagPreference.Compare`

The original draft of this spec proposed extending the comparison key to
`(tagRank, hasLiveConditional, -Rung, action_id)`, implying the live-condition check lives inside
`Compare(a, b)` the same way A31's rung tiebreak does. **That is architecturally wrong, and the reason
is load-bearing**: `StubIntentSource.cs:23-25`'s own doc comment states `HeldActionsOf` *"is expected to
already be preference-ordered — sorted once wherever an actor's action set is frozen (T24's
`FrozenActionSet`), not per decision. Sorting per call would be the exact per-decision allocation this
module's own zero-allocation acceptance line forbids."*

`Compare(a, b)` takes two `CompiledAction` values and nothing else — **no actor, no target, no live
facts**. It runs once, at freeze time, to produce a static order. A32's whole premise (*"is this
specific action's conditional payoff true against THIS target, right now"*) needs exactly the live
context `Compare` structurally cannot see. Folding it into `Compare` either breaks the "sorted once"
invariant (re-sorting the frozen list with live facts on every decision — the exact per-decision
allocation the module was built to avoid) or silently does nothing (if `Compare` is called with only the
static fields, `hasLiveConditional` would have to be pre-computed at freeze time, before any target is
known — meaningless).

### The correct shape: a bounded lookahead inside the existing decision loop, not a new sort

The frozen order (post-A31) already groups actions into **contiguous runs sharing the same
`(tagRank, Rung)` key** — that grouping is what A31's sort key produces, for free. A32 adds a small,
bounded lookahead *inside* `StubIntentSource.TryDeclare`'s existing loop (`StubIntentSource.cs:64-75`),
scoped to one such run at a time, never a global re-sort:

1. **New, narrow read**, added to `IBattleView` (or a new, narrower interface if `IBattleView` is judged
   too broad a place for it — decide at implementation time, not blocking):

   ```csharp
   bool HasLiveConditionalPayoff(string actorKey, string actionId, string targetKey);
   ```

   Evaluates whether the action's already-bound container (`IContainerEffectResolver`/A18a) holds ANY
   atom whose `when.predicate` (E3) currently evaluates true against `(actorKey, targetKey)`'s facts —
   reusing the exact `PredicateCompiler`/`FactReader` machinery `UsabilityEvaluator`'s gate 6 already
   uses, just reading the **container's per-atom** predicates rather than the **action's own** usability
   condition (a real, previously-unavailable distinction — see `action-choice-ideal.md`'s "real gap"
   section for why these are different things).

2. **Loop change, not sort change.** When the loop reaches the first usable candidate at index `i`,
   before committing to it: scan forward only while `heldActions[j]` shares the same `(tagRank, Rung)`
   key as `heldActions[i]` (a contiguous run, bounded by however many actions share that exact key —
   typically 0-2 in a 5-slot loadout, never the whole list). Within that run, prefer the first *usable*
   candidate for which `HasLiveConditionalPayoff` is true; if none, fall back to `heldActions[i]` itself
   (A31's own order, unchanged). This never looks outside the current run, so it cannot promote a
   lower-priority action ahead of a higher-priority one — it only re-picks *within* a tie A31 already
   established.

3. **Cost discipline (T9's own zero-alloc/bounded-`Reads` precedent) — now precise, not just asserted:**
   `HasLiveConditionalPayoff` is called **only** for candidates inside a tied run that has already passed
   the usability gate — never for every held action on every decision, and never for a run of size 1
   (nothing to break a tie between). This preserves `StubIntentSource`'s existing "`FactReader.Reads`
   scales with targets, not actions × targets" property (`StubIntentSource.cs:18-21`) — the new reads
   scale with *tied-run size*, which is small and bounded by the 5-slot loadout, not with held-action
   count in general.

### ⚠️ Second audit finding: `StubIntentSource` and `SiegeAiIntentSource` do not share a loop

Unlike A31 (a single shared `ActionTagPreference.Compare` function both intent sources call, so the fix
propagates to both automatically), **A32's tied-run lookahead lives inside `TryDeclare`'s loop body**,
and `SiegeAiIntentSource.cs:112-123` has its **own**, separate loop over `heldActions`. Implementing the
lookahead only inside `StubIntentSource` would leave siege's AI silently un-improved — the same
"provably correct here, silently absent there" shape this whole reopening exists to close. **Extract the
tied-run lookahead into a shared helper** (e.g. `ActionChoice.PickFromTiedRun(heldActions, i, view,
actorKey, targetKey)`) that both `StubIntentSource.TryDeclare` and `SiegeAiIntentSource`'s equivalent
step call — never duplicate the scan logic into two call sites, which is exactly the kind of
copy-drift this repo's SOLID rule (§2.15) exists to prevent.

## ActorHub gate

Not applicable — reads atom-level predicate truth (a boolean fact about the container/battle state),
never an actor combat/derived magnitude, and never composes or forks Hub output.

## Numeric types

None introduced — this is a boolean read, not a magnitude.

## Tunables

None. Predicate evaluation is already-shipped, already-priced (E9's predicate-frequency pricing,
action-ideal.md §8.6) machinery; this module adds no new number, just a new read of existing structure.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~ActionChoiceConditionAwareness"
```

## Project Structure

```
src/FusionRpg.Core/Battle/Timeline/IBattleView.cs        (new method, or a new narrow interface)
src/FusionRpg.Core/Actions/ActionChoice.cs               (new — shared tied-run lookahead helper)
src/FusionRpg.Core/Actions/StubIntentSource.cs           (calls the shared helper)
src/FusionRpg.Core/Battle/Siege/SiegeAiIntentSource.cs   (calls the shared helper — same fix, same call)
tests/FusionRpg.Core.Tests/Actions/ActionChoiceConditionAwarenessTests.cs   (new)
```

## Code Style

Reuse `PredicateCompiler`/`FactReader` exactly as `UsabilityEvaluator` already does — no second
predicate-evaluation path, no new leaf vocabulary (E3's leaf list stays closed).

## Testing Strategy

| Case | Expect |
|---|---|
| Two same-tag, same-rung actions; one holds a Chill-conditional payoff atom, target IS Chilled | the conditional one is chosen |
| Same setup, target NOT Chilled | order falls back to A31's rung/id tiebreak — no regression |
| Different-tag or different-rung candidates | `HasLiveConditionalPayoff` never even called (cost discipline, asserted via a call-counting fixture) |
| An action with no pool/conditional atoms at all | `HasLiveConditionalPayoff` returns `false`, no exception |
| `FactReader.Reads` for a decision with no tied candidates | unchanged from pre-A32 (the new read never fires) |
| Same fixture, run through `SiegeAiIntentSource` instead of `StubIntentSource` | **also** picks the live-conditional action — proves the shared helper, not a `StubIntentSource`-only fix |

## Boundaries

**Always:** reuse E3's closed predicate leaf list and `PredicateCompiler` — never a second condition
language.

**Ask first:** whether the new read belongs on `IBattleView` directly or a new, narrower interface —
implementation detail, not a blocking design question.

**Never:** turn this into a scored/weighted system — the ideal doc explicitly rejected full utility
scoring for now (new tunable surface, bigger problem than what's broken); this module stays a boolean
tiebreak layer, matching the ordered-priority-list shape A31 already established. **Never** implement the
lookahead twice (once per intent source) — extract the shared helper first.

## Success Criteria

1. A live conditional payoff outranks a same-rung, same-tag peer with none.
2. Zero extra reads for candidates that were never tied to begin with.
3. Falls back cleanly to A31's ordering when no candidate has a live conditional.
4. Both `StubIntentSource` and `SiegeAiIntentSource` exhibit the fix, via one shared helper — not one
   fixed and the other silently unchanged.
