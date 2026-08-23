# Shield and elements — the layered pool, two matrices, and weighted payloads

**Status:** Detail design, 2026-08-23. **Document 8 of 9** owed by
[gap-audit-2026-08-22.md](gap-audit-2026-08-22.md) §7. Covers gaps **A25** (shield stack),
**A27** (element matchup matrix), **A28** (actor element typing), **A29** (hybrid weighted payload).

Third and last of the documents backed **entirely by shipped code** — `Combat/Shield/` is 8 files and
`Combat/Element/` is 7, all green. Nothing here is designed against a program that does not exist.

**Consumes** [spec-magnitude-and-units.md](spec-magnitude-and-units.md) for every number, and shares
four of the twelve grid rows with [spec-derived-stat-sheet.md](spec-derived-stat-sheet.md).

**Sources, all read this session:** [`ShieldRuntime.cs`](../../src/FusionRpg.Core/Combat/Shield/ShieldRuntime.cs) ·
[`ShieldMath.cs`](../../src/FusionRpg.Core/Combat/Shield/ShieldMath.cs) ·
[`ShieldPolicy.cs`](../../src/FusionRpg.Core/Combat/Shield/ShieldPolicy.cs) ·
[`ShieldInstance.cs`](../../src/FusionRpg.Core/Combat/Shield/ShieldInstance.cs) ·
[`ShieldElementMatrix.cs`](../../src/FusionRpg.Core/Combat/Shield/ShieldElementMatrix.cs) ·
[`ElementTable.cs`](../../src/FusionRpg.Core/Combat/Element/ElementTable.cs) ·
[`ElementRingMatrix.cs`](../../src/FusionRpg.Core/Combat/Element/ElementRingMatrix.cs) ·
[`ElementHub.cs`](../../src/FusionRpg.Core/Combat/Element/ElementHub.cs) ·
[`ElementPayload.cs`](../../src/FusionRpg.Core/Combat/Element/ElementPayload.cs) ·
[element-hub-ssot.md](../architecture/element-hub-ssot.md) · [shield-system-spec.md](../architecture/shield-system-spec.md).

---

## 0. A correction to my own audit, made before designing on it

The gap audit's **A27** said the two matrices *"are asymmetric"*, and
[DESIGN-GATE.md §1](../DESIGN-GATE.md) says *"the shield matrix is asymmetric with the combat ring."*
**Checked against code, the contents are identical.**
[`ElementTable.Shipped()`](../../src/FusionRpg.Core/Combat/Element/ElementTable.cs) ends:

```csharp
return new ElementTable(elements, ring, ring.Select(r => r with { }).ToArray());
//                                ^^^^  ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
//                                combat            shield — a copy of the same rows
```

All 36 pairs agree by construction, light ⇄ dark included. `ShieldElementMatrix.cs:17-19` already
records this: *"Verified 2026-08-22: the two are the same in all 36 pairs today, so the asymmetry the
atom spec warned about is not there."*

**The asymmetry is real but it is in the *contract*, not the content**, and that is what the UI must
respect:

| | `ElementRingMatrix` | `ShieldElementMatrix` |
|---|---|---|
| Returns | `ElementMatchupRelation` — **`Same` is distinct from `Neutral`** | a bare `int` unit `∈ {−1, 0, +1}` |
| Same-element attack | `Same` (its own value) | collapsed to **`0`** |
| K (0.25) | **baked in** by `RelationShare` → `±0.25` | applied **once downstream**, in `ShieldMath` |
| K constant | `ElementMatchupPolicy.MatchupShareK` (double) | `ShieldPolicy.MatchupShareKPm` (permille long), *"own constant, decoupled"* |

So one matrix component cannot silently serve both, and **it must be able to render the diff** — the
tables are independently editable and divergence is an Ask-first balance decision
([shield-system-spec.md §8](../architecture/shield-system-spec.md)). A UI that shows only "the element
chart" would hide the day they stop agreeing.

*Corrected in the audit's A27 row and stated here because designing on a wrong premise is the failure
this repo's gate exists to stop — including when the wrong premise is mine.*

