# Spec: `demon-metrics`

Module `demon-metrics` in the [seedsmith map](../seedsmith-map.md) §3b. Wave **D3**.
Depends on `metrics`, `motif-derive`. **Gates D4.**

Ideal: [seedsmith-demons-ideal.md](../seedsmith-demons-ideal.md); `A#` = its §6 audit.

**Status: APPROVED by the owner 2026-08-31. Authorized to build.**

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

**"Content" means any generated artifact** (owner, 2026-08-31) — an item, action, aspect, commander
effect or theme. A demon is uncovered only when it has *nothing*, and coverage is otherwise binary:
a demon with SOME content produces no finding at all, the same "silence is healthy" convention
`Coverage/EmptyPartition` already uses.

**⚠️ Corrected 2026-08-31, after building it:** an earlier draft of this section implied the finding
also names which kinds ARE present for a partly-covered demon. That would need a second finding
type (or a NOTE alongside every healthy demon), which is exactly the noise the "any artifact
counts" decision was chosen to avoid. What actually ships is narrower and still answers the useful
question: **the one GAP finding a zero-content demon produces carries `absentKinds` — every kind
that was checked and found missing** — so a reader sees *what to generate*, not merely that
something is wrong. There is no "present" side to report, because the only finding that exists is
for demons with nothing present. Collapsing coverage to a bare pass/fail boolean (no kind list at
all) would still have discarded this; carrying `absentKinds` is what "covered, and by what" reduces
to once the stricter multi-kind reading was rejected.

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
| `demonCount` | the `n` the figure was measured over — it varies between runs now (§2.2a) |
| `excludedTautological` | how much of the corpus could not be measured |
| `singleUseMotifs` | a motif used by exactly one demon is a private adjective, not a shared vocabulary |

A run where `excludedTautological` is most of the roster is not a failing corpus — it is a corpus
whose input is too thin to judge yet, which is a different fact and must read differently. Folding it
into one number would hide precisely the thing `basis` was introduced to expose.

**`loop = OPEN`.** Whether a sharing level is *good* has no machine answer — a tightly-themed roster
and an under-differentiated one produce similar numbers. So this reports and samples for review; it
never passes or fails. Giving it a verdict field would be the "mark its own homework" defect
`audit_open_loop_schema` refuses.

### 2.2a The roster is uncapped, so `n` is a measurement — audit S4, revised 2026-08-31

Audit S4 said this metric would run on **n=24** against machinery built for **1,438 item entries**,
and that a sharing figure from 24 entities is closer to an anecdote than a measurement.

**The premise is gone.** The owner removed the species cap on 2026-08-31: `Generate` now takes
`int? maxSpecies = null` meaning *no limit*, so every captured species becomes a demon and a PVZ
update that adds almanac entries adds demons. `n` is now **whatever capture coverage yields** — 84
eligible rows today (18 zombie + 66 plant), rising toward the game's ~904 types as spawn coverage
improves. See [`ssot-power-scale.md`](../power/ssot-power-scale.md) §11.10a.

**What that changes here, and what it does not.**

- **It does not change the design.** The metric was never threshold-based, so nothing needs
  re-tuning for a larger `n`.
- **It does change how the output must be read.** `n` is no longer a fixed design point to reason
  against, so **the metric reports `demonCount` alongside every figure**. A sharing number without
  its `n` is uninterpretable when `n` moves between runs — and it will move, every time capture
  coverage improves.
- **It removes the reason to distrust the figure.** At 84 and climbing, demons-per-motif is a real
  measurement rather than an anecdote.

§2.2 still reports `excludedTautological` and `singleUseMotifs` as **raw counts**, now for a
different reason: with `n` varying between runs, a ratio silently conflates "sharing improved" with
"the roster grew". Counts plus `demonCount` let a reader tell those apart; a ratio cannot.

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
| A demon with a commander effect but no aspect | **covered**, and the evidence names `aspect` as absent (§2.1) |
| Two runs over rosters of different size | each carries its own `demonCount`; the sharing figures are not comparable without it (§2.2a) |
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

**Both closed 2026-08-31.**

1. ~~What counts as "content"?~~ **DECIDED (owner): any generated artifact counts, and the finding
   carries a per-kind breakdown** — see §2.1. The metric ships non-gating, so the breakdown is its
   real output; a single boolean would throw away the part worth acting on.
2. ~~Should `singleUseMotifs` have a reporting threshold?~~ **DECIDED: report all.** A threshold
   would be a number invented to suppress output, and suppression hides precisely the failure the
   field exists to expose (A2's private-adjective case). If the list is long, that *is* the finding.
