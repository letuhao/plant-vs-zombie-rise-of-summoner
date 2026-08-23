# Spec: battle-rates

Module **`battle-rates`**, wave 2 in the [power map](../power-map.md). Depends on **`power-ladder`**.

> **Reads [ssot-power-scale.md](ssot-power-scale.md)** — the parent SSOT. Where this spec and the
> SSOT disagree, **the SSOT wins**.

**Status:** Draft — pending owner review. No build authorized.

---

## 1. Objective

Move the four rate baselines onto **`Θ`** — not `P(Θ)` — and prove parity invariance survives.

`BaseAccuracy / BaseDodge / BaseCritRate / BaseCritResist` decide *contests*, so PS-3 says they read
the linear index. This is the module where PS-3 stops being a rule in a document and becomes a test.

## 2. Design

### 2.1 The change is a rename, not a formula

```text
BaseAccuracy(Θ)   = 220 + 26·Θ        BaseCritRate(Θ)   = 10·Θ
BaseDodge(Θ)      =       26·Θ        BaseCritResist(Θ) = 10·Θ + 250
```

Identical arithmetic; the parameter's *meaning* changes from "actor level" to "power index". At
`Θ = level` — what wave 2 hands them — output is byte-identical.

**These must never call `PowerLadder.Value`.** Under a `B > 0` dial both accuracy and dodge would
grow quadratically, and so would their *difference* — the only thing the sigmoid sees. A one-index
gap at `Θ=1000` would be worth 400× what it is at `Θ=10`, which is precisely the §2 failure the
theorem forbids. This is the module most likely to be "helpfully" migrated to `P(Θ)` by someone
tidying up; §5's tripwire exists for that person.

### 2.2 What is being protected

```text
parity: σ((220 + 26Θ − 26Θ)/100) = σ(2.2) ≈ 0.900
```

The constant `220` is the whole design — it survives the subtraction; the `26Θ` terms do not. A
level-20 duel and a level-20,000 duel have the same hit rate. `decisions.md:40` locks
`0.90 ± 0.02`, and `BattleAdoptionTests.cs:16-20` already asserts it.

### 2.3 Call site

`BattleStatComposer.cs:59-62` is the **only** production caller — it writes the four into
`CombatAccuracyOmni / CombatDodgeOmni / CombatCritRateOmni / CombatCritResistOmni`. One file.

## 3. Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Battle"
dotnet test tests\FusionRpg.Core.Tests
git status --short tests\
```

## 4. Structure

```
src/FusionRpg.Core/Battle/BattleModels.cs       (edit — 4 signatures take Θ; arithmetic unchanged)
src/FusionRpg.Core/Battle/BattleStatComposer.cs (edit — passes Θ instead of setup.Level)
tests/FusionRpg.Core.Tests/Power/RateParityTests.cs
```

## 5. Testing strategy

| Case | Expect |
|---|---|
| Byte parity | all four match shipped output for `Θ in [0, 5000]` |
| **Parity invariance** | `P(hit)` at equal `Θ` is `0.90 ± 0.02` at `Θ in {1, 5, 10, 20, 100, 1000, 10000}` — the existing rate test, extended well past its current 1/5/10/20 |
| Crit invariance | `P(crit)` at equal `Θ` stays `0.05–0.10` across the same range |
| Fixed gap, fixed value | `BaseAccuracy(Θ+5) − BaseDodge(Θ) − 220 == 130` at **every** `Θ` |
| **PS-3 tripwire** | load `B=0` and `B=1000`; assert all four outputs identical. Fails the moment someone routes a rate through `P(Θ)` |
| No golden moved | Core + Server green, `git status tests/` clean |

## 6. Boundaries

**Always** — keep the arithmetic exactly as shipped · extend the existing rate tests rather than
replacing them.

**Ask first** — changing `220`, `26`, `10`, or `250`. They are rate-tested and `decisions.md`-locked.

**Never** — call `PowerLadder.Value` from a rate function. That is PS-3, and §5 enforces it.

## 7. Success criteria

1. Four functions byte-identical across `[0, 5000]`.
2. Parity holds at `Θ = 10,000`, not just 1/5/10/20.
3. PS-3 tripwire passes, and fails when deliberately violated.
4. No golden re-blessed.

## 8. Open

**None.** Arithmetic unchanged; one caller.
