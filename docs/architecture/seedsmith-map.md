# Seedsmith — capability map

**Status:** Map approved 2026-08-23; all module specs written (§8). Nothing is built yet.

A Python application that owns the health of every seed corpus in the repo: it validates what is
there, measures what is missing or lopsided, and emits a **deterministically-planned** work order
for the LLM pipelines that fill the gaps. Items are the first feature; the core is feature-agnostic
by construction, because the second feature must not rewrite it.

> **Name.** `seedsmith` — decided. `seed-contract.md` §1 keeps *generator* for the thing that
> expands authored bands into ~30,000 database rows.

---

## 1. Why, in one paragraph of evidence

The item corpus reached 1,438 entries, 0 referential errors and 0 reachability gaps — while nine of
its 126 allocated partitions were **empty**, and nobody noticed for three waves. Two validators were
green because neither was asked "is there *enough*, and is it *evenly spread*?" Separately, every
gap that actually closed in that session closed by **script** — 180 set members, 144 acquisition
rows, 740 enhance tracks — in seconds, while the one lane that used agents ran three times because a
constraint lived in a document instead of in code.

Seedsmith is those two observations made permanent: **measure coverage and balance as first-class
properties, and let deterministic code do everything deterministic code can do.**

---

## 2. Principles

**P1 — The LLM writes identity; deterministic code writes magnitude.**
A model names a thing, gives it flavour, and chooses which concept it embodies. It never chooses a
number. This is `seed-contract.md` §3's no-numbers rule generalised from human authors to
pipelines, and the reason is unchanged: a model has no calibrated sense of scale, so a number it
picks is a plausible-looking guess that survives review because nothing looks wrong with it. Base
stats, tier magnitudes, drop weights, costs and curve points are resolved by `numerics` from bands,
role budget weights and the rarity ladder — never generated, never reviewed, never argued about.

**P2 — A metric without a declared target is an opinion.**
"Too few uniques" means nothing until something states how many there should be. The expected count
for uniques currently exists in three documents and disagrees three ways (20, 300, 144). `budget`
makes the target a single declarative artifact so every metric is *actual vs declared* and every
disagreement is a diff.

**P3 — Every metric declares whether it can verify its own fix.**
*Closed-loop*: "60 consumables have no flavour" — detectable, and the fix is verifiable, because the
field is populated or it is not. *Open-loop*: "the flavour is generic" — detectable, not verifiable
by machine. Open-loop metrics produce a review queue, never a pass. Without this split you get green
dashboards over prose nobody read.

**P4 — The plan is deterministic.**
Findings → work order is a pure function: which partitions to author, in what dependency order, at
what model tier, with which constraints. No model decides what work to do. This is the direct
lesson of the unique lane, where the expensive failures were all planning failures that a model
faithfully executed.

**P5 — Feature knowledge lives in adapters.**
The core knows about corpora, budgets, metrics, findings and plans. It knows nothing about roles,
frames, rung bands or drop tables. Everything item-shaped lives in `adapter-items`.

---

## 3. Modules

| id | Capability | Depends on |
|---|---|---|
| `corpus` | Load a seed folder into a typed, queryable graph; ids, kinds, partitions, edges | — |
| `numerics` | Deterministic value resolution: band → number, base stats, curve evaluation, budget-weight math. **P1 lives here.** | `corpus`, `adapter` |
| `budget` | Declarative targets — expected counts and distributions per kind, role, frame, band, element | `corpus`, `adapter` |
| `metrics` | The check catalogue: coverage, linkage, distribution, balance. Emits typed findings with severity and loop-kind | `corpus`, `budget`, `numerics` |
| `report` | Human CLI, CI gate, machine-readable findings for the planner, **deterministic sampling** for open-loop verdicts | `metrics` |
| `planner` | **Deterministic** findings → work order: partitions, ordering, model tier, constraints. Refuses provably-unsatisfiable orders | `metrics`, `budget` |
| `briefkit` | Work order → per-partition briefs, generated from allocation + budget + constraints, never transcribed | `planner`, `adapter-*` |
| `pipeline` | LLM execution: structured output schemas, guardrails, validate-before-accept, bounded retry | `briefkit`, `metrics` |
| `adapter-items` | All item-specific knowledge: kinds, registries, entry shapes, role/frame/band model | `corpus` |

