# Seedsmith — `planner`

**Status:** Proposed 2026-08-23. Nothing is built.

Turns findings into a dispatchable work order. **Deterministic — no model decides what work to do**
(P4). Every expensive failure in the agentic build was a planning failure that a model then executed
faithfully, so this module is where that class of failure gets designed out.

---

## 1. What it must never do again

Three real incidents define the requirements:

| Incident | Cost | Requirement |
|---|---|---|
| 5 themes × 15 uniques competing for 8 roles × 5 axes = 40 slots | 18 agents ran an impossible allocation | **Prove satisfiability before dispatch** (§2) |
| Drop tables labelled the same stage as the uniques they reference | 274 errors after the fact | **Derive order from the graph** (§3) |
| An exemplar taught a wrong shape to every agent that read it, three separate times | 3 re-runs, ~40 agents | **Validate inputs before dispatch** (§4) |

Each is cheap to check and was expensive to discover. That asymmetry is the module's whole
justification.

---

## 2. Feasibility — refuse to dispatch the impossible

Three layers, cheapest first, short-circuiting on the first failure.

**2.1 Pigeonhole.** `Σ demand > Σ capacity` ⇒ infeasible. O(n). Would have caught 75-into-40 in
microseconds.

**2.2 Bipartite matching.** Totals can fit while a subset starves — Hall's condition failing on some
subset of demands whose reachable slots are too few. Checking every subset is exponential, so run
**Hopcroft–Karp** (O(E√V)) on the demand↔slot graph. Max matching < demand ⇒ infeasible.

**2.3 Name the binding constraint.** "Infeasible" is not actionable. **König's theorem** turns the
maximum matching into a minimum vertex cover, and that cover *is* the set of constraints doing the
blocking. The finding reads *"these six (role, axis) pairs are the bottleneck; either widen the role
allocation or reduce per-partition count to 8"* — a decision, not a shrug.

**2.4 Emit the construction, do not search for it.** When the demand is balanced — n themes each
needing a distinct axis per role — a cyclic Latin square `axis = (roleIndex + themeIndex) mod n` is
a closed-form solution. Emit it, verify no collisions, attach it to the brief. This was done by hand
for the unique re-run and produced 144 collision-free triples on the first attempt; there is no
reason for a human or a model to redo it.

---

## 3. Ordering — derive stages, never label them

Kinds carried hand-written stage tags. Drop tables were tagged `1c` while referencing uniques also
tagged `1c`, and the mislabel surfaced as 274 errors only once the references existed.

Instead: build the kind-level reference graph from the corpus and the entry shapes, and run **Kahn's
topological sort**. The layers *are* the stages.

- A **cycle** means no valid order exists. **Tarjan's SCC** (O(V+E)) names the component so the
  report says which kinds are mutually entangled rather than "cycle detected".
- Ordering is recomputed every run, so it cannot drift from the graph it describes. The drop-table
  layer would have moved the instant the first drop table referenced a unique.

**Within a layer**, partitions are independent by construction and dispatch in parallel. That is
what a layer *means*, and it is the only safe basis for concurrency — the earlier waves parallelised
by intuition and hit the shared-word-pool collision twice.

---

## 4. Input validation — check the exemplar before the fleet reads it

An exemplar is the most-read file in a corpus during authoring, and a wrong one is indistinguishable
from a wrong contract. Three separate defects propagated this way — `powerAxis` missing, display
templates, set members declared by role alone.

So before any work order is emitted, every exemplar the order references is **validated as real
content of its own kind**, through the same gates as shipping content. An exemplar that would fail
as an entry fails here, and the work order does not go out. Cost: one validation pass. Benefit:
three re-runs that would not have happened.

Same check for briefs: any registry value a brief cites must resolve at emit time, so a stale
constant cannot reach an agent.

---

## 5. Scheduling

Once layered and feasible, the order is a list-scheduling problem under a concurrency cap:

- **Layer by layer**, never crossing a dependency edge.
- **Longest-job-first within a layer** — classic list scheduling. Partitions with more entries take
  longer, and starting them first shortens the makespan.
- **Concurrency cap** from configuration, not guessed.
- **Model tier by rule table**, not optimisation: partitions that invent identity get the stronger
  model, partitions that consume a fixed vocabulary get the cheaper one. A table is auditable and an
  optimiser is not.

---

## 6. Output

A work order is data, not prose:

```json
{ "budgetVersion": 3, "corpusRevision": "…",
  "layers": [
    { "layer": 1, "parallel": true, "jobs": [
      { "partition": "base-types/footing/plant/a",
        "kind": "base-type", "entries": 12,
        "brief": "…/briefs/base-types_footing_plant_a.md",
        "model": "sonnet",
        "constraints": { "role": "footing", "frame": "plant", "band": "a" },
        "closes": ["Coverage/EmptyPartition"] } ] } ],
  "feasible": true,
  "refusals": [] }
```

Every job names the finding it closes, so after execution `metrics` re-runs and the work order is
graded automatically: a job that ran and did not clear its finding is a **pipeline defect**, not a
content defect, and gets reported as such. Without that link, a failed generation looks identical to
content that was never attempted.

---

## 7. Known-answer test

The eight empty partitions are deliberately left open (map §7.4) as this module's acceptance test:
`metrics` must find exactly those eight, `gems/2` must be placed after its registry dependency, and
the three display-template partitions after the affix families they render. If the plan matches what
a human would write, the module works.

