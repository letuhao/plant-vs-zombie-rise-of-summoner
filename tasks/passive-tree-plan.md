# Implementation plan — passive tree

**Program:** `passive-tree`. Capability map: [docs/architecture/passive-tree-map.md](../docs/architecture/passive-tree-map.md)
(14 modules — 12 original plus `element-conversion` (D56) and `soul-curve-resolution` (D58), both
added 2026-09-06/07). Specs: `docs/architecture/passive-tree/spec-<module-id>.md`. Design record:
[passive-tree-ideal.md](../docs/architecture/passive-tree-ideal.md) — 45 owner decisions.
Task list: [passive-tree-todo.md](passive-tree-todo.md).

**Status:** plan, rewritten 2026-09-05. **Completeness-audited 2026-09-06** against all twelve module
specs (four parallel audit passes, one per dependency wave) — every finding either closed with a task,
folded into an existing task's acceptance criteria, or tracked in the non-blocking-asks table with a
named default.

**Updated 2026-09-07 — build is well underway, not "awaiting review."** Phases A-E are built and
verified. Phase F is partial (F1/F4/F5 done; F2/F3 done pending a real production-scale sweep; F6 done
and revealed a real gap; **F7 closed the same day — the fold-back model is confirmed NOT representative
of the real pipeline, a structural bias, not a units gap — and opened F8** to rebuild it; F8 not started).
Phase G's gate-side work shipped and is
live-probed; its remaining two checkpoint bullets wait on content, not design. Phase H is partial
(H1-H8 done; H9 has generated 379 of 480 primary-tree nodes, bind+commit+review still to run). Phase I
is fully built (I1-I10), with only the owner-only eyeball bullet left. Phase J has two new,
spec-complete, zero-blocker tasks (**J11** `element-conversion`, **J12** `soul-curve-resolution`) ready
to build now, alongside J1's existing block (missing plan-emission factory functions in
`tools/seedsmith`).

Two new module specs landed since this plan's own module count was written:
[`spec-element-conversion.md`](../docs/architecture/passive-tree/spec-element-conversion.md) (D56) and
[`spec-soul-curve-resolution.md`](../docs/architecture/passive-tree/spec-soul-curve-resolution.md)
(D58) — the capability map now lists 14 modules, not 12.

**Why it was rewritten.** Three coverage audits read the twelve module specs against the previous
27-task plan and found **149 requirements with no delivering task** and **16 acceptance criteria that
contradicted the spec they cited** — including a planner named as a new tool when its spec opens by
calling it a seedsmith adapter, a measurement task pointed at two tools its spec rejects by name, and a
checkpoint asserting an output no task produced. Patching would have left the seams. Sources:
[20-plan-coverage-wave0.md](../docs/research/passive-tree/20-plan-coverage-wave0.md),
[21-plan-coverage-data.md](../docs/research/passive-tree/21-plan-coverage-data.md),
[22-plan-coverage-content.md](../docs/research/passive-tree/22-plan-coverage-content.md).

---

## Overview

Build a static, shared passive-tree catalog and the runtime that reads it: a deterministic planner
emits a plan, a language stage fills vocabulary inside it, a binder turns budget shares into stored
coefficients, and the resolver folds them into combat as ordinary channel contributions. Roughly
35,280 nodes across 882 trees when complete (D51, 2026-09-06: 24 statuses, not 21 — was 35,160/879)
— but the plan reaches a playable single tree long before that, deliberately.

**82+ tasks across ten phases, 8 checkpoints.** Every task is S or M; nothing is L, and no task touches
more than about five files. (E1b — the L2b resist feedback path — and F1b — squad-harness's own OQ2,
measuring the shipped commander-replicated allocation shape alongside D21's — were added after this
count was first written, both closing a coverage-audit gap rather than changing scope. **F7 — added
2026-09-07, reconciling `squad-harness`'s tree-power model against the real direct-channel pipeline —
and J11/J12 — added 2026-09-06/07, the two new module specs' own build tasks — are the same kind of
addition: closing a gap the audit/build process found, not new scope invented ahead of it.** F7's own
investigation, closed the same day, found the fold-back model is not representative (a real, structural
bias, not a units gap) and **opened F8** to rebuild it on the real direct-channel shape — no longer
conditional, now a real, scoped, not-yet-built task.)

