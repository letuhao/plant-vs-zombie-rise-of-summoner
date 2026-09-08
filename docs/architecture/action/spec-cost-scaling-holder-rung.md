# Spec: cost-scaling-holder-rung (A23)

Module **A23** in the [action map](../action-map.md) §14. Depends on A11 (`unlock-ladder`, built),
A19 (`action-costs-cooldowns-adoption`, built). **No new mechanism** — a wiring correction to
already-shipped code, found by reading `spec-rung-semantics.md` §3.1 against `CostLedger`'s real
construction site while designing A21, not anticipated when A19 was built.

> **Read [spec-rung-semantics.md](../action-corpus/spec-rung-semantics.md) §3.1 before this spec.**
> It is the authoritative, already-decided contract this module makes real; this spec invents no new
> rung semantics, it wires the existing ones correctly.

## Objective — the exact defect, traced to one line

`spec-rung-semantics.md` §3.1 (drafted 2026-09-03) settles a question with two different correct
answers depending on which system asks it:

> **`Rung` (authored)** — what the *content* was written for. Fixes the structure budget, because
> structure is a property of the action, not of who holds it.
> **`effectiveRung` (derived)** — `min(earnCount, cap)` for this holder. **Fixes magnitude and cost**,
> because those scale with the holder's progression.
> `StructureBudgetGuard` reading the authored rung is therefore CORRECT.

`StructureBudgetGuard.Check` reads `row.Rung` (authored) — correct, per the contract above, and this
spec changes nothing about it. `CostLedger` is the OTHER reader, and it reads the WRONG one:

```csharp
// BattleRunState.cs:493 (A19, T56.1 — this program's own immediately-prior session)
rungOf: actionId => actionCatalog?.Get(actionId)?.Rung ?? 0,
```

This feeds `CompiledAction.Rung` — the **authored** value — into `CostLedger.ScaledAmount`'s rung
parameter, which drives the `RungPolicy.Table`-resolved `CostMulti`/`CdMulti` lookup
(`CostLedger.cs:150-155`). Per the contract above, cost/cooldown scaling must read the **holder's**
`effectiveRung` instead — `UnlockLadder.EffectiveRung(earnCount, tuning)`, per actor, per action, via
that actor's own `UnlockState.Held` list (`HeldUnlock.EarnCountAtAcceptance`). Today, every holder of
the same action pays the same scaled cost regardless of how much they have actually earned it —
exactly the property `effectiveRung` exists to prevent.

**Why this was never caught building A19**: `CostLedger` has never had a real caller with real holder
state. `ActionCostsCooldownsAdoptionTests.cs` (A19's own acceptance suite) hardcodes `Rung: 1` on the
`CompiledAction` fixture and never constructs an `UnlockState` at all — there is no holder-vs-content
divergence to observe in a fixture with no unlock-ladder participation. **Latent for the same reason
every other gap this reopening (§14) names is latent**: nothing grants real content to a real holder
yet. A21 is what creates that state — shipping it against this unfixed wiring would ship real content
that mis-prices itself from day one, which is why this module builds first.

## Design (locked on approval)

### 1. `CostLedger`'s `rungOf` delegate widens from action-keyed to holder-keyed

**Current signature** (`CostLedger.cs:47,55`): `Func<string, int> rungOf` — takes only `actionId`.
**New signature**: `Func<string actorKey, string actionId, int> rungOf` — both call sites inside
`CostLedger` (`Check`, `TryPay`) already have `actorKey` in scope; this is a pure parameter-list
widening, not a new call shape.

This is the **one breaking change** in this module, and its blast radius is exactly one file:
`CostLedger` is constructed in exactly one production place (`BattleRunState.cs`'s constructor) —
`BasicAttack.cs` and `TimelineDispatch.cs` only ever reference the already-constructed
`state.CostLedger`, never build one. `CostLedgerTests.cs`'s `MakeLedger` helper (the unit-level test
harness) also needs its `rungOf` lambda widened — every existing test there passes a fixed rung
regardless of actor, so widening the signature to ignore the new parameter is byte-identical for
every one of those tests.

### 2. The resolution rule, stated exactly

```text
effectiveRungOf(actorKey, actionId):
    held = <actorKey>'s UnlockState.Held
    match = held.FirstOrDefault(h => h.UnlockId == actionId)
    if match found:
        return UnlockLadder.EffectiveRung(match.EarnCountAtAcceptance, tuning).Value
    else:
        return actionCatalog.Get(actionId)?.Rung ?? 0   // unchanged fallback
```

**The fallback is not a hedge, it is a real case.** An actor's intrinsic/basic actions (the basic
attack, species-basic rows) are never held via the unlock ladder at all — `ActionSetAssembler`'s own
"intrinsic ∪ granted" union (T23) never routes an intrinsic action through `UnlockState`. For those,
the authored `Rung` **is** the only rung that has ever existed for them, and reading it is correct,
not a workaround. The fallback also covers every synthetic/test fixture that never populates an
`UnlockState` at all — this module does not require every caller to adopt the unlock ladder, only
makes the ledger read it correctly when one exists.

