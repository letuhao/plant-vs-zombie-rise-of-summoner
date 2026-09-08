# Spec: action-costs-cooldowns-adoption (A19)

Module **A19** in the [action map](../action-map.md) §12/§12.3a. Depends on **A17** (built), **A18f**
(this reopening's own prerequisite — see its spec), and **A3** `action-costs` (specced, six resources,
validate-all/consume-all/rollback rule already authored). **This is Checkpoint G.**

> **Read `action-map.md` §12.3a and `spec-action-costs.md` before this spec.** The former records the
> real, current wiring gap (`CostLedger.TryPay` has exactly one caller, an aura, in the whole repo);
> the latter is the already-sealed design this module wires in, not a design this module invents.

## Objective

Make the affordability check `UsabilityEvaluator` already performs (gate 3, today backed by
`AlwaysAffordable.Instance`, per A17's own explicit deferral) into a real check against a real
`CostLedger`, and make committing to an action actually **spend** its cost — so *"a 3-turn-cooldown
nuke with no cooldown at all"* stops being true.

**What "done" looks like:** an actor with 0 `qi` cannot commit to a `qi`-costed skill (refused by the
same `UsabilityEvaluator` gate that already exists, now backed by real state instead of a
rubber-stamp); an actor who commits to a skill has its cost debited from their real
`ActorResourcePools`, at the moment A3's own rule says it debits (commit, not landing); and a skill's
cooldown, once armed (A18f already reads the right envelope), actually blocks reselection until it
expires — proven with a fixture, not asserted from the gate existing.

**What this module does NOT do:**
- Author new cost data. Every action's cost table is whatever content already carries or will carry —
  this module wires the *mechanism*, never invents a specific action's price.
- Add a seventh resource, change the six-resource identity, or touch `poise`'s own three-part cost
  shape (`spec-action-costs.md` §4) beyond making its existing rules real.
- Build the reaction-lane's own poise spend path — `TimelineDispatch.cs:191`'s
  `ReactionCounter.TryCounter` already spends `poise` for real; this module does not touch it, only
  generalizes the **ordinary action commit** path it currently bypasses.
- Add a battle-board or range gate — A19 has nothing to do with gate 4 (range), which stays exactly as
  it is (§12.3a: RangeChannel stays dead until A10).

## Design (locked on approval)

### 1. `CostLedger` becomes the real `IAffordabilityCheck`, replacing `AlwaysAffordable.Instance` — at BOTH real construction sites, named exactly, not "wherever"

`CostLedger` (`Actions/Cost/CostLedger.cs:42`) already implements `IAffordabilityCheck` and already
has a working `Check(actorKey, actionId)` (line 85) — this is not new code, it is a seam A17
deliberately left unplugged. **Verified by grep, not assumed**: `AlwaysAffordable.Instance` has
exactly two production construction sites in the whole repo, both identical
`new StubIntentSource(view, state.Cooldowns, NoStanceHeld.Instance, AlwaysAffordable.Instance)`
calls — `BasicAttack.cs:117` (`DeclareBasicAttack`'s own `intentSource ?? ...` fallback) and
`TimelineDispatch.cs:70` (the timeline-dispatch path's own identical fallback). **Both must be
swapped, not just one** — missing the second would leave `TimelineDispatch`-profile battles
(all three shipped profiles today, per `decisions.md` 2026-09-05) still rubber-stamping
affordability while the atomic path alone enforced it, a real, easy-to-miss half-fix. A real
`CostLedger` instance is constructed once per `BattleRunState`, mirroring how `ActionCatalog` and
`Cooldowns` are already per-battle-instance state.

**A precise, easy-to-misread detail, stated so it is not**: `Check` (the read-only gate 3
affordability check) validates against `row.AmountSpec.Max` — the cost row's own upper bound — while
`TryPay`'s own pass 1 resolves the ACTUAL amount via `row.AmountSpec.Resolve(rng)`, which can be
**less than** `Max` for a rolled/ranged cost (`CostLedger.cs:99` vs. `:118`). This is deliberate and
correct — checking against the ceiling guarantees a `Check`-then-`TryPay` sequence never fails
`TryPay` after `Check` passed — but it means **`Check` and `TryPay` are not required to report the
same number**, and a test asserting "the checked amount equals the paid amount" for a ranged cost
would be asserting something this ledger was never designed to guarantee. State the actual
guarantee in this module's own tests: `Check` passing implies `TryPay` will not shortfall, never
that the two amounts are equal.

**An actor with no authored cost table for an action is unaffected** — `CostLedger.Check` against an
empty cost list is vacuously affordable, so this swap is inert for every action shipped today with no
authored costs, the identical "additive, byte-identical until content opts in" shape this whole
program has used for every prior adoption (`family_glossary`, `usage_weights`, A18f's own
`CurrentEnvelope` fallback).

### 2. `TryCommitReady` calls `CostLedger.TryPay(..., ActionCostTiming.OnCommit, ...)` before committing

In `TimelineDispatch.cs`'s local `TryCommitReady` (the function already gating on
`slots.HasFreeSlot`/`economy.TryAcquire` before calling `runner.TryCommit`, lines 110-133), one more
gate joins the chain, in the same "check before spend, spend before commit" order those two already
established:

```csharp
if (!costLedger.TryPay(attacker.Setup.Key, envelope.ActionId, ActionCostTiming.OnCommit, atomRng).Success)
{
    // Same refusal shape as the existing no-target/CC-locked branch: give the turn back, do not
    // refund the slot/economy already spent — matching this function's own stated rule for every
    // other "declared but could not commit" exit (line 123-129's own comment).
    machine.TransitionTo(Timeline.TurnState.Charging);
    trace?.Turn(rounds, attacker.Setup.Key, Timeline.TurnState.Ready, Timeline.TurnState.Charging);
    return false;
}
```

**Placement, precisely**: after `DeclareBasicAttack` returns a real `envelope` (so the actionId being
paid for is the one actually selected — this is exactly why A18f had to land first: paying for the
wrong action's cost table would be a worse defect than paying for none), and before
`runner.TryCommit`. `CostLedger.TryPay` already implements validate-all/consume-all/rollback-on-fail
internally (A3 §3) — this module calls it once, correctly ordered; it does not reimplement the
transaction rule.

### 3. `perTick` costs end the action through the interrupt path, not a new failure mode

A3 §3's own rule: *"A `perTick` cost that cannot be paid ends the action through the interrupt path:
cancel remaining resolves, release the slot, charge `interrupt_cooldown_milli`."* `ActionRunner`
already has an `Interrupt` path (referenced in `action-map.md` §10.4c item 4, `InterruptCooldownMilli`
already a real envelope field). This module's `perTick` handling is: at each `Resolve`/tick boundary
where a `perTick`-costed action is mid-run, call `TryPay` again; on failure, call the existing
`runner.Interrupt` rather than inventing a second cancellation mechanism.

### 4. Cooldown arming already generalizes once A18f lands — this module adds nothing here

`BasicAttack.cs:198`'s `state.Cooldowns.Start(...)` call already reads `envelope.CooldownChannel` off
whatever envelope it is given — it was never attack-specific (species-skills S2 built the channel
read generically). A18f's fix (making the *right* envelope reach this call at resolve time) is the
entire remaining gap; A19 verifies it with a real non-attack cooldown fixture rather than re-building
the arming mechanism.

## Golden-safety

**Claim: zero-golden-mover**, for the same reason A18f is one — no shipped action carries a non-empty
cost table today, so `CostLedger.Check`/`TryPay` are vacuously true/no-ops against every real battle
that exists. **Prove it, per this repo's own repeated lesson**: run the full suite, diff all eight
goldens, before asserting rather than predicting.

## Acceptance criteria

1. An actor lacking a resource an action costs is refused at `UsabilityEvaluator`'s existing gate 3 —
   proven with a fixture action carrying a real cost row and an actor pool at 0.
2. Committing to a costed action debits the actor's `ActorResourcePools` by exactly the authored
   amount, at commit — never at resolve, never at landing (fizzle/miss/interrupt all still cost,
   proven with three separate fixtures, one per exit path).
3. **Corrected against `TryPay`'s real, verified implementation** (`CostLedger.cs:106-135`): it is
   not spend-then-rollback, it is **validate-all-then-spend-all** — pass 1 resolves and checks every
   row against the SAME `nowTick`/derived snapshot with nothing spent yet, and only if every row
   clears does pass 2 spend any of them. So the real acceptance line is: a multi-resource cost where
   the first row is affordable and the second is not spends **neither** — asserted per pool (both
   pools' values unchanged after the call), not by checking a single aggregate "did it fail" flag,
   which would pass even if pass 1's own two-phase split were silently broken into spend-then-refund.
4. A `perTick` cost that cannot be paid mid-action interrupts through the existing `ActionRunner`
   path, charging `InterruptCooldownMilli` — never a bespoke cancellation branch.
5. An action on cooldown is refused at commit — proven with a real skill, not the basic attack (the
   basic attack's own cooldown-arming already worked; this proves it generalizes).
6. Every action with no authored cost table behaves byte-identically to today.
7. All eight existing battle goldens byte-identical, run for real.

## Testing strategy

- Unit: `CostLedger.TryPay`'s own validate-all-then-spend-all behavior is **verified real** in this
  audit (`CostLedger.cs:106-135`'s own doc comment states it explicitly) — this module adds no new
  ledger logic, only call sites, so its own tests are integration-shaped: a synthetic
  `BattleRunState` with a fixture actor/action/cost row, driven through `TimelineDispatch`.
- Integration: the five exit-path fixtures named in acceptance #2-#5, each asserting the actor's real
  resource pool value before/after via `ActorResourcePools.Resolve`, never by reading a mock.
- Regression: full `dotnet test tests/FusionRpg.Core.Tests`, eight-golden diff.

## Boundaries

- **Never a second cost-transaction implementation.** `CostLedger.TryPay` is the one mechanism; this
  module only calls it from the places it was never called from.
- **Never authors a cost number.** A magic number on the balance surface belongs in
  `data/tuning/action-costs.v{n}.json` or wherever A3's own tuning file lives — not in this module's
  code.
- **Never touches gate 4 (range).** Stays dead until A10, unchanged by this module.
