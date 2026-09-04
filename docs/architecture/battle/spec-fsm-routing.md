# Spec: fsm-routing (B37)

Module id `fsm-routing` (B37) in the [battle timeline map](../battle-timeline-map.md).
**Written 2026-09-04**, from a finding rather than from the original plan.

## Why this exists

`BattleEngine.Resolve` runs the *round skeleton* on the kernel (T5/B14: `SimulationClock`,
`EventQueue`, `NextEventAdvance`), but **combat itself never touches the per-actor turn machinery**.
Verified, not inferred:

- The `profile` parameter appears **twice** in the whole of `BattleEngine.cs` — the signature and one
  comment. It is never read.
- `ActorTurnMachine`, `ReadinessDriver`, `ActionSlots` and `ITurnEconomy` appear **nowhere** in
  `BattleEngine.cs` or `BattleRunState.cs`.
- The `profile` parameter's own doc says it: *"The profile's other fields (`W`, `WScope`,
  `Commitment`, `Economy`) are accepted and available for future enrichment but **are inert here**."*

This was a correct deferral, not an oversight — `TurnReadiness` names it (*"B9's own remaining half …
is NOT attempted here"*) and `spec-kernel-adoption.md` calls it *"explicitly out of scope"* for T5.
It was simply never scheduled, and **six open items are behind it**: B20/B21/B22 (an interactive dwell
needs a turn to occupy) and B34/B35/B36 (a profile switch that changes nothing measures nothing).

## Scope — honest about what can and cannot bind in a batch resolver

`Resolve` is a **batch** resolver: actions are atomic and time jumps between events. That constrains
which profile fields can possibly matter, and saying so up front is most of this spec's value.

| Field | Can it bind here? | Why |
|---|---|---|
| **`Economy`** | ✅ **Yes** | A per-round budget decides *how many* actions an actor gets. `OneActionPerTurnEconomy` gives 1 (today's behaviour exactly); `ActionPointsEconomy(2)` gives 2. Directly observable |
| **`Commitment`** | ✅ **Yes** | Late-bound picks a target at resolve; early-bound picks it when seated. Changes *who gets hit* when a target dies mid-round |
| **`W` / `WScope`** | ⚠️ **No, and that is structural** | `ActionSlots`' own doc: *"W only **binds** when actions have wind-up: under next-event advance with a strict total order and atomic resolution, a battle is already serialized regardless of W."* The slot path is still exercised (acquire/release around every action) so it is live and testable, but with atomic resolution it can never refuse. **Making W bind needs wind-up, which is a separate module** |
| **`AdvancePolicy`** | ⚠️ **No** | A batch resolve has no frames. Already documented on the `profile` parameter, and B32 measured the mechanism cost at 1.2× for when it does matter |

**So B37 makes two of four fields live and proves the other two are exercised-but-inert, with the
reason.** That is the honest ceiling for a batch resolver, and it is enough to unblock B34 (a sweep
now has something to measure) and B20 (a turn now exists to occupy).

## Design

### 1. The round's action phase becomes budget-gated

Today step 2 is: initiative-order the active actors, then one `RunBasicAttackStep` each. That is
replaced by a loop that asks the profile whether each actor may act, repeated until no one can:

1. At the round boundary, `Economy.ResetForNewTurn(key, tick)` for every active actor.
2. Repeat passes in initiative order. In each pass, an actor acts only if
   `Economy.TryAcquire(key, cost: 1, tick)` **and** `ActionSlots.TryAcquire(actorKey, side)`.
3. Release the slot immediately after the action resolves (atomic resolution — there is no wind-up to
   hold it across).
4. `Economy.OnActionResolved(key, Normal)` after a resolved hit.
5. Stop when a pass produces no action.

**`classic-round` is byte-identical by construction**: `OneActionPerTurnEconomy.TryAcquire` is
`_spent.Add(key)`, so every actor succeeds exactly once in pass 1 and every actor fails in pass 2 —
one action each, in initiative order, which is precisely today's loop. `W = 1` Global acquires and
releases around each sequential action and never refuses.

**`hybrid-atb` genuinely differs**: `ActionPointsEconomy(2)` lets every actor act twice per round.

⛔ **The `Break` outcome keeps its exact meaning.** `AttackStepOutcome.Break` ends the round's action
phase (hazard 3, `StubIntentSource` declining). It must break out of *both* the pass loop and the
per-actor loop, or a round that should end early would keep going — a behaviour change disguised as a
refactor.

### 2. The economy's scope key

`ITurnEconomy.Scope` is `PerActor` or `PerSide`. The key is the actor key for `PerActor` and
`"side:" + Side` for `PerSide` — the string-namespace trick `TurnEconomy`'s own doc already
establishes, so a side-shared press-turn budget works without a second code path.

### 3. Commitment is deferred, deliberately, and said so

`EarlyBoundWithFallback` changes *when* the target is chosen, which the B35 note already flags as
moving `BloodthirstyView`'s lowest-HP read earlier. `classic-round` and `galaxy-sync` are both
`LateBound`, so nothing shipped needs early binding today. **This module wires the economy and slot
gates only**; early binding lands with the profile migration that actually selects a profile using it,
where its golden delta can be predicted in the same pass. Stated here so its absence is a decision.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Golden"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~FsmRouting"
```

## Structure

```
src/FusionRpg.Core/Battle/BattleEngine.cs                      (the action phase)
tests/FusionRpg.Core.Tests/Battle/FsmRoutingTests.cs
```

## Testing strategy

1. **`classic-round` byte-identical** — all eight goldens unchanged, `RulesetVersion` stays 4. This is
   the gate; a moved golden is a defect in the module, not a balance outcome.
2. **The economy binds, proven by contrast**: the same battle and seed under a profile with
   `ActionPointsEconomy(2)` produces a different report than under `OneActionPerTurnEconomy`. If it
   does not, the gate is decorative.
3. **The slot path is exercised**: acquire/release is called per action and the slot is always
   released (a leaked slot would deadlock a later wind-up profile). Asserted by the slots' own `Held`
   returning to 0 after the phase.
4. **`Break` still ends the phase** — a declining intent source ends the round exactly where it did.
5. **Falsifier**: remove the `TryAcquire` gate and test 2 must go red.

## Boundaries

- **Always:** release every acquired slot; reset budgets at the round boundary; preserve `Break`.
- **Ask first:** making `W` bind (that needs wind-up and is a separate module); enabling early binding.
- **Never:** move a golden under `classic-round`; branch on `AdvancePolicyKind` (the architecture test
  bans it in every file); read a wall clock.

## Success criteria

1. Eight goldens unchanged under `classic-round`; `RulesetVersion` 4. 2. A points economy demonstrably
changes a battle. 3. Slots are acquired and released per action with none leaked. 4. `Break` semantics
unchanged. 5. The `profile` parameter is genuinely read — the condition B34 and B20 were waiting on.
