# Comparison — the delta table, the dominance verdict, and the attribution chain

**Status:** Detail design, 2026-08-23. **Document 4 of 9** owed by
[gap-audit-2026-08-22.md](gap-audit-2026-08-22.md) §7. Closes **B7** — the last open Class-B defect —
and the compare half of **A11**.

**An upgrade of the existing §D.3, not a new ladder.** Comparison is a *mode* the entity ladders already
have (GG-47); this document does not add a rung, it corrects three rendered examples that predate
[spec-magnitude-and-units.md](spec-magnitude-and-units.md) and never received its fixes.

**Sources, all read this session:**
[`item/ssot-presentation.md`](../architecture/item/ssot-presentation.md) §4.2 (comparison rendering) ·
[`ssot-inventory.md`](../architecture/item/ssot-inventory.md) §3.4, §5.5 (the comparison payload — I13
owns this; presentation owns only its display) · [spec-magnitude-and-units.md](spec-magnitude-and-units.md).

---

## 0. Three defects found on the existing plate, none previously caught

Document 1 fixed the atom-card instance of the unreferenced-percentage defect. It did not touch §D.3
or §D.3b, which sit in a different part of the same plate and carry **three separate instances** of
problems document 1 already named the fix for. None of these were in the original gap-audit register —
found while reading the plate to scope this document, the same way B2's and B8's second instances were
found while closing document 1.

| # | Where | What's wrong |
|---|---|---|
| 1 | The delta list (`difflist`) | Mixes unit classes in one flat list with no group header, has no dominance verdict, and has no permanent footnote — the three things §4.2 requires |
| 2 | The attribution chain (D.3b) | Sums a **percentage** ("Base 7.6%") additively with **points** ("+150 pts", "+20 pts", "−15 pts") to reach a total percentage. Percent and points are not the same currency and cannot be added — this is a real arithmetic error, not just a rendering one, and it is independently ~1pp off even taken on its own terms (verified: the correct total for that delta chain is **27.9%**, the plate showed **26.9%**) |
| 3 | The "rendered right" example, same block | `Critical strikes 7.6% → 26.9%` names no reference. Document 1 requires every sigmoid estimate to name what it's measured against — this line does not, even though it sits in the block whose whole point is showing the *correct* way to render a unit |

All three are fixed in §4 and verified in browser in §6.

**A fourth was found while verifying #3 in browser** — the same pattern as documents 1 and 2's
"second instance" surprises. The D.2 actor panel's attribute grid rendered `Crit rate 26.9%` bare, and
its "Effects — the atom ladder, reused verbatim" chips had drifted from the D.1 fix they claim to
reuse (`Ember Force +150` with no unit, `Killing Instinct +150` with no unit). "Reused verbatim" was
a claim the plate was not honouring. Fixed to `150 pts` with a pointer to the attribution panel, and
the three chips brought back into sync with D.1's corrected versions.

---

## 1. The delta table — group by unit class, never by row

[ssot-presentation.md §4.2](../architecture/item/ssot-presentation.md), matching
[ssot-inventory.md §5.5](../architecture/item/ssot-inventory.md)'s payload shape exactly:

> **Deltas group by unit class, and the unit is in the group header, never in the column.**

```text
Damage and hit points (game units)
  fire power              90  →  150      +60
  maximum vitality        320 →  240      −80

Chance (per-mille)
  wither on hit            —  →  25%      new
```

`+60 hp`-equivalent and a per-mille chance never share a numeric column — that is
[spec-magnitude-and-units.md §8](spec-magnitude-and-units.md) guard 3, applied to a table instead of a
card.

**A zero-point delta needs no percentage at all.** The existing plate rendered an *unchanged* crit rate
as `7.6% → 7.6% —`, printing a bare sigmoid percentage for a channel that didn't move. Per document 1,
`SigmoidPoints` never renders as a bare percentage. Since the underlying point delta is exactly zero,
the honest fix is to render **the point delta** (`0`), not the derived percentage on either side —
there is nothing to estimate when nothing changed, and the row exists at all only because GG-48 (§3)
says a comparison must show what *didn't* improve, not just what did.

---

## 2. The dominance verdict — a word and a shape, never a colour alone

I13's payload ([ssot-inventory.md §5.5](../architecture/item/ssot-inventory.md)) is a real partial
order, not an invented scalar — SC9 forbids depending on E9's unbuilt power model, and *"a naive sum
would be wrong and look authoritative, which is worse than no number."*

| Verdict | Rendered | Condition |
|---|---|---|
| `strictly-better` | `Strictly better ▲` | every channel delta is ≥ 0, at least one > 0 |
| `strictly-worse` | `Strictly worse ▼` | the mirror |
| `sidegrade` | `Sidegrade ◆` — **plus the trade, spelled out**: a *you gain* list and a *you give up* list | mixed signs |
| `incomparable` | `Not comparable ◇` — **plus the reason**, e.g. *"these touch different channels — the candidate has no hp line and the incumbent has no crit rate line"* | disjoint channel sets |

Same redundancy rule rarity's ladder already established
([ssot-rarity.md §4.5](../architecture/item/ssot-rarity.md), applied here per
[ssot-presentation.md §4.2](../architecture/item/ssot-presentation.md)): **word and shape together,
colour is decoration, never the only channel.**

