# Spec: `timeline-dispatch`

Module `timeline-dispatch` in the [battle-tempo map](../battle-tempo-map.md), addressing map finding **D14**.
**Depends on `action-timing`, `commitment-binding`.**

**Read before editing:** [battle-turn-ideal.md](../battle-turn-ideal.md) §3–§4 ·
[battle-timeline-map.md](../battle-timeline-map.md) (T2/T5, "Scope boundary") ·
`tasks/battle-timeline-todo.md` Checkpoint A / B14 · `Battle/Timeline/ActionRunner.cs` ·
`Battle/BattleEngine.cs` `Resolve` (the round loop) · `Battle/Actions/BasicAttack.cs`.

**Status: BUILT and measured, 2026-09-05 (`TD1` spec, `TD2`/`TD3` implementation).** Every section below
is built: the flag (§2.1), the accessor (§2.3), the declare/apply split (§2.2), **and — revised from
this spec's own original design — a scoped dispatch branch (§2.5) using a LOCAL, per-round-phase
`EventQueue` rather than the shared `roundQueue`, which sidesteps Hazard A (§2.4) entirely rather than
fixing it in place.** `tools/TimelineDispatchProbe/` proves, through the REAL `BattleEngine.Resolve`,
on synthetic never-catalogued profiles only: `W` moves win rate (+14.17pp, W=1 vs W=4) and `Commitment`
moves average rounds-to-win (EarlyBound 6.696 vs LateBound 6.025) — the exact two numbers
`battle-tempo-todo.md` Checkpoint B named as unmet, now measured non-zero for the first time in this
program's history. **No entry in `BattleModeProfileCatalog` sets the flag** — every shipped profile is
provably byte-identical (every existing `battle-tempo` probe reproduces its already-recorded numbers
exactly after this landed). See §2.5 for the revised design and §9 for one additional finding
(`BasicAttackEnvelope.Commitment` was hardcoded, making `DefaultCommitment` permanently unreachable
regardless of dispatch completeness — fixed alongside this module, confirmed inert for the atomic path).

---

## 1. Objective

**Wire `ActionRunner` — the DES-kernel action resolver that already exists, is already tested, and
already has zero production callers — into `BattleEngine.Resolve`'s live per-actor dispatch, for
exactly one profile, with zero observable change to any other profile.**

This is the fix map finding D14 named and declined to attempt inline: `BattleEngine.Resolve`'s round
loop transitions every actor `Ready → Committed → Resolving` in the same loop iteration and calls
`RunBasicAttackStep` synchronously — `ActionEnvelope.WindupTicks`/`RecoveryTicks`/`TimeCostTicks` are
read nowhere in that path. `W`, `Commitment`, and `AdvancePolicy` all measure exactly **0.00%** in the
staged sweep (`MEAS`, `battle-tempo-todo.md`) because of this one structural fact, not because of
anything `action-timing`/`commitment-binding`/`reaction-lane` built incorrectly.

**This is not a new discovery this module is inventing a reason to exist for.** Two prior, careful,
explicitly-scoped decisions already named exactly this boundary and chose not to cross it:

- `battle-timeline-map.md` T5 `kernel-adoption`'s own **Checkpoint A** evidence line: *"Zero
  production code rewired... Phase 2 (`BattleEngine` adoption, B13–B15, the T5 gate) is the next
  battle-timeline push and is explicitly **not** part of this checkpoint."*
- `tasks/battle-timeline-todo.md` **B14**'s own scope note: *"the round skeleton's 10 steps stay a
  synchronous, procedural sequence... NOT routed through `ActorTurnMachine`/`ActionRunner`'s per-actor
  envelope. Both were deliberate scope calls made before writing any code."*

So this module is the third pass at the same boundary, not the first — the first two each proved a
piece (the kernel's own capability in isolation; the round skeleton on the shared clock) and each
explicitly deferred this exact wire. That pattern is itself evidence this needs a spec before code.

### 1.1 What this module does NOT do

- **Does not flip any shipped profile onto the new path.** `hybrid-atb` stays on today's atomic
  dispatch after this module lands. Turning it on is `LAND1`/`LAND2`'s job (`battle-tempo-todo.md`
  Phase 2) — a `RulesetVersion` bump, a re-bless, a win-rate sweep, and **owner sign-off**
  (`combat-unification-plan.md:76` precedent). This module does not shorten that gate; it makes the
  thing being gated buildable and reviewable.
