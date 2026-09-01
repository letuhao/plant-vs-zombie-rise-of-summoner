# Spec: `workflow-runtime`

Module `workflow-runtime` in the [seedsmith map](../seedsmith-map.md) §3d.
Depends on `dependency-baseline`. Enables `quality-gates` and every generator.

Proposal: [seedsmith-agent-runtime-proposal.md](../seedsmith-agent-runtime-proposal.md);
`R#` = [audit](review/audit-agent-runtime-proposal.md).

**Status: SEALED — approved by the owner 2026-09-01. Authorized to build.**

---

## 1. Objective

Give seedsmith a way to **define a workflow** — the steps inside one generation, with typed state,
explicit branching, bounded retry, and crash-resume.

**The layer distinction is the whole reason this module exists**, and an earlier proposal got it
wrong by collapsing the two:

| Layer | Question | Owner |
|---|---|---|
| Job orchestration | *Which content, in what order, under what constraints?* | `planner` — **solved, kept, untouched** |
| **Workflow definition** | *Inside ONE generation: what steps, what state, when to branch/retry/resume?* | **This module. Nothing does it today** |

Today layer 2 is a hand-rolled serial `for` loop in
[`family/extract.py`](../../../tools/seedsmith/seedsmith/adapters/demons/family/extract.py) — no state
model, no branching, no checkpoint, no resume, no concurrency. Repeating that shape across every
future generator is the problem this module ends.

**Done means:** one generator (`commander-effect`) runs as a graph, survives a kill -9 and resumes,
and every node is unit-testable without LangGraph or a model.

---

## 2. Design

### 2.1 ⛔ The seam — the single most important constraint in this spec

**LangGraph is imported in `workflow/graphs/` and nowhere else.**

Node bodies live in `workflow/nodes/`, are plain functions of
`(state: dict) -> dict`, and import **nothing** from LangGraph. The graph modules are thin wiring.

**Why this is non-negotiable:** LangGraph released 10 times in 2026-04 and 1 in 2026-08 (measured) —
it moves. If it must ever be replaced, **every node survives unchanged** and only the wiring is
rewritten. A framework that has spread through the codebase cannot be removed; one confined to a
wiring layer can.

This is also what makes the nodes testable: a node is a function you call with a dict.

### 2.2 State is a bounded `TypedDict`, never a growing transcript

Documented failure mode (R2 §2.3): agents exceed the context window because intermediate outputs
accumulate. State therefore carries **ids and small structs**, never message history:

```python
class GenerationState(TypedDict):
    subject_id: str            # the demon
    brief: str                 # assembled once, by briefkit
    draft: dict | None         # the current candidate
    defects: list[str]         # from the last validate pass — drives repair
    attempts: int              # bounded; see §2.3
    verified: bool             # CoVe outcome, if run
    outcome: Literal["pending", "persisted", "escalated", "blocked"]
```

**Bounded, stated precisely (audit S10):** `subject_id`, `attempts`, `verified` and `outcome` are
fixed-size. `brief` is **bounded by construction — one subject's brief, assembled once, never
accumulated across steps**; it is replaced on each pass, never appended to. `defects` is bounded by
the retry limit. **No `messages: list` accumulator anywhere** — that is the rule that actually
prevents the context-overflow failure, and it is asserted in §6.

Narrow scope per call is already `spec-pipeline.md` §3.2's rule; this makes it structural.

### 2.3 ⛔ Bounded loops are structural, because "not stopping" is the #1 failure

Measured across the field: **28.1% of all observed agent failures are the agent failing to
terminate** — step repetition 15.7%, unaware of termination conditions 12.4%. It is the single
largest failure category, larger than anything about prompt quality.

Three independent stops, all required:

1. `attempts` in state, checked by the routing function.
2. LangGraph's own `recursion_limit` on `compile()` — a backstop if routing is ever wrong.
3. A terminal `escalate` node — exhausting retries is an **outcome**, never a silent give-up.

**No unbounded `while` anywhere in this module.** The retry budget matches
`spec-pipeline.md` §3.6's existing `max_retries = 2` (1 initial + 2 heals); it is a **structural
constant with a comment**, not a tunable.

### 2.4 ⛔ Two retry intents, never conflated

Documented failure (R2 §2.2): *"traditional idempotency breaks when outputs are stochastic"* — and a
$50 batch retried 3× on a network blip costs $200.

| Intent | Trigger | Correct behaviour |
|---|---|---|
| **Transient** | endpoint unreachable, timeout, 5xx | **Replay from checkpoint. No new model call.** The previous answer is still wanted |
| **Quality** | the draft failed a validator | **A genuinely new generation**, with the named defect fed back |