---

## 1. The shield stack — what it actually is

Not a number. **A bounded, ordered stack of typed pools**, each draining in turn.

| Property | Value | Source |
|---|---|---|
| Max shields per actor | **3** | [`ShieldPolicy.cs:17`](../../src/FusionRpg.Core/Combat/Shield/ShieldPolicy.cs) |
| Drain order | **higher priority first** — aura `30` → skill `20` → innate `10` | `:20-22`, owner decision 9 |
| Element | one per shield, **or untyped** (`null`) | [`ShieldInstance.cs:11`](../../src/FusionRpg.Core/Combat/Shield/ShieldInstance.cs) |
| Capacity | `maxHp = grant.BaseHp + combat.shield.capacity.{element}` | [`ShieldRuntime.cs:121-123`](../../src/FusionRpg.Core/Combat/Shield/ShieldRuntime.cs) |
| Regen | `combat.shield.regen.{element}`, **shield hp per second**, carried at permille | `:403-410` |
| Expiry | optional, in shield ticks; innate has none by default | `ShieldInstance.cs:15` |

**Five apply outcomes, and four of them are not "it worked"** (`ShieldInstance.cs:36-48`):
`Applied` · `Merged` · `DroppedWeaker` (at cap, not stronger than the weakest) · `Rejected`
(`maxHp ≤ 0` after capacity) · `MergedRemoved`. A UI that only renders success cannot explain why a
granted shield did not appear — which is exactly the GG-55 *"never disable without saying why"* case,
arriving from the runtime instead of the button.

### The two promises the player is allowed to rely on

These are policy constants with player-visible meaning, and both should be **stated in the UI**, not
left to be discovered:

| Constant | Value | The promise |
|---|---|---|
| `ChipFloorKPm` | **100** (0.10×) | *"A hit always spends at least 10% of its damage on the shield."* **Immunity is impossible** — toughness saturates at 10× efficiency |
| `PenCapKPm` | **3000** (3×) | *"Penetration at best triples shield burn."* No infinite shredding |

Verified: input `100` against toughness `9999` still yields `damageToShield = 10` (the floor); against
pen `9999` it yields `300` (the cap).

---

## 2. The cascade, rendered as arithmetic that reproduces

The absorb math is 64-bit integer at permille scale, no floats in any game-affecting branch
([`ShieldMath.cs:12`](../../src/FusionRpg.Core/Combat/Shield/ShieldMath.cs)):

```text
elemMod        = round(relUnitPm × 250 × input / 1e6)        // half away from zero
raw            = input + elemMod + hitCount × (pen − toughness)
damageToShield = clamp(raw, ceil(0.10 × input), 3 × input)
spent          = min(shieldHp, damageToShield)
remainder      = (input × (damageToShield − spent) + damageToShield/2) / damageToShield
```

**Worked, computed from the shipped formula** — a pure-fire hit of 100 into an ice/fire/untyped stack:

| Layer | element | hp | relPm | elemMod | raw | clamp | to shield | spent | passes on |
|---|---|---:|---:|---:|---:|---|---:|---:|---:|
| aura | ice | 40 | +1000 | **+25** | 125 | [10, 300] | 125 | 40 | 68 |
| skill | fire | 60 | 0 | 0 | 68 | [7, 204] | 68 | 60 | 8 |
| innate | untyped | 100 | 0 | 0 | 8 | [1, 24] | 8 | 8 | **0** |

**100 damage reached 0 HP and cost 108 shield HP.** That is not a rounding artefact — fire is strong
into the ice layer, so `elemMod` *amplifies what the shield pays*. The same hit with `+30 toughness`
on each layer costs **68** and never touches the innate shield at all.

**This is why the shield needs a readout and not just a bar.** "Your shield went down by 108 from a
100-damage hit" is alarming and correct, and only the per-layer breakdown makes it legible.

### The `remainder` rule the UI must not round differently