- **Does not touch damage math.** `battle-turn-ideal.md` §3: *"`Resolving` is atomic... it goes
  resolver → `DamageApplyPipeline` → shield gate → sink. That layer is finished and unified; nothing
  in this document changes it."* This module changes only **when** that call happens, never what it
  computes.
- **Does not restructure `classic-round`'s or `galaxy-sync`'s dispatch.** Both stay on the exact code
  path they use today, unconditionally.

---

## 2. Design

### 2.1 The capability flag — declared, defaulting false, never flipped by this module

**Built 2026-09-05.**

Add one field to `BattleModeProfile` (`Battle/Timeline/BattleModeProfile.cs`), following the exact
precedent `OrdersBySpeed` and `ForecastExactness` already set — *"a declared field, not a computed
branch"* (`ModeProfileArchitectureTests`' own rule, cited verbatim in both existing doc comments):

```csharp
/// <summary>battle-tempo `timeline-dispatch` (D14): whether Resolve dispatches this profile's
/// actors through ActionRunner (commit → wind-up → resolve → recovery, honouring WindupTicks) instead
/// of the atomic Ready→Committed→Resolving path every profile uses today. Declared, never a branch on
/// ProfileId or AdvancePolicyKind — the same discipline OrdersBySpeed already established. Defaults
/// false: every shipped profile (including hybrid-atb) stays on the atomic path until LAND1/LAND2
/// (battle-tempo-todo.md Phase 2) deliberately flips it, with its own re-bless and sign-off.</summary>
public bool UsesTimelineDispatch { get; init; }
```

`BattleModeProfileCatalog` is **not edited by this module** — no `Build(...)` call site sets this to
`true`. That is what makes the module additive: the flag exists and is exercised by this module's own
tests, but no shipped battle ever reaches the branch that reads it.

### 2.2 The declare/apply split — `BasicAttack.cs`, behavior-preserving by construction

**Built 2026-09-05, proven a no-op** (§6's own verification step, executed: `MeasProbe`'s output
diffed byte-for-byte before/after, plus every other existing `battle-tempo` probe reproducing its
already-recorded PASS results).

`RunBasicAttackStep` (`Battle/Actions/BasicAttack.cs:73`) does five things in one synchronous call:
active/CC-lock check → `IIntentSource.TryDeclare` → bodyguard redirect → `OnActivate` fire →
`calculator.Compute` → `OnDamageDealt` fire → cooldown arm. The atomic path needs all of that in one
call, unchanged. The timeline path needs the **first half** (who, with what envelope) at commit time
and the **second half** (the hit itself) at resolve time — which may be a different round.

Split into two methods with the exact same statements, in the exact same order, just returning at the
midpoint instead of continuing:

```csharp
// Front half: active/CC-lock/declare/redirect/OnActivate/trace. Returns early (Continue/Break) on the
// same conditions RunBasicAttackStep already returns early on.
static (AttackStepOutcome Outcome, ActorState? Target, ActionEnvelope Envelope) DeclareBasicAttack(
    ActorState attacker, BattleRunState state, DateTimeOffset now, long nowTick,
    BattleTrace? trace, int round, IIntentSource? intentSource = null);

// Back half: calculator.Compute/OnDamageDealt/cooldown arm, against an ALREADY-RESOLVED target.
static AttackStep ApplyBasicAttack(
    ActorState attacker, ActorState target, ActionEnvelope envelope, BattleRunState state,
    DateTimeOffset now, long nowTick, OverlayCombatCalculator calculator, ICombatRng critRng);

// RunBasicAttackStep becomes a two-line wrapper calling both in sequence — the atomic path's call
// site (BattleEngine.cs:414) needs ZERO changes.
static AttackStep RunBasicAttackStep(...) {
    var (outcome, target, envelope) = DeclareBasicAttack(...);
    return outcome == AttackStepOutcome.Proceed ? ApplyBasicAttack(attacker, target!, envelope, ...) : new AttackStep(outcome, target, 0);
}
```

