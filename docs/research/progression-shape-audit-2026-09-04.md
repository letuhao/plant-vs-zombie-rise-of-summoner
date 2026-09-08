# Progression-shape audit (2026-09-04)

**Question asked (owner):** *"did you confuse arithmetic progression and linear progression? need audit
whole repo? maybe other power ladder, xp progression have this confuse and implement wrong?"*

**Answer: no confusion found — not in the analysis, and not in the codebase.** The distinction is
stated correctly in at least four independent places in `src/`. Two secondary defects surfaced while
looking, both recorded in §4.

---

## 1. The distinction being audited for

An arithmetic progression **is** linear in its index — those are the same property, so there is nothing
to confuse between them. The error that actually happens is **sequence vs cumulative**:

| per-step values | shape of the step | shape of the running total |
|---|---|---|
| constant (5, 5, 5, 5) | constant | **linear / arithmetic** |
| arithmetic (5, 10, 15, 20) | **linear in t** | **quadratic (triangular) in t** |

Getting this backwards inverts a design silently: it is invisible at low index and only appears as a
balance drift much later. That is what this audit looked for.

---

## 2. Inventory rows verified against implementation

`ssot-power-scale.md` §10 is the closed list of power-shaped scales. Rows checked in code:

| # | Claimed shape | Implementation found | Verdict |
|---|---|---|---|
| — | `P(Θ) = C + A·Θ + B·Θ(Θ−1)/2`, quadratic | `PowerLadder.ValueMilli` — exactly that, exact integer, and the triangular term halves the even factor **before** multiplying so `checked` cannot trip on an intermediate the real result would fit | ✅ |
| 1 | `BaseHp/BaseAtk/BaseDefense(level)` — *"becomes P(Θ)"* | **Migration done.** Now `PowerLadder`/`ChannelLadder` `.Value(level)` | ✅ |
| 2 | `BaseAccuracy/Dodge/CritRate/CritResist` — *"becomes Θ"* (contest, PS-3) | Linear in Θ (`220 + 26·theta`, `26·theta`, `10·theta`, `10·theta + 250`) and deliberately **not** routed through the ladder | ✅ |
| 6 | `XpToNext` — arithmetic | `first + (level − 1) * step` | ✅ |
| 7 | Affix tier ladder — geometric `m₁ × ratio^(t−1)` | `FamilyExpansion.TierLadder` — iterative multiply-divide, one step at a time to bound the intermediate | ✅ |
| 13 | `PowerScalar.Of` — geometric mean, display only | `(vᵢ+1)` clamped at 1 so a negative category cannot flip the product's sign | ✅ |
| 19 | Action rung — `min(earnCount, rungCap)` | `Math.Min(earnCount, tuning.RungCap)` | ✅ |
| — | `ChannelLadder` — proportional `B_ch = B · pinCh / pinHp` | one exact `long` numerator over a fixed denominator, single end-rounding | ✅ |

**Row 2 is the strongest evidence that the distinction is understood**, because it is a deliberate
*refusal* to use the quadratic ladder, with the reason written next to it:

> *"Under B>0 both accuracy and dodge would grow quadratically, and so would their difference — the only
> thing the sigmoid sees — turning a fixed one-index gap into a dial-dependent blowout."*

That is PS-3 (contests read `Θ`, magnitudes read `P(Θ)`) enforced at the call site, not just asserted in
a document.

---

## 3. Every explicit shape claim in `src/`, checked

Six comments in `src/` make a load-bearing claim about a progression's shape. All six are accurate:

| Location | Claim | Verdict |
|---|---|---|
| `Power/PowerLadder.cs:19` | *"A + B·(Θ−1) grows linearly, so the total is triangular"* | ✅ **This is precisely the sequence-vs-cumulative distinction, stated correctly** |
| `Items/Drops/DropVolume.cs:11` | *"Volume is LINEAR in Θ, not quadratic. P(Θ) is quadratic (the triangular term)"* | ✅ Distinguishes them explicitly, and argues why volume must not follow power |
| `Stats/Aptitudes/AptitudeTuning.cs:6` | *"Contest is Θ-free (linear in the point share); Magnitude reads P(Θ)"* | ✅ |
| `Actions/Rungs/RungRow.cs:5` | exponent forms *"are never evaluated at runtime; a human computed them once"* | ✅ Correct **and** the right choice — no float in a shipped ladder |
| `Combat/ClampedContest.cs:6` | *"arithmetically identical"* | n/a — algebraic equality, not a progression claim |
| `Combat/OverlayCombatCalculator.cs:274` | *"arithmetically irrelevant"* | n/a — multiplication order, not a progression claim |

---

## 4. Two defects found while looking

### 4.1 XP is a floating-point magnitude on a persisted path ✅ FIXED 2026-09-04

