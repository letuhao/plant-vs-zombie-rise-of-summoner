# Spec: battle-magnitude

Module **`battle-magnitude`**, wave 2 in the [power map](../power-map.md). Depends on **`power-ladder`**.

> **Reads [ssot-power-scale.md](ssot-power-scale.md)** — the parent SSOT. Where this spec and the
> SSOT disagree, **the SSOT wins**.

**Status:** Draft — pending owner review. No build authorized.

---

## 1. Objective

Move `BattleRuleset.BaseHp / BaseAtk / BaseDefense` onto `PowerLadder`, **changing nothing**.

**Done means:** the three functions delegate to the ladder, and the entire suite is green with **no
golden re-blessed**. This is the module that proves the ladder reproduces shipped behaviour, and
every later wave rests on it.

At `B = 0` the ladder *is* `80 + 30·Θ` (SSOT §4.4), so the migration should be arithmetically a
no-op. If a golden moves here, the ladder is wrong — not the golden.

## 2. Design

### 2.1 One shape, three channels — `B` applies *proportionally*

Each channel keeps its own anchor. `B` is a **shape** parameter, so it scales to each channel's size:

```text
B_ch        = B × pinValue_ch / pinValue_hp
Value_ch(Θ) = C_ch + A_ch·Θ + B_ch·Θ(Θ−1)/2       # one dial B; C, A, B_ch per channel
```

> **An earlier draft shared `B` as an absolute increment and was broken** (audit F1). The quadratic
> term at the pin is `0.4 × 190 = 76`, and defense's *entire* pin value is 22 — so the solver
> produced `A = −2.800` and `BaseDefense` would have **decreased** with level. Atk survived only by
> luck at `A = 0.2`.
>
> | Channel | `B_ch` | `A` | `P(100)` | vs linear |
> |---|---|---|---|---|
> | hp | 0.40000 | 26.2000 | 4,680 | ×1.52 |
> | atk | 0.05412 | 3.4859 | 628 | ×1.53 |
> | defense | 0.01294 | 0.8771 | 154 | ×1.51 |
>
> All positive, and all three grow by the **same ratio** — the property the absolute form wanted and
> did not achieve.

with `C` and `A_ch` derived from each channel's own pin at Θ=20, read from shipped code:

| Channel | Shipped formula | Pin at Θ=20 |
|---|---|---|
| hp | `80 + 30L` | 680 |
| atk | `12 + 4L` | 92 |
| defense | `2 + L` | 22 |

**Why not one ladder and two ratios.** The naive model — `BaseAtk(Θ) = Value(Θ) × 92/680` — **cannot
be exact**, and the arithmetic disproves it before any test runs: at Θ=0 the true ratio is `12/80 =
0.150`, at Θ=20 it is `92/680 = 0.135`. A single ratio reproduces the pin and nothing else, so
`BaseAtk` would move at `B=0` and break the zero-movement guarantee this entire wave exists to
provide.

Per-channel `C` and `A` cost two extra tuning rows and preserve the property that matters: **`B` is
still the only dial**, so turning it moves hp, atk and defense together. Difficulty stays a
designer's decision rather than the accidental ratio of three independently tuned functions.

### 2.2 The name collision — the most likely way this module breaks something

**`BaseHp` means two unrelated things in this codebase.**

| Symbol | Meaning | Files |
|---|---|---|
| `BattleRuleset.BaseHp(int level)` | the ladder — **this module** | `BattleModels.cs:61` |
| `.BaseHp` on a shield grant / instance | a shield's hit points — **untouched** | `BattleModels.cs:35`, `ShieldGate.cs:42`, `ShieldInnateCatalog.cs:7`, `ShieldInstance.cs:27`, `ShieldRuntime.cs:123`, `EffectBag.cs:606`, `FoundationHarness.cs`, `SimEngine.cs` |

A grep-and-replace migration corrupts the shield system. The shield `BaseHp` is a stored magnitude on
an instance, not a function of level, and it is out of scope entirely.

### 2.3 Call sites do not change

`WaveCatalog.cs:61-62` (`MaxHp`, `Atk`) and `BattleEngine.cs:200` keep calling `BattleRuleset.*`;
only the implementation moves. That keeps the diff readable and the zero-movement claim auditable.

## 3. Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests
dotnet test tests\FusionRpg.Server.Tests      # battle goldens live here too
git status --short tests\                     # MUST show no golden file modified
```

## 4. Structure

```
src/FusionRpg.Core/Battle/BattleModels.cs        (edit — 3 functions delegate; literals removed)
src/FusionRpg.Core/Power/ChannelLadder.cs        (new — per-channel C/A, shared B)
data/tuning/power-scale.v1.json                  (edit — channels block: hp / atk / defense)
tests/FusionRpg.Core.Tests/Power/BattleMagnitudeParityTests.cs
```

## 5. Testing strategy

| Case | Expect |
|---|---|
| **Parity, hp** | `BaseHp(L) == 80 + 30L` for `L in [0, 5000]` at `B=0` — exact |
| **Parity, atk** | `BaseAtk(L) == 12 + 4L` for `L in [0, 5000]` at `B=0` — exact |
| **Parity, defense** | `BaseDefense(L) == 2 + L` for `L in [0, 5000]` at `B=0` — exact |
| Pins | `680 / 92 / 22` at Θ=20 |
| **The disproof, asserted** | a single-ratio model is shown wrong: `Value(0) × 92/680 != 12`. Keeps §2.1's reasoning from being re-litigated |
| **F1 regression** | every channel's derived `A` is **> 0** for `B ∈ {0, 200, 400, 1000, 9998}`. An absolute-`B` model gives defense `A = −2.8`; this test fails if anyone reintroduces it |
| Proportional growth | at `B=400`, `P_ch(100) / linear_ch(100)` is within ±0.02 across hp, atk and defense |
| `B > 0` moves all three | at `B=400` every channel's growth rate rises; none drifts alone |
| **No golden moved** | Core + Server suites green, `git status tests/` clean |
| Shield `BaseHp` untouched | shield suite green; source scan asserts no shield file references `BattleRuleset` |

## 6. Boundaries

**Always** — delegate, never reimplement · keep call sites unchanged · prove parity across a wide
range, not at three points.

**Ask first** — changing any channel's `C` or `A` · touching a shield file · altering a call site.

**Never** — re-bless a golden in this module. A moved golden here means the ladder is wrong, and that
is the entire point of wave 2.

## 7. Success criteria

1. All three exact against their shipped formulas across `[0, 5000]` at `B=0`.
2. Core, Server, Guard green; **`git status tests/` clean**.
3. No numeric curve literal left in `BattleModels.cs`.
4. Shield suite untouched and green.

## 8. Open

**None.** The single-ratio question was raised and closed inside §2.1 by arithmetic: it cannot be
exact, so per-channel `C`/`A` is the design. That resolution is asserted by a test rather than left
as a note.
