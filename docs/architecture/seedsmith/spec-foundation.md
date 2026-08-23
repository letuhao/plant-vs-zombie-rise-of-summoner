# Seedsmith — `corpus`, `adapter`, `report`, `briefkit`

**Status:** Proposed 2026-08-23. Nothing is built.

The four modules with little algorithmic content and a great deal of interface consequence. Specced
together because their value is entirely in the boundaries they draw — get these wrong and the
second feature rewrites the core, which is the failure mode a "feature-agnostic" core with exactly
one feature almost always hits.

---

## 1. `corpus` — the graph

Loads any seed folder into a typed, queryable graph. **Knows nothing about items.** No role, no
frame, no rung band, no drop table appears anywhere in this module; if one does, the abstraction has
already failed.

```
Entry     id, kind, partition, path, data, provenance
Corpus    entries; by_id; by_kind; by_partition; edges
Edge      from_id → to_id, via field path
```

Three things it must get right, each learned the hard way this session:

- **Edges are discovered, not declared.** Any string matching an allocated id namespace is an edge.
  A declared edge list drifts from the data the moment a field is added.
- **Minted runtime ids are first-class.** A milestone mints `atom.enhance-vigor`; a base type points
  at it; `by_id` will never contain it. The tracking-id vs runtime-id split caused **four separate
  defects** in one session, so the graph carries both and resolution consults both.
- **Exemplars load but are flagged.** `is_exemplar` on every entry. They are patterns, not content,
  and must never occupy a slot in any cross-row ledger — a rule that was broken twice, once by me
  after I had already written the guard for it elsewhere.

Loading is pure: no network, no database, no mutation. `Corpus.load(path)` and nothing else.

## 2. `adapter` — the feature seam

The interface a feature implements to be understood by the core. Everything item-shaped lives behind
it.

```python
class SeedAdapter(Protocol):
    def kinds(self) -> list[KindSpec]: ...          # kind → directory, namespace, required fields
    def dimensions(self) -> list[Dimension]: ...    # role, frame, band … for coverage and distribution
    def legal_combinations(self) -> LegalityFn: ... # which dimension pairs are possible at all
    def registries(self) -> RegistrySet: ...        # closed vocabularies
    def channels(self) -> list[Channel]: ...        # for numerics; empty if the feature has no magnitudes
```

`legal_combinations` is the one that is easy to omit and expensive to omit. Pairwise coverage
(analytics §2.2) counts uncovered pairs as holes, and `ward-array × hybrid` or
`unique × jewel-minor` are *forbidden*, not missing. Without legality the coverage metric produces
permanent false findings, which is how a metric becomes noise everybody filters out.

**Conformance is tested against a stub adapter** (map §7.3) that describes a tiny invented feature
with two kinds and two dimensions. It exists only in the test suite. If the core reaches into item
concepts, the stub stops passing — which is a cheap, loud, continuous proof that the seam is real.

## 3. `report` — findings out

Three consumers, one finding model.

**Human CLI.** Grouped by severity then family, worst first, each finding naming the entry, the
partition and the rule. Counts at the top; nobody reads past the summary when the summary is fine.

**CI gate.** Exit non-zero on GAP. Notes never gate. New metric families arrive **measure-only** and
are promoted to gating once their target is calibrated — a metric that gates before anyone knows the
right threshold trains people to ignore the build.

**Machine output.** Stable JSON for `planner`. Every finding carries a stable `code`, the entry, the
dimension, and — for closed-loop findings — the assertion that must become true. That assertion is
what lets a work order be graded automatically.

**Sampling** (analytics §8): `--sample N --metric X`, stratified, seeded from
`metric id + corpus revision`, so the same sample is reproducible and a reviewer can diff their own
judgement across runs. This is how open-loop metrics reach a verdict, so it is a feature and not a
debugging aid.

## 4. `briefkit` — work order to briefs

Generates the brief for each job. **Every fact in a brief is generated from an authority; none is
transcribed.** Four of the six BLOCKED reports in the agentic build, and every partition-id error,
came from a brief where I had typed a value by hand.

Each brief is assembled from: the allocation (partition id, id template, sequence), the budget
(entry count, distribution), the adapter (closed vocabularies, inlined literally), the planner
(constraints, role/axis tables, dependency notes) and the metric (what must become true).

Two rules learned from the waves:

- **Inline the vocabulary, do not cite it.** "Tags come from `tags.v1.json`" cost 51 invented tags.
  The literal legal list, emitted from the registry, costs nothing and removes the failure.
- **State what is already checked.** A brief that lists what the validator enforces stops agents
  re-reporting known-good rules and burning turns on them.

Briefs are content-addressed and recorded in the job, so a bad brief version can be identified and
exactly its output re-run.

---

## 5. Layout

```
tools/seedsmith/
  seedsmith/
    corpus/        graph, loader, edges
    adapters/      base protocol, items/, _stub/ (tests only)
    numerics/      formulas, tier-bands, rebalance
    budget/        derive, reconcile, versions
    metrics/       families, registry, findings
    planner/       feasibility, ordering, scheduling
    briefkit/      templates, assembly
    pipeline/      schemas, guardrails, runners
    report/        cli, ci, sampling
  tests/
  seedsmith.py     entry point
```

Stdlib only in the core; `pipeline` is the sole module permitted a network dependency, and it is
optional — every other module must run offline, in CI, with no credentials.

`tools/seed_graph` is absorbed: `Corpus`, `Acquisition`, `Finding` and the nine existing checks move
in with their 16 tests, and the directory is removed rather than left to rot beside its replacement.

---

## 6. Build order within W1

1. `corpus` + stub adapter — the seam, proven before anything depends on it.
2. `adapter-items` — port what the C# validator and `seed_graph` already know.
3. `numerics` — formulas are locked; tier-bands is the new artefact.
4. `budget derive` — emits targets with conflicts preserved.
5. `metrics` — absorb the nine existing checks, then add coverage, distribution, constraint,
   feasibility, exemplar-conformance, semantic-dedup.
6. `report` — CLI, CI gate, sampling.

Acceptance for W1: running seedsmith against today's corpus finds **the eight known-empty partitions
and nothing spurious**, and every metric family in Appendix A of the map is either implemented or
explicitly listed as not-yet — visible, never silently absent.
