# Plan: Seedsmith W1 — measurement

Map: [docs/architecture/seedsmith-map.md](../docs/architecture/seedsmith-map.md)
Specs: [seedsmith/](../docs/architecture/seedsmith/) — analytics · numerics · budget · metrics · planner · pipeline · foundation
Audit: [seedsmith/review/](../docs/architecture/seedsmith/review/) — 66 findings, 11 blockers, all closed

> `tasks/plan.md` and `tasks/todo.md` belong to the **perf-v3** stream, so this program uses the
> prefixed pair (AGENTS.md, parallel programs).

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

## Out of scope

`planner`, `briefkit`, and `pipeline`'s *generation* logic — schemas, briefs, the actual act of
writing content into the corpus — all still gate on `metrics`/`planner` existing (W2/W3). `llm_caller`
(S0) is explicitly **not** in that bucket: it is the transport underneath `pipeline`, has no edge to
`metrics` or `planner`, and is in scope now as its own slice.

Also out of scope: closing the eight accidental empty partitions — they are W2's known-answer test
and must stay open · the adjective `axis` registry addition · any change to `tools/ItemSeedValidator`,
which stays the referential gate.
