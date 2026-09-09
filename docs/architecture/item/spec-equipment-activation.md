# Spec: equipment-activation

**Status:** Proposed — module 24 in [item-map.md](../item-map.md). Depends on
`requirement-profiles` (23) and `equip-runtime` (5).

## Objective

Make an equipped item's frozen requirement profile observable at runtime without
unequipping it. An item moves between `trial`, `active`, and `suspended`; only
an active item contributes its effects. Resource maintenance is the tradeoff
for strong items. Trial is non-blocking guidance, never a resource charge or
an assignment refusal.

`EquipAtomSource` already reads durable equipment bindings into battle and
derived composition (`Battle/EquipAtomSource.cs:23-45`). This module filters
that existing read; it does not add an effect delivery path or compose a new
actor magnitude.

## Contract

### Deployment-run status

The requirement profile and durable assignment remain item facts. Activation is
an in-memory `EquipmentRunStatus`, keyed by one host-provided deployment key
and the existing assignment identity:

```text
(deploymentKey, specimenId, role, refKind, refId)
```

It holds `state`, typed `reason`, `nextDueTick`, `hpRecoveryLocked`, and a
monotonic state revision for that deployment only. The possible states are:

```text
trial       build or level trial is not satisfied; no item effects
active      all trial conditions pass and upkeep, if any, is current
suspended   upkeep failed or an active build trial lapsed; no item effects
```

Reasons are `build_unmet`, `level_unmet`, `upkeep_shortfall(resourceId)`,
`hp_recovery_locked`, and `set_trial_unmet` (reserved for module 25). Removing
an assignment, clearing its binding, or ending its deployment clears the status.
A new deployment always evaluates the frozen profile afresh; it inherits no due
tick, suspension, HP latch, or resource debt from a previous deployment.

`deploymentKey` is opaque to Core. It identifies a lawn board by `MatchKey`, a
standalone server battle by its battle-run key, a Delve deployment by its run
key across all rooms, or a siege engagement by its resolver-issued key. Lawn
status begins only at `PendingSpawn -> Bound`, not at roster assignment or
`Deploying`; it clears on `Bound -> Cleared`, failed deploy, or board end. A
paused lawn freezes its logical clock and charges nothing. Delve status and its
schedule survive room boundaries, then clear when the Delve deployment ends.
Siege creates status only for an individually equipped participant admitted to
the engagement; world-map troop legions and structures are never participants
of this system and pay no maintenance. Rest can refill pools and permit
ordinary reactivation, but it never creates a back-charge.

### Current ownership and future convergence

Deployment lifecycle is currently distributed: `MatchRuntime` and
`UniqueBindings` own live-lawn binding, `BattleEngine` owns standalone battle
execution, the Delve session owns cross-room continuity, and the planned siege
resolver owns a siege engagement. There is no general deployment-scope runtime
today. Module 24 uses narrow host adapters over those owners; it must not
replace or merge their FSMs as incidental equipment work.

A later cross-program refactor may introduce a common deployment contract for
the four owners. It must standardize only an opaque deployment key, logical
clock, participant-bound/cleared events, and actor-pool lookup. It must not
centralize combat scheduling, lawn observation, world-map movement, or durable
actor lifecycle. Migrate one adapter at a time after its existing lifecycle
tests are green; retain each owner's existing FSM as the source of transitions.

### Evaluation and effect visibility

On assignment projection, loadout deployment, aptitude change, and every due
upkeep tick, `EquipmentActivationService` evaluates the frozen profile through
module 23. It never calls `EquipGate.Admits`; generated requirements remain
outside that hard gate (`Items/EquipGate.cs:54-65`).

The service supplies an `IsActive(assignmentIdentity)` predicate to the
existing equipment binding resolver. `EquipAtomSource` excludes inactive
bindings before either battle or derived projection. The durable binding is
never deleted for a trial or suspension. A changed state revision triggers the
normal snapshot refresh so every atom from that item appears or disappears as
one contribution.

### Upkeep schedule and ordering

Upkeep uses the deployment's simulation logical ticks, never wall time. On
activation, first payment is due at `currentTick + periodTicks`; equipping
itself costs nothing. Every supported deployment supplies one monotonic logical
clock and actor-pool owner: the lawn board clock while `InMatch`, the standalone
battle clock, the Delve run clock, or a siege engagement clock. Equipment owns
neither a second clock nor a pool. The scheduler advances all due intervals,
including a delayed frame, but never charges offline, paused, rest, or
cross-deployment elapsed time.

For each due tick, sustained active assignments are processed in this exact
order:

```text
specimenId ordinal, role ordinal, refKind ordinal, refId ordinal
```