`remainder` is *proportional*, not `input − spent`. When a layer's `damageToShield` exceeds what the
pool can pay, the leftover is scaled back down into HP-space. Re-deriving it in the UI as a
subtraction gives a different number from the engine on almost every partial break — a
[spec-magnitude-and-units.md §5 R3](spec-magnitude-and-units.md) violation (*the renderer never
recomputes*). **The server sends the cascade; the UI renders it.**

---

## 3. The components

### 3.1 Shield stack — a segmented bar, ordered by drain

Not three separate bars, and not one merged bar. **One bar, segmented, drawn in drain order
left-to-right**, so the reading direction *is* the depletion direction.

- Each segment carries its **element** (or an untyped hatch), its current/max, and its source.
- **Segment width is proportional to `maxHp`, and the fill within each is its own `hp/maxHp`** — a
  broken layer collapses to an empty slot rather than vanishing, because the player needs to see the
  layer existed.
- The three priority tiers are **labelled, not just ordered** — aura / skill / innate. Order alone
  cannot survive two shields sharing a tier.
- Regen shows as a rate on the segment (`+3/s`), in the `GameUnitsPerSecond` unit class.
- **A typed segment carries its element's colour**, because the element *is* the mechanic here —
  `elemMod` is computed against it, so a generic fill would hide the thing deciding how much the
  layer pays. Untyped renders as a neutral hatch.

**Measured, not eyeballed (GG-30).** The segment label is `--text` over the blended fill. At the
first-drawn opacity of `.55` the pale elements **failed WCAG AA** — `light` 3.28, `air` 3.90,
`ice` 4.41. At **`.38`** the worst element is `light` at **5.46** and all six clear 4.5 with headroom;
`ice` reads 6.85 and `fire` 8.73. The opacity is a contrast constraint, not a style preference.

### 3.2 Absorb readout — a band-4 toast, expandable

Fires on a hit that touched a shield. Collapsed: *"Shield absorbed 100 — 0 reached you."* Expanded: the
per-layer table from §2, with `elemMod` labelled in words (*"fire is strong into ice: +25"*).

### 3.3 The matchup matrix — 6 × 6, and it can show either table

One component, three modes:

| Mode | Shows |
|---|---|
| **Combat** | `ElementRingMatrix` — STR / WEK / NEU / **SAME**, four values |
| **Shield** | `ShieldElementMatrix` — `+1 / 0 / −1`, three values, `SAME` folded into `0` |
| **Diff** | cells where the two disagree. **Empty today, and it says so** |

**The diff mode is the point.** The tables are independently editable and currently identical; a
component that cannot express "these agree" also cannot express the day they do not. An empty diff
renders as *"The shield chart matches the combat chart in all 36 pairs"* — a positive statement, not a
blank panel.

Rows and columns come from `ElementTable.Current.Elements`, **ordered by `Ordinal`**, which is
explicit and append-only — *"reordering the roster silently renames every generated channel"*
([`ElementTable.cs:6-9`](../../src/FusionRpg.Core/Combat/Element/ElementTable.cs)). The matrix must
never sort alphabetically.

### 3.4 Element typing — a two-slot chip, with a constrained picker

Rules, all validated in the engine ([element-hub-ssot.md §5](../architecture/element-hub-ssot.md)):

| Rule | UI consequence |
|---|---|
| 0, 1, or 2 concrete types | the chip renders one, two, or **"untyped"** — never a blank |
| `primary == secondary` invalid | the second picker excludes whatever the first chose |
| **`omni` may never occupy a slot** | the picker must not offer it. `omni` is an additive baseline, not a type |
| unknown id rejects | a legacy/unknown id renders as a neutral chip rather than being dropped |

**Dual typing multiplies, Pokémon-style**, and the UI should say so where it matters:
`combinedMult = m_primary × m_secondary`, then `bonus = (combinedMult − 1) × baseDamage`
([`ElementHub.cs:16-24`](../../src/FusionRpg.Core/Combat/Element/ElementHub.cs)). So fire into
`ice + earth` is `1.25 × 1.0 − 1 = +0.25×`, and into `air + earth` is `0.75 × 1.0 − 1 = −0.25×`. A
defender with two types is not "twice as resistant"; it can be **anywhere from 0.5625× to 1.5625×**.

