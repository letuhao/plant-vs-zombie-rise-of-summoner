# Spec: `membership-events`

**Module id:** `membership-events` · **Program:** [buff-debuff-scope-map.md](../buff-debuff-scope-map.md) ·
**Status:** Draft — pending owner review.

**Depends on:** nothing · **Blocks:** `battlefield-scope`'s own-side WHO-value completeness only
(target/type/unique-demon need nothing from this module)

---

## Corrected during audit, 2026-08-29 — the first draft overclaimed how much already exists

The original draft, based on a `grep` hit count alone, claimed `MatchRuntime.cs`/`SimEngine.cs` already
*consume* `zombie.hypno` and this module would just add a downstream call. **Read in full this pass —
that was wrong:**

- `"zombie.hypno"` **is** a real, shipped event —
  [`Bridges/pvzrh-3.9/CreateZombieHooks.cs:273-286`](../../../src/FusionRpg.Injector/Bridges/pvzrh-3.9/CreateZombieHooks.cs),
  a Harmony postfix on `Zombie.SetMindControl`, firing on **every** call (both directions) with `ptr`,
  `controlLevel`, `isMindControlled`. This part of the original finding holds.
- **But `MatchRuntime.cs` does not consume it.** Its event dispatch
  ([`Match/MatchRuntime.cs:60-111`](../../../src/FusionRpg.Core/Match/MatchRuntime.cs)) is a chain of
  `if (string.Equals(kind, "...", OrdinalIgnoreCase)) { ...; return; }` blocks — `plant.spawn`,
  `zombie.spawn`, `plant.die`, `zombie.die`, each mutating `_state.Board`/`_state.UniqueBindings` and
  calling `Bump()`. **Line 110 is a bare comment: `// W1 later: bullet.init, zombie.hypno, …`** — a
  placeholder for work that was never built, not a working case with a downstream hook to extend.
- `SimEngine.Hypno` (`SimEngine.cs:641-647`) only **produces** a synthetic `zombie.hypno` event for test
  harnesses to inject — it is a test helper matching the real event's shape, not a consumer either.

**So there is, today, no code anywhere in Core that does anything with a mind-control transition.** This
module is not "hook into existing handling" — it is **the first real consumer** of an event that already
exists and already arrives, but has never been acted on. Smaller than "detect mind-control from
scratch" (the event, its shape, and its arrival point are all real and proven); larger than the original
draft claimed. The dispatch chain it extends is a real, well-understood, already-extensible pattern —
adding a `zombie.hypno` case is exactly the shape of change `plant.die`/`zombie.die` already are, not a
foreign concept grafted on.

**Scope decision, following this program's own established precedent:** the action program's `P0.2`–
`P0.5` and this program's own `world-map-scope` were each, at first, treated as blocked on something
external — and each time, the owner chose to authorize building across that boundary rather than leave
the gap as someone else's problem (`tasks/action-todo.md`: *"unblocked by building it across the program
boundary under explicit owner authorization"*). The `zombie.hypno` gap is the same shape: nobody else's
work is currently blocked on it, and it was always intended to be built (the comment says so). **This
module owns building it**, named explicitly as a scope increase found during audit rather than absorbed
silently.

## Objective