**Dependency direction** is strictly downward in that table; nothing depends on `pipeline`.

```
corpus ── adapter ─┬─ numerics ─┐
                   ├─ budget ───┼─ metrics ─┬─ report
                   └────────────┴───────────┴─ planner ── briefkit ── pipeline
```

---

## 4. Build order

**W1 — measurement (standalone value).** `corpus`, `adapter-items`, `numerics`, `budget`, `metrics`,
`report`. On completion this finds the nine empty partitions automatically, plus every distribution
skew, without a single model call. Worth shipping alone even if W2 never happens.

**W2 — planning.** `planner`, `briefkit`. Turns findings into a dispatchable work order and the
briefs to execute it. Still no model calls.

**W3 — generation.** `pipeline`. The LLM layer, one pipeline per metric, guardrailed.

Each wave gates on the previous being green.

---

## 5. Boundaries — what seedsmith is not

- **Not the band→rows generator.** That expander is a separate, later thing (`seed-contract.md` §1).
- **Not a replacement for `tools/ItemSeedValidator`.** The C# validator stays the **referential**
  gate: ids resolve, vocabularies are closed, computed fields are absent. 71 tests, wired to CI, no
  reason to port. Seedsmith owns **sufficiency, balance, numerics and planning**. Two tools, two
  questions, one clean boundary. Consolidation is a later option, not a v1 goal.
- **Not a place for game-balance opinions.** It measures against `budget`. If the budget is wrong,
  the fix is a budget edit, not a metric edit.
- **`tools/seed_graph` is absorbed**, not kept alongside. Its `Corpus`, `Acquisition`, `Finding` and
  check registry become the first cut of `corpus` and `metrics`; its 16 tests come with them.

---

## 6. The metric catalogue, sketched

Not the spec — the shape, so the map can be judged. Each is closed-loop unless marked.

