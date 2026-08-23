# Spec: content-scale

Module **`content-scale`**, wave 3 in the [power map](../power-map.md). Depends on **`power-ladder`, `power-index`**.

> **Reads [ssot-power-scale.md](ssot-power-scale.md)** — the parent SSOT. Where this spec and the
> SSOT disagree, **the SSOT wins**.

**Status:** Draft — pending owner review. No build authorized.

---

## 1. Objective

Apply `contentScale` to item magnitudes **exactly once**, at drop time.

This is the module that makes the same authored item worth more when it drops deeper — the payoff
the whole ladder exists for.

## 2. Design

### 2.1 Where the multiplication goes

```text
contentScale(Θc) = PowerLadder.Value(Θc) / pinValue     # pinValue from tuning, PS-7 — not a literal
finalValue       = round_half_away(rolledValue × contentScaleMilli / 1000)
```

**After the roll, never before.** `Instantiator.cs:203` resolves `RollPolicy.OnInstantiate` via
`spec.Resolve(rng)` — that roll happens in **relative space**, inside the authored `[lo_t, hi_t]`
band, and the scale multiplies its result. SSOT §4: *"the roll happens in relative space; the scale
is applied after."*

Scaling the band before rolling would work arithmetically and be wrong operationally: the drop log
would record a scaled band, and an author comparing two drops from different depths could no longer
see they rolled the same relative quality.

### 2.2 Exactly once — the PS-2 obligation

The classic failure is two systems each applying 1.5x and nobody finding the 2.25x. Three defences:

1. **One call site.** `contentScale` is multiplied in `Instantiator` and nowhere else.
2. **The instance records it.** `content_scale_milli` and `theta_content` are stored on the instance,
   so any value can be divided back to its relative roll and audited.
3. **`power-guard` scans for a second multiplication** (wave 4).

### 2.3 What is not scaled

| Not scaled | Why |
|---|---|
| `PowerVector` / atom price | prices *relative* content; the magnitudes it prices are already scaled — scaling it double-counts (SSOT §1, §10.2 row 12) |
| Affix tier ladder `1.75^(t-1)` | bounded, level-free, relative (§10.2 row 7) |
| `level_req`, `maxTierAt` | gates, not magnitudes (§10.2 row 14) |
| Rates of any kind | PS-3 — `contentScale` never touches a rate input |

### 2.3a The seedsmith seam — the corpus must stay scale-free

`tools/seedsmith` authors item magnitudes, and PS-2 says a magnitude is scaled exactly once, here.
So the corpus must contain **no absolute value** that already anticipates depth — every authored
number is relative to the calibration point, and `contentScale` is what makes it absolute.

`numerics` already enforces the relative half (`lo_t`/`hi_t` from a share of
`referenceBaseGameUnits(20)`). What is **not** checked today is that nothing downstream re-scales it.
This module adds that check: re-resolving the corpus at `Θc = 20` must reproduce the shipped values
byte-for-byte (§5, "Identity at the pin").

Reassigned here from `power-guard`, whose C# source scan cannot see a Python tool
([spec-power-guard.md](spec-power-guard.md) §8).

### 2.4 Θ_content at drop time

The dropping context supplies it: a wave drop uses the wave's `ContentIndex`; an expedition inherits
through the wave chain; a world drop uses `mapLevel(M)` when the world program lands `Wm`. **Absence
is a rejection, not a default of 1.0** — a silent 1.0 is a drop that quietly ignores depth, which is
the bug this module exists to prevent.

## 3. Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Instantiat|FullyQualifiedName~ContentScale"
dotnet test tests\FusionRpg.Core.Tests
dotnet test tests\FusionRpg.Data.Tests
python -m seedsmith validate        # corpus must still resolve
```

## 4. Structure

```
src/FusionRpg.Core/Power/ContentScale.cs                (new — the ratio, pure)
src/FusionRpg.Core/Effects/Atoms/Instantiator.cs        (edit — the single multiplication)
src/FusionRpg.Data/Sqlite/RpgStore.Atoms.cs             (edit — persist content_scale_milli, theta_content)
tests/FusionRpg.Core.Tests/Power/ContentScaleTests.cs
```

## 5. Testing strategy

| Case | Expect |
|---|---|
| Identity at the pin | `Θc == 20` -> `contentScale == 1.000` -> instantiated values byte-identical to today. **The corpus does not move at calibration depth** |
| Scaling table | SSOT §4.5: `Θc=50 -> 2.76x`, `100 -> 6.88x`, `200 -> 19.5x` |
| Roll then scale | same seed at `Θc=20` and `Θc=100` produces the **same relative roll**, differing only by the ratio |
| Recorded and reversible | `value / contentScale` recovers the relative roll within one unit of rounding |
| **Applied once** | instantiate twice through the full path; the second result is not scaled again |
| Missing `Θ_content` | rejection, **not** a silent 1.0 |
| `PowerVector` unscaled | an item's priced power is identical at `Θc=20` and `Θc=200` — the double-count tripwire |
| `B=0` behaviour | at `B=0`, `contentScale` is still ratio-correct — the wave-2 no-op does not make this module inert |

## 6. Boundaries

**Always** — multiply after the roll · record scale and Θ on the instance · reject a missing Θ.

**Ask first** — scaling anything outside item magnitudes.

> Audit F5: an earlier draft hardcoded `680` and listed "changing the 680 denominator" as ask-first —
> violating PS-7 in the same program that declares it. The denominator is `pinValue`, and it is
> already in the tuning file.

**Never** — scale `PowerVector`, a tier ladder, a gate, or a rate · default a missing Θ to 1.0 ·
multiply anywhere but `Instantiator`.

## 7. Success criteria

1. Corpus byte-identical at `Θc = 20`.
2. Scaling table matches SSOT §4.5.
3. Same seed, two depths, identical relative roll.
4. Double-application and `PowerVector` tripwires both fire when violated.

## 8. Open

**None.** The seam (`Instantiator.cs:203`) and the not-scaled list are fixed by the SSOT, and the
denominator is `pinValue` **read from tuning** — audit F5 corrected an earlier draft of this section
that named `680` as a fixed literal, which violated PS-7 in the program that declares it.