Emit one well-defined signal — an entity's scope-membership just changed — on exactly three transitions:
a demon specimen binds (spawns), a demon specimen clears (dies/leaves), and a mind-control state flips
(either direction). This module also becomes the first place mind-control state is tracked in
`MatchState`/`BoardProjection` at all — there is nothing to read that state from today. It does not
decide what a consumer does with the signal (`battlefield-scope`'s job).

**Users:** `battlefield-scope`'s own-side WHO-value (the only consumer this program defines so far).

**Success is measurable:** all three transitions are observable as one signal shape; the spawn/clear two
are proven to already work via `UniqueBindings.cs`'s existing tests (cited, not re-proven); the
hypnotize-toggle one is proven end-to-end against the real `zombie.hypno` event shape, through a real new
`MatchRuntime.cs` dispatch case, not an assumed one.

## Design

```csharp
public enum ScopeMembershipTransition
{
    Bound,               // UniqueBindingPhase.PendingSpawn -> Bound
    Cleared,              // UniqueBindingPhase.* -> Cleared
    MindControlToggled,   // zombie.hypno, either direction
}

public readonly record struct ScopeMembershipEvent(
    string Ptr,
    ScopeMembershipTransition Transition,
    bool? MindControlledNow);   // only meaningful for MindControlToggled
```

**Deliberately a struct, no allocation on the hot path** — matches `UniqueBinding.Clone()`'s own
allocation discipline and `spec-ai-commander.md`'s stated zero-allocation bar for anything in a per-tick
path.

**Bound/Cleared:** `MatchUniqueBindingsFacet.TryBindOnSpawn`/`ClearInstance` already run on exactly the
right transitions (`Match/UniqueBindings.cs:110-140`, `:202-215`) — this module adds an event raised from
those two existing call sites, not new detection logic.

**MindControlToggled:** the real new piece, scoped precisely now. `MatchRuntime.cs`'s dispatch chain
(`:60-111`) gains one more case, in the exact shape of its existing siblings:

```csharp
if (string.Equals(kind, "zombie.hypno", StringComparison.OrdinalIgnoreCase))
{
    var ptr = ReadPtr(payload);
    var mc = ReadBool(payload, "isMindControlled");   // new helper, mirrors ReadPtr/ReadTypeId
    if (_state.MindControl.Set(ptr, mc))   // new, minimal tracked state — see below
        Bump();
    Raise(new ScopeMembershipEvent(ptr, ScopeMembershipTransition.MindControlToggled, mc));
    return;
}
```

`_state.MindControl` is new — the smallest possible tracked set (a `ptr → bool` map, or a `HashSet<ptr>`
of currently-controlled entities), added to `MatchState` alongside `Board`/`UniqueBindings`. Nothing
today reads mind-control state on the Core side at all; this module is what makes it exist there for the
first time, not merely what re-announces an existing read.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~MembershipEvents
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~MatchRuntime
dotnet test tests\FusionRpg.Core.Tests
.\scripts\guard-secondary-no-unity.ps1
```

The `~MatchRuntime` filter is deliberate: this module edits shipped dispatch code, so its own existing
tests are the direct regression check, not just this module's new ones. The no-Unity guard matters
because this module must consume `zombie.hypno` at the point Core already receives it, never add a
second, ad-hoc read of Unity's mind-control state.

## Project structure

```
src/FusionRpg.Core/Match/ScopeMembershipEvents.cs   → the event type
src/FusionRpg.Core/Match/UniqueBindings.cs          → edited: raise Bound/Cleared from existing call sites
src/FusionRpg.Core/Match/MatchRuntime.cs            → edited: new "zombie.hypno" dispatch case (:110)
src/FusionRpg.Core/Match/MatchState.cs              → edited: new MindControl tracked set
tests/FusionRpg.Core.Tests/Match/ScopeMembershipEventsTests.cs
```

Lives under `Match/`, next to `UniqueBindings.cs` — this module extends that file's own FSM rather than
wrapping it from outside.

## Code style

Struct, not class, for the event (allocation discipline above). No new detection logic anywhere — every
raise site is an addition to an existing, already-correct transition point, never a new poll or a new
Unity read.

## Testing strategy

- **Bound/Cleared:** a test proving the event fires exactly once per real `TryBindOnSpawn`/`ClearInstance`
  call, reusing `UniqueBindings.cs`'s own existing test fixtures rather than building parallel ones.
- **MindControlToggled, end to end:** feed a real `zombie.hypno` payload (via `SimEngine.Hypno`, the
  existing test-harness producer — reused, not duplicated) through `MatchRuntime`'s dispatch and assert
  `_state.MindControl` updates **and** the `ScopeMembershipEvent` fires with the right `ptr`/direction.
  Both directions covered — mind-controlled and released — since the real hook fires on both.
- **No double-fire:** a specimen that binds then immediately gets hit by `zombie.hypno` produces two
  distinct events, not one merged/lost one.
- **Idempotent re-application:** two `zombie.hypno` events with the same `isMindControlled` value in a
  row (the real hook can fire redundantly — nothing in the postfix de-duplicates) update `MindControl`
  without a spurious second `Bump()`, matching `plant.die`/`zombie.die`'s own `if (...TryRemove...)
  Bump()` pattern of only bumping on an actual change.
- **Regression:** `Match/UniqueBindings.cs`'s own existing test suite, and `MatchRuntime.cs`'s existing
  dispatch tests for `plant.spawn`/`zombie.spawn`/`plant.die`/`zombie.die`, stay green with zero behaviour
  change — this module adds one new case and one new tracked set, never alters an existing one.

## Boundaries

- **Always:** raise the event from an existing, already-correct transition point (Bound/Cleared) or a new
  dispatch case built in the exact shape of its siblings (hypno); read mind-control state only from the
  event Unity already sends, never poll it.
- **Ask first:** any new Unity-side hook — the event and its shape are already real; a *second* Unity-side
  signal would be a real scope change, not a detail.
- **Never:** a second Unity read of mind-control state (`guard-secondary-no-unity.ps1`'s exact concern);
  a change to `UniqueBindings.cs`'s existing transition *logic*, only an event raised alongside it; a
  change to `plant.spawn`/`zombie.spawn`/`plant.die`/`zombie.die`'s existing behaviour while adding the
  new case next to them.

## Success criteria

1. All three transitions observable as one event shape, zero new allocation.
2. Bound/Cleared proven via `UniqueBindings.cs`'s own existing, unmodified test fixtures.
3. MindControlToggled proven end-to-end: a real `zombie.hypno` payload through `MatchRuntime`'s dispatch
   produces both the tracked-state update and the `ScopeMembershipEvent`, both directions.
4. `guard-secondary-no-unity.ps1` stays green — no new Unity read introduced.
5. `MatchRuntime.cs`'s four existing dispatch cases are unchanged and their tests stay green.
