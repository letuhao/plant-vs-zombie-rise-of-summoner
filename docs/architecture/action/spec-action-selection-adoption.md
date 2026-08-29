# Spec: action-selection-adoption (A17)

Module **A17** in the [action map](../action-map.md) §12. Depends on A2, A4, A7 (all built and
sealed). **This is the module that finally delivers Checkpoint A/C's own promise** —
`BattleEngine` actually calling the action program at runtime, not just proving the envelope shape
is compatible.

> **Read `action-map.md` §12 before this spec.** It records why this reopening exists (a
> completeness audit found zero production callers into the action program from `BattleEngine`),
> the two scope calls the owner made explicitly (full switch-over, not staged; full multi-action
> loadouts, not a single-action slice), and what stays out of scope (grant-writer, Server/API, FE,
> movement/board/reaction-lane).

## Objective

Replace `BattleEngine`'s hardcoded `SelectTarget` + implicit "always basic attack" behavior with a
real per-actor decision loop: for every attacker, every turn, ask `StubIntentSource` (via a new
`IBattleView` adapter over `BattleRunState`) **which target** and **which held action** to use —
driven by whatever synthetic loadout (`BattleActorSetup.EquippedActionIds`, already plumbed since
this session's Phase 2 work) the caller supplied.

**What "done" looks like:** two actors with different equipped loadouts, fighting the same
opponent under the same seed, target differently or act differently, and the difference is
attributable to a specific `CompiledAction` in their loadout — not to engine code that happens to
read `EquippedActionIds` as a label.

**What this module does NOT do** (later modules in the same reopening):
- Resolve a **real skill's** effects — A18's job. In this module, whichever action is chosen still
  resolves through the exact same `calculator.Compute` → `ApplyHp` path the basic attack already
  uses, treating every chosen action as "a basic-attack-shaped hit" regardless of its own atom
  container. Proving selection is real, before resolution complexity layers on top, is the point.
- Enforce real costs or non-trivial cooldowns — A19's job. `AlwaysAffordable.Instance` and
  `NoStanceHeld.Instance` (both already shipped, `src/FusionRpg.Core/Actions/IAffordabilityCheck.cs`
  — the exact seam this codebase used to stage A3 and A8 the same way) are wired in as-is.
- Give actors real resource pools, a battle-board, or movement.

## Design (locked on approval)

### 1. `IBattleView` over `BattleRunState`

A new sealed class, `BattleViewAdapter` (or nested in `BattleRunState.cs` alongside the other B13
extraction, matching that file's own "state + the things that read it" shape), implements
`IBattleView` by reading `BattleRunState.Actors`/`ByKey`/`Status`:

| `IBattleView` member | Source |
|---|---|
| `LiveActorKeys` | `Actors.Where(a => a.Active).Select(a => a.Setup.Key)` — **same `Active` predicate `SelectTarget` already uses** (alive and not retreated), so "who is a legal target" does not silently diverge between the old and new paths |
| `SideOf(key)` | `ActorState.Setup.Side` mapped to `0`/`1` (squad/wave) — the two-side vocabulary `EntityFacts.Side` already uses elsewhere in this codebase; no third side exists in battle mode |
| `PositionOf(key)` | **Always `null`.** `BattleEngine` has no board (`action-map.md` §10.4d: the grid is "deferred but its parameters are not" — this module is exactly the code path that stays byte-identical-shaped with no board, per `StubIntentSource.NearestEnemy`'s own documented `SourceOrder` fallback). Confirmed as the CORRECT default, not a placeholder to fix later in this module. |
| `FactsOf(key)` | `EntityFacts` populated from what's genuinely known today: `Side`, `TypeId`, `HpMilli` (`Hp * 1000 / MaxHp`, clamped), `ElementId` (from `ElementTypes`/`ElementPrimary`). `Row`/`Col` default `0`/`0` (inert — no board, matching `PositionOf`); `IsMindControlled` from the actor's status set if a mind-control status exists, else `false`; `IsKiller` `false` (no kill-tracking concept in `EntityFacts` terms exists yet); `Stock0Qty..Stock3Qty` all `0` (A19's resource pools land later — this module's actions carry no stock-gated conditions, so this is inert, not silently wrong) |
| `HeldActionsOf(key)` | The actor's compiled loadout — see §2 |

### 2. Compiling a synthetic loadout

`BattleActorSetup.EquippedActionIds` already exists and is already threaded through
`BattleRunState`'s construction (this session's Phase 2 work) — but it is currently pure
observability, never compiled. This module adds: at `BattleRunState` construction, for each actor
with a non-empty `EquippedActionIds`, resolve each id to a `CompiledAction` via `ActionCatalog`
(already built, A6 — this is its **first production caller**). An actor with an **empty**
`EquippedActionIds` gets exactly one held action: the basic attack (`BasicAttack.BasicAttackEnvelope`
+ `BasicAttackTargeting`, wrapped as a `CompiledAction`) — so "no loadout supplied" still produces a
legal, single-action AI decision, never `ActionIntent.None` by construction.

**Ordering, load-bearing per `StubIntentSource`'s own contract** (`HeldActionsOf` "is expected to
already be preference-ordered... sorted once wherever an actor's action set is frozen"): compiled
actions are sorted once at construction by `ActionTagPreference`'s existing ranking (T34's own
preference key — offensive first, utility last, then `action_id` ordinal), not per decision.

### 3. The per-attacker loop, replacing `SelectTarget`

`BattleEngine`'s round body currently does, per attacker in initiative order:

```
active check → CC-lock check → SelectTarget → calculator.Compute → miss? continue
```

This module changes it to:

```
active check → CC-lock check
  → StubIntentSource.TryDeclare(actorKey, roundClock.Now)   [NEW]
  → intent.IsNone? treat as "no valid target" (today's Break semantics — see §4)
  → resolve intent.TargetKey through the SAME bloodthirsty/loyal post-processing
    BattleEngine already runs (see §5 — this does NOT move into StubIntentSource)
  → calculator.Compute against the resolved target → miss? continue
  → [cooldown] _cooldowns.Start(actorKey, intent.Envelope, now) if the chosen action has one
```

`RunBasicAttackStep` (`Actions/BasicAttack.cs`) is the method that changes — its `SelectTarget` call
is replaced by the intent-source call above; everything from `calculator.Compute` onward, and the
whole `EngineBehavior` trait tail in `BattleRunState.DispatchHit`, is **untouched**, per the same
boundary A5's own spec already drew ("Everything from the berserker ramp onward... stays engine
code").

### 4. `ActionIntent.None` semantics — reusing the existing `Break`, not inventing a new one

`RunBasicAttackStep` already has an `AttackStepOutcome.Break` case for "no valid target for this
attacker at all — hazard 3: the round breaks, it does not continue" (A5's own hazard fixture 3,
still in force). `StubIntentSource.TryDeclare` returning `ActionIntent.None` — which now covers a
strictly larger set of reasons (no held actions, no live enemy, nothing usable, can't move into
range) than `SelectTarget`'s old "no live enemy" — maps onto the **same** `Break` outcome. This is
a deliberate simplification for this module: a full turn-economy "pass and keep going" (matching
`StubIntentSource`'s own step-5 "pass — a requirement, not a fallback" framing, and the timeline
kernel's `action.passed` concept from B4) is real richer behavior the round-based `BattleEngine`
loop was never built to express turn-by-turn. Recorded as a **named scope boundary**, not silently
narrowed: `BattleEngine` breaks the round's attacker loop on the first actor with nothing usable,
exactly as it does today for "no live enemy" — a genuinely fuller pass/continue turn economy is
future work, not this module's.

### 5. `bloodthirsty` and `loyal` stay engine-side, deliberately

`SelectTarget` special-cases two ENGINE traits that `StubIntentSource` has no vocabulary for and
must not gain one for (`action-map.md` §7's own refusal table: "no effect vocabulary is invented
here that [another program] already owns" — traits are `EngineBehavior`, owned by `BattleEngine`
itself, not the action program):

- **`bloodthirsty`**: today, `SelectTarget`'s own branch picks the LOWEST-HP live enemy instead of
  the first-in-order one. `StubIntentSource.NearestEnemy` has no such branch and never will —
  reimplementing it there would duplicate trait logic the action program does not own.
- **`loyal` bodyguard interception**: today, after a target is chosen, `SelectTarget` checks for an
  adjacent `loyal` ally of the target and redirects to them instead.

**Resolution:** both stay exactly where they are, as `BattleEngine`-side wrapping around whatever
`ActionIntent.TargetKey` the intent source proposes — `bloodthirsty` as a pre-filter that reorders
`IBattleView.LiveActorKeys`'s enemy view **for that one attacker's decision only** (so
`StubIntentSource`'s own `NearestEnemy` naturally picks the lowest-HP one without knowing why), and
`loyal` as a post-check identical to today's, applied to `intent.TargetKey` before it reaches
`calculator.Compute`. Neither trait's behavior changes for a player — this module's "full
switch-over" scope call (§ owner decision, `action-map.md` §12) is about the SELECTION MECHANISM,
not about silently dropping shipped trait content nobody asked to remove.

### 6. Cooldowns: real ledger, inert today

A real `CooldownLedger` (already built, `Battle/Timeline/CooldownLedger.cs`) is constructed once
per battle on `BattleRunState`, and `_cooldowns.Start(actorKey, intent.Envelope, now)` is called
after every resolve — the same call `Timeline.ActionRunner` already makes. For the basic attack's
all-zero envelope (`Class = CooldownClass.None`), `CooldownLedger.Start` is a documented no-op
(`TrySlot` returns false for `Class.None`) — so this is genuinely inert today, and A19 does not need
to touch this specific wiring point when real non-zero-cooldown skills arrive.

## Tunables

None. This module wires existing mechanism; it authors no new balance number. (`A19` will introduce
real cost/cooldown VALUES on real skill rows — those are `data/tuning`-governed at that point, per
`tunables-ssot.md`, same as every other action row.)

## Numeric types

No new magnitudes. `EntityFacts.HpMilli` reuses the existing per-mille convention
(`CLAUDE.md` "Numeric overflow" — per-mille intermediates, `int` is safe well past any battle-scale
actor count).

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~ActionSelectionAdoption"
dotnet test tests\FusionRpg.Core.Tests
dotnet test tests\FusionRpg.Guard.Tests
.\scripts\guard-funnel-delta.ps1 ; .\scripts\guard-single-writer.ps1 ; .\scripts\guard-dal.ps1
```

## Project structure

```
src/FusionRpg.Core/Battle/BattleRunState.cs   (IBattleView adapter, loadout compilation, CooldownLedger)
src/FusionRpg.Core/Actions/BasicAttack.cs     (RunBasicAttackStep calls StubIntentSource, not SelectTarget)
src/FusionRpg.Core/Battle/BattleEngine.cs     (bloodthirsty/loyal wrapping around the intent's target)
tests/FusionRpg.Core.Tests/Battle/Adoption/ActionSelectionAdoptionTests.cs
```

## Testing strategy

**The gate is golden re-bless with a predicted delta, not byte-identity.** Unlike A5, this module
is a declared mover (`action-map.md` §12.2) — the owner chose full switch-over, so a change in
WHICH target gets picked (once `bloodthirsty`/`loyal` are correctly excluded from the comparison,
since those are proven unchanged in §5's own tests) is expected and acceptable, not a red flag.

- **`bloodthirsty` and `loyal` behavior unchanged** — direct fixtures reusing the existing trait
  test setups, proving both still pick the same target as before this module landed.
- **No-loadout actors resolve identically to `SourceOrder`** — an actor with empty
  `EquippedActionIds` (the single-basic-attack fallback) targets the same actor `SelectTarget`'s
  default branch would have, on a board with no `bloodthirsty`/`loyal` involved. This is the
  narrowest possible "did the wiring actually preserve the degenerate case" check — a real
  regression here would mean the intent-source integration itself is broken, not that the deliberate
  golden move is doing its job.
  * When this test contract fails to hold literal byte-identity against the pre-existing goldens
    (a real possibility, since `StubIntentSource`'s tie-break is ordinal-ptr-on-distance-tie while
    `SelectTarget`'s is pure list order — the two only provably coincide when no board exists AND no
    tie occurs at the SAME distance, which is every case here since there is no board at all): treat
    it as the parity-ladder tells you which stream/round it landed in, predict the delta, and it
    becomes part of this module's single re-bless (§ below), not a silent surprise.
- **A two-action loadout changes target OR action choice measurably** — the actual capability proof:
  construct two actors with different `EquippedActionIds` (both still basic-attack-shaped in terms
  of resolution, per this module's own scope, but with different `ActionTagPreference` ranks or
  different `MinRange`/`MaxRange`), same opponent, same seed, and assert the `ActionIntent`s differ
  in a way attributable to the loadout.
- **`ActionIntent.None` maps to `Break`, matching hazard 3** — an actor with a genuinely unusable
  loadout (e.g. every held action gated by a condition that never holds) breaks the round exactly
  like "no live enemy" does today.
- **Cooldown starts but stays inert for the all-zero envelope** — `CooldownLedger.IsReady` still
  returns true immediately after a basic attack resolves, proven directly.
- **Full suite + guards, no test edits outside the golden re-bless itself.**

## Boundaries

- **Always:** read board/roster facts only through `IBattleView`, never a direct `BattleRunState`
  read from inside `StubIntentSource` (already an architecture-tested invariant, T33 — this module
  must not weaken it); keep `bloodthirsty`/`loyal` engine-side; keep the `EngineBehavior` trait tail
  (berserker onward) exactly where `A5` already fixed it.
- **Ask first:** any change to `StubIntentSource`'s own decided contract (T34) — e.g. trying the
  next-nearest target, or reading `priority_band`. Those were explicit, documented decisions in a
  sealed module; changing them here is a T34 re-open, not an A17 detail.
- **Never:** invent a second condition/targeting language; give `EntityFacts` real `Row`/`Col`
  values without a real board (that's the board module's job); let the basic-attack-shaped
  resolution path in this module quietly start applying atom effects — that's explicitly A18.

## Success criteria

1. `BattleEngine` calls `StubIntentSource.TryDeclare` for every attacker, every turn — `SelectTarget`
   is deleted as the live targeting path (it may still exist as the wrapping `bloodthirsty`/`loyal`
   logic per §5, but it no longer chooses the primary target itself).
2. Two different synthetic loadouts on otherwise-identical actors produce measurably different
   `ActionIntent`s under the same seed.
3. `bloodthirsty` and `loyal` are proven unchanged, not merely assumed unchanged.
4. One combined re-bless, one predicted-delta writeup, `RulesetVersion` bumped once — not a silent
   golden drift discovered after the fact.