| Phase | What it lands | Tasks |
|---|---|---|
| A — foundations | The three files eleven downstream tasks read | 3 (A1–A3) ✅ |
| B — one trait, end to end | The vertical slice: planner → catalog → binder → store → a changed number | 6 (B1–B6) ✅ |
| C — the plan corpus, the catalog and the store | Corpus invariants, migration, the store's own hardening | 11 (C1–C11) ✅ |
| D — the binder and the resolver completed | Channel legality, the soul track, cross-unlock, the report | 8 (D1–D8) ✅ |
| E — mechanism wiring | G1–G3 across Core, the injector, Battle and Sim | 7 (E1, E1b, E2–E6) ✅ |
| F — `squad-harness` and the measurements | The tool, A10a, S2–S4, and reconciling the tree-power model against the real pipeline | 9 (F1, F1b, F2–F8) 🟡 F1–F7 done (F7 an investigation, no code), F8 not started |
| G — the gate quantities | The two counters, the index, the surface, D43's seed | 8 (G1–G8) 🟡 gate-side shipped; checkpoint waits on J1's content |
| H — generation machinery and the primary corpus | The 24 gates, their runner, the metrics, 480 nodes | 9 (H1–H9) 🟡 H1–H8 done, H9 partial (379/480) |
| I — the player surface | The wire, and the spec's levels 0 / 0b / 1 / 2 / 3 | 10 (I1–I10) ✅ owner eyeball pending |
| J — volume | The elemental, status and species corpora, the census, plus the two new atom/curve-vocabulary modules | 12 (J1–J12) ⬜ J11/J12 ready now, no blockers |

## Architecture decisions this plan is built on

- **The catalog is content, not loot (D24).** Generation is a build-time step whose output is
  committed data. Nothing rolls per player. This is what lets the plan defer volume without deferring
  correctness.
- **The binder stores coefficients, not magnitudes.** A per-million share of `P(Θ)`, multiplied at
  runtime. This is the single most load-bearing decision in the plan's ordering: **a balance
  re-measure becomes a tuning republish, never a regeneration**, so the unmeasured numbers (D42) can
  ship early and be corrected later without touching a node id. J4 is what makes the *review* half of
  that republish cheap too.
- **The tier gate reads aptitude points; nodes are bought with skill points** (R1). Different
  currencies, and three audits converged on it.
- **Node ids are minted once and read back** (R3). The plan is therefore not a pure function of its
  inputs — the committed plan is itself an input, and `--emit` refuses to mint over an existing key.
- **The planner is a seedsmith adapter, not a new tool.** `spec-tree-plan.md` §Project structure opens
  with that sentence and every command is `python -m seedsmith trees plan …`. A second tool grows a
  second copy of `largest_remainder_count`, the integer algorithm §8 depends on.
- **`squad-harness` is its own project at `tools/SquadHarness/`**, with a thin `Program.cs` over
  referenceable types and its own test project. Its spec rejects the single-top-level-`Program.cs`
  shape of `tools/HybridViability` and `tools/CombatSim` **by name**, because determinism is the
  module's hard requirement and an untestable tool cannot carry `DeterminismTests`.
- **Nothing before the first shipped catalog is irreversible.** See "Gates" below.

## Slicing