### 3.5 Payload weights — a proportional split bar

A hit carries `[{element, weight}]`, and the invariant is enforced, not assumed: weights must be
**positive** and **sum to 1.0 within 1e-6**, or `ElementPayload.Validate` throws
([`ElementPayload.cs:24-38`](../../src/FusionRpg.Core/Combat/Element/ElementPayload.cs)).

So the component is a **100%-width split bar** — it cannot be anything else without lying about the
model. Each band is an element, width is its weight, and the whole bar is always full.

**An empty payload is legal and means untyped**, with zero matchup
([`ElementHub.cs:31-33`](../../src/FusionRpg.Core/Combat/Element/ElementHub.cs)) — rendered as a
single neutral band, never as an error and never as an empty bar.

---

## 4. Guards

| # | Guard | Fails when |
|---|---|---|
| 1 | **Matrix rows/columns come from `Elements` ordered by `Ordinal`** | someone sorts alphabetically or hand-lists; breaks on the next element added |
| 2 | **The matrix component renders all three modes**, and the empty diff renders as a statement | "they agree" becomes indistinguishable from "not loaded" |
| 3 | **The type picker never offers `omni`, and never offers the other slot's pick** | an invalid actor typing can be built in the UI |
| 4 | **A payload bar always totals 100%** | a weight set that does not sum to 1 renders as a gap instead of throwing |
| 5 | **The cascade is rendered from the server's per-layer result, never recomputed** | `remainder` re-derived as `input − spent`, diverging from the engine on partial breaks |
| 6 | **All five apply outcomes have a rendering** | a `DroppedWeaker` grant silently does nothing |
| 7 | **Chip floor and pen cap are stated in the UI** | a player believes stacking toughness can reach immunity |

---

## 5. What this changes elsewhere

| File | Change |
|---|---|
| [gap-audit-2026-08-22.md](gap-audit-2026-08-22.md) | **A27 corrected** — the matrices differ in contract, not content (§0) |
| [DESIGN-GATE.md §1](../DESIGN-GATE.md) | the Elements row says *"the shield matrix is asymmetric with the combat ring"*. Sharpen to **contract, not content** — the content is identical today and the gate should not teach otherwise |
| [00-foundation.html](00-foundation.html) | new **§C.9** (matrix, typing, payload) and **§D.6** (shield stack + cascade) |
| [00-foundation.html §C.1](00-foundation.html) | its matchup readout shows three sample pairs from one table; now points at §C.9 for the real component |

---

## 6. Design-gate checklist

```
[x] I identified the subsystem(s) this touches — shield, element, combat, derived stats.
[x] I read every doc in the §1 row(s) this session: DESIGN-GATE.md, element-hub-ssot.md,
    shield-system-spec.md (matrix/policy rows), actor-hub-ssot.md §3.E, plus all ten source files.
[x] I checked decisions.md for a lock covering this (Game GUI row; shield program rows).
[x] Every factual claim cites file:line.
[x] I verified claims against CODE, not comments — including the ShieldElementMatrix comment
    asserting 36-pair identity, which I confirmed independently at ElementTable.Shipped().
    That check overturned my OWN audit finding, which is recorded in §0 rather than quietly fixed.
[x] I read the surrounding section of every rule I quoted.
[~] I tested (not assumed) any constraint I am reporting. PARTIAL: no test suite was run. The §2
    cascade was COMPUTED from the shipped formula (ShieldMath.cs) rather than observed from a
    running engine — the arithmetic is reproducible, the execution is not claimed.
[x] Nothing contradicts a §2 invariant.
[~] Corrections propagated. Audit A27 is corrected and plate §C.9/§D.6 land with this document.
    DESIGN-GATE.md §1's Elements row is FLAGGED in §5, not edited — it is the gate's own file and
    sharpening a binding rule is the owner's call.
```
