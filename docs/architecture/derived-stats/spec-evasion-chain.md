# Spec — `evasion-chain`

**Program:** `derived-stats` · **Map:** [../derived-stats-map.md](../derived-stats-map.md)
**Depends on:** `element-families` · **Parallel with:** `mitigation-chain`
**Status:** Spec — awaiting review. Not built.

---

## 1. Objective

**Add parry and block without authoring a second saturation curve.**

Eight channels, four `Contest` pairs, all **role-inverted** — the *defender* owns the half that raises
an outcome:

| Defender raises | Attacker suppresses | Contest over |
|---|---|---|
| `block.rate` | `block.break` | is the hit blocked |
| `block.strength` | `block.shred` | how much a block removes |
| `parry.rate` | `parry.break` | is the hit parried |
| `parry.strength` | `parry.shred` | how much a parry removes |

**Q6 is why this module is not trivial.** `block.strength ↔ block.shred` is arithmetically identical
to the shipped `shield.toughness ↔ shield.pen`. Two curves for one shape is how three level curves
shipped simultaneously.

### 1.1 Boundary with A8 — reconciled 2026-08-24

[action/spec-defence-actions.md](../action/spec-defence-actions.md) (A8) previously called its reaction
shape **block**, while this module specifies `block.*` as a passive stat. Same word, two mechanisms,
found before either shipped. **A8's category is now `guard`; `block` and `parry` are stats.**

| | **Block / parry** (here) | **Guard** (A8) |
|---|---|---|
| What | Stat contest on incoming hits | An **action** the defender chooses |
| Cost | None — passive | `stamina` / `WReact`, cooldown |
| Decided by | The build | The player |

**They compose rather than compete.** A guard action grants a timed buff that raises
`combat.defense.*`, `status.resist.*` **and `block.rate` / `parry.rate` themselves** — so a guarded
actor blocks more often *because* they guarded. This module owns the roll and the contest; A8 owns the
choice that feeds it.

**No identifier here is named `guard`, and none in A8 is named `block` or `parry`.** Both specs assert
it, because a vocabulary collision that already happened once will happen again.

---

## 2. The helper, extracted from shipped code

[shield-system-spec.md](../shield-system-spec.md) §2.4 already contains the general shape, specialised
to shields:

```text
breakerDelta   = pen(attacker) − toughness(owner)
damageToShield = clamp(input + elemMod + hitCount × breakerDelta,
                       ceilPm(ChipFloorKPm × input),      // ≥ 0.10 × input
                       PenCapKPm × input / 1000)          // ≤ 3 × input
```

Generalised — **the same arithmetic, no new maths:**

```text
ClampedContest(base, attackerSide, defenderSide, hitCount, floorKPm, capKPm) =
    clamp(base + hitCount × (attackerSide − defenderSide),
          ceilPm(floorKPm × base),
          capKPm × base / 1000)
```

| Caller | base | attacker | defender | floor / cap |
|---|---|---|---|---|
| shield (existing) | `input + elemMod` | `shield.pen` | `shield.toughness` | `100` / `3000` — unchanged |
| block (new) | authored block amount | `block.shred` | `block.strength` | `0` / **`950`** |
| parry (new) | authored parry amount | `parry.shred` | `parry.strength` | `0` / **`950`** |

**Extracting the helper must not move a single shield golden.** The shield call site passes exactly its
current constants; refactor and behaviour change stay separate, per T7. `git status tests/` clean is
the proof, and it is the first acceptance criterion.

### 2.1 The bound follows the **status** precedent, not the shield's — owner decision 2026-08-24

The shield's floor exists for a reason that **does not transfer**: a shield is a *pool*, and the chip
floor guarantees it always spends, making permanent immunity impossible by construction. A parry or
block has no pool, so there is nothing to protect from non-spending. **Block and parry take no floor
(`0`)** — a fully shredded block doing nothing is a legitimate outcome of a contest.

What they do need is a ceiling, and the shipped precedent for *"mitigation may not reach total"* is the
status system, not the shield:

> `StatusPolicy.CategoryResistCap = 0.95` — category resist caps at 0.95 before sum
> ([status-ssot.md](../status-ssot.md) §6, `data/tuning/status.v1.json`).

