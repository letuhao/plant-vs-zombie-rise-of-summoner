# Spec: coverage-report (A-S5)

**Module id:** `coverage-report` · **Program:** [action-corpus](../action-corpus-map.md) §4 · **Build order:** 6 of 7 model-free
**Status: proposed 2026-09-03.** Written against the capability map; no build authorized until the map is approved.
**Model calls: none.** Every metric here is arithmetic over committed files.

It owns the measurement of the accepted corpus and the derivation of the next round's targets. Its one
non-negotiable rule is the reason it is built before the expensive stage: **every metric declares
closed-loop or open-loop, and an open-loop metric never contributes to a pass.** *An open-loop metric
that contributes to a pass verdict is a lie with a checkmark on it.* Coverage is reported before it is
claimed — no round declares success against a metric it did not evaluate.

## The four constraints this module is bound by (map §3, restated inline)

1. **Seeds, not a cartesian.** An atom names a **pool**; element, tier and cell resolve at layer 4, per
   player, at roll time. **A cell is a target, never an identity** — so cell counts are not a coverage
   axis, and a "thin cell" here means a thin *mechanical* cell, never a lawn cell.
2. **Small-batch proof before any full run.** The call budget is a **ceiling, not a plan**; a full run
   is an owner decision behind a quality gate, and **this module's report is that gate's evidence.**
   It must therefore be able to report honestly on 12 rows without pretending they are a corpus.
3. **The roster is 84 species, not 904.** So the denominators here are 84 species, 19 families and 53
   family-assigned species — measured, not projected. The research band (1,500-3,500 named abilities
   for a ~900-unit roster) was derived for a 904 roster and **must be re-derived against the shipped
   one** before any occupancy verdict cites it.
4. **C1's family-access widening is gated.** Until its three preconditions hold, every tier draws from
   the same atom-family set, so cross-tier similarity is expected. This module reports the rate and
   does not call it a defect.

## 1. What exists today

### Built

| Thing | Evidence |
|---|---|
| `Metric` / `Finding` / `Loop` / `Severity` / `Ctx`, with `loop`, `gates`, `needs` and `covers` as first-class fields | `seedsmith/metrics/model.py:26-49`, `:64-99` |
| **`gates` starts `False` for every new metric**; promotion is a deliberate, later, separate act | `metrics/model.py:85` and its comment at `:8-9` |
| A metric whose `needs` are unmet reports `NOT_MEASURED`, never a pass — silence and success stay distinguishable | `metrics/model.py:10-11`, `Severity.NOT_MEASURED` at `:34` |
| An existing coverage/dedup/distribution metric family to model on | `metrics/coverage.py`, `metrics/dedup.py`, `metrics/distribution.py`, `metrics/corpus_coverage.py` |
| The runner and its `--gate` / `--json` surface | `report/cli.py:154` (`cmd_report`), flags at `:778-780` and `:793-795` ⛔ **corrected 2026-09-03: `:728-741` is `_cmd_demons_diff_legacy`, not the runner** |
| The in-game closed-loop pairing assertion this report mirrors | `EnablerPayoffCoverage.cs:21-34` |
| The rung table the rung-band axis is indexed against | `data/tuning/action-rungs.v1.json` |

### Real gap

No action-corpus metrics exist, and no next-round target derivation exists.

## 2. Inputs and outputs

