# Spec: value-spec-and-curve (E2)

Module **E2** in the [atom effect map](../effect-atom-map.md). No dependencies; pure. Nothing in the game changes.

> **Reads [definitions.md](definitions.md)** — the shared vocabulary pinned after the 2026-08-22 audit. Where this spec and the definitions disagree, **the definitions win**.

## Objective

Own **every number an atom can carry** and **when that number resolves**. One value type, three roll policies, one curve table for scaling, and named RNG streams so nothing on a replayable path ever touches an ambient random.

## Design (locked on approval)

### The value spec

```csharp
readonly record struct ValueSpec(int Min, int Max, RollPolicy Roll, string? CurveId);
```

Anywhere a number could go in an atom's params, a `ValueSpec` can go. `fixed` is simply `Min == Max`.

### Three roll policies — the four roll moments

Every ARPG separates these; the owner's picks touch moments 2 and 4.

| Policy | Resolves at | Prior art |
|---|---|---|
| `Fixed` | never — the number is the number | |
| `OnInstantiate` | **moment 2** — the item drops, the value freezes forever | D2/D4/PoE roll at drop; Last Epoch tier + roll within tier |
| `OnApply` | **moment 4** — every time the atom resolves | PoE/D2 roll the damage range **per hit** |

GAS makes exactly this a per-value flag — snapshotted attributes captured at spec creation, non-snapshotted at apply. `100–200 fire damage on hit` is `OnApply`; a dropped item's `+7 atk` is `OnInstantiate`.

Moment 3 (bind/equip) deliberately has **no policy**: binding never rolls. If a value would change at equip, it is `OnApply`.

### Scaling is a curve reference, never a formula

`scale` names a row in `effect_curve`; omitted means no scaling.

| Column | Notes |
|---|---|
| `curve_id` | `curve.atk.level`, `curve.rarity.band` |
| `input` | `level` \| `rarity` \| `tier` |
| `points_json` | ordered `(x, multiplierMilli)`, **integer ‰**, linearly interpolated between points |
| `revision` | joins the content hash (E8) |

**E2 owns the `effect_curve` DDL and DAL.** The table had no owner: Core cannot hold SQL under `guard-dal.ps1`, yet E4 validates `curveId` and E8 hashes the table. Three modules depended on a table nobody created.

**No formula strings, ever.** A formula string is a language, and a language is a parser, a sandbox, and a security surface.

The same table serves the power cost function's **reference scale** (E9), so a value and its price read one source instead of drifting apart.

### Units are not interchangeable — the trap this module must document

`+10 hp` is **ten hit points**. `+10 fire power` is **ten resolver points** — sigmoid scale, where `AccuracyScale` and `CritRateScale` are `100.0`, so ten points is 0.1 sigmoid units. Calibration: `critical-hunter` grants **+150** crit-rate points and moves crit from ~7.6% to ~26.9%; the patron aura divides per-mille by ten, so its 150‰ clamp is **+15 points**, not +15%.

Therefore: **tier bands are authored per channel family and never copied across**, and E9's `normalize(magnitude, referenceScale)` is not optional polish — without it a coefficient table prices `+10 hp` and `+10 fire power` alike and is wrong by an order of magnitude.

### Determinism

| Policy | Stream |
|---|---|
| `OnInstantiate` | the **per-instance `roll_seed`** stored on `effect_instance`, so re-reading an item reproduces it exactly |
| `OnApply` | a **named per-system stream**, `atom.apply`, joining the battle engine's existing `initiative` / `crit` / `essence` / `status` / `proc` streams |

`System.Random` never backs a replayable path — the same law `BattleEffectHost` already follows. Integer math throughout: magnitudes are `int`, curve multipliers are ‰, and interpolation rounds half away from zero exactly once.

### Hot-path shape

Resolution runs per hit. Per the E13 benchmark the rule is **no dictionaries, no string comparison, no allocation**. `ValueSpec` is a readonly struct; `CurveId` resolves to an **int index** at bake time (E7), never a string lookup at resolve time.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Atom.Value"
```

## Structure

```
src/FusionRpg.Core/Effects/Atoms/ValueSpec.cs        (new — struct, RollPolicy)
src/FusionRpg.Core/Effects/Atoms/CurveTable.cs       (new — points, integer interpolation)
src/FusionRpg.Core/Effects/Atoms/AtomRandom.cs       (new — named streams; no ambient RNG)
src/FusionRpg.Data/Sqlite/RpgStore.Curves.cs         (new — effect_curve DDL + DAL)
tests/FusionRpg.Core.Tests/Atoms/ValueSpecTests.cs
tests/FusionRpg.Core.Tests/Atoms/CurveTableTests.cs
```

## Testing strategy

| Case | Expect |
|---|---|
| `Fixed` with `Min != Max` | rejection — `fixed` means one number |
| `OnInstantiate`, same `roll_seed`, two reads | identical value |
| `OnInstantiate`, different seeds | distribution covers `[Min, Max]` inclusive at both ends |
| `OnApply` ×1000 on one stream | inclusive bounds, no value outside, reproducible for a fixed seed |
| Two atoms rolling `OnApply` in one hit | consume the stream in a **defined order**; golden'd, so a content change that shifts draw order is visible |
| Curve with one point | constant multiplier |
| Curve interpolation | integer ‰, half-away-from-zero, rounded exactly once |
| Curve `x` below first / above last point | clamps to the end point, never extrapolates |
| Unknown `curveId` | rejection at **E4 load** — E7's bake only interns an already-valid id to an int. One owner, not two |
| Curve with zero points | rejected at load (`BadCurve`) — never a hot-path divide-by-zero |
| Duplicate or unsorted `x` | rejected — "ordered" is validated, not assumed |
| Curve applied to an `OnApply` range | scales `Min`/`Max` **before** the roll, so the inclusive-bounds guarantee holds |
| Allocation probe over 10⁵ resolves | **zero** bytes allocated |

## Boundaries

**Always:** integer math; named streams; reject at bake rather than at resolve; document units next to every band.

**Ask first:** adding a roll policy; adding a curve `input`; changing interpolation or rounding.

**Never:** a formula string; `System.Random` on a replayable path; a string lookup in the resolve path; extrapolating past a curve's ends; copying a tier band between channel families.
