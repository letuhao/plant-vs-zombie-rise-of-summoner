# Plan: Seedsmith — full program

Map: [docs/architecture/seedsmith-map.md](../docs/architecture/seedsmith-map.md)
Specs: [seedsmith/](../docs/architecture/seedsmith/) — analytics · numerics · budget · metrics · planner · pipeline · foundation
Audit: [seedsmith/review/](../docs/architecture/seedsmith/review/) — 66 findings, 11 blockers, all closed

> `tasks/plan.md` and `tasks/todo.md` belong to the **perf-v3** stream, so this program uses the
> prefixed pair (AGENTS.md, parallel programs). One program, one file pair — this plan now covers
> all three waves (W1/W2/W3) rather than forking into per-wave files, so the W1 defect trail below
> stays attached to the plan that cites it (S9's mutation tests, S10's CI cutover).

**Status (2026-08-23): Part 1 (W1) complete.** Part 2 (W2) and Part 3 (W3) below are planned, not
built — see [seedsmith-todo.md](seedsmith-todo.md) for the live per-task checklist across all
three parts.

---

## Part 1 — W1: measurement (COMPLETE)

All ten tasks (S0-S10), all five checkpoints (CP-A through CP-E) reached. 165 tests, all green.
The CI cutover's four steps are all done: parity was proven byte-identical across every historical
state this corpus has ever had (not a one-week calendar wait, which the plan originally called
for — real drift already in the corpus's git history stood in for it), the seven ported
Linkage/Registration metrics were promoted to `gates=True` as a verified replacement for
`seed_graph`'s existing gate, and `tools/seed_graph/` is deleted. `seedsmith` is now the sole
reachability gate in CI. Full detail and evidence: [seedsmith-todo.md](seedsmith-todo.md) Part 1.

---

## Scope

**W1: measurement.** `corpus`, `adapter`, `numerics`, `budget`, `metrics`, `report`.

On completion, seedsmith finds every known defect in the item corpus with **zero model calls** —
including the nine empty partitions that survived three authoring waves unnoticed. W2 (`planner`,
`briefkit`) and the rest of W3 (`pipeline`'s generation logic) are out of scope and gate on this
being green.

**Plus one independent slice: `llm_caller` (S0).** Not part of the measurement chain — it has no
edge to or from `corpus`/`adapter`/`metrics` — so it isn't "held" pending W3's gate, it's simply not
on that path. Porting a proven, already-solved LM Studio caller (reasoning disabled, self-heal
verify loop) is buildable and fully testable today against a mock server, with nothing above it
required to exist first. W3 still gates on `metrics`/`planner` before *using* it to generate
content — S0 only proves the transport mechanism, in isolation.

**Not in W1, deliberately:** the `SemanticDedup` conceptual-clustering metric, blocked on a 516-word
adjective `axis` registry addition (analytics §6.3). It ships as a declared gap, not as a check
implemented against the wrong grouping.

---

## Dependency graph

```
corpus ── adapter ─┬─ numerics ─┐
                   ├─ budget ───┼─ metrics ── report

llm_caller (S0, independent) ── (feeds W3's pipeline; gates on nothing above)
```

Strictly downward. `numerics` and `budget` depend on `adapter`, not on the item registries directly
— corrected after audit finding B2, and enforced by the stub adapter rather than by discipline.

`llm_caller` is a second, disconnected component of this graph, not a follow-on to it. It imports
nothing from `corpus`/`adapter`/`metrics` and they import nothing from it, so it has no position in
the topological order above — it runs whenever, including now.

---

## Slicing: vertical, not layered

Each task delivers **one complete path from corpus to CLI output**. No task builds a layer and stops.

The reason is specific to this program: the failure mode being designed out is *"a check that looks
present and is absent or wrong."* A horizontal slice — "build all the metrics, wire the report
later" — reproduces exactly that state for the duration of the build. A vertical slice is always
either working or visibly not.

---

## Phases and checkpoints

### Phase 0 — `llm_caller` (S0), independent

Ports `D:\Works\source\lore-weave\scripts\i18n_translate.py`'s LM Studio caller: reasoning disabled
at the request (`reasoning_effort: "none"` + `chat_template_kwargs.enable_thinking: false` —
different servers/templates read different keys, so both are always sent), robust JSON extraction,
and a self-heal retry loop generalized so any future pipeline supplies its own verify rule instead
of a translation-specific one. Full spec: [spec-pipeline.md §5.1](../docs/architecture/seedsmith/spec-pipeline.md#51-dont-build-a-model-calling-client---reuse-one-that-already-works).

Proven in production on the source project's translate pipeline — this is a port and a
generalization of the self-heal signature, not new design. Tested entirely against a stdlib mock
HTTP server; no live model, no seedsmith dependency, no ordering relative to S1–S10.

**⭐ CP-0 — the transport is proven before anything is built on top of it.** The reasoning-disable
payload and the self-heal retry loop are verified against a fixture server, independent of whether
`metrics` or `planner` exist yet.

### Phase 1 — walking skeleton (S1)

One trivial metric travelling the whole path on a synthetic fixture. Establishes the module
boundaries, the `Finding` shape, the CLI and the four exit codes before anything real depends on
them.

**⭐ CP-A — the seam is real.** `seedsmith check --adapter stub` runs green on a clean fixture and
red on a broken one, with correct exit codes. The stub adapter is the only adapter that exists.

### Phase 2 — the real corpus (S2)

`adapter-items`, then the same single metric against live data. This is the **known-answer test**:
`Coverage/EmptyPartition` must find exactly nine partitions, and no others.

**⭐ CP-B — measurement beats memory.** The tool independently rediscovers, in one command, a defect
that took three authoring waves and a hand-written diff to notice.

### Phase 3 — parity and absorption (S3)

Port `seed_graph`'s seven check functions. Run both implementations against the live corpus and
diff. Byte-identical finding sets or the port is wrong.

**⭐ CP-C — no regression, and a free defect detector.** Two independent implementations agreeing is
the cheapest test of the tester; the week they run side by side in CI is worth more than the code.

### Phase 4 — the measurement families (S4–S8)

Coverage, numerics + Balance, budget + Distribution, Constraint/Exemplar/Dedup, sampling + Quality.
Each is a vertical slice: metric, fixtures, CLI output, docs entry in the Appendix-A `covers` table.

**⭐ CP-D — W1 measurement complete.** Every Appendix-A row owned by seedsmith is either claimed by a
metric or printed as a known gap. `seedsmith metrics --coverage` is the evidence.

### Phase 5 — trust and cutover (S9–S10)

Mutation testing over the metric suite, then the four-step CI cutover and deletion of
`tools/seed_graph`.

**⭐ CP-E — W1 done.** CI gates on seedsmith; the old tool is gone rather than rotting beside its
replacement.

---

## Task summary

| # | Task | Delivers | Gates on |
|---|---|---|---|
| S0 | `llm_caller` | ported LM Studio transport + self-heal loop, mock-server tested | — (independent) |
| S1 | Walking skeleton | corpus + stub adapter + 1 metric + report + CLI | — |
| S2 | `adapter-items` | the same path on 1,438 real entries | S1 |
| S3 | Absorb `seed_graph` | Linkage + Registration families, parity-proven | S2 |
| S4 | Coverage family | allocation + pairwise t-way with legality | S2 |
| S5 | `numerics` | tier-bands, resolve, explain, rebalance, Balance family | S2 |
| S6 | `budget` | derive with conflicts preserved, Distribution family | S2, S5 |
| S7 | Constraint · ExemplarConformance · SemanticDedup | three families | S4 |
| S8 | Sampling + Quality | stratified `--sample`, open-loop review queue | S6 |
| S9 | Mutation testing | proof the checks would notice being broken | S3–S8 |
| S10 | CI cutover | gate armed, `seed_graph` deleted | S9 |

S4, S5 and S6 are independent once S2 lands and may run in parallel.

---

## Verification discipline

Every task, without exception:

- **A fixture that must trip it and one that must not.** Synthetic, never the live corpus — a test
  reading shipping content stops testing the day the content is fixed.
- **A CLI command in the task**, so "done" is something a human can run rather than a claim.
- **Registry facts read, never transcribed.** Four of six BLOCKED reports in the agentic build, and
  every partition-id error, came from a value typed by hand.
- **New metrics ship `gates=False`.** Promotion happens after calibration against a corpus believed
  healthy — never in the same task that adds the metric.

---

## Risks

| Risk | Mitigation |
|---|---|
| The stub adapter is theatre and the core still leaks item concepts | S1 writes the stub *first*; S5/S6 must resolve against it with no `bands.v1.json` present |
| Pairwise coverage floods with false holes from illegal pairs | `LegalityFn` is required, not optional, and S4's fixtures include a `False` case |
| Diversity indices gate before anyone knows a healthy value | Measure-only for the whole of W1; thresholds are a post-W1 calibration task |
| The port silently changes a check's meaning | S3's parity diff is the acceptance criterion, not a smoke test |
| Absorbing `seed_graph` breaks CI | Four-step cutover, both tools running in parallel at step 2 |

---

## Out of scope (for Part 1 — planned below as Part 2/3)

`planner`, `briefkit`, and `pipeline`'s *generation* logic — schemas, briefs, the actual act of
writing content into the corpus — all gated on `metrics`/`budget` existing, which they now do.
`llm_caller` (S0) was explicitly **not** in that bucket: it is the transport underneath `pipeline`,
had no edge to `metrics` or `planner`, and was built in Part 1 as its own independent slice.

---

## Part 2 — W2: planning (`planner` + `briefkit`)

W1 answers "what's wrong with the corpus." It cannot answer "what should be generated, in what
order, and how." That's `planner` (feasibility, ordering, validation, scheduling, the
demand-declare/fulfil split) and `briefkit` (work order → briefs). Both have audited specs
(`spec-planner.md`, and the `briefkit` section of `spec-foundation.md` §4) but no task breakdown
before this plan.

### Dependency graph

```
metrics + budget (Part 1, DONE) ──► planner ──► briefkit
```

`planner` consumes `Finding` (`seedsmith/metrics/model.py`) and `BudgetRow`
(`seedsmith/budget/model.py`) — both stable since Part 1. `briefkit` consumes a work order from
`planner` plus `budget`/`adapter` directly, for inlining vocabularies.

**One real prerequisite gap**, found by reading `spec-planner.md` §3 against the current code
rather than assumed: `KindSpec` has no declared reference fields today (S2 deliberately left
`id_pattern`/`runtime_id_fields` unset — a documented gap in `kinds.py`). Deriving the kind-level
reference graph for topological ordering needs some way to know "`unique.baseType` points at a
base-type, `drop-table.groups` entries can point at almost anything" — `corpus.discover_edges`
(built in S1) needs an `id_pattern` per kind to match against, which the real adapter doesn't
supply yet. Task P2 below resolves this as its first step, not a separate blocker.

### Phase 1 — Feasibility and ordering (P1–P2)

**P1 — Feasibility: pigeonhole → Hopcroft–Karp → König**
`seedsmith/planner/feasibility.py`. Three layers, cheapest first, short-circuiting: (1) pigeonhole
sum check, O(n); (2) max bipartite matching between demand and slot graphs via Hopcroft–Karp,
O(E√V); (3) when infeasible, König's theorem turns the maximum matching into a minimum vertex
cover — that cover *is* the binding constraint, named in the finding rather than a bare
"infeasible." When demand is balanced (n themes × n axes per role), emit the closed-form cyclic
Latin square `axis = (roleIndex + themeIndex) mod n` directly rather than searching for it, and
verify zero collisions.

**Acceptance**
- [ ] A synthetic 5-themes×15-uniques-into-8-roles×5-axes fixture (mirrors the real 75-into-40
      incident) is refused with the specific bottleneck named, not "infeasible"
- [ ] A balanced 5-theme fixture's Latin-square construction produces 0 axis collisions across all
      25 (role, theme) pairs
- [ ] A feasible-but-locally-starved fixture (totals fit, one subset doesn't) is caught by layer 2
      where layer 1 would incorrectly pass it

**P2 — Ordering: derive kind-level stages, never hand-label them**
`seedsmith/planner/ordering.py`. First resolves the prerequisite gap above: extends `KindSpec` with
a `reference_fields: frozenset[str]` (which fields on this kind hold a cross-kind reference —
`baseType` on `unique`, `outputRef` on `recipe`, `sourceAllow`+`groups` entries on `drop-table` —
read from the same `KindCatalog.cs` source S2 already cites, not re-derived from prose). Builds the
kind-level graph from those fields via `corpus.discover_edges` (S1, reused, not reinvented), runs
Kahn's topological sort into layers, and Tarjan's SCC to name any cycle's exact members rather than
reporting "cycle detected."

**Acceptance**
- [ ] Reproduces the real historical order on the real corpus — `drop-table` lands after
      `unique`/`base-type`/`set`/`gem`/`charm`/`consumable` (the 274-error incident this fixes
      structurally, not by a human remembering to relabel a stage)
- [ ] A synthetic two-kind cycle fixture is caught and both kinds are named by Tarjan's SCC
- [ ] The derived order needs no hand-maintained stage label anywhere in the adapter

**⭐ CP-F1 — the planner refuses the impossible and orders the possible**, proven against synthetic
fixtures reproducing both real incidents (75-into-40, the 274 same-stage errors) and the real
corpus's own kind graph.

### Phase 2 — Validation, scheduling, and the demand split (P3–P5)

**P3 — Input validation: exemplar gate before dispatch**
`seedsmith/planner/validate.py`. Before any work order is emitted, every exemplar it would
reference is checked with the already-built `ExemplarConformance` metric (S7) — reused, not
reimplemented. A failing exemplar refuses the whole order (exit code 3, already defined in
spec-foundation.md §7.3's CLI contract).

**Acceptance**
- [ ] A work order referencing a synthetic exemplar with a missing required field is refused, not
      partially emitted
- [ ] A clean exemplar set passes through untouched

**P4 — Scheduling and work-order output**
`seedsmith/planner/schedule.py`. List scheduling under a concurrency cap: layer-by-layer (from
P2), longest-job-first within a layer (partition entry count as job size), model tier by a small
adjustable rule table (identity-inventing partitions → stronger model, closed-vocabulary-consuming
partitions → cheaper model — a config table, not an optimizer). Emits the JSON work order exactly
per spec-planner.md §6's shape, with `closes` on every job naming which `Finding`(s) (by metric id
+ subject) it would clear.

**Acceptance** — the module's own known-answer test (spec-planner.md §7)
- [ ] Given the real corpus's still-open partitions (`gems/2`, `display-templates/{4,5,6}`,
      `attributes`), the emitted plan places `gems/2` after its registry dependency and the three
      display-template partitions after the affix families they render — "if the plan matches what
      a human would write, the module works"
- [ ] The four base-type partitions from S2 are correctly NOT included as generation jobs here —
      they are mislabeled, not empty (S2's finding); relabeling is a corpus fix, not a generation
      job, and the planner must not confuse the two

**P5 — Generation pipelines: the declare/fulfil split (spec-planner.md §8)**
`seedsmith/planner/demand.py`. Phase A: each kind's deterministic stages (set slots, set threshold
structure, set members, recipe material demands) emit `Demand` objects — no generation, no file
writes. Phase B: the planner holds the full demand graph, topologically sorts it, checks
feasibility (P1), and resolves each demand against existing content first — reuse is the default,
not the exception (owner decision: no structural cap on set-member overlap; spreading demand across
candidates is a measured planner policy, not a capped rule).

**Acceptance**
- [ ] A synthetic 3-set-theme fixture with overlapping role/frame demand reuses existing base types
      where they satisfy the demand and requests new ones only for the genuine shortfall, without
      concentrating all three sets' demand onto the same handful of base types (a distribution
      check on the *demand* graph, not the corpus)
- [ ] A recipe fixture proves materials are demanded, and therefore generated, before the recipe
      that consumes them — structurally, not by a human remembering the order

**⭐ CP-F2 — W2 done.** Feasibility, ordering, validation, scheduling and the demand split all
proven against both synthetic incident-replay fixtures and the real corpus's own remaining gaps.

### Phase 3 — `briefkit` (P6)

**P6 — Work order → briefs**
`seedsmith/briefkit/`. One brief per job, assembled from: the allocation (partition, id template,
sequence — from `planner`), the budget row (target, tolerance, rationale — from `budget`), the
adapter's closed vocabularies (**inlined literally, never cited by filename** — "tags come from
tags.v1.json" cost 51 invented tags historically), the planner's constraints, and the metric's
`assertion`/`remedy` (what must become true). Content-addressed: a brief's hash is a pure function
of its inputs, recorded in the job, so a bad brief version is identifiable and exactly its output
re-runnable.

**Acceptance**
- [ ] A brief for a real still-open partition (`gems/2`) inlines the literal legal `family`
      vocabulary read from the registry at brief-generation time — grep the brief text for a
      citation string like "see tags.v1.json" and fail if found
- [ ] Two brief generations from byte-identical inputs produce the identical content hash (no
      wall-clock/random baked in)
- [ ] A brief whose exemplar failed P3's gate is never emitted

**⭐ CP-F3 — briefkit done.** A brief for a real, currently-open partition inlines everything an
agent would need and cites nothing.

---

## Part 3 — W3: generation (`pipeline`)

`llm_caller` (S0, Part 1) is the transport. Everything else pipeline needs — schemas, guardrails,
idempotence, the open-loop review queue — is unbuilt.

### Dependency graph

```
briefkit (Part 2) ──► pipeline (generation logic)
                          ▲
    llm_caller (S0, DONE) ─┘
    sampling (S8, DONE) ────┘  (reused for the open-loop queue, G3)
```

### Phase 4 — `pipeline` generation logic (G1–G3)

**G1 — Pipeline scaffold: schema-per-metric + guardrails**
`seedsmith/pipeline/`. `Pipeline` dataclass (metric, scope, schema, gate, max_retries, on_persist,
model) per spec-pipeline.md §2. Guardrails per §3: JSON Schema validated locally always (and via
the model's own structured-output mode where the endpoint supports it — S0/S5.1 already flagged
this as `llm_caller`'s one real upgrade path over the ported i18n tool); narrow scope per call;
closed vocabularies inlined (reuses `briefkit`'s own inlining, not a second implementation);
**never a number** — a schema containing a numeric magnitude field fails a static test over the
schemas themselves, mechanically, not by review; validate-before-accept (scratch → gate → move);
bounded retry with the exact error attached, then escalate — this is
`llm_caller.call_with_self_heal` (S0), generalized from a flat string-keyed payload to an arbitrary
schema-validated JSON object, reused rather than rebuilt; every schema carries a `blocked` variant
with a reason string.

**Acceptance**
- [ ] A schema-audit test rejects any registered pipeline whose schema has a bare numeric field
- [ ] A fixture pipeline run against a fake model server (the `MockModelServer` pattern from S0,
      reused) proves retry-with-named-defect then escalate-on-persistent-failure, with zero real
      model calls
- [ ] A `blocked` response writes nothing and is reported, not treated as a failure

**G2 — Idempotence and provenance**
Every generated entry records `_provenance` (pipeline id, model, prompt version, budget version,
timestamp, finding closed). Re-running a pipeline checks the finding is already closed (via a
`metrics` re-run) before generating anything.

**Acceptance**
- [ ] Running a pipeline twice over unchanged input produces zero new writes on the second run
- [ ] Provenance is queryable by finding id (answers "why does this row exist" and "which prompt
      version produced it")

**G3 — Open-loop review queue wiring**
Wires an open-loop pipeline (e.g. flavour generation) to the stratified sampling already built in
S8 (`seedsmith/sampling/`, reused, not reimplemented) — writes content, marks it `needsReview`,
samples it for human review exactly like `Quality/FlavourGeneric` already does for existing text.

**Acceptance**
- [ ] An open-loop pipeline's schema never includes a pass/fail field
- [ ] Re-running `metrics` after an open-loop pipeline's generation still reports the same finding
      as open-loop (never silently flips to a pass) — proving generated content can be sampled for
      review without the pipeline being able to mark its own homework

**⭐ CP-G — W2+W3 close the loop end-to-end, against a fake model, before any real token is spent.**
The concrete integration test: `metrics` finds `gems/2` empty → `planner` schedules it (P4) →
`briefkit` briefs it (P6) → `pipeline` (G1, fake `MockModelServer`, no real LLM spend) generates
content → `metrics` re-run shows the finding cleared. This is the actual promise behind "seedsmith
replaces the agentic fanout," proven mechanically for the first time, without spending a single
real token to prove it.

---

## Verification discipline (Parts 2 and 3, unchanged from Part 1)

Every task: a fixture that must trip it and one that must not, synthetic never live-corpus for
metric-level tests; a CLI or direct-call command in the task description so "done" is runnable, not
asserted; registry facts read fresh, never hand-copied; real defects found while building get fixed
in place and documented, same discipline as every task in Part 1.

## Out of scope for Parts 2 and 3

Actually spending real model calls/tokens — every acceptance criterion above is provable against a
fake model server or a synthetic fixture. The first REAL generation run against the live corpus
(closing `gems/2` for real) is a deliberate, separate, owner-approved act after CP-G, not part of
building the machinery.

Also out of scope: closing the eight accidental empty partitions — they are W2's known-answer test
and must stay open · the adjective `axis` registry addition · any change to `tools/ItemSeedValidator`,
which stays the referential gate.