**Reads:** the accepted corpus (A-S3's survivors plus everything already under `data/seed/actions/`) ·
`role-lean.json` and `type-weights.json` (the plan's intent, so "thin" can mean "thin against quota"
rather than "thin against nothing") · the round's briefs · `data/tuning/action-corpus-run.v1.json`.

**⛔ DECIDED 2026-09-03 — `action-corpus-run.v1.json` ships with stated neutral defaults, and the
default is `mode: "smoke"`.** The file is authored in `spec-distribution-planner.md` §3 step 1, which
carries the full shape and the reasoning in its `_meta`; this module reads `mode`, the three counts
and nothing else. Two consequences belong here rather than there:

- **The smoke batch is this report's first real input.** The default is `mode: "smoke"`,
  `perSpeciesCount: 1` over the **8** species in the four-way catalog/motif/family/anchor join
  (`spec-characteristic-pool.md:192`) — the natural smoke size, because those 8 are the only species
  carrying every signal A-S0 derives, so a thin result is the pipeline's fault and not the data's.
- **Every finding this report produces over that batch is the evidence the counts are re-tuned on**,
  and re-tuning is a config change. §5's *"small batch honesty"* case already refuses to call a
  12-row batch a corpus-level pass; that refusal is what makes the neutral default safe to ship.

**Writes** `data/seed/actions/_reports/coverage-round-<n>.json`, `kind: "action-coverage"`:

```jsonc
{
  "schemaVersion": 1, "kind": "action-coverage",
  "_meta": { "partition": "round-1", "corpusHash": "...", "roster": { "species": 84, "families": 19, "familyAssigned": 53 } },
  "entries": [
    { "id": "cell.species.attack.5-10.enabler",
      "scope": "species", "category": "attack", "rungBand": [1,10], "pairingRole": "enabler",
      "count": 3, "quota": 7, "thin": true },
    { "id": "target.round-2.species.cherrybomb",
      "kindOfEntry": "next-target", "scope": "species", "scopeKey": "cherrybomb",
      "category": "defense", "want": 2, "because": "cell.species.defense.5-10.neutral is thin" }
  ]
}
```

**The metric register**, each with its loop declared up front:

| Metric id | Loop | Gates? | What it asserts |
|---|---|---|---|
| `action.corpus.cellOccupancy` | **CLOSED** | no (starts false) | every planned cell has at least one accepted row |
| `action.corpus.thinCell` | **CLOSED** | no | a cell's count is below its quota; names the shortfall |
| `action.corpus.quotaDrift` | **CLOSED** | no | accepted per-category counts match A-T1's weights within the largest-remainder tolerance |
| `action.corpus.enablerPayoffCoverage` | **CLOSED** | no | every accepted **payoff atom family** has an accepted enabler **atom family** in the same anchor — the corpus-side twin of `EnablerPayoffCoverage.cs:21-34` |
| `action.corpus.pairingReach` | **CLOSED** | no | how many accepted rows carry `pairingRole: "none"`, and what share of the corpus's atom families could reach a payoff key at all — the honest denominator behind the metric above. ⛔ **The denominator is 98** (2026-09-03), the authored affix families; see the namespace note under this table |
| `action.corpus.atomFamilyNamespace` | **CLOSED** | no | every accepted row's `atomFamilies` and `pairedPayoffFamily` id is an `entries[].id` of `data/seed/items/affix-families/*.json`; an id from the 17 fixture families under `data/seed/atoms/`, or one that resolves nowhere, is a finding that names the row and the id |
| `action.corpus.speciesCollision` | **CLOSED** | no | two species whose signature sets are tier-2 identical — the named re-tune trigger for the per-species count |
| `action.corpus.singletonShare` | **CLOSED** | no | median rows per mechanical cell and the singleton share, against the research target of median 1 and ~68% singletons |
| `action.corpus.structureEnforceability` | **CLOSED** | no | how many accepted rows spend **`restriction`**, which `StructureBudgetGuard.cs:30-34` cannot detect, and (separately) that **zero** rows spend `reaction`, which is unspendable rather than undetectable |
| `action.corpus.rosterReconciliation` | **CLOSED** | no | the corpus size against the **shipped 84**, and it refuses to quote a band derived for 904 |
| `action.corpus.flavourQuality` | **OPEN** | **never** | prose reads generic — a review queue |
| `action.corpus.semanticNeighbour` | **OPEN** | **never** | A-S3's tier-3 flags — a review queue |

**⛔ DECIDED 2026-09-03 — the atom-family namespace both pairing metrics count against.** Until now
no spec said which set `atomFamilies` names, and the tree holds three **completely disjoint**
candidates (zero overlap between any pair, measured 2026-09-03): **17** demo families under
`data/seed/atoms/`, **98** authored families under `data/seed/items/affix-families/`, and the **5**
ids in `data/seed/actions/pairings.json`. **The 98 are the namespace** — the decision and its
evidence table live in `spec-distribution-planner.md` §2.

That fixes both metrics above, and it makes one of them worse before it makes it better:

- **`enablerPayoffCoverage`** keys on ids drawn from the 98.
- **`pairingReach`'s denominator is 98**, and its numerator today is **zero**: none of
  `pairings.json`'s five ids is in the namespace, so `IsPayoff` is false for every family a row can
  carry and **100%** of accepted rows are `pairingRole: "none"` — not "most", which is what the
  earlier note assumed. Rewriting `pairings.json` into the namespace is a named deliverable of A-S1
  (`spec-distribution-planner.md` §3 step 6), and **this metric is how the rewrite is observed**: the
  reach number moves off zero only when real payoff families exist. Reporting zero honestly is the
  metric working, not the corpus failing.

## 3. The algorithm

1. **Load the accepted corpus** through A-C1's envelope and partition it by the cell key
   `(scope, category, rungBand, pairingRole)`. The rung band is the *planned* band, an index pair into
   `data/tuning/action-rungs.v1.json`, never a computed power number.
2. **Recompute the quota** the same way A-S1 did — largest remainder over A-T1's `categoryMilli`, in
   `long`, widening before the multiply, dividing by 1000 last, exactly once. Recomputing rather than
   reading A-S1's answer is deliberate: it makes *"is the plan satisfiable?"* answerable independently
   of whether the planner ran correctly.
3. **Run every CLOSED metric** and collect `Finding`s. A metric whose inputs are missing emits
   `NOT_MEASURED`, never a pass (`metrics/model.py:34`).
4. **Run every OPEN metric into the review queue only.** They are constructed with `Loop.OPEN`, and
   `Loop.OPEN` with `gates=True` raises at registration — the rule is enforced by the registry, not by
   this module's good behaviour.
5. **Derive next-round targets** deterministically: for each thin cell, in cell-id order, emit one
   `next-target` entry per subject that is short, ordered by `(shortfall desc, subjectKey ordinal)`.
   Round n+1's briefs are then a pure function of round n's report, so each round is individually
   replayable and the sequence is auditable.
6. **Emit the verdict.** `pass` requires *every gating CLOSED metric* green **and** an explicit list of
   which metrics were evaluated. An unevaluated metric is named in the report as unevaluated; it never
   silently counts as green.
7. **Canonical write** — sorted keys, fixed indent, `\n`, explicit nulls.

## 4. What it must NOT do

- **Never let an open-loop metric contribute to a pass.** `flavourQuality` and `semanticNeighbour`
  produce review queues and nothing else.
- **Never register a new metric with `gates=True`.** Promotion is a separate, later, deliberate act
  (`metrics/model.py:8-9`, `:85`).
- **Never report a pass for a metric it did not run.** `NOT_MEASURED` is a distinct severity and must
  stay visible in the output.
- **Never quote the 1,500-3,500 band against the shipped roster without re-deriving it.** That band was
  derived for ~900 units; at 84 species and 3 signature actions each the signature tier is **252**, and
  the whole corpus is roughly **850** — below the band rather than inside it. The derivation is correct
  in method and was applied to the wrong roster, and repeating that here would re-ship the same error.
- **Never schedule past the smoke batch.** The report is the evidence for the owner's decision; it does
  not make the decision and it does not plan the full run.
- Never call a model. Not to summarise, not to judge prose, not to "explain" a thin cell.
- Never treat a high cross-tier similarity rate as a generation defect while constraint 4 holds.

## 5. Testing strategy

| Case | Expect |
|---|---|
| **Determinism** | two runs over an unchanged corpus produce a byte-identical report, asserted by hash; the injected clock is pinned |
| **Planted violation — open metric gates** | constructing an `OPEN` metric with `gates=True` **raises** at registration; the test asserts the raise, not a log line |
| **Planted violation — unevaluated pass** | a corpus missing the inputs one CLOSED metric needs yields `NOT_MEASURED` for it and a verdict that is **not** `pass` |
| **Planted violation — thin cell hidden** | a corpus with one empty planned cell produces a `thinCell` finding naming the cell and the shortfall; the test fails if the verdict is green |
| **Planted violation — unpaired payoff** | an accepted row carrying a payoff family with no accepted row carrying one of its enablers in the same anchor fails `enablerPayoffCoverage`, mirroring the Core-side planted-pool test. ⛔ **CORRECTED 2026-09-03:** the planted pair was `atom.rot-punisher` / `atom.rot-applier`, called "the **real** keys" — neither is in the 98-family namespace. The test reads its pair **from the rewritten `pairings.json` at test time** (first key, first enabler), never hard-coded, so it runs against the rewritten file and moves with it |
| **Planted violation — a family outside the namespace** | an accepted row whose `atomFamilies` names a fixture id from `data/seed/atoms/` (e.g. `atom.fx-cold-on-hit`) fails `atomFamilyNamespace`, naming the row, the id and the file it came from |
| **Planted violation — a status where a family belongs** | a report row whose pairing key is a status id (`"rot"`) rather than an atom family (`"atom.rot-punisher"`) fails, naming the field |
| **Planted violation — `reaction` accepted** | an accepted row spending `reaction` fails `structureEnforceability`; it is unspendable, so a non-zero count is a real defect upstream, not a reporting nuance |
| **Planted violation — roster inflation** | a report quoting a 904-based denominator or the raw research band fails `rosterReconciliation` |
| **Next-round targets** | the same report produces the same target list in the same order; shuffling corpus input order changes nothing |
| **Small batch honesty** | over 12 accepted rows the report emits findings and a verdict that is explicitly not a corpus-level pass |
| **Offline guarantee** | the suite passes with the transport stubbed to raise |

## 6. Acceptance criteria

1. `coverage-round-<n>.json` is written through A-C1's envelope and loads back.
2. Every metric in the register declares `loop`, and every OPEN one has `gates=False`, enforced by a
   test that asserts the raise on the contradiction.
3. The report lists, explicitly, which metrics were evaluated and which were `NOT_MEASURED`.
4. Cell counts and quotas are present for every planned cell, including cells with count 0.
5. Next-round targets are a pure function of the report — same report in, same targets out, byte-identical.
6. `rosterReconciliation` states the shipped roster (84 species, 19 families, 53 family-assigned) and
   re-derives any band it quotes against it.
7. `structureEnforceability` reports the count of accepted rows spending **`restriction`**, with a
   note that `StructureBudgetGuard.cs:30-34` cannot detect it because detection needs the effect-atom
   program's per-atom payload/target data — and, **separately**, asserts the count of rows spending
   **`reaction`** is **zero**, because it is unspendable rather than undetectable
   (`StructureBudgetGuard.cs:27-30`; `spec-tier-access-gate.md` AC5). ⛔ **CORRECTED 2026-09-03
   (review F3/F4):** the two axes were reported as one undetectable pair. They are in different
   states, and under A-S1's old *intersection* rule neither was even reachable, so this metric had
   **zero** possible instances; **union-to-ceiling**
   (`spec-distribution-planner.md` §3 step 5) makes `restriction` the signature tier's one exclusive
   axis and the count real.
7b. `enablerPayoffCoverage` keys on **atom families**, never statuses — `pairings.json` maps payoff
   families to enabler atom families and
   `EnablerPayoffCoverage.Check(IReadOnlyList<string> poolAtomFamilies, …)`
   (`EnablerPayoffCoverage.cs:21-23`) takes families. `pairingReach` states the denominator alongside
   it, and a coverage number without that context reads as a success it has not earned
   (`spec-distribution-planner.md` §3 step 6). ⛔ **CORRECTED 2026-09-03 (review F7).**
7c. Both pairing metrics count against the **98** authored affix families
   (`data/seed/items/affix-families/*.json`), and `atomFamilyNamespace` asserts every accepted id is
   one of them. `pairingReach` reports **zero reach** while `pairings.json` still carries its five
   out-of-namespace ids, and the report says so in those words rather than showing an empty pairing
   section. ⛔ **DECIDED 2026-09-03** — the namespace was never stated and the three candidates are
   disjoint (`spec-distribution-planner.md` §2).
8. A rerun over unchanged inputs is byte-identical by hash, with provenance recording the corpus hash
   and the tuning version.
9. Zero model calls, proven by a stub that raises.

## 7. Dependencies

**Depends on:** **A-S3** (the accepted corpus — map §4 and §5), **A-S1** and **A-T1** (for quotas),
A-C1's envelope.
**Depended on by:** **A-S1**, which reads the report to build round n+1's briefs. The cycle is broken
by round 1 reading no report at all.
**Cross-program (map §7):** **effect-pipeline module 4** (`instance-producer`) owns binding production;
`effect_binding` has zero rows today, so a coverage number here measures an authored corpus that
nothing yet instantiates — worth stating in the report rather than discovering later.
