# Seedsmith — `budget`

**Status:** Proposed 2026-08-23. Nothing is built.

The declarative target every metric measures against. P2's home: *a metric without a declared
target is an opinion.*

---

## 1. The problem it solves, stated precisely

Right now the expected number of uniques exists in three places and disagrees three ways:

| Source | Says | Status |
|---|---|---|
| `ssot-uniques.md` §5.33 | 20 (5 per rung band) | superseded, banner added |
| `authoring-fleet-plan.md` §2 | 300 (20 agents × 15) | never revised after the D2-scale decision |
| The corpus | 144 | shipped, owner-confirmed |

None of them is *wrong* in isolation; they were written at different times against different scope
decisions. The failure is that no artefact holds the current answer, so "are there enough uniques?"
cannot be answered mechanically — and a distribution metric run today would report a skew that is
really a documentation lag.

`budget` is the single artefact that holds the current answer for every kind, every dimension.

---

## 2. Derivation, not authorship — with conflicts surfaced

Owner decision (map §7.2): **derived, then corrected.** Hand-authoring 126 partitions' worth of
targets means holding every number in one head, and it buries the existing contradictions instead of
resolving them.

`budget derive` walks every SSOT, the fleet plan and the naming allocation, extracts every stated
count, and emits `budget.v1.json` with **conflicts preserved rather than resolved**:

```json
{
  "kind": "unique",
  "target": 144,
  "tolerance": { "under": 0, "over": 0 },
  "provenance": [
    { "value": 20,  "source": "ssot-uniques.md §5.33",        "status": "superseded 2026-08-23" },
    { "value": 300, "source": "authoring-fleet-plan.md §2",   "status": "stale — predates D2 scale" },
    { "value": 144, "source": "corpus + owner decision",      "status": "authoritative" }
  ],
  "conflict": false
}
```

A row where no source is marked authoritative gets `"conflict": true`, and **`metrics` refuses to
run distribution checks against a conflicted row** — reporting the conflict instead. A target nobody
has adjudicated is not a target, and measuring against it produces confident nonsense.

That refusal is the point of the whole module: it converts "these documents disagree" from something
discovered years later into a build output.

---

## 3. What a budget row declares

```
target      the expected count
tolerance   asymmetric: {under, over} — absolute or fractional
dimension   what the row is counting over (kind | role×frame | band×element | …)
rationale   one line: why this number
provenance  every source that stated a value, and its status
loop        closed | open  — whether a metric can verify its own fix (P3)
```

**Tolerance is asymmetric on purpose.** Being three uniques short of target and three over are not
the same event: short means a gap a player can feel, over means content nobody asked for but nobody
is hurt by. Symmetric tolerance forces one threshold to be wrong.

**Tolerance may be zero.** For an exact allocation — 8 roles × 18 partitions = 144 — any deviation
is a defect, and the tolerance says so rather than allowing drift.

---

## 4. Deriving targets that are not written down anywhere

Most dimensions have no stated target at all. Nobody ever wrote "how many plant footings should
exist". Three methods, in preference order — and the order matters, because the temptation is to
jump straight to the third.

**4.1 Stated.** A document says it. Cite and use. Highest confidence.

**4.2 Structural.** The target follows from an allocation that already exists. 18 unique partitions
× 8 allocated roles = 144, exactly; 5 set themes × 6 sets = 30. These are not estimates — they are
arithmetic on a committed allocation, and they carry zero tolerance because a deviation means the
allocation was not executed.

**4.3 Proportional.** Nothing states it and no allocation fixes it, so distribute a known total
across a dimension by a declared weight. Base types across roles: `budgetWeightMilli` already exists
and sums to 1000‰, so it is the natural splitter — the same weights that decide power decide volume,
which keeps the corpus consistent with itself.

Split integers with **largest-remainder apportionment**, not rounding, for the same reason as
`numerics` §9.2: `round(740 × weight)` across 15 roles does not sum to 740, and a target set that
does not sum to its own total is a bug factory.

Proportional rows carry **wider tolerance and `"derivation": "proportional"`**, because they are a
reasoned default rather than a decision. A metric may report them; it should not gate on them until
someone has looked. Silently gating on a number nobody chose is how a tool loses trust in one
afternoon.

---

## 5. Distribution shape, not just totals

A total is not enough. 144 uniques all in one rung band satisfies `target: 144` and is obviously
broken. So a row may declare a **shape** as well as a count:

```json
{ "dimension": "unique × rungBand",
  "shape": "uniform",
  "target": { "30": 40, "50": 40, "70": 24, "90": 40 },
  "evenness": { "pielouMin": 0.90 } }
```

`evenness.pielouMin` is the hook into analytics §1.2. Three honest notes about it:

- **Set it from measurement, not intuition.** Nobody can name a correct Pielou value in advance. The
  first run reports the number; the band gets set from what a healthy corpus actually scores.
- **Until then, `evenness` is measure-only.** Absent the key, the metric reports and never gates.
- **Pair it with richness.** Evenness across three of fifteen roles scores 1.0 and is still broken,
  so a shape row declaring `pielouMin` must also declare `minCellsOccupied`.

---

## 6. Versioning

`budget.v{n}.json`, same discipline as tier-bands. A target change is a deliberate, reviewable,
revertible act with a rationale attached — not a silent edit that makes yesterday's red build green.

`budget diff v1 v2` reports which targets moved and which findings that would create or clear, so
the consequence of a target change is visible before it lands. Changing the target to match the
content is sometimes exactly right and sometimes cheating; showing the diff is what keeps the two
distinguishable.