```text
RpgProgression.cs:43   public static double XpToNext(string kind, long level)
RpgStore.cs:360,397    xp REAL NOT NULL DEFAULT 0
RpgStore.Progression.cs:226   Xp = r.GetDouble(1)
```

`CLAUDE.md`'s numeric rule is explicit: **`long` is "the default for every magnitude"**, and `double` is
*"never in a hashed or persisted path — non-deterministic across runtimes."* XP is a progression
magnitude, it is persisted as SQLite `REAL`, and it is read back through `GetDouble`.

**Severity: low today, and it grows on a schedule.** Cumulative XP is quadratic in level, so even at
level 100,000 the value sits near 10¹¹ — far inside `double`'s 2⁵³ exact-integer range — and `+`/`*` are
exactly specified by IEEE-754, so today's arithmetic is deterministic. **The risk is latent, not
absent:** the moment XP gains a multiplier (a % bonus, a rested-XP rate, an event modifier) the values
stop being integral, and `state.Xp >= need` becomes a comparison between accumulated fractions whose
result depends on summation order. That is the same shape as the defect §10 row 3 records — a curve that
was harmless *only* at the value it happened to be tested at.

**Fixed 2026-09-04.** `long` end to end — `XpCurveParams`/`XpAwardsTuning`, `RpgXpCurve.XpToNext` and
`TotalToReach` (both `checked`), `Award.Delta`, `RpgActorState.Xp`, `RpgXpApply.Apply`'s delta, the
`xp` and `delta` DTO/column types. Details:

- **The loader now refuses a fractional tuning value** instead of truncating it (`80` and `80.0` are
  both accepted; `80.5` is rejected as "not a whole number — XP is an integer magnitude").
- **No data migration is required, and no legacy save breaks.** `CREATE TABLE IF NOT EXISTS` never
  rewrites an existing database, so a returning player's `xp` column keeps REAL affinity holding whole
  values like `100.0`. The new `ReadXp` helper reads through `GetValue` + `Convert.ToInt64`, which
  accepts either storage class; only fresh databases get the INTEGER declaration.
- **Two fractional computations were found and made exact rather than cast away.**
  `RpgXpAwardMap` now rounds a scaled award once at its own boundary (`ScaledAward`, half away from
  zero — the direction `PowerLadder` already uses), so the future content-scale multiplier cannot leak
  a fraction into a persisted total. And `ExpeditionEndpoints` accumulated
  `XpPerBattleWon * XpMilli / 1000.0` **per battle** — now summed in milli-XP as `long` and divided by
  1000 exactly once at the award, which is CLAUDE.md's own "widen before multiplying, divide by 1000
  last" rule applied where it had been missed. That one was live, not latent.

**Verification:** 7,229 tests green (Core 6,304 · Data 723 · E2E 202); all of `src/` and all five test
projects compile.

### 4.2 §10 inventory location column has drifted ✅ FIXED 2026-09-04

The shapes are right; the addresses are stale.

| Row | Doc says | Actually at |
|---|---|---|
| 1 | `BattleModels.cs:61-63` | `BattleModels.cs:150-153` |
| 2 | `BattleModels.cs:73-76` | `BattleModels.cs:171-174` |
| 11 | `CombatPolicies.cs:10-12` | `Stats/Derived/CombatPolicies.cs` (moved) |
| 14 | `ssot-generation.md` §4.1 | `Items/IlvlTierLadder.cs` (now has a real implementation) |

**Fixed 2026-09-04** — all four addresses corrected in §10, and row 6 now records the `double` → `long`
type correction alongside its unchanged shape.

Low severity on its own, but §10 is the *anti-duplication clause* — *"a power-shaped number that is not
in this table does not have permission to exist."* A closed list whose addresses no longer resolve is
harder to audit against, which is exactly how a fourteenth curve got in last time.

---

## 5. Conclusion

| | |
|---|---|
| Progression-shape confusion | **none found** — 8 inventory rows and 6 explicit claims all verified |
| Does the codebase understand sequence vs cumulative? | **Yes, demonstrably** — `PowerLadder.cs:19` and `DropVolume.cs:11` each state it independently, and row 2 acts on it by refusing the ladder |
| Was the owner's tier sequence mis-analysed? | No. 10/15/25/40/60/85/115 has arithmetic *first* differences and constant *second* differences ⇒ quadratic thresholds, `req(t) = 10 + 2.5·t·(t−1)` |
| Open defects | §4.1 (XP as `double`, latent), §4.2 (doc drift, cosmetic) |

**A full sweep of every `f(level)` in `src/` was not performed** — this audit verified the closed
inventory plus every file that makes an explicit shape claim. A scale that is neither in §10 nor
self-documenting would not have been caught by either pass; that gap is what `scripts/audit-*.py`-style
tooling would close if it is ever worth automating.