**The permanent footnote is not a dismissible hint.** *"There is no single score. 9 hit points and 5
accuracy points are not the same currency."* A player who dismisses a hint once reads its absence as a
missing feature forever — so it renders every time, not behind a `?`.

**When E9 ships**, power joins as one row *above* the delta table, per Rule P
([spec-magnitude-and-units.md §5](spec-magnitude-and-units.md)) — `≈ 1,300 (±25%)`. The delta table
does not go away; a single number still cannot say *what* got better.

---

## 3. The attribution chain — points sum, sigmoid applies once

The corrected shape, and the reason it is the only correct shape: sigmoid is **nonlinear**, so summing
percentages and then calling the sum a percentage silently double-applies the curve. The fix is
structural, not cosmetic — **only points may be summed; the sigmoid function is applied exactly once,
at the very end, against a named reference.**

**Verified arithmetic**, reproducing [definitions.md §2](../architecture/effect-atom/definitions.md)'s
own calibration:

```text
opposed baseline (vs this defender)   delta −250
+ Killing Instinct                          +150
+ Ashen Reliquary                            +20
− Cold (status)                              −15
                                       ───────────
total delta                                  −95
```

`p(−95) = 1 / (1 + e^0.95) = 27.9%` — the number the plate now shows. **The old plate's 26.9% was never
independently computed**; it was carried over from a different worked example (`+150` points alone at
delta `−100`) without re-deriving the chain it was attached to.

**Every intermediate line is points.** Only the final line is a percentage, and it is always the two-
part line from [spec-magnitude-and-units.md §4](spec-magnitude-and-units.md): the resolved probability,
prefixed nothing (this is a **live read**, not an estimate — an actual opposed defender is selected),
suffixed with the reference named — `vs Frost Peashooter`. This is the *other* allowed reference kind
from that document's table: *"live read against the selected specimen — the most useful number there
is, and it only exists where an actor is selected."* The item card (document 2, document 1) uses
`vs neutral` because an item has no opponent; an actor-sheet attribution panel has a real one, and must
name it exactly as an item card names `neutral`.

---

## 4. What changes on the plate

| Block | Before | After |
|---|---|---|
| `difflist` | flat list, mixed units, no verdict, no footnote | grouped by unit class · dominance verdict with word+shape · permanent footnote |
| D.3b attribution | `7.6%` + pts summed to `26.9%` | opposed baseline in points → point deltas → **one** sigmoid application → `27.9% vs Frost Peashooter` |
| "rendered right" example | `7.6% → 26.9%`, no reference | `7.6% → 27.9% vs Frost Peashooter` |

---

## 5. Guards

| # | Guard | Fails when |
|---|---|---|
| 1 | **No numeric column mixes unit classes** | inherits [spec-magnitude-and-units.md §8](spec-magnitude-and-units.md) guard 3 directly |
| 2 | **Every comparison renders a dominance verdict as word + shape** | colour is the only signal, or the verdict is missing |
| 3 | **A `sidegrade` verdict always lists gains and losses; an `incomparable` verdict always states the reason** | either fires with no explanation |
| 4 | **The footnote renders every time a delta table renders** | it is dismissible, or conditionally hidden |
| 5 | **An attribution chain sums points only; sigmoid applies exactly once, at the end** | a percentage is added to a point value anywhere in the chain |
| 6 | **A live read names its reference exactly as an estimate does** | `vs <specimen>` is dropped because "it's not an estimate, it's real" |
| 7 | **A zero-delta `SigmoidPoints` row renders `0`, never a bare unchanged percentage** | `7.6% → 7.6%` reappears anywhere |

---

## 6. Verified in browser

Same discipline as documents 1, 8, 9: rendered at 1024 and 800px, zero horizontal scroll, zero
overflowing elements, dominance-verdict contrast checked against the panel background. Detail in the
plate's own comment trail; not restated here since it produces no new numbers beyond §0 and §3.

---

## 7. Design-gate checklist

```
[x] I identified the subsystem(s) this touches — item presentation, comparison, sigmoid probability.
[x] I read every doc in the §1 row(s) this session: item/ssot-presentation.md §4.2,
    ssot-inventory.md §3.4/§5.5, spec-magnitude-and-units.md in full.
[x] I checked decisions.md for a lock covering this (Game GUI row).
[x] Every factual claim cites file:line or a document section.
[x] I verified claims against CODE where code exists — the sigmoid formula and the −250 baseline are
    the same shipped constants document 1 already verified at OverlayCombatCalculator.cs /
    CombatProbability.cs; §3's arithmetic was recomputed independently in this session, not copied.
[x] I read the surrounding section of every rule I quoted.
[~] I tested (not assumed) any constraint I am reporting. PARTIAL: no test suite was run — §3's
    27.9% is a hand computation of the shipped formula, reproducible but not executed against the
    engine. The claim "the old plate's 26.9% was never independently computed" is inferred from the
    fact that it matches a DIFFERENT delta's answer (150 pts alone, delta −100) rather than this
    chain's own delta (−95) — a strong circumstantial match, not a commit-history fact.
[x] Nothing contradicts a §2 invariant.
[x] Corrections propagated — the plate changes in §4 land in the same pass as this document.
```