### 3. Where the per-actor `UnlockState` comes from

**This module does not build `UnlockState` persistence.** That is A21's job (§14: "the unlock-ladder's
own persistence, `RpgStore.ActionUnlocks.cs`, was never built"). A23's own scope is narrower and
buildable **today**, against a synthetic `UnlockState` supplied the same way every other per-battle
input already is: `BattleRunState` gains a `Func<string, UnlockState>` (or an
`IReadOnlyDictionary<string, UnlockState>`, resolved once at construction — the shape A20's own
harness would need to construct one per synthetic actor either way) that defaults to
`_ => UnlockState.Empty()` for every existing caller, which resolves through the fallback branch
above and is therefore byte-identical to today for all of them. **A21, once it exists, is the first
real caller that supplies a non-empty one** — this module does not wait for A21 to be provable; it
proves itself against a hand-built `UnlockState.FromPersisted(...)` fixture, exactly as
`UnlockStateTests.cs` already does for the unlock ladder itself.

## Golden-safety

Every existing caller either supplies no `UnlockState` source (defaults to empty, falls back to the
authored rung — the exact value read today) or has no unlock-ladder participation in its fixtures at
all (every shipped golden, every A17-A22 test built so far). **Zero-golden-mover by the same
"nothing today diverges from the fallback" argument every prior A17-A22 module proved, not assumed.**

## Acceptance criteria

1. `CostLedger`'s `rungOf` delegate is holder-keyed (`Func<string, string, int>`), and
   `StructureBudgetGuard`'s own use of the authored `Rung` is asserted, by a direct test, as
   **unchanged** — this module must not touch it.
2. Two synthetic actors holding the **same** action at **different** `EarnCountAtAcceptance` values
   pay **different** scaled costs for it, proven by a real `CostLedger.TryPay`/`Check` call against
   both, not asserted from the formula alone.
3. An actor with **no** `UnlockState` entry for an action (every intrinsic/basic action, every
   existing fixture) resolves to the **authored** `Rung`, byte-identical to today — the control every
   other criterion here is measured against.
4. Full A17-A22 regression sweep stays green, including the real battle goldens — proven by running
   the suite, not assumed from the change being "additive."

## Testing strategy

- Unit: `CostLedgerTests.cs` gains cases for holder-vs-authored divergence (criterion 2) and the
  no-`UnlockState`-entry fallback (criterion 3), built the same way its existing cases construct a
  `Snapshot`/`ActorResourcePools` fixture directly — no `BattleEngine` required to prove the ledger's
  own contract.
- Integration: one `ActionCostsCooldownsAdoptionTests.cs`-style test constructs two actors via
  `SyntheticLoadoutBuilder`, gives each a hand-built `UnlockState` holding the same action at
  different earn-counts, and asserts their resolved costs differ through a real `BattleEngine.Resolve`
  call — proving the wiring, not just the formula.
- Regression: the standing A17-A22 sweep (`ActionDispatchGeneralizationTests`,
  `ActionCostsCooldownsAdoptionTests`, `BattleGoldenTests`, `ActionSelectionAdoptionTests`,
  `TurnFsmActionEnvelopeTests`, `SkillChannelReaderTests`, `SkillModifiersTests`, `CostLedgerTests`,
  `SyntheticLoadoutHarnessTests`) plus a full `Core.Tests` run.

## Boundaries

- **Never** change what `StructureBudgetGuard` reads. The authored rung is correct there
  (`spec-rung-semantics.md` §3.1) — this module's whole existence is that `CostLedger` is the
  **other** reader and was wrong.
- **Never** build `UnlockState` persistence here. `Func<string, UnlockState>` is a seam this module
  defines and defaults to empty; wiring it to `RpgStore` is A21's scope, named, not silently assumed.
- **Never** invent a third rung reading. `spec-rung-semantics.md` §4 already forbids this outright:
  "two, named" — authored and effective, nothing else.
- **Ask first** before changing `UnlockLadder.EffectiveRung`'s own formula (`min(earnCount, cap)`) —
  this module consumes it, it does not renegotiate it.
