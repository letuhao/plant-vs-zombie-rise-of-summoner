# Spec — `mitigation-chain`

**Program:** `derived-stats` · **Map:** [../derived-stats-map.md](../derived-stats-map.md)
**Depends on:** `element-families` · **Unblocks:** `reflection`
**Status:** Spec — awaiting review. Not built.

---

## 1. Objective

**Place four families in the damage pipeline, each on the side of mitigation its class requires.**

| Pair | Class | Side of mitigation |
|---|---|---|
| `penetration ↔ absorption` | `Contest` | **inside** the delta — it modifies `defense` itself |
| `amplification ↔ reduction` | `Contest` | **after** mitigation — a multiplier on final damage |

Both pairs are differences with **neither half capped**
([spec-stat-taxonomy.md](spec-stat-taxonomy.md) §2.2). Placement is not a style choice — it is what
decides whether a pair is owed at all (§2.3 of the taxonomy), and both of these earn theirs.

---

## 2. The pipeline, with the two insertions

[combat-damage-ssot.md](../combat-damage-ssot.md) §6.7 today, with additions marked:

```text
attackerPower(E)    = combat.power.omni   + combat.power.E
defenderDefense(E)  = combat.defense.omni + combat.defense.E

penDelta(E)         = penetration(E) − absorption(E)                        ← NEW
effectiveDefense(E) = defenderDefense(E) × pierceFactor(penDelta(E))        ← NEW

effectiveDelta(E)   = (attackerPower(E) − effectiveDefense(E)) + componentBonus(E)
weightedDelta       = Σ (w × effectiveDelta(E))
powerAdjustedDamage = baseDamage + weightedDelta
finalDamage         = max(0, powerAdjustedDamage)

if crit: finalDamage ×= critMultiplier_final
ampDelta            = amplification − reduction                             ← NEW (untyped sum over components)
finalDamage        ×= ampFactor(ampDelta)                                   ← NEW
```

### 2.1 `penetration` modifies defense; it does not add damage

This is the distinction that keeps the pair honest. Penetration **scales the defender's mitigation**,
so a target with no defense gains nothing from an attacker's penetration — which is what makes
`absorption` a real answer to it rather than a parallel damage knob.

`pierceFactor` is monotone, `1.0` at `penDelta = 0`, and **bounded in `(0, 1]` on the defense side** —
penetration can reduce defense arbitrarily close to zero but never below it. **That bound is structural,
not a cap:** negative defense would turn mitigation into amplification and give a second, unintended
damage source. Comment it as structural per PS-8.

### 2.2 `amplification` lands after crit, and order does not matter

Both `critMultiplier` and `ampFactor` are multipliers on `finalDamage`. **Multiplication commutes, so
the order between them is arithmetically irrelevant** — stated explicitly so it never becomes an
argument, and so nobody "fixes" it into a saturating form where it *would* matter.

`ampFactor` must therefore stay a **plain multiplier with no clamp**. A saturating `ampFactor` would
make order significant and would cap the attacker half of a `Contest` pair — both defects.

### 2.3 `amplification` is untyped

`penetration/absorption` are per-component, because defense is. **`amplification/reduction` apply once
to the already-summed final damage**, so they read `omni + Σ(w × element)` and produce one factor.
Applying them per component and then re-summing would double-count the weights.

---

## 3. R3 is **not this module's** — owned by `element-families`

Both copies of the *"Deferred from Chaos"* list — [combat-damage-ssot.md](../combat-damage-ssot.md) §5
and [element-hub-ssot.md](../element-hub-ssot.md) §6 — are retitled by
[spec-element-families.md](spec-element-families.md) §5, which found the second one.

Recorded here only so a reader of this module does not see §5 still calling `penetration` and
`absorption` "not in v1" and assume the contradiction is unowned. **One owner, named** — an earlier
draft had both modules claiming it, which is how a change ends up made by neither.

---

## 4. Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~OverlayCombat|FullyQualifiedName~Mitigation"
dotnet test tests\FusionRpg.Core.Tests
.\scripts\guard-stat-pairs.ps1
python scripts\audit-overflow.py
python scripts\audit-magic-numbers.py --domain combat
```

---

## 5. Project structure

| Path | Change |
|---|---|
| `src/FusionRpg.Core/Combat/OverlayCombatCalculator.cs` | the two insertions |
| `data/tuning/combat.v1.json` | `pierceScale`, `ampScale` — **shapes only; values chosen so both factors are identity at delta 0** |
| `docs/architecture/combat-damage-ssot.md` §5, §6.7 | R3 + the new order |
| `docs/architecture/element-hub-ssot.md` §6 | R3's second copy |

---

## 6. Testing strategy

| Test | Asserts |
|---|---|
| **`AllGoldensUnchangedAtZero`** | All four families at `0` → `pierceFactor = ampFactor = 1.0` → byte-identical. **This is the module's acceptance test** |
| `PenetrationNeedsDefenseToMatter` | Against a zero-defense target, penetration changes nothing (§2.1) |
| `AbsorptionAnswersPenetration` | Equal pen and absorption cancel exactly |
| `DefenseNeverGoesNegative` | Unbounded penetration asymptotes to zero defense, never inverts (§2.1) |
| `AmpCritOrderIrrelevant` | Applying amp-then-crit and crit-then-amp give identical output (§2.2) |
| `AmpIsUnclamped` | Arbitrarily large amplification keeps scaling — no ceiling (PS-8) |
| `AmpAppliedOnceNotPerComponent` | A 3-component payload gets one amp factor (§2.3) |
| `MatchupStillAppliedOnce` | `componentBonus` enters once — the new families do not read the matrix |
| `LongThroughout` | Permille intermediates widen before multiplying; overflow throws |

`AmpCritOrderIrrelevant` and `DefenseNeverGoesNegative` are the two that encode §2's reasoning as
something that can fail.

---

## 7. Boundaries

**Always** — widen before multiplying; divide by 1000 last, exactly once; let overflow throw. Every
new scale in `data/tuning/combat.v1.json`, never a literal.

**Ask first** — a balance value for either scale. This module ships **identity at delta 0**; tuning is
a separate pass (T7).

**Never** — cap `amplification` or `penetration` (attacker halves of `Contest` pairs). Let defense go
negative. Make `ampFactor` saturating (§2.2). Read the matchup matrix from the new families
([spec-element-families.md](spec-element-families.md) §2.2). Land a refactor and a rebalance together.

---

## 8. Success criteria

- [ ] Four families placed; each side of mitigation matches its class.
- [ ] **`git status tests/` clean** — identity at delta 0.
- [ ] Penetration provably requires defense to matter; defense never inverts.
- [ ] Amp and crit provably order-independent; amp uncapped and applied once.
- [ ] **Both** deferred lists retitled (§3).
- [ ] `audit-overflow.py` and `audit-magic-numbers.py` clean; no literal in the calculator.

---

## 9. Open questions

**None.** §2.2's commutativity is arithmetic, §2.1's bound follows from what a negative defense would
mean, and §2.3 follows from the weights already being applied once.
