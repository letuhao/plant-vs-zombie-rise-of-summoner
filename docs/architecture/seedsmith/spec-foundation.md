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
    __main__.py    entry point (NOT seedsmith.py beside the package — §7.5)
  tests/
```

Stdlib only in the core; `pipeline` is the sole module permitted a network dependency, and it is
optional — every other module must run offline, in CI, with no credentials.

`tools/seed_graph` is absorbed: `Corpus`, `Acquisition`, `Finding` and its **seven check functions**
(emitting nine finding codes — the two counts get confused easily) move in with their 16 tests, and
the directory is removed rather than left to rot beside its replacement.

---

## 6. Build order within W1

1. `corpus` + stub adapter — the seam, proven before anything depends on it.
2. `adapter-items` — port what the C# validator and `seed_graph` already know.
3. `numerics` — formulas are locked; tier-bands is the new artefact.
4. `budget derive` — emits targets with conflicts preserved.
5. `metrics` — absorb the seven existing checks, then add coverage, distribution, constraint,
   feasibility, exemplar-conformance, semantic-dedup.
6. `report` — CLI, CI gate, sampling.

Acceptance for W1: running seedsmith against today's corpus finds **all nine known-empty partitions
— eight accidental, one deferred — and nothing spurious**, and every metric family in Appendix A of the map is either implemented or
explicitly listed as not-yet — visible, never silently absent.

---

## 7. Audit corrections — the four buildability blockers

The 2026-08-23 audit found four things an implementer could not start W1 without. Each is resolved
here rather than left to be invented at the keyboard.

### 7.1 (B2) `numerics` and `budget` depend on the adapter — the specs were wrong, not the map

The map lists both as feature-agnostic core; their specs are written start-to-finish in item
vocabulary — 14 channel names, `budgetWeightMilli`, AE premiums, `rungBand` — reading
`bands.v1.json` and `core.v1.json` directly. Meanwhile `SeedAdapter.channels()` exists with the
comment *"for numerics"*, which `spec-numerics` never calls. Two documents disagreeing about the
same module.

**Resolution: the map is right; the specs took a shortcut.** The *algorithms* in both modules are
generic — geometric tier growth, largest-remainder apportionment, monotone interpolation, target
derivation, tolerance comparison, conflict preservation. Only the *vocabulary* is item-shaped, and
vocabulary is exactly what an adapter exists to supply.

So the dependency edge is added, and the diagram corrected:

```
corpus ── adapter ─┬─ numerics ─┐
                   ├─ budget ───┼─ metrics ─┬─ report
                   └────────────┴───────────┴─ planner ── briefkit ── pipeline
```

Concretely: `numerics` gets channels, reference bases and tier constants from `adapter.channels()`
and `adapter.registries()`; `budget` gets its dimensions from `adapter.dimensions()`. Neither opens
a file under `data/seed/items/`. **Every item name in `spec-numerics.md` and `spec-budget.md` is
illustrative** — worked examples of a generic mechanism, not the mechanism.

The stub adapter (§2) is what keeps this honest: it declares two channels with invented names and no
`bands.v1.json`, and `numerics` must resolve against it. If any item constant is reachable without
going through the adapter, the stub fails.

### 7.2 (B1) The interface types, defined

Named but never given fields in the first draft. Full definitions, stdlib only:

```python
@dataclass(frozen=True)
class KindSpec:
    kind:          str                 # "base-type"
    directory:     str                 # "base-types"
    namespace:     str                 # allocation key
    required:      frozenset[str]
    optional:      frozenset[str]
    id_pattern:    re.Pattern
    runtime_id_fields: frozenset[str]  # fields holding a MINTED id (§1's four-defect split)

@dataclass(frozen=True)
class Dimension:
    id:        str                     # "role"
    values:    tuple[str, ...]
    field:     str                     # entry field carrying it
    applies_to: frozenset[str]         # kinds it is meaningful for

@dataclass(frozen=True)
class Channel:
    id:             str                # "maxHp"
    unit:           Unit               # GAME_UNITS | PER_MILLE | MILLISECONDS
    reference_base: Callable[[ProgressionPoint], int]
    group:          str                # primary | flatDerived | sigmoidDerived | statusMagnitude
    ops:            frozenset[str]     # Flat | Increased | More

LegalityFn = Callable[[str, str, str, str], bool]   # (dimA, valA, dimB, valB) -> is this pair possible

@dataclass(frozen=True)
class RegistrySet:
    vocabularies: Mapping[str, frozenset[str]]   # "tags" -> the closed set
    versions:     Mapping[str, int]
    def is_legal(self, vocabulary: str, value: str) -> bool: ...
```

`LegalityFn` returning `True` by default is a trap — an adapter that forgets it turns every illegal
pair into a permanent false finding (analytics §2.2). It is **required**, not optional, and the stub
adapter exercises a `False` case.

### 7.3 (B3) The CLI

```
seedsmith check      [--adapter items] [--gate] [--json PATH] [--metric ID]...
seedsmith sample     --metric ID [-n 8] [--seed KEY]
seedsmith budget     derive | diff V1 V2 | show [--dimension D]
seedsmith numerics   resolve ENTRY_ID | explain ENTRY_ID | rebalance --set K=V... [--publish]
seedsmith plan       [--out PATH] [--dry-run]
seedsmith metrics    list | --coverage
```

**Exit codes**, because CI depends on them being stable: `0` clean · `1` findings at GAP · `2` could
not run (corpus unreadable, adapter missing, registry unparseable) · `3` refused (planner proved the
work order unsatisfiable). `2` and `3` are distinct from `1` on purpose — "the tool broke" and "the
tool worked and says no" must never look alike to a script.

Config lives in `seedsmith.toml` at the repo root: adapter name, seed root, suite time budget,
concurrency cap, gating overrides. Every flag has a config equivalent; the flag wins.

### 7.4 (B4) The `seed_graph` cutover

CI shells `tools/seed_graph/check_reachability.py` and `test_reachability.py` today, so deleting the
directory breaks the build. Four ordered steps, each independently green:

1. `seedsmith check` reproduces the nine existing reachability checks and its findings are **diffed
   against `seed_graph`'s** on the live corpus. Byte-identical finding sets, or the port is wrong.
2. CI runs **both**, briefly, and fails if they disagree — a cheap dual-implementation cross-check.
3. CI switches to `seedsmith check --gate`; the `seed_graph` step is removed.
4. `tools/seed_graph/` is deleted, its 16 tests having moved with the checks.

Step 2 is the one worth insisting on. Two independent implementations of the same property
disagreeing is the cheapest possible detector for the "checker was wrong" defect class — the one
Appendix A originally missed — and it costs one CI step for one week.

### 7.5 The smaller findings

- **(M5) Suite time budget: 30 s** for the whole metric suite at current corpus scale, asserted by
  the runner. A number, because "fails if it exceeds its budget" with no budget is not a rule.
- **(N2) `Finding` carries `schemaVersion`.** It crosses a process boundary into `planner`; an
  unversioned wire format between two modules that ship separately is a future afternoon lost.
- **(N3)** Entry point is `seedsmith/__main__.py`, not `seedsmith.py` beside `seedsmith/` — the
  latter shadows the package on `sys.path`.
- **(M4)** `SetRuleCheck` (C#) and the absorbed `set_completability` (Python) both read `members`.
  The C# one owns *shape* (role cap, thresholds, hybrid core); the Python one owns *completability*
  (is a base type pinned). Documented here so the overlap is deliberate, and step 2 above catches it
  if they drift.