| Family | Asks |
|---|---|
| **Coverage** | Does every allocated partition have content? Every role×frame? Every element? *(This is the family that would have caught all nine empty partitions on day one.)* |
| **Linkage** | Is everything reachable and completable? *(Today's `seed_graph` checks.)* |
| **Distribution** | Is any kind, role, band or element over- or under-represented against `budget`? |
| **Balance** | Do resolved magnitudes sit inside their declared budget envelope? Are rarity rungs monotonic? |
| **Registration** | Is anything acquirable absent from every drop table? Any table entry pointing at nothing? |
| **Quality** *(open-loop)* | Flavour present, tone on-theme, names not clustered. Produces a review queue. |
| **Constraint** | Are the rules that live only in lane documents actually held? |
| **Feasibility** | Can the planned allocation be satisfied at all, before anything is dispatched? |
| **Exemplar conformance** | Does each exemplar validate as real content of its own kind? |
| **Semantic dedup** | Do two entries say the same thing in different rows? |

---

## 7. Decisions — resolved 2026-08-23

1. **Name** → `seedsmith`. "Generator" stays reserved for the band→rows expander.
2. **Budget authorship** → **derived, then corrected.** A script reads every count already stated in
   the SSOTs and the fleet plan, emits `budget.json`, and marks each conflict inline — the uniques
   row will read *20 (ssot §5.33) vs 300 (fleet plan) vs 144 (shipped)*. The owner resolves a marked
   diff rather than recalling numbers.
3. **v1 scope** → **items, plus a stub adapter that exists only in the test suite.** The core cannot
   quietly reach into item concepts if a second, fake adapter compiles and passes. Roughly 5% the
   cost of a real second feature and it fails loudly the moment the interface leaks.
4. **The 8 empty partitions** → **left open, as seedsmith's first work order.** Known-answer
   end-to-end test: `metrics` must find exactly those eight, `planner` must order them, `briefkit`
   must brief them.

---

## 7b. The operating model — what a human actually does

Owner decision, and it is the requirement that shapes everything else:

> *Seedsmith must cover every gap class the agentic generation produced. The human controls
> seedsmith, monitors metrics, samples output to validate by hand, and improves seedsmith when it
> turns out to have a coverage gap. Manual work is minimised.*

Four consequences that are not obvious:

- **`report` owes a sampling mode**, not just totals. "60 of 60 consumables now have flavour" is a
  closed-loop pass; a human still needs to read eight of them to know whether the flavour is any
  good. Sampling is how open-loop metrics (P3) get their verdict, so it is a first-class feature
  rather than a debugging convenience: `--sample N` per metric, deterministic seed so the same
  sample can be re-read.
- **A miss found by a human becomes a metric, permanently.** When sampling catches something the
  catalogue did not, the fix is a new metric plus its regression test — never a one-off content
  edit. That is the loop that makes manual effort decline over time instead of recurring.
- **The catalogue's completeness is itself testable.** Appendix A lists every defect class this
  corpus actually produced. A metric family must claim each row. An unclaimed row is a known
  coverage gap in seedsmith, visible rather than latent.
- **Feasibility is checked before dispatch, not after.** The single most expensive failure in the
  agentic build was an allocation that could not be satisfied — 75 uniques competing for 40
  (role, band, axis) slots — and eighteen agents faithfully executed it before anyone noticed.
  `planner` refuses to emit a work order it can prove is unsatisfiable.

---

## Appendix A — defect taxonomy from the agentic build

Every class of defect the item corpus actually produced, and the metric family that must catch it.
This is the completeness test for the catalogue: **an unclaimed row is a coverage gap in seedsmith.**

| # | Defect actually observed | Caught by | Owner |
|---|---|---|---|
| 1 | Partition id / id template transcribed wrong | Identity | C# (exists) |
| 2 | Invented vocabulary — tags, elements outside the closed set | Vocabulary | C# (exists) |
| 3 | Name collisions, three-word names, possessives, rarity words in names | Naming | C# (exists) |
| 4 | Reference derived from a pattern instead of looked up (`item.humanoid-core-a-001`) | Referential | C# (exists) |
| 5 | Reference invisible to the resolver (snake_case vs kebab) | Referential | C# (fixed) |
| 6 | Tracking id vs runtime id confused — **four separate times** | Referential | C# (fixed) |
| 7 | Content rules that live only in a lane document until something violates them — the class, not any one rule (see note below) | **Constraint** | seedsmith |
| 8 | An allocation that is **arithmetically unsatisfiable** before a single agent runs | **Feasibility** | seedsmith |
| 9 | An exemplar propagating a wrong shape to every agent that reads it — **three times** | **Exemplar conformance** | seedsmith |
| 10 | Content that ships unreachable — no drop path, no recipe | Linkage | absorbed from `seed_graph` |
| 11 | A set nothing can complete — members declared by role, never pinned | Linkage | absorbed |
| 12 | A whole feature unbound — 10 milestones, no base type granting them | Linkage | absorbed |
| 13 | **Allocated partition with zero entries** — nine, of which eight were accidental, unnoticed for three waves | **Coverage** | seedsmith |
| 14 | Distribution skew — humanoid uniques half of plant across four roles; top rarity band entirely dark/light | **Distribution** | seedsmith |
| 15 | Rarity ladder not monotonic — a band-90 unique reading flatter than its own band-50 | **Balance** | seedsmith |
| 16 | Two entries rendering identically for mechanically different families (`Increased` vs `More`) | **Semantic dedup** | seedsmith |
| 17 | Flavour absent — 60 consumables, 30 of 70 charms, three silent themes | Quality *(open-loop)* | seedsmith |
| 18 | Names legally distinct but all saying one idea | Quality *(open-loop)* | seedsmith |
| 19 | Same-stage / wrong-order references between kinds authored in parallel | **Dependency order** | `planner` |
| 20 | A material that drops and nothing consumes | Linkage *(note)* | absorbed |

Twelve of twenty rows are seedsmith's to own; the rest are already gated and stay where they are.

> **Correction, 2026-08-23 audit.** Row 7 originally named the jewel-minor ban, the 8-of-15 role
> quota and the one-per-(role, band, axis) rule as *"never enforced"*. That was **false when
> written**: `UniqueRuleCheck.cs` and `SetRuleCheck.cs` enforce all of them, wired at
> `Validator.cs:70-71` and covered by tests — code added earlier the same day, by the same author as
> this map. The grounding reviewer caught it.
>
> The defect class is real; the examples were stale. Those rules lived only in prose for the whole
> agentic build and were violated 28 + 10 + 1 times before anyone wrote a predicate. What `Constraint`
> owns is **the recurrence** — the next rule that exists only in a lane document — not re-implementing
> five checks that now ship in C#. Seedsmith's job there is to notice that a documented rule has no
> corresponding check *at all*, in either tool.
>
> Worth keeping visible because it is the exact failure this map warns about elsewhere: asserting the
> state of the codebase from memory rather than reading it.

---

## 8. Specs

| Module | Spec | Carries |
|---|---|---|
| `numerics` | [spec-numerics.md](seedsmith/spec-numerics.md) | locked formulas, the tier-bands artefact, `rebalance`, the telemetry refit path |
| — | [spec-analytics.md](seedsmith/spec-analytics.md) | the algorithms `metrics` and `numerics` share |
| `budget` | [spec-budget.md](seedsmith/spec-budget.md) | target derivation, conflict preservation, distribution shape |
| `planner` | [spec-planner.md](seedsmith/spec-planner.md) | feasibility, derived ordering, scheduling |
| `pipeline` | [spec-pipeline.md](seedsmith/spec-pipeline.md) | guardrails, structured output, open-loop review |
| `corpus` `adapter` `report` `briefkit` | [spec-foundation.md](seedsmith/spec-foundation.md) | the interfaces and the feature seam |

Next: `tasks/seedsmith-plan.md` and `tasks/seedsmith-todo.md`, then build W1.

---

## 9. Audit — 2026-08-23

Five adversarial reviewers, one lens each: methodology, grounding, buildability, game design, gaps.
**66 findings, 11 of them BLOCKER.** Reports in [review/](seedsmith/review/).

The audit paid for itself twice over on its first two findings, both of which were mine and both of
which would have shipped:

- **The overlap guardrail was inverted.** `spec-numerics` asserted `hi_t < lo_(t+1)` — "so tier
  windows do not overlap into ambiguity". `bands.v1.json` `tierScaling.overlap` requires the exact
  opposite and proves it with the same arithmetic, because overlap is design guarantee **OD4**: a
  well-rolled lower rung must be able to beat a badly-rolled higher one. The guardrail would have
  raised on the first resolve of every channel. Written one paragraph after the spec congratulated
  itself on reading the registry first.
- **`metrics` had no spec at all.** The map listed it, six documents referenced it, nothing defined
  it.

### Owner decisions, resolving four blockers

| # | Blocker | Decision |
|---|---|---|
| 1 | Multi-set membership risks set jail; audit wanted a structural cap | **No cap.** The problem is the missing pipeline, not the absence of a rule — see [spec-planner §8](seedsmith/spec-planner.md#8-generation-pipelines--the-architecture-the-agentic-build-never-had). A planner that resolves member demands with sight of every set spreads them deliberately; a cap only refuses at an arbitrary number. |
| 2 | `opWeight[More] = 0.55` stands in for a non-constant relationship | **Ship it.** An adjustable tuning number, revisited for balance later. |
| 3 | Appendix A omits its own most frequent defect class | **Pipelines plus ordering plus validators.** Correct for the ordering half; the residue — logic bugs *inside* a check — is answered by mutation testing (`scripts/mutate.ps1`), now scoped into W1. |
| 4 | Calibrating a budget threshold is the same motion as editing a target to hide a failure | **Not material.** `budget` is a config file set before a run, not a live gate being negotiated. |

Decision 1 and decision 3 turned out to be the same decision. Both blockers traced to one absent
thing — **dependency-correct generation order** — and the set case showed that ordering is needed
not only *between* kinds but *inside* one: a set is five ordered stages, three of them deterministic,
and the agentic build asked a single agent to do all five at once.

### Still open

Buildability B1–B4 (undefined interface types, item vocabulary inside the feature-agnostic modules,
no CLI specification, no CI cutover for absorbing `seed_graph`) and the grounding corrections — all
spec work, no decisions needed.
