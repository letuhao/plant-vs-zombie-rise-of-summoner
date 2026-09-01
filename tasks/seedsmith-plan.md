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

**Acceptance — ✅ all met, BUILT 2026-08-31 (full evidence: seedsmith-todo.md's own P1 entry, `tests/test_feasibility.py`, 15/15)**
- [x] A synthetic 5-themes×15-uniques-into-8-roles×5-axes fixture (mirrors the real 75-into-40
      incident) is refused with the specific bottleneck named, not "infeasible"
- [x] A balanced 5-theme fixture's Latin-square construction produces 0 axis collisions across all
      25 (role, theme) pairs
- [x] A feasible-but-locally-starved fixture (totals fit, one subset doesn't) is caught by layer 2
      where layer 1 would incorrectly pass it

**P2 — Ordering: derive kind-level stages, never hand-label them**
`seedsmith/planner/ordering.py`. First resolves the prerequisite gap above: extends `KindSpec` with
a `reference_fields: frozenset[str]` (which fields on this kind hold a cross-kind reference —
`baseType` on `unique`, `outputRef` on `recipe`, `sourceAllow`+`groups` entries on `drop-table` —
read from the same `KindCatalog.cs` source S2 already cites, not re-derived from prose). Builds the
kind-level graph from those fields via `corpus.discover_edges` (S1, reused, not reinvented), runs
Kahn's topological sort into layers, and Tarjan's SCC to name any cycle's exact members rather than
reporting "cycle detected."

**Acceptance — ✅ all met, BUILT 2026-08-31 (full evidence: seedsmith-todo.md's own P2 entry, `tests/test_ordering.py`, 12/12)**
- [x] Reproduces the real historical order on the real corpus — `drop-table` lands after
      `unique`/`base-type`/`set`/`gem`/`charm`/`consumable` (the 274-error incident this fixes
      structurally, not by a human remembering to relabel a stage)
- [x] A synthetic two-kind cycle fixture is caught and both kinds are named by Tarjan's SCC
- [x] The derived order needs no hand-maintained stage label anywhere in the adapter

**⭐ CP-F1 — the planner refuses the impossible and orders the possible**, proven against synthetic
fixtures reproducing both real incidents (75-into-40, the 274 same-stage errors) and the real
corpus's own kind graph. ✅ **REACHED 2026-08-31.**

### Phase 2 — Validation, scheduling, and the demand split (P3–P5)

**P3 — Input validation: exemplar gate before dispatch**
`seedsmith/planner/validate.py`. Before any work order is emitted, every exemplar it would
reference is checked with the already-built `ExemplarConformance` metric (S7) — reused, not
reimplemented. A failing exemplar refuses the whole order (exit code 3, already defined in
spec-foundation.md §7.3's CLI contract).

**Acceptance — ✅ all met, BUILT 2026-08-31 (full evidence: seedsmith-todo.md's own P3 entry, `tests/test_exemplar_gate.py`, 9/9)**
- [x] A work order referencing a synthetic exemplar with a missing required field is refused, not
      partially emitted
- [x] A clean exemplar set passes through untouched

**P4 — Scheduling and work-order output**
`seedsmith/planner/schedule.py`. List scheduling under a concurrency cap: layer-by-layer (from
P2), longest-job-first within a layer (partition entry count as job size), model tier by a small
adjustable rule table (identity-inventing partitions → stronger model, closed-vocabulary-consuming
partitions → cheaper model — a config table, not an optimizer). Emits the JSON work order exactly
per spec-planner.md §6's shape, with `closes` on every job naming which `Finding`(s) (by metric id
+ subject) it would clear.

**Acceptance** — the module's own known-answer test (spec-planner.md §7) — **✅ both met, BUILT
2026-08-31 (full evidence: seedsmith-todo.md's own P4 entry, `tests/test_schedule.py`, 14/14)**
- [x] Given the real corpus's still-open partitions (`gems/2`, `display-templates/{4,5,6}`,
      `attributes`), the emitted plan places `gems/2` after its registry dependency and the three
      display-template partitions after the affix families they render — "if the plan matches what
      a human would write, the module works"
- [x] The four base-type partitions from S2 are correctly NOT included as generation jobs here —
      they are mislabeled, not empty (S2's finding); relabeling is a corpus fix, not a generation
      job, and the planner must not confuse the two

**P5 — Generation pipelines: the declare/fulfil split (spec-planner.md §8)**
`seedsmith/planner/demand.py`. Phase A: each kind's deterministic stages (set slots, set threshold
structure, set members, recipe material demands) emit `Demand` objects — no generation, no file
writes. Phase B: the planner holds the full demand graph, topologically sorts it, checks
feasibility (P1), and resolves each demand against existing content first — reuse is the default,
not the exception (owner decision: no structural cap on set-member overlap; spreading demand across
candidates is a measured planner policy, not a capped rule).

**Acceptance — ✅ both met, BUILT 2026-08-31 (full evidence: seedsmith-todo.md's own P5 entry, `tests/test_demand.py`, 13/13)**
- [x] A synthetic 3-set-theme fixture with overlapping role/frame demand reuses existing base types
      where they satisfy the demand and requests new ones only for the genuine shortfall, without
      concentrating all three sets' demand onto the same handful of base types (a distribution
      check on the *demand* graph, not the corpus)
- [x] A recipe fixture proves materials are demanded, and therefore generated, before the recipe
      that consumes them — structurally, not by a human remembering the order

**⭐ CP-F2 — W2 done.** Feasibility, ordering, validation, scheduling and the demand split all
proven against both synthetic incident-replay fixtures and the real corpus's own remaining gaps.
✅ **REACHED 2026-08-31.**

### Phase 3 — `briefkit` (P6)

**P6 — Work order → briefs**
`seedsmith/briefkit/`. One brief per job, assembled from: the allocation (partition, id template,
sequence — from `planner`), the budget row (target, tolerance, rationale — from `budget`), the
adapter's closed vocabularies (**inlined literally, never cited by filename** — "tags come from
tags.v1.json" cost 51 invented tags historically), the planner's constraints, and the metric's
`assertion`/`remedy` (what must become true). Content-addressed: a brief's hash is a pure function
of its inputs, recorded in the job, so a bad brief version is identifiable and exactly its output
re-runnable.

**Acceptance — ✅ all met, BUILT 2026-08-31 (full evidence: seedsmith-todo.md's own P6 entry, `tests/test_briefkit.py`, 14/14)**
- [x] A brief for a real still-open partition (`gems/2`) inlines the literal legal `family`
      vocabulary read from the registry at brief-generation time — grep the brief text for a
      citation string like "see tags.v1.json" and fail if found
- [x] Two brief generations from byte-identical inputs produce the identical content hash (no
      wall-clock/random baked in)
- [x] A brief whose exemplar failed P3's gate is never emitted

**⭐ CP-F3 — briefkit done.** A brief for a real, currently-open partition inlines everything an
agent would need and cites nothing. ✅ **REACHED 2026-08-31.**

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

**Acceptance — ✅ all met, BUILT 2026-08-31 (full evidence: seedsmith-todo.md's own G1 entry, `tests/test_pipeline_scaffold.py`, 16/16)**
- [x] A schema-audit test rejects any registered pipeline whose schema has a bare numeric field
- [x] A fixture pipeline run against a fake model server (the `MockModelServer` pattern from S0,
      reused) proves retry-with-named-defect then escalate-on-persistent-failure, with zero real
      model calls
- [x] A `blocked` response writes nothing and is reported, not treated as a failure

**G2 — Idempotence and provenance**
Every generated entry records `_provenance` (pipeline id, model, prompt version, budget version,
timestamp, finding closed). Re-running a pipeline checks the finding is already closed (via a
`metrics` re-run) before generating anything.

**Acceptance — ✅ both met, BUILT 2026-08-31 (full evidence: seedsmith-todo.md's own G2 entry, `tests/test_provenance.py`, 13/13)**
- [x] Running a pipeline twice over unchanged input produces zero new writes on the second run
- [x] Provenance is queryable by finding id (answers "why does this row exist" and "which prompt
      version produced it")

**G3 — Open-loop review queue wiring**
Wires an open-loop pipeline (e.g. flavour generation) to the stratified sampling already built in
S8 (`seedsmith/sampling/`, reused, not reimplemented) — writes content, marks it `needsReview`,
samples it for human review exactly like `Quality/FlavourGeneric` already does for existing text.

**Acceptance — ✅ both met, BUILT 2026-08-31 (full evidence: seedsmith-todo.md's own G3 entry, `tests/test_open_loop.py`, 24/24)**
- [x] An open-loop pipeline's schema never includes a pass/fail field
- [x] Re-running `metrics` after an open-loop pipeline's generation still reports the same finding
      as open-loop (never silently flips to a pass) — proving generated content can be sampled for
      review without the pipeline being able to mark its own homework

**⭐ CP-G — W2+W3 close the loop end-to-end, against a fake model, before any real token is spent.**
✅ **REACHED 2026-08-31** (full evidence: seedsmith-todo.md's own CP-G entry, `tests/test_cp_g_end_to_end.py`, 4/4; full suite at CP-G 299/299).
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

---

# Part 4 — Feature 2: demons (D1–D4)

Map: [seedsmith-map.md](../docs/architecture/seedsmith-map.md) §3b ·
Ideal: [seedsmith-demons-ideal.md](../docs/architecture/seedsmith-demons-ideal.md) ·
Spec audit: [review/audit-demons-specs.md](../docs/architecture/seedsmith/review/audit-demons-specs.md)

Seven module specs, all written 2026-08-31, **APPROVED by the owner 2026-08-31 — authorized to
build.** (Parts 1–3's verified-complete state — 299/299 Python, 71/71 `ItemSeedValidator`,
`tools/seed_graph/` deleted — was re-confirmed the same day, before authorizing Part 4.)

**Why this feature exists structurally:** §1 of the map claimed *"the core is feature-agnostic by
construction, because the second feature must not rewrite it."* Parts 1–3 asserted that with a
`_stub`. Part 4 is the first real test of it, and **§D-F1 below records where it already fails.**

## Findings this plan surfaced before any code (read these first)

### §D-F1 ⛔ `KindSpec` cannot carry motif expression rules without a core change

`spec-adapter-demons` §2.7 says each `KindSpec` carries how a motif is expressed for that kind, and
that this must be *"data on the kind, not a rule in a prompt, so it is inlined into every brief."*

**Verified against code, not assumed:** `KindSpec`
([`adapters/base.py:24-37`](../tools/seedsmith/seedsmith/adapters/base.py)) is a frozen dataclass with
`kind, directory, namespace, required, optional, id_pattern, runtime_id_fields, reference_fields` —
**no expression field**, and `planner/ordering.py:75` duck-types only on `.kind` and
`.reference_fields`.

So §2.7 requires editing `adapters/base.py`, which contradicts the same spec's §1 (*"not one line of
core code changed"*) and §4 (*"no file outside `adapters/demons/` should need to change"*). Those two
claims and §2.7 cannot all hold.

**The spec told us what to do here:** *"If the core needs an edit, that is the finding — record it
rather than patch around it."* So:

| Option | Verdict |
|---|---|
| **A. Add `motif_expression: str \| None = None` to `KindSpec`** | **Planned default.** Additive with a default, so `items` and `_stub` are untouched and every existing test stays green. The "zero core change" claim becomes **false and is recorded as such** rather than quietly preserved |
| B. Keep the rules in a demons-local dict | Preserves the slogan, but `briefkit` then cannot reach them without *another* core change to know the accessor exists — the same edit, moved and made less honest |
| C. Encode them as a `registries()` vocabulary | No core change, but abuses `dict[str, frozenset[str]]` to carry one phrase per kind; the shape lies about what it holds |

**Option A, and D1.3's acceptance criterion is amended to "exactly one core file changes, additively,
and the finding is written down"** — not "no core file changes". An honest failed claim is worth more
than a preserved slogan.

### §D-F2 ⛔ D2's `aspect` kind is blocked on another program

`aspect-scope` is approved (2026-08-31) but **not built** — `DemonSpeciesDef` still carries
`ElementPrimary`/`ElementSecondary`/`TraitPool` on the species. Owner decision: **the demon program
builds it first.** D1 *declares* the kind (free); nothing generates into it until the tier exists.
**D2 and D4 are otherwise unblocked** — only aspect generation waits.

### §D-F3 ⚠️ The roster now grows, and two artifacts move with it

The species cap was removed 2026-08-31 (24 → 84 species; see
[`ssot-power-scale.md`](../docs/architecture/power/ssot-power-scale.md) §11.10a). Consequences this
plan must respect rather than rediscover:

- **Partitions are a snapshot.** Rarity is recomputed by rank over a growing pool, so a demon changes
  tier without moving rank. Any golden or fixture keyed on rarity moves when capture coverage improves.
- **`n` is a measurement, not a design point.** D3's metrics report `demonCount` beside every figure.
- **Fixtures must be synthetic.** Part 1's standing rule already says this; here it is load-bearing
  rather than stylistic, because the live roster is no longer a fixed size.

### §D-F4 ✅ Two risks measured away before planning

- **Flavour coverage is 100%** for all 84 eligible species (889/904 almanac rows overall). D2 is not
  starved of input, and `lore-enrich` does **not** need to precede it.
- **`almanac_seed` is real code**, not just a spec — D1 is not blocked on it.

## Dependency graph

```
demon-corpus-emit (C#, no seedsmith dep)
        │
        ▼
  adapter-demons ──────────────────────────────► [D1 shippable alone: zero model calls]
        │
        ▼
  family-extract ──► family-consolidate ──► motif-derive
                                                  │
                              ┌───────────────────┴───────────────┐
                              ▼                                   ▼
                        demon-metrics ────── gates ─────────► demon-themes
```

`demon-metrics` **gates** `demon-themes` deliberately: generating themed content from a taxonomy not
yet checked for tautology (A2) puts the wrong structure into the *item* corpus, which is a far more
expensive place to find it.

## Slicing

Vertical, same as Parts 1–3: every task ends with a passing test and, where it produces an artifact,
that artifact committed. No task is "add the schema" with the consumer in a later task.

**D1 is a shippable slice on its own** — it makes demons queryable by every metric seedsmith already
has with **zero model calls**, the same property that made W1 worth shipping alone.

## Phases and checkpoints

### Phase D1 — foundation, zero model calls

- **D1.1 — `DemonCorpusBuilder`, pure** · **M** · 2 files
  Pure `(species, almanacRows, recipeRows) -> entries`. Coverage flags carried through unchanged;
  `lineage` emitted; **`families` never emitted** (§2.4); catalog fields (element, rarity) **absent**.
  *Deviation from spec §4, deliberate:* tests go in `tests/FusionRpg.Core.Tests/Demons/` rather than a
  new `FusionRpg.DemonCorpusEmit.Tests` project. The builder is pure Core code, that is where Core's
  pure code is tested, and a new project needs a `ci.yml` step — which, per the known CI defect, would
  be masked anyway since only the last `dotnet test` exit code is checked.

- **D1.2 — `tools/DemonCorpusEmit` + committed corpus** · **M** · 2 files + emitted data
  Program.cs, DAL reads, deterministic file writing. **Must call `DerivedStatPolicy.Configure` before
  touching `RpgStore`** — `DemonCatalogGen` did not and could not run at all until 2026-08-31; this
  tool must not repeat it.

- **D1.3 — `DemonsAdapter`, five methods** · **M** · 4 files (+1 core, per §D-F1)
  `kinds/dimensions/legal_combinations/registries/channels`. `channels()` empty; no `item`/`action`
  kind; `family` declared with **empty values**; `environment` declared but excluded from coverage.

- **D1.4 — D1 integration** · **S** · tests only
  The emitted corpus loads through `Corpus.load`; existing metrics run against it unchanged.

#### ✅ CP-D1 — REACHED 2026-08-31 (full evidence: seedsmith-todo.md's own CP-D1)
- [x] `python -m pytest -q` full seedsmith suite green, **including `test_stub_adapter.py`** — the row
      that proves the core did not learn a demon concept
- [x] `dotnet test tests\FusionRpg.Core.Tests` green; `.\scripts\guard-dal.ps1` green
- [x] Emitter run twice ⇒ **byte-identical** output
- [x] §D-F1's core change is exactly one additive field, and the finding is written into the spec

### Phase D2 — taxonomy (first model calls, all faked in tests)

- **D2.1 — `family-extract`** · **M** · 3 files
  Deterministic batching (sorted `speciesId`, fixed windows, **batch size 8** as a structural
  constant); brief assembly; schema carrying `label` + `nativeLabel` + `basis`; `blocked` legal.
  Keep the **falsifier** test: single-demon batching must produce three distinct labels, proving
  §2.2's batching is real rather than assumed.

- **D2.2 — `family-consolidate`** · **M** · 3 files
  Normalize → head-noun merge on `label` → synonym merge → id assignment. Append-only vocabulary.
  Keep the **empty-synonym-map contrast** test, which is what proves the map is load-bearing.

- **D2.3 — `motif-derive`** · **M** · 2 files
  Family inheritance → own text → trim 3–5 family-first; anti-motifs by contrast; `basis` propagated;
  **A2's tautology case flagged in the output**, not left for the consumer to re-derive.

#### ✅ CP-D2 — REACHED 2026-08-31 (full evidence: seedsmith-todo.md's own CP-D2)
- [x] Every D2 artifact byte-identical across re-runs
- [x] **Zero real model calls in the suite** — `MockModelServer` only, offline, no credentials
- [x] `blocked` demons carry no family and no motifs, and this is not an error anywhere
- [x] Append-only proven: adding a demon leaves existing family and motif ids untouched

### Phase D3 — measurement (gates D4)

- **D3.1 — `Coverage/DemonUncovered`** · **S** · 2 files
  Per-demon, with a **per-kind breakdown** in the evidence. A5's exact case asserted: one demon
  uncovered while all its families are covered ⇒ one finding.

- **D3.2 — `Distribution/MotifSharing`** · **S** · 2 files
  `demonsPerMotif`, `demonCount`, `excludedTautological`, `singleUseMotifs`. `loop = OPEN`, **no
  verdict field**. The decisive test: a **wholly tautological corpus reports "cannot be measured"**,
  not perfect sharing — without it this metric would be worse than absent, it would be reassuring.

#### ✅ CP-D3 — the gate — REACHED 2026-08-31 (full evidence: seedsmith-todo.md's own CP-D3)
- [x] Both metrics ship `gates = False`
- [x] Both live in `metrics/`, and work for a non-demon adapter supplying the same strata
- [x] The tautology test passes — **D4 does not start until it does**

### Phase D4 — consumption

- **D4.1 — theme registry + items vocabulary** · **M** · 3 files
  `demons/_registry/themes.v1.json` emitted, append-only, `demon.*`-prefixed. Items' `themeKey`
  becomes registry-backed. Carries motifs, anti-motifs, expression rules, `basis`, and the `rarity`
  it was published against.

- **D4.2 — coexistence and churn proof** · **S** · tests only
  All **38 existing themed entries** (30 sets + 8 uniques, 5 legacy `theme.*` keys — count corrected
  2026-08-31, the original 31/39 was off by one) still validate; a retired demon's theme stays
  resolvable; direction asserted structurally — nothing in `adapters/demons/` reads the items corpus.

#### ✅ CP-D4 — closes the feature — REACHED 2026-08-31 (full evidence: seedsmith-todo.md's own CP-D4)
- [x] Full seedsmith suite green; full `dotnet test` green; four guard scripts green
- [x] Exactly **one** file outside `adapters/demons/` changed in D4, and it adds a vocabulary
- [x] An item can be authored themed to a demon and validates

## Task summary

| Task | Module | Size | Deps | Model calls |
|---|---|---|---|---|
| D1.1 | `demon-corpus-emit` (pure) | M | — | none |
| D1.2 | `demon-corpus-emit` (tool) | M | D1.1 | none |
| D1.3 | `adapter-demons` | M | D1.2 | none |
| D1.4 | integration | S | D1.3 | none |
| D2.1 | `family-extract` | M | D1.4 | faked |
| D2.2 | `family-consolidate` | M | D2.1 | faked (residue pass only) |
| D2.3 | `motif-derive` | M | D2.2 | none |
| D3.1 | `demon-metrics` coverage | S | D2.3 | none |
| D3.2 | `demon-metrics` sharing | S | D2.3 | none |
| D4.1 | `demon-themes` | M | CP-D3 | none |
| D4.2 | `demon-themes` proof | S | D4.1 | none |

Eleven tasks, four checkpoints, no task above M.

## Risks — all six materialized as real findings during the build, not hypothetical

| Risk | Impact | Mitigation | Outcome (2026-08-31) |
|---|---|---|---|
| §D-F1's core change is treated as a failure and worked around with option B or C | Medium | The claim is already recorded as false in this plan; D1.3's criterion is amended so the honest path is the specified one | ✅ Built as planned — one additive `KindSpec.motif_expression` field; `spec-adapter-demons.md` §1/§4 corrected |
| Extraction yields one family per demon (no sharing), making the whole taxonomy decorative | **High** | D2.1's falsifier test catches it at build time; D3.2's `singleUseMotifs` catches it on real data | ✅ Falsifier passed (batch size 1 → 3 distinct labels, proving batching is real) |
| A2's tautology — motifs and families both derived from the name, every metric green | **High** | `basis` propagates from D2.1 through D2.3; D3.2 excludes those pairs and reports how many it excluded | ✅ The decisive test passed: a wholly-tautological corpus reports "cannot be measured", never perfect sharing |
| Roster growth silently moves partitions and rarity-keyed fixtures | Medium | §D-F3; synthetic fixtures only; `demonCount` reported beside every figure | ✅ Confirmed live: real `check` run found 3 genuine empty partitions (roster-growth artifact, not a bug) |
| A rarity/theme artifact outlives its evidence once `power-estimate` lands | Medium | Themes record the `rarity` they were published against; provisional tiers are marked | ✅ `PublishedTheme.rarity` proven never re-derived on republish (`test_republishing_never_recomputes...`) |
| D4 breaks the 38 existing themed entries | **High** | The `demon.*` / `theme.*` prefix split makes collision impossible by construction; D4.2 asserts all 38 | ✅ Verified against the **live** items corpus, not a fixture — all 38 (count corrected from 39) still validate |

## Out of scope for Part 4

- **Real model calls.** Same rule as Parts 2–3: every criterion is provable against `MockModelServer`
  or a synthetic fixture. The first real run is a separate, owner-approved act after CP-D4.
- **`aspect` generation** — blocked on `aspect-scope` being built by the demon program (§D-F2).
- **`power-estimate` (D5)** — decided 2026-08-31 (LLM tier from almanac text, `basis`-tagged,
  **provisional**), but **not specced**. It also needs a `provisional` marker on the species side,
  which is demon-program code. Needs its own spec before it can be planned.
- **`lore-enrich`** — measured unnecessary as a prerequisite (§D-F4); it remains the answer for
  `basis = "name"` demons, later.
- **Promoting either D3 metric to `gates = True`** — a separate, later act, per the standing rule.

---

# Part 5 — Feature 3: generation runtime (G0–G4)

Map: [seedsmith-map.md](../docs/architecture/seedsmith-map.md) §3d ·
Proposal: [seedsmith-agent-runtime-proposal.md](../docs/architecture/seedsmith-agent-runtime-proposal.md) ·
Audits: [agent-runtime-proposal](../docs/architecture/seedsmith/review/audit-agent-runtime-proposal.md) (8 findings) ·
[generation-runtime-specs](../docs/architecture/seedsmith/review/audit-generation-runtime-specs.md) (10 findings)

**Five module specs, all SEALED 2026-09-01, zero open questions.** Every design decision was closed
by measurement before this plan was written — three of them answering the *opposite* of what the
question assumed.

## Why this feature exists

Part 4 built a **classifier**: 84 species sorted into families, **zero content generated**.
`Coverage/DemonUncovered` reports 84 gaps because `aspect`, `commander-effect` and `environment` are
declared kinds nothing writes into. Part 5 is the generator.

## Decisions already locked (do not re-litigate during the build)

| Decision | Choice | Evidence |
|---|---|---|
| Workflow engine | **LangGraph** `==1.2.11` | 4 claims verified by execution; nodes stay plain functions |
| Structured output | **LM Studio constrained decoding**, zero deps | Hostile-prompt A/B: unconstrained returned prose and failed `json.loads`; constrained conformed at no latency cost |
| Checkpoint store | **`SqliteSaver`** | Owner: seedsmith never ships, so the shipped-game SQL invariant does not reach it. **Scope: checkpoints only** |
| Model | **Local Gemma-26B** | 8/8 first-attempt pass, 0/8 anti-motif violations |
| Motif cleanup instrument | **POS filtering, NOT a frequency floor** | Frequency fails both ways: keeps `为什么` (df 3), risks `樱桃` (df 9) |
| CoVe | **Specified, NOT built** | Subjective form 1/3 (useless); source-grounded 2/3; root cause is bad motifs, which G1 fixes free |
| Commander effects per demon | **Exactly one** | 3 generations produced 2 identical names, mean Jaccard 0.52 = synonyms |
| `environment` generation | **Cancelled** | Deterministic mapping — `spec-pipeline.md:109` |

## Dependency graph

```
G0 dependency-baseline  (BLOCKING — a fresh clone fails a test today)
        |
        +--------------+--------------
        v              v
  G1 motif-prose   G2 workflow-runtime
     -filter            |
     (no model)         v
        |         G3 quality-gates
        |              |
        +------+-------+
               v
      G4 commander-effect
```

`G1` and `G2` are independent once `G0` lands and may run in parallel. **`G1` must precede `G4`'s
real model run** — generating from today's `一类`/`僵尸` motifs would bake bad input into committed,
append-only content.

## Slicing

Vertical, as in Parts 1–4: every task ends with a passing test, and where it produces an artifact,
that artifact committed. No task is "add the schema" with its consumer in a later task.

**G0 and G1 have standalone value with zero model calls** — G0 fixes a broken fresh clone, G1 fixes
every motif in the corpus. Neither needs LangGraph.

## Phases and checkpoints

### Phase G0 — dependency baseline (blocking)

- **G0.1 — `pyproject.toml`, exact pins, lockfile, isolated venv** · **M** · 2 files
  Declare `jieba` (the undeclared D2.3 debt), `langgraph==1.2.11`, `langgraph-checkpoint-sqlite`.
  CI installs from the lockfile.
- **G0.2 — offline guarantee as a test** · **S** · 1 file
  Assert `LANGSMITH_TRACING`/`LANGCHAIN_TRACING_V2` unset; run a graph under a socket guard that
  raises on any non-loopback connection.
- **G0.3 — `response_format` in `llm_caller`** · **S** · 2 files
  Optional parameter, `None` default. **Must be provably inert for every existing caller.**

#### ✅ CP-G0
- [ ] Fresh clone + clean venv + install from lockfile + full suite **passes** (not a fixed number)
- [ ] `import jieba` succeeds in a fresh venv
- [ ] Offline guarantee is a passing test
- [ ] `call_model(schema=None)` produces a byte-identical request body to today

### Phase G1 — motif prose filter (no model, no framework)

- **G1.1 — four-rule line classifier** · **S** · 2 files
  `label：value`, section headers, **circled numerals `①…⑫`**, **ASCII digits**. Pure function.
- **G1.2 — POS filtering via `jieba.posseg`** · **M** · 2 files
  Keep `n*`/`v*`/`a*`/`i`/`l`; drop `r`/`c`/`d`/`p`/`u`/`t`. `_CJK_STOPWORDS` shrinks to an override.
- **G1.3 — wire in, regenerate, verify** · **M** · 3 files + regenerated data
  ⚠️ Regeneration **drops** motif ids like `一类` from an append-only vocabulary. Safe **only while no
  content is bound to them** — true today (84/84 demons have zero content), and it is why G1 precedes
  G4. A reviewed correction, not a routine re-run.

#### ✅ CP-G1
- [ ] `一类`, `伤害`, `优先` gone from the three named regression demons
- [ ] `为什么`/`是因为`/`不过` dropped by POS; `铁头功`/`坚果` kept
- [ ] `可在三种攻击模式之间切换` survives (CJK numeral is not an ASCII digit)
- [ ] Determinism and the ≤4-char token guarantee hold
- [ ] Rise in `basis="name"`/`blocked` reported as a **result**, not a failure

### Phase G2 — workflow runtime (parallel with G1)

- **G2.1 — state + nodes, no LangGraph** · **M** · 5 files
  `state.py` + `nodes/`. **Seam test: zero LangGraph imports outside `graphs/`, asserted by grep.**
- **G2.2 — graph skeleton and bounded loops** · **M** · 2 files
  generate→validate→route; three independent stops (`attempts`, `recursion_limit`, terminal
  `escalate`).
- **G2.3 — checkpointing and resume** · **M** · 2 files
  `SqliteSaver`, thread-id per subject. Kill mid-run then resume without re-calling the model.
- **G2.4 — bounded fan-out runner** · **S** · 2 files

#### ✅ CP-G2
- [ ] **Zero** LangGraph imports in `nodes/`/`state.py`, asserted
- [ ] Graph structure assertable **offline** — no model, no network
- [ ] Kill mid-run then resume; finished nodes do not re-call the model
- [ ] A deliberate routing bug is still stopped by `recursion_limit`
- [ ] Transient vs quality retry are demonstrably different code paths

### Phase G3 — quality gates

- **G3.1 — deterministic validator library** · **M** · 4 files
  `motif_coverage`, `anti_motif_violation`, `field_echo`, `non_empty`. Each with a positive **and**
  negative test — `field_echo` must reject `"DOCTRINE: …"` and accept `"The doctrine of …"`.
- **G3.2 — tier labelling** · **S** · 2 files
  Every result carries its tier; no summary reports tier-2 as quality.
- **G3.3 — CoVe: specified, wired off** · **M** · 3 files
  Source-grounded questions only; rejection escalates, never auto-repairs; **asserted disabled**.

#### ✅ CP-G3
- [ ] All four validators exist with positive and negative tests
- [ ] Tier labelling exists; nothing reports a pass rate as quality
- [ ] CoVe present, **not wired into the default graph**, asserted
- [ ] Zero real model calls in the suite

### Phase G4 — commander-effect (the first real generator)

- **G4.1 — brief, schema, gate** · **M** · 2 files
  Expression rule inlined literally; schema numeric-free and verdict-free.
- **G4.2 — graph wiring** · **S** · 2 files
- **G4.3 — real run + quality sample** · **M** · run + committed data
  ⛔ **Only after G1 has landed.** Report quality from a **read stratified sample**, never from the
  tier-2 pass rate.

#### ✅ CP-G4 — closes the feature
- [ ] Every non-blocked demon has a commander effect; `Coverage/DemonUncovered` falls by that count
- [ ] `blocked` demons generate nothing, provably
- [ ] An unprefixed id fails corpus load (the `wallnut` collision), asserted
- [ ] Re-run produces zero new writes
- [ ] Quality reported from a read sample, **separately** from the pass rate
- [ ] Full suite green; four guard scripts green

## Task summary

| Task | Module | Size | Deps | Model calls |
|---|---|---|---|---|
| G0.1–G0.3 | `dependency-baseline` | M+S+S | — | none |
| G1.1–G1.3 | `motif-prose-filter` | S+M+M | G0 | **none** |
| G2.1–G2.4 | `workflow-runtime` | M+M+M+S | G0 | faked |
| G3.1–G3.3 | `quality-gates` | M+S+M | G2 | faked |
| G4.1–G4.3 | `commander-effect` | M+S+M | G1,G2,G3 | **real, owner-approved** |

Sixteen tasks, five checkpoints, no task above M.

## Risks

| Risk | Impact | Mitigation |
|---|---|---|
| LangGraph spreads beyond `graphs/`, making it unremovable | **High** | Seam asserted by grep test (G2.1), not by discipline |
| G4 runs before G1, baking stat-vocabulary motifs into append-only content | **High** | Build order; named a **Never** in `spec-commander-effect` §7 |
| Append-only motif ids dropped by G1.3 break bound content | Medium | Safe only while nothing is bound — true today, closes when G4 writes its first row |
| A tier-2 pass rate gets reported as quality | Medium | Tier labelling (G3.2) + a read sample required at G4.3 |
| CoVe gets built because it is specified | Low | Asserted **disabled** (G3.3); build gated on measured need |
| An agent loop fails to terminate | **High** (28.1% of field failures) | Three independent stops, each tested (G2.2) |

## Out of scope for Part 5

- **`aspect` generation** — blocked on `aspect-scope` being built by the demon program.
- **`environment` generation** — cancelled; deterministic mapping.
- **`lore-enrich`** — deferred, and blocked on `basis="enriched"` existing first.
- **Enabling CoVe or self-consistency** — both specified, both off, both gated on measurement.
- **Merging generated content onto corpus entries** so `Distribution/MotifSharing` can measure —
  real, still unspecced, and now the natural next feature.
