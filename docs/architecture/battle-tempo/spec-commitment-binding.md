# Spec: `commitment-binding`

Module `commitment-binding` in the [battle-tempo map](../battle-tempo-map.md).
**Depends on `action-timing`** — it is unbuildable and untestable until wind-up exists.

**Read before editing:** [battle-turn-ideal.md](../battle-turn-ideal.md) §2 (knob 3) ·
[battle/audit-2026-08-21.md](../battle/audit-2026-08-21.md) D6 · [combat-damage-ssot.md](../combat-damage-ssot.md).

---

## 1. Objective

**Make the third knob real: honour `Commitment` when a target dies mid-wind-up.**

The ideal names three knobs that distinguish every battle mode. Two now do work. The third —
*"is an actor's target chosen when it becomes ready, or re-read the instant the action lands?"* — is
declared on every profile row and **read nowhere**. `hybrid-atb` carries
`EarlyBoundWithFallback` and `B34` measured its contribution at exactly **0.00 %**.

The reason is not a missing branch; it is a missing *window*. With zero wind-up, commit and resolve are
the same instant, so there is nothing that can change between them. `action-timing` creates the window;
this module decides what happens inside it.

### 1.1 The question, concretely

An actor commits a heavy attack at its target. During the wind-up, the target dies to someone else.

| Commitment | Behaviour |
|---|---|
| `LateBound` | Re-read the target at resolve. It is gone, so the action re-targets by the same selection rule that chose the original. |
| `EarlyBound` | The target was locked at commit. The action **fizzles** — the wind-up is spent, nothing lands. |
| `EarlyBoundWithFallback` | Locked, but a dead target falls back to re-selection rather than fizzling. |

⭐ **This is the mechanic that makes focus-fire a decision.** With late binding, over-committing on one
target costs nothing. With early binding, it is a real risk — which is what gives a slow, telegraphed
action its downside.

---

## 2. Design

### 2.1 Where the branch lives

The profile's `DefaultCommitment` is **declared row data**, and the engine reads it at the single point
where a committed action resolves. It must **not** become a branch on the profile *id* anywhere —
`ModeProfileArchitectureTests` bans that, and the lesson is already recorded twice in this codebase
(`ForecastExactness`, `OrdersBySpeed`): adding a mode adds a row, never a branch.

⚠️ **An `ActionEnvelope` may override the profile default.** The envelope already carries its own
`Commitment` field; a specific action can be locked in a late-bound mode. Resolution order is
**envelope first, profile default second** — the same precedence the envelope's other fields use.

### 2.2 Re-selection must reuse the original rule

A re-targeting action must pick its new target with the **same selection the intent used**, not a fresh
ad-hoc choice. Two different selectors would make an action's behaviour depend on *how* it lost its
target, which is unexplainable to a player and a determinism hazard.

### 2.2a ⛔ D6 — "reuse the original rule" has no seam today

**Review finding, 2026-09-04.** The first draft said re-selection must *"reuse the original rule"* and
left it there. **The seam does not exist.**

`BasicAttack` records it plainly: *"`SelectTarget` is gone as the live targeting path — the intent
source decides who"*, and `BattleEngine` then does a bare `state.ByKey[intent.TargetKey!]`. But
`IIntentSource.TryDeclare` returns an **action and a target together** — so calling it again at resolve
would re-choose the *action* as well, which is not re-targeting, it is a different decision.

⭐ **The fix needs no new interface, because the rule is already data.** `ActionTargetSpec` is reified —
`BasicAttack.BasicAttackTargeting` is `Mode = Single`, `Ordering = SourceOrder`. So re-selection resolves
**that spec** engine-side at resolve time, against the live actor set:

- Same spec, same ordering, same tie-breaks — so re-targeting cannot pick differently from how the
  original choice was made.
- ⭐ **Verification round 2: the compiled spec is already on the run state, so there is nothing to
  build.** `BattleRunState.BasicAttackCompiled` is constructed with
  `Targeting: TargetSpecCompiler.Compile(BasicAttackTargeting)` — the engine already holds a
  **compiled** `ActionTargetSpec` for the action it is resolving. Re-selection reads that field; it does
  not compile, look up, or re-derive anything at resolve time.
- ⛔ **Do NOT add `IIntentSource.ReselectTarget`.** A second seam invites a second selection policy, and
  two selectors would make behaviour depend on *how* a target was lost — unexplainable to a player and a
  determinism hazard.