**⛔ Corrected 2026-08-31 — the four base-type partitions are EXCLUDED, not layered.** This section
previously read *"`planner` must place the four base-type partitions in the base-type layer"*. That
sentence predates **S2's own finding** and is wrong: those four cells' `_meta.partition` **string** is
wrong while their entries' own `role`/`frame` fields are **intact**. They already hold real content.
Scheduling them as generation jobs would author duplicates of rows that exist — they need a **corpus
relabel**, which is not a generation job and must not be confused with one.

The planner therefore reports them under `excluded[]` with the reason
`mislabeled-partition-needs-relabel-not-generation`, rather than dropping them silently (a partition
that vanishes with no explanation reads as a planner bug, and someone re-adds it).

Built and tested this way in `seedsmith/planner/schedule.py`; the resolution is pinned in that
module's docstring and in `tests/test_schedule.py` so it is not re-derived from the superseded
sentence. Corrected here per the design gate's own propagation rule — a correction that lands in
code but not in its sibling spec has not landed.

---

## 8. Generation pipelines — the architecture the agentic build never had

**Owner direction, 2026-08-23.** Reviewing the set-overlap and Appendix-A blockers, the owner
identified one root cause behind both:

> *The old agentic process had no real pipeline and no order. That is why it could not generate set
> items correctly. The correct way is: generate the set, fix the number of slots, generate the bonus
> for each piece count, generate set-only affixes, then generate base items to match the slots — or
> reuse base items already generated. Same with recipes: we failed because the correct order is base
> items and material definitions first; you cannot make a recipe with no material and no craft
> result.*

That is right, and it reframes §3. Topological ordering of **kinds** is only half the problem —
ordering also exists **inside** a kind, and no document held it.

### 8.1 A kind is generated by ordered stages, not one call

A set is not one generation step. It is five, and they must run in this order:

| # | Stage | Produces | Kind | Gate |
|---|---|---|---|---|
| 1 | `set.header` | id, name, theme | identity → model | naming, collision |
| 2 | `set.slots` | which roles, how many | **deterministic** — role legality, hybrid core, the 6-role cap | `SetRoleCap`, `SetRoleNotHybridCore` |
| 3 | `set.thresholds` | bonus at each piece count | **deterministic** structure (2 always, 4 for grand); model picks families | `SetNoTwoPieceThreshold`, `SetGrandMissingStep` |
| 4 | `set.affixes` | set-only families | identity → model; magnitude → `numerics` | vocabulary, no-numbers |
| 5 | `set.members` | slot → concrete base type | **deterministic** — reuse an existing base type, or raise a demand for one | `SetUncompletable`, `UniqueSetMembership` |

Stage 5 is where the agentic build broke. It was never run at all: 30 sets shipped with members
declared by role and no base type behind any of them, and every reference in those files resolved
because there was no reference to resolve.

Note the split down the middle: **stages 2, 3 and 5 are deterministic.** Only 1 and 4 need a model.
The wave that produced 30 broken sets asked one agent to do all five in one pass, so the three
mechanical stages inherited the failure modes of the two creative ones.

### 8.2 Demand declaration precedes fulfilment

Stage 5 can discover it needs a base type that does not exist. That looks like a backward edge and
would break a simple topological order — which is exactly why "generate sets" and "generate base
types" could not be sequenced by hand.

The fix is two phases, and it generalises to every kind:

**Phase A — declare.** Every kind runs its deterministic stages and emits *demands*: "this set needs
a plant `core-guard` base type at band b", "this recipe needs a `catalyst.forge` material". Nothing
creative runs. No file is written.

**Phase B — fulfil.** The planner now holds the entire demand graph. It topologically sorts it,
checks feasibility (§2), reuses whatever already exists, and generates only the genuine shortfall —
in dependency order, by construction.

This is what makes the owner's recipe example impossible to get wrong: a recipe's demand for
`catalyst.forge` is declared in Phase A, so materials are provably generated before any recipe is
written. The old process had no Phase A, so ordering was a thing a human remembered or did not.

### 8.3 Reuse is the default, not the exception

Phase B resolves a demand against existing content first. That is why **set overlap needs no cap**
(owner decision, superseding the audit's structural-cap recommendation): the pipeline decides which
base type serves which set with full sight of every other set, so concentration is a *planner
policy* — spread demands across candidates — rather than a rule bolted on afterwards.

The measured overlap under the hand-written binder was already mild: of 154 base types used as
members, 129 serve one set, 24 serve two, one serves three. A planner that can see the whole demand
graph can do at least as well deliberately, and unlike a cap it can weigh theme fit against spread
instead of refusing at an arbitrary number.

What the audit was right about survives as a **metric, not a cap**: equip-concentration across the
option space is worth measuring, because a win-rate sweep cannot see it. That belongs in the
`Distribution` family, over member demands rather than over corpus counts.

### 8.4 What this does and does not fix

**Fixes** — every ordering defect in the build log: recipes before materials, sets without members,
drop tables referencing same-stage uniques, the 274 `SameStageReference` errors. All were one class,
and Phase A/B removes it structurally rather than by remembering.

**Does not fix** — the other half of Appendix A's missing row. Several of the eleven
checker-was-wrong incidents were *logic bugs in the checking code itself*: the inverted overlap
guardrail found in this very audit, `idLike` silently not matching underscores, the exemplar
squatting in a cross-row ledger, `TagAxisNotApplicable` gating on a field the registry calls
advisory. A pipeline cannot catch a wrong predicate — it will run the wrong check in exactly the
right order.

That residue needs its own answer, and this repo already has the tool: **`scripts/mutate.ps1`**.
Mutation testing on the metric suite — break each check on purpose, confirm a fixture notices — is
the only mechanism that tests the tester. Cheap, established here, and it belongs in W1 beside the
metrics rather than as a later nicety.