**Why re-declaring is unnecessary and re-selection is not duplicated.** `ApplyBasicAttack` is called
with the target `ActionRunner.OnResolveDue` has already re-resolved (via `commitment-binding`'s
`reselectTarget` delegate, D6/D11 — reads the *already-compiled* `BasicAttackCompiled.Targeting`, no
second selection seam). The timeline path never calls `DeclareBasicAttack` a second time; it commits
once, and lets `ActionRunner`'s own commitment-binding logic own what happens if the target dies
mid-wind-up. `EarlyBound` fizzles unconditionally; `LateBound`/`EarlyBoundWithFallback` re-select or
fizzle — exactly the existing, already-tested `ActionRunner.OnResolveDue` behavior, unmodified.

**Verification this split changes nothing on the atomic path:** re-run every existing probe this
program already built against real compiled code (`MeasProbe`, `CommitmentProbe`, `ReactionLaneProbe`)
after the split and confirm byte-identical numbers to the values already recorded in
`battle-tempo-todo.md`'s evidence blocks. A pure refactor: if any number moves, the split introduced a
behavior change and must be reverted before anything else in this spec proceeds.

### 2.3 `ActionRunner.CurrentTarget` — one additive, backward-compatible accessor

`ActionRunner`'s own `ActionRun.TargetKey` is private and can change under re-selection
(`ActionRunner.cs:376`). The caller applying the hit needs to know the *current* target after
`OnResolveDue` returns `Resolved`. Add:

```csharp
/// <summary>The target this actor's in-flight action currently resolves against — reflects any
/// re-selection commitment-binding performed. Null when the actor holds no active run.</summary>
public string? CurrentTarget(string actorKey) =>
    _runs.TryGetValue(actorKey, out var run) && run.Active ? run.TargetKey : null;
```

Purely additive (a new public method, no signature change to anything existing); every current caller
of `ActionRunner` is unaffected. **Built and probed 2026-09-05** (`tools/TimelineDispatchProbe/`).

⚠️ **A real timing hazard, caught by the probe's own falsifier, not predicted.** The first probe draft
read `CurrentTarget` *after* draining both the `Resolve` and `Recovery` events for an action, and the
assertion failed: `OnRecoveryDue` sets `run.Active = false`
(`ActionRunner.cs:266`), so by the time `Recovery` has also fired, `CurrentTarget` correctly reports
`null` — reading it that late is reading a run that has already ended. **The correct sequence (now
proven, not assumed) is: read `CurrentTarget` immediately after `OnResolveDue` returns `Resolved`,
before yielding control back to whatever drains `Recovery`.** §2.5's dispatch branch must follow this
exact order.

### 2.4 The two hazards originally found, and why the shipped design sidesteps both rather than fixing them in place

Both were found by tracing `BattleEngine.Resolve`'s real body, not predicted — and both drove the
revised §2.5 design actually built, rather than being patched individually.

**Hazard A — event-`Kind` collision, if a shared queue were used.** `BattleEngine.Resolve` schedules
its own round-boundary and status-pulse events onto `roundQueue` (a `Timeline.EventQueue`) using
**local** `int` constants: `RoundEventKind = 0`, `StatusPulseEventKind = 1` (`BattleEngine.cs`). These
numerically alias `Timeline.TimelineEventKind.Readiness = 0` and `.Resolve = 1` (`ActionRunner.cs:10–
11`). Scheduling a real `TimelineEventKind.Resolve` event onto the *same* queue would be silently
misread by the existing `if (ev.Kind == StatusPulseEventKind)` check as a status pulse. **Resolved by
construction, not by an offset fix:** §2.5's dispatch branch gives its `ActionRunner` a **LOCAL**
`EventQueue`, scoped to one round's action phase, never `roundQueue` — the two numbering spaces never
share a queue, so the collision cannot occur. Simpler and more robust than a `+10` offset scheme, and
it needed no falsifier of its own once the queues were separated (there is nothing left to collide).

