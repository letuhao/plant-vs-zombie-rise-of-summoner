# Spec: `demon-metrics`

Module `demon-metrics` in the [seedsmith map](../seedsmith-map.md) §3b. Wave **D3**.
Depends on `metrics`, `motif-derive`. **Gates D4.**

Ideal: [seedsmith-demons-ideal.md](../seedsmith-demons-ideal.md); `A#` = its §6 audit.

**Status: proposed 2026-08-31, awaiting owner review. Not authorized to build.**

---

## 1. Objective

Two checks that answer the question the rest of the feature cannot: **is the taxonomy real structure,
or does it merely look like structure?**

This module gates D4 for that reason. Without it, `demon-themes` would generate content from a
family/motif graph nobody has verified — and both failure modes below are invisible to every check
seedsmith already has.

**Done means:** per-demon coverage exists alongside per-family coverage, and motif sharing is measured
in a way that a tautology cannot pass.

---

## 2. Design

### 2.1 `Coverage/DemonUncovered` — per-demon, because per-family is not enough

Audit A5:

> `families[]` is multi-valued. If a demon belongs to three families, **a handful of multi-family
> demons can satisfy every partition** while most of the roster gets no content — and coverage
> reports green.

Family coverage answers *"is every family represented"*. That is a real question, and a **different**
one from *"does every demon have content"* — which is the one a player notices.

So: a finding per demon that has no generated content, regardless of how well its families are
covered. `Coverage/EmptyPartition` keeps doing its job unchanged; this sits beside it.

**Severity `GAP`, `loop = CLOSED`, `gates = False` on ship** — the program's standing rule is that
new metrics ship non-gating and promotion is a separate, later act.

### 2.2 `Distribution/MotifSharing` — and the exclusion that makes it honest

Audit A2, the sharpest finding in the ideal:

> With thin text, **both** motifs and families derive from the same string — the name. Every "-nut"
> demon gets nut-ish motifs *and* the nut family, and inheritance appears to work beautifully. It is
> a tautology: one token read twice. **And every metric reports success** — sharing looks high,
> families look populated, coverage looks complete.

The property worth measuring is **demons-per-motif**, not vocabulary size. Q7 settled that already,
by the repo's own precedent: *"what the audit was right about survives as a metric, not a cap"* —
because a 40-motif cap is satisfied perfectly by 40 motifs each used once, at which point families
inherit nothing and the coherence mechanism is decorative.

**The exclusion is what makes the metric mean anything.** A demon whose motifs **and** families are
both `basis = "name"` contributes no information about sharing — its agreement is arithmetic, not
evidence. `motif-derive` flags exactly this case (§2.4 of that spec), and this metric excludes those
demons from the numerator and denominator both.

Reported, always, in the finding's evidence:

| Field | Why it is reported rather than folded in |
|---|---|
| `demonsPerMotif` | the measured property |
| `excludedTautological` | how much of the corpus could not be measured |
| `singleUseMotifs` | a motif used by exactly one demon is a private adjective, not a shared vocabulary |

A run where `excludedTautological` is most of the roster is not a failing corpus — it is a corpus
whose input is too thin to judge yet, which is a different fact and must read differently. Folding it
into one number would hide precisely the thing `basis` was introduced to expose.

**`loop = OPEN`.** Whether a sharing level is *good* has no machine answer — a tightly-themed roster
and an under-differentiated one produce similar numbers. So this reports and samples for review; it
never passes or fails. Giving it a verdict field would be the "mark its own homework" defect
`audit_open_loop_schema` refuses.

### 2.2a ⚠️ n=24 bounds what this metric can resolve — audit S4

seedsmith's distribution machinery was built against **1,438 item entries**. The demon roster is
**24** (`DemonSpeciesGenerator.DefaultMaxSpecies`). With perhaps 5–8 families that is 3–5 demons
each, and after §2.2's tautology exclusion it may be fewer.

