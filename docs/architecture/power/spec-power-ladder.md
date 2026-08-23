# Spec: power-ladder

Module **`power-ladder`**, wave 1 in the [power map](../power-map.md). Depends on nothing.

> **Reads [ssot-power-scale.md](ssot-power-scale.md)** — the parent SSOT. Where this spec and the
> SSOT disagree, **the SSOT wins**. This module implements §4 and §9; it decides nothing.

**Status:** Draft — pending owner review. No build authorized.

---

## 1. Objective

Ship the one function every magnitude in the game will eventually come from, and the config loader
that keeps its constants out of code.

**Done means:** `PowerLadder.Value(Θ)` exists, is pure, is integer-exact, reads every constant from
a versioned tuning file, and **has no callers.** Adding callers is waves 2–3. This module is
deliberately inert — it can land, be reviewed, and sit in the tree changing nothing.

That inertness is the point. It is what lets the whole migration be verified against a curve that is
already proven to reproduce shipped behaviour before a single consumer moves.

**Who it is for:** every other module in the program, plus the balance owner, who gets one file to
edit instead of a rebuild.

---

## 2. Design

### 2.1 The function

```text
ValueMilli(Θ) = C_milli + A_milli·Θ + B_milli·Θ(Θ−1)/2
Value(Θ)      = round_half_away(ValueMilli(Θ) / 1000)      # once, at the end
```

Per-mille integers throughout, per `P13` and [definitions.md](../effect-atom/definitions.md) §2's
rounding rule (*half away from zero, exactly once, at the end*).

`Θ(Θ−1)` is a product of consecutive integers, therefore always even, so the triangular term divides
exactly. **No rounding occurs inside the sum.**

### 2.2 `A` is derived, and `B` must be even

```text
A_milli = (pinValue·1000 − C_milli − B_milli·pin(pin−1)/2) / pin
        = (600000 − 190·B_milli) / 20
        = 30000 − 19·B_milli/2
```

Exact **iff `B_milli` is even** (SSOT §9.1). An odd `B` is a load rejection naming the two nearest
legal values — never a silent round, because a rounded `A` breaks the pin, and the pin is the only
thing protecting the item corpus from every future retune.

| `B_milli` | `A_milli` | |
|---|---|---|
| 0 | 30000 | reproduces `BattleRuleset.BaseHp` exactly |
| 200 | 28100 | gentle |
| **400** | **26200** | **decided** |
| 401 | — | **rejected** — odd |
| 1000 | 20500 | steep |

### 2.3 The tuning file

`data/tuning/power-scale.v1.json`, versioned like `data/seed/items/_tuning/tier-bands.v1.json`.
Never hand-edited; a tool republishes `v{n+1}` and the old version stays for revert.

```jsonc
{
  "schemaVersion": 1, "version": 1,
  "curve":   { "cMilli": 80000, "bMilli": 400, "pinIndex": 20, "pinValue": 680 },
  "weights": { "WdMilli": 1000, "WaMilli": 25000, "WrMilli": 250,
               "WzMilli": 1000, "WmMilli": null, "WwMilli": 5000 },
  "report":  { "axisShareEnabled": true }
}
```

`weights` is loaded and validated here but **consumed by `power-index`** — one file, one load, so a
balance edit is one place. `WmMilli: null` is legal at rest and throws when `power-index` reads it
(SSOT §9.1: reject, never guess).

### 2.4 Load-time validation — all rejections, no defaults

| Condition | Rejection |
|---|---|
| `bMilli` odd | `PowerTuningRejected.OddB` — names `b−1` and `b+1` |
| `bMilli < 0` | `NegativeB` — a concave ladder is not a design, it is a typo |
| `Value(pinIndex) != pinValue` | `PinBroken` — the belt-and-braces check on §2.2's algebra |
| `cMilli`, `pinIndex`, `pinValue` differ from `80000/20/680` | `FixedConstantChanged` — ask-first, not a tuning knob |
| any weight negative | `NegativeWeight` |
| file absent / unparseable | `TuningMissing` — **no built-in fallback constants** |

**No defaults anywhere.** A missing tuning file is a startup failure, not a silent `B=0`. The failure
mode this prevents is the one §0 of the SSOT documents: a plausible-looking constant nobody chose.

### 2.5 Overflow

`maxIndex` is a **computed property of the loaded `B`**, not a constant:

```text
maxIndex = largest Θ with ValueMilli(Θ) ≤ long.MaxValue
```

`B=400 → Θ ≈ 2.14×10⁸`. `B=0 → Θ ≈ 3.5×10¹⁴`. `Value` throws `PowerIndexOverflow` above it rather
than wrapping. Reporting the ceiling as a function of the dial is how a balance owner learns that a
steeper curve costs headroom.

### 2.6 Code shape

Match the neighbours — `BattleRuleset`, `StatusPolicy`, `LoamPolicy`: static, integer, comment-carries-the-why.