The capability map's waves are *module* order. This plan slices **vertically through them**: phase B
takes one trait from planner to a changed number in a battle, touching six modules at 1/40th of a
tree's width. That instinct was right and is kept — it now has its prerequisites in front of it
(phase A's three files, which did not exist and which eleven tasks read) and the rest of each module
behind it (phases C and D).

Volume arrives in phase J, after the spine is proven and after the two measurements that would
invalidate it have run. The expensive, hard-to-reverse work (generation, review) sits behind the cheap
work that can invalidate it.

**Where the phase count changed and why.** `tree-language` §7 numbers 24 validation gates and owns
them; 15 had no delivering task and nothing built the runner they are checked by. That is a phase
(H), not three bullets inside a generation task. `squad-harness` is a five-stage module that had one
task; S4 alone is the only evidence in the program for *"no tree is OP"*. That is a phase (F). The
previous six phases could not hold either without hiding work inside a run.

## Gates — there is one, and it is not in this plan

Checked against `planning-and-task-breakdown`'s gates-vs-checkpoints test:

| Candidate | Irreversible? | Verdict |
|---|---|---|
| A10a before `tree-language --write` | No — expensive (~5,040 calls, D51 2026-09-06: 24 statuses not 21, was ~4,680; ~34 h review), but **detectable and redoable**; nothing is minted into a save | **Checkpoint F**, with a reversible default: emit the 12 primary trees first (~1,440 calls), measure, then decide on the rest |
| A tree's gate quantity before its content | No — nodes generated early become reachable when the counter lands | **Sequencing rule**, and now a refusal in code (`R-G1`, task C2) rather than a note in prose. Cost, not correctness |
| **First catalog shipped to players** | **Yes** — after that a node id change is a migration (D24) | The one real gate, at Checkpoint J, and it is already an owner decision rather than a plan artifact |

**Checkpoints verify work that is already done.** No checkpoint in this plan asserts an output no task
produces — that was the previous plan's defect at Checkpoint B, and it is why F6 (`squad-harness` S4)
is scheduled before the measurement checkpoint closes rather than left implied.

Everything else that could read as a gate is tracked as a **non-blocking ask with a named default** —
ten of them, in the todo's own table, each with a resolver.

## Phases

### Phase A — foundations

`data/tuning/passive-tree.v1.json`, `data/tuning/passive-tree-targets.v1.json` and the two roster
mirrors (`data/seed/statuses/roster.json`, `data/seed/atoms/vocabulary.json`) did not exist and were
created by nobody, while eleven downstream tasks read a key or a count from them. Verified absent
2026-09-05. None of this is design work and none of it is blocked.

Tasks A1–A3. No checkpoint — the next phase's first task fails immediately if any of the three is wrong.

### Phase B — one trait, end to end

Proves the spine on a single hand-authored tree with no language stage involved. If the coefficient
math, the id scheme or the resolver read is wrong, it is wrong here, at a cost of one tree.

Tasks B1–B6. **Checkpoint B: a trait allocated on an actor changes a number in a battle.**

### Phase C — the plan corpus, the catalog and the store completed

The properties that only exist across the corpus (`C1`, `R-A1`, the mechanism ramp, `P-1`/`P-2`), the
reproducibility contract (`planHash`, canonical JSON, `--diff`), the import transaction, the five
migration rules, and the store's own hardening — the `selfSpent` projection `H` reads, respec, the
reconciler, the soft-bound proof and the two reward bands.

Tasks C1–C11. No checkpoint — Checkpoint D covers both halves of the runtime.

### Phase D — the binder and the resolver completed

Channel legality for all thirteen `UnitClass` values, the derived channel anchor, the soul track end to
end, cross-unlock, `TreeResolveReport`, battle parity, and the reads that fail silently when they are
wrong (PS-3, `F`'s scope, `Fmax = 1000‰`, memoisation).

Tasks D1–D8. **Checkpoint D: both progression tracks resolve, and lawn and battle agree.**

Phase D is scheduled before Phase E even though the map lists `tree-resolve depends on: tree-state,
mechanism-wiring`. This is not a hard block: the dependency is for **scoring** mechanism atoms (Sim
reading a live value), not for binding or gating — a node binds and ships whether or not Sim can score
it yet. None of D1–D8's tests resolve over `stat.derived`/mechanism atoms; all run over primary and
contest channels. Checkpoint E is where the two tracks actually meet.

### Phase E — mechanism wiring

The node class §3.5 proved is the only one that rescues a focused build. G1 is the critical path — one
subsystem, ~90 lines, unblocking Erosion, layer parity and conditional scaling at once. G4 stays
excluded on purpose and no task in this phase adds a new atom kind — `element.convert` (D56,
`spec-element-conversion.md`) is J11's own, separately-scoped addition, not this phase's.

Tasks E1, E1b, E2–E6. **Checkpoint E: a status-granted derived channel reaches a live actor and is
scored in Sim.**

### Phase F — `squad-harness` and the measurements

The measurement tool, then the four staged measurements it exists to produce: A10a's Erosion
differential, concentration and cross-unlock, the soul track, and S4's budget evidence. S4 is *claimed,
not optional* — no other module is scoped to produce it, and it is the only thing that can re-derive
D42's two dials.

**F6 ran S4 for real and found a genuine structural gap, not an under-measurement — F7 answers it.**
`BudgetSweep`'s `ProposeTreeTotalPoints`/`ProposeTreeShareMilli` always report `Resolved: false`,
because `TreeModel` folds tree power back as extra **aptitude allocation**
(`effective += AptitudeAllocation.Single(...)`, inherited from `tools/HybridViability --trees`'s
pre-passive-tree sweep) while the real, shipped pipeline (`TreeAtomSource.BoundAtomsFor`) writes a
node's contribution **directly to its own derived channel**, through the same fan-in
`AtomDerivedSubsystem` uses for traits/equipment — never through aptitude at all. These are two
different causal paths, not two units of one path. Reconciling them is possible (an `AptitudeEdge`'s
own `KMilli` rate is a known, invertible linear map for whichever channel a node writes to) but only
**per representative channel**, matching this program's own `combat.power.fire`/`combat.power.omni`
worked-example convention (`spec-tree-binder.md` §3.4) — not a single universal constant, and not by
having the harness read a corpus that mostly doesn't exist yet (its own spec forbids that, for good
reason: purity and speed against unbuilt content).

**F7 ran this investigation 2026-09-07 and found the reconciliation does not exist — not a units gap,
a mechanism gap.** `TreeModel.Resolve`'s fold-back adds points onto a tree's OWN `AllocationScope.
Commander` entry, which the real battle resolver (`AptitudeResolver.Resolve`) reads through
`AptitudeAllocation.Share` — `Total(id)/GrandTotal()`, a **zero-sum ratio across every aptitude the
actor holds**. Growing one tree's fold-back bonus therefore also shrinks every OTHER tree's share
(the denominator grows), a cross-tree coupling the real `TreeAtomSource` direct-channel path has no
analog for at all (one node's contribution never touches any other tree's). Worse, the coupling is
**asymmetric between the exact two build shapes `concentration`/`crossunlock` compare**: a corner
build's one already-near-saturated share barely moves under a fold-back bonus while every other
share it holds still shrinks; a spread build's several non-saturated shares move much more under the
identical bonus. This is a bias on the specific axis being measured, not background noise a bigger
sample averages out. **F8 opened** (`tasks/passive-tree-todo.md`) to rebuild `TreeModel`'s power
contribution as independent, per-channel modifiers with no shared-total normalization — mirroring
`BoundDerivedAtom`'s own shape — and to re-run F4's `concentration`/`crossunlock` sweeps and F5's
`soultrack` sweep against it. F4/F5's own already-reported win-share numbers are flagged, not treated
as settled, until F8 lands.

Tasks F1, F1b, F2–F7 (F8 conditional on F7's own finding). **Checkpoint F: A10a produces `D` with a
half-width, and D42's two dials are republished — with F7's own honest label if the republish is
representative-channel-derived rather than corpus-measured.**

### Phase G — the gate quantities

Without these, 30 of 42 trees (D51, 2026-09-06: 24 statuses, not 21 — was 27 of 39) sit at tier 0
(§13.4). D37 put them in this program; D43 seeds existing saves. G1 lands the two shipped-code
prerequisites (`DamageOrigin`, `OnFreshApplication`) that the counting rules are undeliverable without.

Tasks G1–G8. **Checkpoint G: all 42 trees reachable; an existing save shows non-zero counters. ✅ Both
gate quantities shipped and live-probed 2026-09-06 (G6) — this checkpoint's gate-side work is done; the
remaining block on those 30 trees is `passive-tree-todo.md` task J1's missing plan-emission factory
functions, not this phase.**

### Phase H — generation machinery and the primary corpus

The 24 validation gates, the runner that executes them, the eight `PassiveTree/*` metrics and the two
`tree-review` ones, the tree card, the corpus sheet, the review pilot — and then 12 trees (480 nodes)
rather than 42 (D51, 2026-09-06: 24 statuses, not 21 — was 39). The pilot lands here because every
hour estimate in the program rests on a rate nobody has measured.

Tasks H1–H9. **Checkpoint H: 480 nodes generated, gated and reviewed; the gating metric measured.**

### Phase I — the player surface

Standalone-first: works with the game closed. The spec's own levels are **0 / 0b / 1 / 2 / 3**; the
previous plan numbered them 1–4 and every cross-reference between the two documents was off by one.
I1 builds the web verification suite first, because until it exists no surface task has a bar to pass.

Tasks I1–I10. **Checkpoint I: browse, plan, spend and see why a tier is locked.**

### Phase J — volume

Elemental and status trees behind their gate quantities, then the species corpus and the census. The
only phase whose cost is measured in days of machine time.

Tasks J1–J10. **Checkpoint J: full corpus reviewed and ready to ship — the one irreversible point.**

## Risks

| Risk | Impact | Mitigation |
|---|---|---|
| `treeShareMilli` / `budget.treeTotalPoints` unmeasured (D42) | High if wrong late | Coefficients, not magnitudes — a re-measure is a tuning republish, never a regeneration (C5's R6 proves it). **The evidence is `squad-harness` S4 (task F6), scheduled before Checkpoint F closes.** Until then both ship flagged `UNMEASURED` in `passive-tree.v1.json`. Phase F's earlier tasks do **not** produce this data — F3 measures the Erosion differential and nothing else |
| A10a says mechanism nodes do nothing | High | It runs in phase F, **before** any corpus is generated. That ordering is the mitigation. UNRESOLVED holds Checkpoint F exactly as FAIL does |
| Re-certifying the corpus after a tuning republish | High | J4's `O(diff)` re-review — a magnitude retune must produce an **empty** human queue, proven by test. Without it, every D42-style republish costs a full census |
| `provenance-supersede` is unbuilt and pass 2 cannot run without it | High | `ProvenanceLedger.record` raises on a re-recorded row. J9 budgets 2–3 passes and depends on J4, which must either build it or record it here as blocking pass 2. **Raised at J4's task start, not discovered mid-run** |
| The species-namespace affix bill: **6,720 authored affixes** against a shipped authored corpus of two | High | J7 is its own task and its own run, with the cost stated here before it is scheduled. J6's marking rule keeps a later `speciesUniqueAffixMin` change `O(diff)` |
| `battle-tempo` is editing `BattleModels.cs` / `BattleRunState.cs` right now | Medium | R9: cite by symbol, never by line. Seventeen citations already drifted twice during the spec round. G1 and E3 both touch `BattleRunState` — the one place wave 0's "no shared files" claim does not hold, and they are sequenced accordingly |
| Review rate unknown; every hour figure rests on it | Medium | H8's 20-tree pilot, early, gates the full census rather than the whole program |
| ~~The generic corpus is generated before its gate quantities exist~~ — **superseded 2026-09-06**: both gate quantities shipped and are live-probed (G6); `R-G1` no longer refuses anything. **Current risk: the 30 elemental/status trees have no plan-emission tooling at all** (`elemental_tree_spec`/`status_tree_spec` don't exist in `tools/seedsmith`) | Medium | J1 names this precisely; building the two factory functions is a mechanical extension of `primary_tree_spec`'s own already-generalized pattern (H9), not new design |
| Species volume (33,600 nodes, ~105,840 calls) | Medium | Last phase, resumable with a mid-run-kill test, and D41's 8-of-40 bounds the *unique* authoring to 6,720 affixes |
| No guard can detect a missing `ssot-power-scale.md` row for this program | Medium | `guard-power.ps1:74` keys on a parameter named `level`/`lvl`/`index`; this program's are `t`, `count`, `nodesOwned`, `soulLevel`, `thetaActor`. D8, E6 and G8 exist because a green guard is not evidence |
| ~~Existing saves show 27 trees at tier 0~~ — superseded: D43's proxy seed (G5) already ships, and the count is now 30 of 42 (D51) for whichever trees still lack content once J1 unblocks them | Low | Stamped and auditable; re-verify against a live save once J1 lands |

## Open questions

None block phase A, and none blocks any task. The todo's own non-blocking-asks table originally
tracked twelve; **reconciled 2026-09-07** against the D44-D59 owner-decisions batch and one older,
pre-batch decision — seven were already answered and had simply never been propagated back into the
table (the same defect class the module-spec audit found and fixed six times over). **Five remain
genuinely open**, each with a standing default already carrying it: player-facing naming, the
`DemonsPage` volume defect the Codex route hangs off, auto-drafting a species starter plan, shareable
build codes as a marketed feature, and D15's equal-budget rule — the last of these re-scoped, not
closed: S4 (task F6) has now run and found a structural non-resolution rather than an answer, so this
question is repointed at the new **F7** task instead of "after F6."

**Closed since the table was first written:** the L2b resist path question — the owner answered
*contribute everything*, shipped as task E1b. Removed from the table, not left stale.

One item is neither a task nor an ask because nobody owns it: **A10b's prerequisite** — a Battle status →
`BattleDerivedModifierLedger` producer that no module's modified-files table contains. Recorded at the
foot of the todo so it stays visible. A10a (F3) is unaffected.

## Verification standard

Every task: the module's own tests green, `dotnet build` clean, and the four boundary guards
(`guard-single-writer`, `guard-secondary-no-unity`, `guard-funnel-delta`, `guard-dal`) plus
`guard-power` where a magnitude is touched. Overflow and magic-number audits before any task that
introduces a number. **Any task touching `web/fusion-rpg-web` also runs the web suite** — the seven
guard tests, `npm run build`, `npm run check:bundle` and `npm run test:e2e` — built by task I1, which is
scheduled first in its phase for exactly that reason.