**Block and parry cap at `950‰` — a block removes at most 95% of a hit, never all of it.** Same number,
same meaning, and a quantity the team already reasons about, rather than a second unrelated asymmetry
to learn.

Two properties this preserves:

1. **Immunity stays impossible** — the shield achieves it with a floor on damage, this achieves it with
   a ceiling on mitigation. Same guarantee, expressed on the side that has a pool to protect.
2. **Neither stat is capped.** `block.strength` and `block.shred` both scale with `Θ` without limit.
   What is bounded is the **fraction of one hit** a single exchange may remove — a **bounded ratio,
   PS-8 exempt, and the comment must say so**, since a bare `950` is exactly what §11's sweeps kept
   missing.

**Own tuning keys, shared default.** `blockCapPermille` and `parryCapPermille` in
`data/tuning/combat.v1.json`, both `950`. Separate keys mean a later balance pass can diverge them with
a file save rather than a code change — and never by editing the status system's constant, which they
merely *agree with* today rather than depend on.

---

## 3. Resolution — one roll, cumulative bands (attack table)

**Decided 2026-08-24 after surveying how shipped games do this.** An earlier draft rolled parry and
block as two *additional* draws after the hit roll. That works mechanically — Diablo 2 does exactly
that, sequencing defense → block → dodge/avoid/evade — but it costs one `SeededRng` draw per layer,
and **every added draw shifts the stream for everything downstream**, which would have made
`mitigation-chain`'s "zero goldens" claim depend on module landing order.

The World of Warcraft **attack table** solves it: compile every outcome into one cumulative list and
roll **once**.

```text
r = one draw   ← the hit roll the pipeline ALREADY makes. No new draw.

  r < p_miss                              → miss
  r < p_miss + p_parry                    → parried   → parry.strength contest removes damage
  r < p_miss + p_parry + p_block          → blocked   → block.strength contest removes damage
  otherwise                               → clean hit
      └─ mitigation chain (penetration/defense) ← spec-mitigation-chain.md
           └─ crit, amplification, shields, Funnel
```

Three properties, and the first is why this shape was chosen:

1. **Zero additional RNG draws — the stream is byte-identical.** No ordering constraint against
   `mitigation-chain`, no short-circuit special case, and the parallel build order in the map stays
   valid.
2. **At defaults the bands are empty**, so `p_parry = p_block = 0` and every outcome matches today
   **by arithmetic, not by a guard clause**. A guard clause is the thing that rots.
3. **Stacking avoidance composes**, because all of it works on one roll rather than compounding
   independent survivals — the property WoW's own documentation calls out, and the reason its
   un-hittable threshold is a single cumulative number.

### 3.1 The band total needs its own bound — and it is the same `0.95`

With one roll, `p_miss + p_parry + p_block` reaching `1.0` makes an actor **literally untouchable**.
That is a hard progression ceiling arriving through the back door.

> **The cumulative avoidance band caps at `950‰`.** An attack always retains at least a 5% chance to
> land. Same constant and the same reasoning as the magnitude cap in §2.1 and as
> `StatusPolicy.CategoryResistCap` — *"mitigation may not reach total."*

**Bounded ratio, PS-8 exempt, comment required.** `parry.rate` and `block.rate` themselves stay
uncapped magnitudes; what is bounded is the **share of one roll** they may occupy. Own key
`avoidanceBandCapPermille` in `data/tuning/combat.v1.json`.

### 3.2 Why these are still `Contest`, not `Feeder`

Parry and block resolve *before* mitigation, which by the taxonomy's rule would make them `Feeder`.
They are not: they do not scale a quantity `defense` later answers — they **remove** damage on their
own contested band. Each has a live opponent-side stat, which is the definition of `Contest`, and each
carries its own pair. Stated because "before mitigation ⇒ inherits" is the rule and this is its one
legitimate exception shape.

---