```csharp
/// <summary>
/// The power ladder (ssot-power-scale.md §4). Arithmetic progression on the increment:
/// the step A + B·(Θ−1) grows linearly, so the total is triangular — local exponent 1.1 → 1.9.
///
/// <para>Θ(Θ−1) is always even, so the triangular term divides exactly and no rounding
/// happens inside the sum. The single rounding is milli → whole, at the end.</para>
/// </summary>
public sealed class PowerLadder
{
    readonly PowerTuning _t;

    public long ValueMilli(int index)
    {
        Guard(index);
        return _t.CMilli
             + (long)_t.AMilli * index
             + (long)_t.BMilli * index * (index - 1) / 2;   // exact: index(index−1) is even
    }

    public long Value(int index) => RoundHalfAway(ValueMilli(index), 1000);
}
```

No numeric literal appears outside `PowerTuning`'s loader — `power-guard` (wave 4) enforces it.

---

## 3. Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~PowerLadder"
dotnet test tests\FusionRpg.Core.Tests        # full domain suite — must stay green
.\scripts\guard-dal.ps1                        # no SQL leaks into Core
```

---

## 4. Structure

```
src/FusionRpg.Core/Power/PowerLadder.cs          (new — the function)
src/FusionRpg.Core/Power/PowerTuning.cs          (new — record + load-time validation)
src/FusionRpg.Core/Power/PowerTuningLoader.cs    (new — JSON read, version pick, rejections)
src/FusionRpg.Core/Power/PowerRejection.cs       (new — the typed rejection list, §2.4)
data/tuning/power-scale.v1.json                  (new — the only place constants live)
tests/FusionRpg.Core.Tests/Power/PowerLadderTests.cs
tests/FusionRpg.Core.Tests/Power/PowerTuningTests.cs
```

`Core/Power/` is a **new namespace** and the only place the curve may exist.

---

## 5. Testing strategy

xUnit, `tests/FusionRpg.Core.Tests`, matching the neighbours. Every case is exact — no `InRange` on a
pure integer function.

| Case | Expect |
|---|---|
| `B=0` | `A_milli == 30000`, and `Value(L) == 80 + 30L` for `L ∈ [0, 5000]` — **byte-identical to `BattleRuleset.BaseHp`** |
| Pin holds for every legal `B` | `Value(20) == 680` for `B ∈ {0, 2, 200, 400, 1000, 9998}` |
| `A` derivation | `A_milli == 30000 − 19·B/2` for every even `B` tested |
| Odd `B` | `OddB` rejection naming `b−1` and `b+1`; **no ladder is constructed** |
| Integer exactness | `ValueMilli` computed two ways (closed form vs. summing `ΔP` from 0) agree **exactly** for `Θ ∈ [0, 2000]` |
| Increment is arithmetic | `ValueMilli(Θ) − ValueMilli(Θ−1) == A_milli + B_milli·(Θ−1)` for all Θ |
| Monotonic | `Value(Θ+1) > Value(Θ)` for all Θ in range, every legal `B` |
| Local exponent band | at `B=400`, exponent ∈ (1.2, 1.7) for `Θ ∈ [40, 250]` — the §4.5 claim, asserted not assumed |
| `maxIndex` | `Value(maxIndex)` succeeds; `Value(maxIndex+1)` throws `PowerIndexOverflow`; `maxIndex` shrinks as `B` grows |
| Missing file | `TuningMissing`. **No fallback constants exist** — asserted by reflection over `Core/Power` for numeric literals |
| Changed fixed constant | `FixedConstantChanged` when `cMilli`/`pinIndex`/`pinValue` differ from `80000/20/680` |
| Purity | same index, 1000 calls, identical result; no allocation on the hot path |
| Determinism | no `Math.Pow`, `Math.Exp`, `double`, or `decimal` anywhere in `Core/Power` — asserted by source scan, matching `PowerReads.cs`'s reasoning |

**Not tested here:** anything about `Θ`'s composition (that is `power-index`) or any consumer
(waves 2–3). A test in this module that needs a battle actor is in the wrong module.

---

## 6. Boundaries

**Always**
- Integer per-mille. One rounding, at the end, half away from zero.
- Every constant from the tuning file; `A` derived from the pin at load.
- Reject with a typed reason and a suggested fix. Never default, never guess, never round `A`.
- Report `maxIndex` as a function of the loaded `B`.

**Ask first**
- Changing `C`, `pinIndex`, or `pinValue` — these are anchored to `BattleRuleset` and the item corpus.
- Adding a term to `P(Θ)`. The shape is the SSOT's, not this module's.
- Widening beyond `long`.

**Never**
- A numeric curve literal outside the loader.
- `double`, `decimal`, `Math.Pow`, or `Math.Exp` in `Core/Power` — the output is hashed.
- A caller. This module ships inert; wiring is waves 2–3.
- A fallback constant for a missing or invalid tuning file.

---

## 7. Success criteria

1. `Value(L) == 80 + 30L` at `B=0` across `[0, 5000]` — the zero-movement proof the whole migration rests on.
2. `Value(20) == 680` for every legal `B`.
3. Odd `B` rejected; no silent rounding of `A`.
4. Closed form and iterated sum agree exactly to `Θ = 2000`.
5. Full `FusionRpg.Core.Tests` green, **no golden re-blessed** — trivially true, since nothing calls it.
6. Source scan: zero numeric literals outside the loader, zero floating-point types in `Core/Power`.

---

## 8. Open

**None.** Every constant is decided (SSOT §10.3, §10.6) and every shape question is answered by the
parent SSOT. The `B`-must-be-even constraint was found while writing §2.2 of this spec and is
recorded back in SSOT §9.1.