**Hazard B — `ActionSlots`/`ActionRunner` lifetime.** `BattleEngine.Resolve` constructs `slots = new
Timeline.ActionSlots(...)` fresh inside the round loop today, correct only because resolution is
atomic (`ActionSlots`' own doc: *"W only binds when actions have wind-up... a battle is already
serialised regardless of W"*). **Narrowed to a defended, verified assumption rather than solved by a
per-battle restructuring:** the built design (`RunTimelineActionPhase`,
`src/FusionRpg.Core/Actions/TimelineDispatch.cs`) constructs `ActionSlots`/`ActionRunner`/the local
`EventQueue` **once per round's action phase** and requires every committed action's full commit →
wind-up → resolve → recovery lifecycle to complete **within that same round** — true today by
configuration (`basicAttack.windupTicks=150` + `recoveryTicks=50` = 200 ticks, vs a 1000ms
`roundDurationMs`) and enforced by a structural `MaxLocalIterations` guard that **throws** rather than
silently carrying state into the next round if it is ever exceeded (matching the exact "throw rather
than silently wrap" ethos `BattleEngine.Resolve`'s own `maxLoopIterations` guard already uses). This is
the scope boundary a future action with a longer authored lifecycle would need revisited — named
explicitly, not silently assumed away.

**Why `NextEventAdvance` matters here even though the queue is local.** `hybrid-atb` declares
`AdvancePolicyKind.FixedIncrement`, but `Resolve` always drives its OUTER round clock via
`Timeline.NextEventAdvance` regardless of profile — documented as deliberate (a batch resolver has no
per-frame ticks for `FixedIncrementAdvance` to consume). The LOCAL action-phase loop reuses the same
`NextEventAdvance`/`SimulationClock` primitives on its OWN queue/clock pair, which is what lets it
"jump straight to the next scheduled Resolve/Recovery" the same way the outer loop jumps to the next
round — the same primitive, a second, smaller instance of it, not a new mechanism.

### 2.5 The dispatch branch — built as `RunTimelineActionPhase`, not inline in `Resolve`

**Built and measured 2026-09-05.** Rather than interleaving Resolve/Recovery events into
`BattleEngine.Resolve`'s existing `do { } while (anyActed && !phaseBroken)` pass structure (this
spec's original plan, and the source of both hazards above), the actual implementation replaces that
whole pass structure, for this profile only, with a self-contained local discrete-event loop —
`static void RunTimelineActionPhase(...)` in `src/FusionRpg.Core/Actions/TimelineDispatch.cs` (the
same "declares part of `BattleEngine`, lives under `Core/Actions/`" seam `BasicAttack.cs` already
established). `BattleEngine.cs`'s round loop gates on the flag with a plain `if`/`else`: the `else`
branch is the pre-existing do-while, byte-for-byte; the `if` branch calls `RunTimelineActionPhase` and
nothing else changes.

**What the local loop does, each round, only when `UsesTimelineDispatch` is true:**

1. Offer every `Charging` actor with economy budget a commit attempt, in initiative `order` — mirrors
   the atomic path's own `economy.TryAcquire` → `Ready` → declare → `TryCommit` sequence, but through
   `ActionRunner.TryCommit` instead of an immediate synchronous resolve.
2. `DeclareBasicAttack` resolves target+envelope exactly as the atomic path does (§2.2); a `Continue`/
   `Break` outcome reverts `Ready → Charging` without a wasted state cycle (a deliberate, documented
   deviation from the atomic path's own "cycle through all five states regardless of outcome" shape —
   this is a genuinely new, event-driven mechanism, not required to bit-for-bit replicate the atomic
   path's degenerate-case transitions, only its observable outcomes).
3. Advance the LOCAL clock to the next due `Resolve`/`Recovery`; on `Resolved`, read
   `runner.CurrentTarget` (§2.3, in the exact sequence its own falsifier proved correct) and call
   `ApplyBasicAttack` against the resolved target, then `state.DispatchHit` — identical tail to the
   atomic path.
4. Loop until nothing is pending and nothing new committed; a structural iteration guard throws rather
   than silently misbehaving if that assumption (§2.4 Hazard B) is ever violated.

**Re-selection reuses the SAME `IIntentSource` the commit itself used**, rather than routing through
`BasicAttackCompiled.Targeting`'s `CompiledTargetSpec` (the wire-format contract D6/D11 named as the
long-term seam for a general action, compiled for a *different* consumer — confirmed by reading
`TargetSpecCompiler.cs`, it targets the "shipped resolver" wire DTOs, not `BattleEngine`'s own
`IBattleView`/`ActorState` model). For the basic attack specifically, `StubIntentSource.TryDeclare`
already reads live battle state on every call, so calling it again at resolve time **is** a correct
re-selection. A real second seam for a general action's authored targeting spec remains open work, out
of this module's scope (the basic attack is still the only action any live battle dispatches, per D14).

**A real, previously-undiscovered finding this build turned up:** `BasicAttack.cs`'s own
`BasicAttackEnvelope.Commitment` was hardcoded to `Commitment.LateBound`, not left `null`
("inherit the profile default", D6). `ActionRunner.TryCommit`'s precedence (envelope wins over
profile) meant `BattleModeProfile.DefaultCommitment` was **permanently unreachable for the basic
attack — the only action any live battle dispatches — regardless of how complete this module's own
dispatch branch was.** This was NOT this module's bug; it predates `timeline-dispatch` and was never
exercised because `ActionRunner` had no live caller before this module. Fixed alongside this module
(the line simply removed, letting `ActionEnvelope.NoOp`'s own `null` default apply) and confirmed
inert for the atomic path — `RunBasicAttackStep`/`DeclareBasicAttack` never read `envelope.Commitment`
at all (only `ActionRunner` does, and `ActionRunner` had zero production callers until this module).
See §9.

---

## 3. Commands

Same as every other `battle-tempo` module — no new build/test/lint commands. Verification is
probe-based (§6): `Core.Tests` stays blocked by pre-existing, unrelated WIP in other streams (see
`PU1`'s evidence in `battle-tempo-todo.md`), so a standalone `tools/TimelineDispatchProbe/` console app
bootstraps real production tuning and exercises real compiled code, matching every other module this
program built.

---

## 4. Project structure

| Path | Change | Status |
|---|---|---|
| `Battle/Timeline/BattleModeProfile.cs` | +1 field, `UsesTimelineDispatch` (§2.1) | ✅ built |
| `Battle/Timeline/ActionRunner.cs` | +1 method, `CurrentTarget` (§2.3) | ✅ built |
| `Battle/Actions/BasicAttack.cs` | `RunBasicAttackStep` split into `DeclareBasicAttack` + `ApplyBasicAttack` (§2.2); `BasicAttackEnvelope.Commitment` unhardcoded (§9) | ✅ built |
| `Battle/BattleRunState.cs` | +1 accessor, `BasicAttackEnvelopeCompiled` (narrow exposure of one field the local action phase needs) | ✅ built |
| `Battle/BattleEngine.cs` | New `if`/`else` branch in the per-attacker loop, gated on the flag; the `else` is the pre-existing do-while, byte-for-byte | ✅ built |
| `Actions/TimelineDispatch.cs` (new) | `RunTimelineActionPhase` — the local discrete-event action phase (§2.5) | ✅ built |
| `tools/TimelineDispatchProbe/` | Standalone probe (net6.0, `ProjectReference` to `FusionRpg.Core.csproj` only) — API-level checks plus an end-to-end sweep through real `BattleEngine.Resolve` | ✅ built, 15/15 PASS |

No new production test project files — this module's verification is probe-only throughout, matching
how every prior `battle-tempo` module was proven while `Core.Tests` stays blocked by unrelated,
pre-existing WIP in other streams (see `PU1`'s own evidence in `battle-tempo-todo.md`).

---

## 5. Code style

Match the two files' existing conventions exactly: `BasicAttack.cs`'s doc-comment density (every
non-obvious branch gets a one-line "why", citing the deciding spec/decision), `ActionRunner.cs`'s
`readonly record struct`/`sealed class` shapes for new value types, and `BattleEngine.cs`'s existing
practice of naming which hazard a check defends against inline (see hazard 1–7 in B14's own comments).

---

## 6. Testing strategy

All built and executed 2026-09-05, via `tools/TimelineDispatchProbe/` (15/15 PASS) since `Core.Tests`
stays blocked by unrelated, pre-existing WIP in other streams (`PU1`'s own evidence).

1. ✅ **The split is a no-op, proven by re-running existing evidence.** `MeasProbe` captured before the
   split, re-run after, diffed byte-for-byte identical; `CommitmentProbe`/`ReactionLaneProbe`/
   `TurnOrderProbe`/`ForecastProbe`/`ActionTimingProbe`/`TempoProbe`/`PoiseProbe`/`TraceOptInProbe`/
   `ContractParityProbe` all reproduce their already-recorded PASS results exactly.
2. ✅ **`ActionRunner.CurrentTarget`.** Commits, forces a `LateBound` re-selection, asserts
   `CurrentTarget` reflects the new target immediately after `OnResolveDue`. The falsifier fired for
   real during development (not merely available): a first draft read `CurrentTarget` after `Recovery`
   had already cleared the run and the assertion correctly reddened, which is what fixed the read
   ordering documented in §2.3.
3. ✅ **Hazard A resolved by construction.** No shared-queue falsifier is needed or applicable — the
   built design gives the local action phase its own `EventQueue`, so `RoundEventKind`/
   `StatusPulseEventKind` and `TimelineEventKind` never share a numbering space to collide in.
4. ✅ **Hazard B's scope-limiting guard.** `TheDispatchBranchRunsToCompletionWithoutThrowing` proves the
   ordinary case never trips `MaxLocalIterations`; the guard itself throws loudly (by construction, not
   separately probed) if the one-round-lifecycle assumption is ever violated, matching
   `BattleEngine.Resolve`'s own `maxLoopIterations` precedent.
5. ✅ **The full mechanism, end to end, through the REAL `BattleEngine.Resolve`, on synthetic profiles
   only.** `WIsMeasurablyNonZeroUnderTimelineDispatch`: W=1 vs W=4 win rate, +14.17 percentage points.
   `CommitmentIsMeasurablyNonZeroUnderTimelineDispatch`: EarlyBound vs LateBound average rounds-to-win,
   6.696 vs 6.025. Both are the first non-zero measurements of these two axes in this program's
   history, closing `battle-tempo-todo.md` Checkpoint B's own previously-unmet line.
6. ✅ **Every shipped profile, unchanged — with an explicit falsifier, not just a rerun.**
   `FalsifierWDeltaIsZeroWhenTheFlagIsOff` and `FalsifierCommitmentDeltaIsZeroWhenTheFlagIsOff` apply
   the exact same axis changes to `UsesTimelineDispatch = false` profiles and assert the delta is
   exactly zero — proving the non-zero deltas above come from timeline-dispatch actually mattering,
   not from an unrelated effect of the `with` expressions themselves.
7. ✅ Standard: `audit-overflow.py`/`audit-magic-numbers.py` clean on every touched file (`BasicAttack.cs`,
   `ActionRunner.cs`, `BattleModeProfile.cs`, `BattleRunState.cs`, `BattleEngine.cs`,
   `Actions/TimelineDispatch.cs`); all four boundary guards green; `dotnet build` clean on Core and
   Server.

---

## 7. Boundaries

- **Always:** keep the flag false for every entry in `BattleModeProfileCatalog`; keep `RunBasicAttackStep`'s
  wrapper behavior-identical to today; probe-verify every existing recorded number before and after any
  future change to these files.
- **Never:** flip `UsesTimelineDispatch` to `true` for `hybrid-atb` (or any shipped profile) — that is
  `LAND1`/`LAND2`'s decision, gated by their own re-bless/sweep/sign-off
  (`combat-unification-plan.md:76` precedent, `LAND2`'s explicit "owner-only, do not self-approve").
  Never bump `RulesetVersion` for this module. **A green probe here is not license to land** — landing
  is a separate, owner-only gate this module does not shorten.
- **Scope boundary, stated so a later session does not silently widen it:** `RunTimelineActionPhase`
  dispatches the basic attack only (the only action any live battle dispatches today, per D14), assumes
  every committed action's lifecycle fits inside one round (defended by a throwing guard, not silently
  assumed), and re-selects via `IIntentSource` rather than `BasicAttackCompiled.Targeting`'s
  `CompiledTargetSpec`. A future real action-authoring path, or a lifecycle spanning multiple rounds,
  needs this module revisited — not silently stretched to cover it.

---

## 8. Success criteria

1. ✅ `UsesTimelineDispatch` exists, defaults `false`, and is read only inside `RunTimelineActionPhase`'s
   own call site — no entry in `BattleModeProfileCatalog` sets it.
2. ✅ Every existing `battle-tempo` probe reproduces its already-recorded numbers exactly with this
   module's code present (byte-for-byte diff on `MeasProbe`; PASS-for-PASS on the other nine).
3. ✅ A synthetic, never-shipped test profile with the flag `true` measures `W` (+14.17pp win rate) and
   `Commitment` (−0.671 rounds-to-win, EarlyBound vs LateBound) both **non-zero** — closing
   `battle-tempo-todo.md` Checkpoint B's own previously-unmet line.
4. ✅ Both hazards are resolved (A by construction, B by a defended, throwing scope guard) rather than
   patched in place — no red-then-green falsifier was needed for Hazard A since the collision it
   described cannot occur in the built design.
5. ✅ `audit-overflow.py`/`audit-magic-numbers.py` clean on every touched file.
6. ✅ No entry in `BattleModeProfileCatalog` sets the flag. No `RulesetVersion` change. No golden moves
   (confirmed, not assumed: `MeasProbe`'s byte-for-byte diff).

---

## 9. A pre-existing defect this build surfaced: `BasicAttackEnvelope.Commitment` was hardcoded

`BasicAttack.cs`'s `BasicAttackEnvelope` set `Commitment = Commitment.LateBound` explicitly, rather than
leaving it `null` ("inherit the active profile's `DefaultCommitment`", the precedence D6 established).
`ActionRunner.TryCommit`'s own rule — `envelope.Commitment ?? _defaultCommitment`, envelope wins when
set — meant `BattleModeProfile.DefaultCommitment` was **permanently unreachable for the basic attack,
the only action any live battle dispatches, regardless of how complete any future dispatch branch
was.** Measured directly: `RunTimelineActionPhase` produced *identical* results (6.025 rounds either
way) for `DefaultCommitment = EarlyBound` vs `LateBound` until this was fixed, even after tracing and
confirming the fizzle/re-select branch itself fired correctly.

**Not a defect in this module** — it predates `timeline-dispatch` (CB1 made `Commitment` nullable
without auditing every existing envelope's explicit value) and was never exercised because
`ActionRunner` had zero production callers before this module existed. **Confirmed inert for the
atomic path** before fixing: `RunBasicAttackStep`/`DeclareBasicAttack` never read `envelope.Commitment`
at all (only `ActionRunner` does), so removing the hardcoded value changes nothing observable until an
`ActionRunner` caller exists — which, until this module, there was none. Fixed by deleting the line and
letting `ActionEnvelope.NoOp`'s own `null` default apply; `MeasProbe`'s byte-for-byte diff confirms the
atomic path is unaffected.

---

## 10. Golden movement

**None, by construction.** Every existing profile's dispatch is byte-for-byte unchanged (the flag
gates a branch nothing shipped ever takes). If any golden moves after this module lands, that is a
defect in the split (§2.2) or the branch gating (§2.1), not an accepted cost — unlike every other
`battle-tempo` module, this one carries **zero** predicted golden movement, because it is additive
capability, not a shipped behavior change. The behavior change (flipping the flag for `hybrid-atb`) is
`LAND1`/`LAND2`'s cost to predict and pay, separately, later, with its own sign-off.