Each payment delegates to a shared `CostLedger.TryPayProfileMaintenance` entry
point. Its `FrozenResourceCharge(resourceId, cost, reserve)` holds exactly one
legal resource and frozen `long` values. It does not read an action row, rung,
Theta scaling, or RNG. In checked arithmetic it validates
`current - cost >= max(reserve, hpFloor)` and then spends `cost` atomically,
returning the existing typed shortfall result. A shortfall suspends only that
item and records its resource id. There is no aggregate debit, partial payment,
or negative resource debt.

Only profiles from maintenance-eligible strong power bands can reach this path;
the profile resolver owns that selection. The activation module must not infer
item power or rarity again.

### Reactivation and HP

A non-HP suspended item may reactivate at a later evaluation when its trial
passes and its next payment plus reserve is affordable. Reactivation does not
back-charge missed intervals.

HP upkeep is non-lethal. It uses the ledger's HP floor and additionally keeps
the profile reserve. An HP shortfall sets `hpRecoveryLocked` and suspends the
item. The lock clears only at an evaluation where `currentHp == maxHp` from the
same fresh derived snapshot. Clearing the lock does not waive the next payment:
the item becomes active only after that payment can preserve reserve. A changed
maximum HP changes the definition of full at the next evaluation; no stale max
value is persisted.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~EquipmentActivation"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~AuraUpkeep"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~ActorResourcePools"
```

## Project structure

```text
src/FusionRpg.Core/Items/Activation/
  EquipmentActivationState.cs       state/reason contract
  EquipmentActivationService.cs     pure transitions and payment orchestration
  EquipmentUpkeepScheduler.cs       canonical logical-tick order
  EquipmentRunStatusStore.cs         deployment-scoped status owner
  EquipmentDeploymentClock.cs        host clock/pool adapter contract
src/FusionRpg.Core/Battle/
  EquipAtomSource.cs                 EDIT: filter through activation predicate
tests/FusionRpg.Core.Tests/Items/EquipmentActivationTests.cs
tests/FusionRpg.Core.Tests/Items/EquipmentDeploymentStatusTests.cs
```

## Code style

```csharp
// Durable assignment remains true. Activation is a deployment-scoped read
// state, so an unpaid item disappears from the effect source, never inventory.
if (!trial.Ready) return ActivationTransition.ToTrial(trial.Reason);
if (!payment.CanPreserveReserve) return ActivationTransition.Suspend(payment.Reason);
return ActivationTransition.Activate(nextDueTick);
```

State transitions are pure and named. Resource values and all magnitudes are
`long`; overflow throws. Period, reserve, and cost are profile/tuning data, not
code literals.

## Testing strategy

| Test | Asserts |
|---|---|
| unmet trial equips | assignment survives and item is `trial` with no effects |
| active filtering | inactive item atoms reach neither battle nor derived projection |
| canonical contention | scarce shared resource leaves the same active set regardless of source order |
| no partial payment | a failed multi-row upkeep spends nothing |
| fresh deployment | a new deployment has no inherited due tick, suspension, or HP latch |
| delayed tick | every due interval is processed in canonical order |
| no offline charge | reopening after offline time creates no retroactive debit |
| non-HP reactivation | current affordability restores effects without back-charge |
| HP reserve and floor | payment cannot kill or cross profile reserve |
| HP latch | failed HP upkeep stays inactive until fully restored, then still needs payment |
| assignment removal | record and contribution are both gone after projection |
| lawn lifecycle | only Bound starts status; pause freezes it; clear/end removes it |
| Delve continuity | status and schedule survive rooms, then clear at deployment end |
| siege eligibility | equipped combat participants pay; troop legions and structures never do |
| all runtimes | each supported runtime has a logical tick caller before sustained content is enabled |

## Boundaries

**Always:** preserve durable assignment; use `CostLedger`; process the canonical
order; retain due state only within its deployment; filter at the existing
equipment read; expose typed state/reason to module 20.

**Ask first:** a new deployment runtime; enabling sustained profiles in a
runtime without a logical tick caller; lethal HP maintenance; or any change to
static equip-gate semantics; or beginning the cross-program deployment-scope
refactor.

**Never:** force unequip for trial/upkeep; charge wall-clock/offline time; make
negative debt; write resource pools directly; create a second effect path;
activate one atom of a suspended item; or calculate upkeep from a private power
curve.

## Success criteria

- [ ] A player can equip any item admitted by the existing static gate even
      when its generated trial is unmet.
- [ ] Active state is reproducible within one deployment, clears at deployment
      end, and filters all of an item's effects through the existing equipment
      source.
- [ ] Shared-resource contention, reserve checks, and delayed ticks have one
      canonical outcome.
- [ ] HP maintenance is non-lethal and obeys full-recovery reactivation.
- [ ] No sustained profile can run in a mode lacking its logical-tick caller.