**The metric still earns its place** — it catches the catastrophic case where every motif is private,
which is the failure it exists for. But a sharing figure from a 24-entity corpus is closer to an
anecdote than a measurement, and it must not be read as a balance signal. Raising the roster size is
a demon-program decision, not this feature's.

This is why §2.2 reports `excludedTautological` and `singleUseMotifs` as raw counts rather than a
ratio: on n=24 the counts are legible and a ratio is spurious precision.

### 2.3 What this module does not do

It does not measure whether motifs are *well chosen* — that is taste, and it is `lore-enrich`'s
review queue. It does not gate. It does not fix. It reports two facts nothing else can see.

---

## 3. Commands

```powershell
cd tools\seedsmith
python -m pytest tests/test_demon_metrics.py -q
python -m seedsmith report --adapter demons
python -m pytest -q
```

---

## 4. Project structure

```
tools/seedsmith/seedsmith/metrics/
    demon_coverage.py      → Coverage/DemonUncovered
    motif_sharing.py       → Distribution/MotifSharing
tools/seedsmith/tests/test_demon_metrics.py
```

**They live in `metrics/`, not in `adapters/demons/`** — deliberately. Per-entity coverage and
vocabulary sharing are generic properties; items will want both. The adapter supplies the strata, the
metric does the counting. Putting them under the adapter would make a general check demon-shaped by
accident, which is the same reasoning that moved `provenance-supersede` to core backlog.

---

## 5. Code style

Follow `metrics/coverage.py` exactly: a `Metric` subclass with `id`, `family`, `loop`, `gates`,
`needs`, `covers`, and a `run(ctx)` returning typed `Finding`s. Sorted iteration everywhere —
`FlavourGeneric` was bitten live by unsorted set iteration producing set-equal but list-unequal
output across runs.

---

## 6. Testing strategy

| Case | Expect |
|---|---|
| Every demon has content | `Coverage/DemonUncovered` reports **nothing** |
| One demon uncovered, its families all covered | **one finding** — this is A5's exact case, and the test states it as such |
| Motifs each used by one demon | `singleUseMotifs` equals the vocabulary size; sharing reported as absent |
| Motifs shared across a family | `demonsPerMotif` > 1 |
| A demon with motifs and families both `basis = "name"` | **excluded** from the sharing calculation, and counted in `excludedTautological` |
| A corpus that is entirely tautological | reports "cannot be measured", **not** perfect sharing — the failure A2 describes, asserted directly |
| `MotifSharing` schema | carries **no** pass/fail field (`audit_open_loop_schema`) |
| Both metrics on ship | `gates = False` |
| Findings ordering | stable across runs, byte-identical |

The "entirely tautological" row is the one that proves the module. Without it, a corpus derived
wholly from names would report flawless sharing and this metric would be worse than absent — it would
be reassuring.

---

## 7. Boundaries

- **Always:** report `excludedTautological` alongside the sharing number; keep both metrics in
  `metrics/`; ship `gates = False`; sort before emitting.
- **Ask first:** promoting either to `gates = True`; folding the exclusion count into the sharing
  number; adding a threshold.
- **Never:** give `MotifSharing` a pass/fail field; count a tautological demon as evidence of
  sharing; report per-family coverage as if it answered per-demon coverage.

---

## 8. Success criteria

1. A demon with no content is found even when all its families are covered (A5).
2. A wholly tautological corpus reports "cannot be measured", not success (A2).
3. `MotifSharing` has no verdict field.
4. Both metrics ship non-gating.
5. Deterministic finding order.
6. Both live in `metrics/` and work for a non-demon adapter that supplies the same strata.

---

## 9. Open questions

1. **What counts as "content" for per-demon coverage?** An aspect? Any generated artifact? A demon
   with a commander effect but no aspect is partly covered, and whether that reads as covered or as a
   gap depends on what the player sees.
2. **Should `singleUseMotifs` have a reporting threshold?** Reporting every one is noisy on a small
   roster; suppressing them hides the failure. Wants measuring rather than choosing.
