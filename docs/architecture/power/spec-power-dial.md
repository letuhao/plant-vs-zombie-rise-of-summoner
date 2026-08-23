# Spec: power-dial

Module **`power-dial`**, wave 4 in the [power map](../power-map.md). Depends on **everything**.

> **Reads [ssot-power-scale.md](ssot-power-scale.md)** — the parent SSOT. Where this spec and the
> SSOT disagree, **the SSOT wins**.

**Status:** Draft — pending owner review. No build authorized.

---

## 1. Objective

Turn `B` from `0` to `400` — and re-bless the goldens that move, knowingly.

**This is the only golden-moving change in the program.** Every prior module was built so that this
one has exactly one variable in it.

## 2. Design

### 2.1 The change

```jsonc
// data/tuning/power-scale.v2.json  — v1 kept for revert
"curve": { "cMilli": 80000, "bMilli": 400, "pinIndex": 20, "pinValue": 680 }
```

**One field.** `A` re-derives to `26200` automatically (SSOT §4.3), the pin holds at 680, and the
item corpus does not move at calibration depth.

`BattleRuleset.RulesetVersion` bumps `2 -> 3`, per its own contract: *"Locked engine constants.
Changing any bumps RulesetVersion."*

### 2.2 What moves, and what must not

| Moves | Must not move |
|---|---|
| Battle goldens away from `Θ=20` | Anything at `Θ=20` — the pin |
| Item values away from `Θc=20` | Item values at `Θc=20` |
| World acceptance hashes, if any actor's `Θ != 20` | Rate goldens — **`B` must not touch a rate** (PS-3) |

**The rate goldens are the assertion, not a side effect.** If a hit-rate golden moves when `B`
changes, `battle-rates` has a PS-3 violation and **this module stops.** That is the highest-value
signal in the whole program, and it is only visible because wave 2 landed at `B=0` first.

### 2.3 Procedure

1. Confirm clean `main`, all five guards green.
2. Publish `power-scale.v2.json`. **Nothing else in the commit.**
3. Run every suite. Record which goldens move.
4. **Triage before re-blessing.** Each moved hash is either expected (a magnitude away from the pin)
   or a defect (a rate, or anything at the pin). Any defect stops the module.
5. Re-bless expected goldens in one commit whose message carries the before/after table.
6. Bump `RulesetVersion`.

Step 4 is the one that cannot be skipped. Re-blessing first and reasoning later is how a real
regression enters a codebase disguised as a tuning change.

### 2.4 Revert

`v1` stays on disk. Reverting is republishing it plus un-bumping `RulesetVersion` — no code change,
which is PS-7's whole point.

## 3. Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests
dotnet test tests\FusionRpg.Server.Tests
dotnet test tests\FusionRpg.Data.Tests
dotnet test tests\FusionRpg.Guard.Tests
git status --short tests\        # the moved-golden list — triage BEFORE re-blessing
```

## 4. Structure

```
data/tuning/power-scale.v2.json                  (new — bMilli 400; v1 retained)
src/FusionRpg.Core/Battle/BattleModels.cs        (edit — RulesetVersion 2 -> 3)
docs/architecture/decisions.md                   (edit — record the dial and its date)
tests/**/goldens/*                               (re-blessed, triaged, one commit)
```

## 5. Testing strategy

| Case | Expect |
|---|---|
| Pin holds | `Value(20) == 680`; every `Θc=20` item value unchanged |
| **Rate goldens frozen** | zero hit/crit goldens move. **A move here stops the module** |
| Magnitudes move as predicted | `Θ=100` hp matches SSOT §4.5's `4,680` |
| Exponent band | local exponent in `(1.2, 1.7)` for `Θ in [40, 250]` |
| Revert | republishing `v1` restores every pre-dial hash byte-identically |
| Guards | all five green after the change |

## 6. Boundaries

**Always** — one field in one commit · triage every moved hash before re-blessing · keep `v1`.

**Ask first** — any `B` other than 400 · re-blessing a hash you cannot attribute.

**Never** — combine this with a refactor · re-bless a rate golden · re-bless in the same commit as
the tuning change · skip the `RulesetVersion` bump.

## 7. Success criteria

1. `bMilli: 400` is the only functional change.
2. Zero rate goldens moved.
3. Nothing at the pin moved.
4. Every re-blessed hash attributed in the commit message.
5. `RulesetVersion == 3`; `v1` revert proven.

## 8. Open

**None.** `B = 0.4` is decided (SSOT §10.3). The procedure is the content of this module.