⚠️ **`state.ByKey[…]` is a bare indexer.** Once a target can vanish between commit and resolve, that
lookup is a live throw risk and must become an explicit miss-check, not an exception path.

### 2.3 ⛔ Determinism — the trap this module must not fall into

Re-selection happens **at resolve**, which is a different tick from commit. Any RNG it consumes shifts
every downstream draw.

- Re-selection must consume **the same number of draws** whether or not it re-targets, or it must
  consume **none**.
- The `initiative`/`crit`/`essence`/`status` one-stream-per-system rule holds; re-selection does not get
  a new stream without a reviewed decision.
- **This is the `B39` lesson applied in advance**: there, hoisting the initiative draw out of the sort
  key kept both orderings consuming the RNG identically, which is the only reason the delta stayed
  attributable to ordering alone.

### 2.4 Death is `Downed`, not `Dead`

The FSM ships `Downed` — *"HP ≤ 0 but still present, targetable, and revivable. Death is a decision, not
an edge."* So "the target died" is **not** a single condition:

- `Downed` — still targetable. An action may legitimately land on it (an execute, a heal, a revive).
- `Dead` / `Withdrawn` — terminal, `TurnTransitions.IsTerminal`.

⛔ **Re-targeting must trigger on terminality, not on HP ≤ 0.** Treating `Downed` as gone would silently
break revive and execute interactions, and would do so only under wind-up — a bug that appears with this
module and looks like it came from somewhere else.

---

## 3. Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Commitment|FullyQualifiedName~Battle.Timeline"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Golden|FullyQualifiedName~HybridAtbSweep"
```

---

## 4. Project structure

```
src/FusionRpg.Core/Battle/BattleEngine.cs                  the resolve-time branch
src/FusionRpg.Core/Battle/Timeline/ActionEnvelope.cs       Commitment already declared — no change
tests/FusionRpg.Core.Tests/Battle/Timeline/CommitmentBindingTests.cs   NEW
```

---

## 5. Code style

```csharp
// Envelope first, profile default second -- the same precedence every other envelope field uses.
var commitment = intent.Envelope?.Commitment ?? activeProfile.DefaultCommitment;

// Terminality, NOT hp <= 0: `Downed` is still targetable by design (TurnState.cs), so an execute or a
// revive must still land. Re-targeting a Downed actor would break both, and only under wind-up.
var lost = TurnTransitions.IsTerminal(state.MachineFor(targetKey).State);
```

---

## 6. Testing strategy

1. ⭐ **`Commitment` stops measuring 0.00 %** in `HybridAtbSweepTests`' staged attribution — the
   module's headline acceptance, and the direct answer to `B34`'s finding.
2. **All three values behave differently on the same seed and setup.** `EarlyBound` fizzles,
   `LateBound` re-targets, `EarlyBoundWithFallback` re-targets — proven by contrast in one file, the
   shape `ModeProfileCapabilityTests` already uses for `W`.
3. **A `Downed` target is still hit** — the execute/revive guard. Falsifier: switching the check to
   `hp <= 0` must redden it.
4. **Determinism:** the same battle re-resolves byte-identically, and a re-target consumes the same RNG
   draws as a non-re-target (assert the `initiative`/`crit` draw sequences match).
5. **The envelope overrides the profile** — a locked action in a late-bound profile stays locked.
6. **No branch on profile id** — `ModeProfileArchitectureTests` stays green.

---

## 7. Boundaries

- **Always:** read the declared row/envelope value; re-select with the original rule; branch on
  terminality.
- **Ask first:** giving re-selection its own RNG stream; changing `hybrid-atb`'s default away from
  `EarlyBoundWithFallback`.
- **Never:** branch on a profile id or `AdvancePolicyKind`; treat `Downed` as gone; let re-selection
  change the RNG draw count; add a second target-selection seam (D6).

---

## 8. Success criteria

1. `Commitment` measurably affects outcomes; the sweep no longer reports 0.00 % for it.
2. All three values are distinguishable on identical input.
3. `Downed` targets remain targetable.
4. Byte-identical replay holds, with draw-sequence parity asserted.

---

## 9. Golden movement

**Should move nothing on its own.** `action-timing` lands the re-bless; this module changes behaviour
only in the window that module opened, and only when a target becomes terminal mid-wind-up — which the
golden fixtures may never do.

⚠️ **Measure rather than assume.** Three predicted movers in `battle-timeline` each moved nothing, and
one non-obvious mover (`B39`) also moved nothing for a reason nobody predicted. Run the goldens and
report what actually happened.