Conflating them is where production bugs are born, in both directions: regenerating on a network blip
burns budget and churns output; replaying a cached bad answer loops forever. The graph expresses them
differently — transient is checkpoint resume (outside the graph's own edges), quality is the
`validate → generate` edge.

### 2.5 Checkpointing — `SqliteSaver`, owner-approved

`langgraph-checkpoint-sqlite`, thread-id keyed per subject. §5's 30–90 minute runs make crash-resume
load-bearing, not decorative.

**Owner decision, 2026-09-01:** *"seedsmith is a tool outside the game, it is dev tool not ship in
release."* `guard-dal`'s SQL invariant protects the **shipped game's** data layer; `tools/seedsmith/`
is dev tooling that never ships. **Scope is pinned: `sqlite3` here is for checkpoint state only.**
Python still never reads the game's SQLite (`types`, `almanac_seed`, `recipes`) — that stays
C#-through-the-DAL per [`spec-demon-corpus-emit.md`](spec-demon-corpus-emit.md), for that spec's own
reason, which shipping does not affect.

### 2.6 Fan-out with bounded concurrency

`extract_family_candidates` is serial today. The runner fans out over a work order with a **bounded**
worker count — unbounded concurrency against one local model queue makes latency worse, not better
(`llm_caller`'s existing comment: *"hammering a wedged local queue with retries makes it worse"*).

Concurrency is a **structural constant with a comment**, not a tunable: it trades local-queue
saturation against wall-clock, and a balance pass would never touch it.

### 2.7 What this module does not do

No generation logic (that is `commander-effect`), no validators (that is `quality-gates`), no changes
to `planner`, `briefkit`, `metrics`, `corpus`, or existing `llm_caller` callers.

---

## 3. Commands

```powershell
cd tools\seedsmith
python -m pytest tests/test_workflow_runtime.py -q
python -m pytest tests/test_workflow_structure.py -q   # offline, no model, no network
python -m pytest -q
```

---

## 4. Project structure

```
tools/seedsmith/seedsmith/workflow/
    __init__.py
    state.py        → GenerationState + outcome enum. NO langgraph import
    nodes/          → plain functions. NO langgraph import
        __init__.py  brief.py  generate.py  persist.py
    graphs/         → the ONLY modules importing langgraph
        __init__.py  base.py        # shared skeleton: generate→validate→route
    runner.py       → fan-out, bounded concurrency, checkpoint config
tools/seedsmith/tests/
    test_workflow_runtime.py     → behaviour, against MockModelServer
    test_workflow_structure.py   → offline graph-shape assertions
```

`nodes/` and `state.py` importing nothing from LangGraph is **asserted by test** (§6), not left to
discipline — the seam is the deliverable.

---

## 5. Code style

Match `pipeline/run.py`: pure functions for anything decidable without a model; the model call behind
the injected seam so tests never reach the network. `MockModelServer` is reused from
`test_llm_caller`, never re-rolled. Nodes return **partial** state dicts (LangGraph merges), so each
node's contract is "what I changed", which reads and tests well.

---

## 6. Testing strategy

| Case | Expect |
|---|---|
| ⛔ **`nodes/` and `state.py` import LangGraph** | **zero** — grepped by test; the seam (§2.1) is enforced, not trusted |
| Graph structure | nodes/edges assertable **offline**, no model, no network |
| Clean draft | `generate → validate → persist`, `attempts == 1` |
| Draft with defects, then clean | repairs and persists; the second prompt **contains the named defect** |
| Draft that never clears | **escalates** at the retry bound; writes nothing |
| ⛔ Routing bug that would loop forever | `recursion_limit` **still stops it** — the backstop is exercised, not merely configured |
| Kill mid-run, re-invoke same `thread_id` | resumes from checkpoint; **the completed node does not re-call the model** |
| Transient failure | replay, **zero** new model calls (§2.4) |
| Quality failure | a **new** generation with the defect attached (§2.4) |
| State after N repairs | no unbounded field growth (§2.2) |
| Fan-out over K subjects | bounded concurrency respected; results deterministic per subject |
| `LANGSMITH_TRACING` unset + non-loopback socket guard | no outbound call (inherited from `dependency-baseline`) |

The seam test and the `recursion_limit` test are the two that must never be relaxed: one protects
against the framework, the other against ourselves.

---

## 7. Boundaries

- **Always:** keep LangGraph imports inside `graphs/`; bound every loop three ways; distinguish the
  two retry intents; keep state fields bounded; reuse `MockModelServer`.
- **Ask first:** raising the retry bound or concurrency; adding a LangGraph API beyond the eight
  primitives already probed; any second checkpoint backend.
- **Never:** an unbounded `while`; a `messages` accumulator in state; a LangGraph import in
  `nodes/`; reading the game's SQLite from Python (§2.5); enabling LangSmith tracing.

---

## 8. Success criteria

1. `commander-effect` runs as a graph, end to end, against `MockModelServer`.
2. Kill mid-run → resume completes without re-calling the model for finished nodes.
3. Retries are bounded three independent ways, each proven by test.
4. **Zero** LangGraph imports outside `graphs/`, asserted.
5. Graph structure testable offline.
6. Transient and quality retries are demonstrably different code paths.
7. Full seedsmith suite green.

---

## 9. Open questions

None. Engine, checkpoint store, and pinning were decided by the owner 2026-09-01 (map §3d).