## 4. Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Shield|FullyQualifiedName~Parry|FullyQualifiedName~Block"
dotnet test tests\FusionRpg.Core.Tests
.\scripts\guard-stat-pairs.ps1
python scripts\audit-magic-numbers.py --domain combat
```

---

## 5. Project structure

| Path | Change |
|---|---|
| `src/FusionRpg.Core/Combat/ClampedContest.cs` | **new** — the §2 helper, permille `long` |
| `src/FusionRpg.Core/Combat/Shield/ShieldMath.cs` | call the helper; **constants and behaviour unchanged** |
| `src/FusionRpg.Core/Combat/OverlayCombatCalculator.cs` | parry and block stages (§3) |
| `data/tuning/combat.v1.json` | `blockCapPermille` / `parryCapPermille` = **950**, no floor (§2.1); `avoidanceBandCapPermille` = **950** (§3.1) |
| `docs/architecture/combat-damage-ssot.md` §6 | the resolution order |

---

## 6. Testing strategy

### 6.1 The refactor must be invisible

| Test | Asserts |
|---|---|
| **`ShieldGoldensByteIdentical`** | `ShieldMath` through the helper produces identical output. **Run before parry/block exist** — a two-step landing, not one |
| `HelperMatchesShieldMathExactly` | Property test over the existing shield golden matrix |

### 6.2 The new mechanics

| Test | Asserts |
|---|---|
| **`NoExtraRngDraws`** | The hit path consumes **exactly one** draw with parry/block live — asserted on the `SeededRng` counter, not inferred (§3) |
| **`RateGoldensUnchangedAtZero`** | Empty bands → byte-identical outcomes. **Replaces the earlier draft's "rate goldens move once"** — with one roll they do not move at all |
| `BandsAreExclusive` | miss / parry / block / hit partition the roll; no outcome double-counts |
| `BandTotalCapsAt950` | Maximal avoidance still leaves ≥5% chance to land — **untouchable is unreachable** (§3.1) |
| `ParryShortCircuits` | A parried hit ends resolution — no block, no mitigation |
| `BlockSubtractsBeforeMitigation` | Order per §3 |
| `BreakAnswersRate` · `ShredAnswersStrength` | Each pair cancels at equality |
| `CapIsNinetyFivePercent` | A maximal block removes `950‰` of the hit and **never 100%** — immunity impossible (§2.1) |
| `NoFloorOnProcs` | A fully shredded block removes **zero** — legitimate, not clamped up to a floor (§2.1) |
| `CapIsARatioNotACeiling` | `block.strength` and `block.shred` scale past any literal; only the **fraction removed** is bounded (§2.1) |
| `BlockCapIndependentOfStatusCap` | Editing `blockCapPermille` does not touch `CategoryResistCap`, and vice versa — they agree, they do not share |
| `RollsAreDeterministic` | Same seed → same outcomes; **stream order unchanged from `main`** |
| `ChipFloorStillPreventsImmunity` | The shield invariant survives extraction: a shield always spends |

---

## 7. Boundaries

**Always** — reuse `ClampedContest`. Land the refactor and the new mechanics as **two separate steps**.
Comment both caps as bounded ratios with their PS-8 class. **Resolve on one roll** (§3).

**Ask first** — moving either cap off `950`. That is a balance decision in a versioned file, not a
structural one.

**Never** — add an RNG draw to the hit path (§3). Let the avoidance band reach `1.0` (§3.1). Fork the saturation curve (Q6). Change shield behaviour while extracting the helper. Let
`block.*` read `ShieldElementMatrix` — block is not a shield
([spec-element-families.md](spec-element-families.md) §2.3). Cap `block.shred`/`parry.shred`; the
*bounds* are on the exchange, not the stats.

---

## 8. Success criteria

- [ ] `ClampedContest` extracted; **shield goldens byte-identical**, proven before parry/block land.
- [ ] Eight channels live, four `Contest` pairs, `role` marking the defender as the raising side.
- [ ] **One roll, cumulative bands — zero additional RNG draws**; parry short-circuits.
- [ ] Band total caps at `950‰`; an attack always has ≥5% chance to land.
- [ ] Cap is `950‰`, no floor; PS-8 comment present; **no magnitude capped**.
- [ ] Rolls deterministic; **stream order identical to `main`, so no golden moves at defaults**.
- [ ] `audit-magic-numbers.py --domain combat` clean.

---

## 9. Open questions

**None.** The bound question was decided 2026-08-24 (§2.1): follow the **status** `0.95` precedent
rather than the shield's floor/cap asymmetry, in their own tuning keys. Moving either value later is a
file save in a versioned config, not a refactor.
