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

**Updated 2026-09-07 (end of day) — build is well underway, not "awaiting review."** Phases A-E are
built and verified. Phase F is fully closed: F1/F4/F5 done; F2/F3 done pending a real production-scale
sweep; F6 done and revealed a real gap; **F7 closed the same day — the fold-back model is confirmed NOT
representative of the real pipeline, a structural bias, not a units gap — and opened F8, which is now
ALSO built + verified** (`tools/SquadHarness/TreeChannelModel.cs`, independent per-channel modifiers, no
shared-total normalization; 194/194 SquadHarness tests green; real before/after deltas captured at two
cells, both small and consistent with noise at those specific corner/spread pairs — the structural
finding stands regardless). Phase G's gate-side work shipped and is live-probed; its remaining two
checkpoint bullets wait on content, not design. Phase I is fully built (I1-I10), with only the
owner-only eyeball bullet left.

**Phase H and J1 both advanced enormously the same day, past their own morning starting point of
"379/480 generated, bind never run."** J1's factory-function blocker closed
(`elemental_tree_spec`/`status_tree_spec`, mechanical extensions of `primary_tree_spec`'s own pattern),
then its server-side gate-shape fix (`PassiveTreeEndpoints.IsWiredGateQuantity`), then all 30 real
elemental/status plans emitted for real — which surfaced and fixed two genuine code bugs no prior task
had ever exercised (a node-id grammar rejecting the 5 real status ids containing `_`/`.`; quota
resolution requiring a `forcedElement`/`forcedStatus` plan field `build_plan` had never written for any
tree). Real generation then ran to convergence across the FULL 42-tree corpus (12 primary + 30 new): 22
total passes, **1677/1680 nodes generated (99.8%)**, 3 evidenced, root-caused holdouts remaining
(low-probability vote non-convergence on large permitted-affix pools — the same, already-understood
pattern, not a new one). `tools/TreeBinder` then ran for the first time ever against this size of
corpus, found and fixed a real silent-data-loss bug (three dotted `nerve.*` tree ids collapsing onto one
dictionary key), and landed at **266/1677 bound (15.9%)** — capped by the SAME disclosed, cross-program
`tier-bands.v1.json` pricing gap D2 already named, now with a third contributing reason (`atom.savagery`'s
"More"-op family) found and documented at full scale. J1's own "committed" gap (`ImportTreeCatalog` had
zero production callers anywhere) is now closed too — `PassiveTreeImportRunner` wires the bound corpus
into the store at boot, independent of and never gating the existing atom-content self-heal, proven
against an isolated fixture (6/6 new tests).

**Later the same continuous session (2026-09-07, second pass): the live-corpus boot proof was
finished for real, and it found the actual, much bigger defect the isolated fixture proof could never
have seen.** The `DemonSpeciesCatalog` local-dev-data gap named above was fixed directly (`dotnet run
--project tools/DemonSpeciesImport -- --db src/FusionRpg.Server/data`, a safe, isolated dev database
confirmed via `tasklist`/`netstat` to be unrelated to the owner's own live server). That retry
surfaced the real blocker: `tools/TreeBinder`'s own `ReportWriter` had **never**, in this program's
history, written the actual `tree-catalog` `TreeRecord`/`NodeRecord` shape its own spec mandates
(`spec-tree-binder.md`: *"THIS is what ships"* / *"tree-catalog owns the on-disk record shape; this
module writes it and never redefines it"*) — only a narrower internal audit-trail report, per its own
prior (self-contradicting-the-spec) doc comment. Fixed: the identity fields the loader needs were
never actually missing from `Program.cs`'s own inputs, only from the report object alone —
`BindInputNode`/`PlanReader` now carry them through, and `ReportWriter` emits the real record
alongside the original audit trail, never replacing it. Two more real bugs surfaced by the SAME
live-boot retry, both fixed in the same pass: `LoadAtom` needs `attachPoint`/`trigger`/`whenJson`
(real fields on the same atom object, simply never serialized), and `PassiveTreeCatalogLoader`'s own
`IdMismatch` check compared a node id's stripped tree-slug against a RAW, unstripped `treeId`,
falsely refusing the 5 real dotted/underscored trees (`nerve.*`, `charm_pulse`, `pact_mark`) — fixed
by mirroring `seedsmith`'s own `tree_slug_for` stripping rule on the C# side too. One stale, orphaned
committed file (`nerve.json`, dead since an earlier, already-fixed filename-collision bug) found and
removed. **Real, final result: a live server, booted from source against the real committed
842-node-bound corpus, printed `imported the passive-tree catalog — 42 tree(s), now at revision 1` —
zero refusals.** Full C#/TreeBinder/Data test suites re-run green throughout, zero regressions. H9's
"committed" bullet — genuinely open the entire session before this — is closed with real, live,
end-to-end evidence, not a synthetic-fixture stand-in.

J1's own remaining open item is narrower now too: only the "bound" clause's cross-program tier-bands
pricing gap (D2, unchanged, not this program's to resolve unilaterally) and Checkpoint G's own two
content-dependent bullets (now finally unblocked by real content existing) stay open. **A same-pass
side finding, sharing the identical wiring-gap root cause**: `check --family PassiveTree`'s own
`PassiveTreePlanCtx` had never been given `targets`/`tree_plans`/`nodes_by_tree`/`outcomes_by_tree` by
ANY caller, so every H4/H5 corpus-side metric (including the one hard gate, `UnresolvedCount`) had
been reporting `NOT_MEASURED` on every invocation ever made, regardless of what the real corpus
looked like — H9's own "gate measured" bullet, and every prior claim resembling it, had never actually
been backed by a real measurement. Fixed the same pass: `_cmd_check_family` now derives all four
fields from real, already-committed data (the real ledger + `nodegen.plan_run`, the SAME resume logic
generation itself already uses). The real, now-measured result: the ONE hard gate is genuinely green
(3/1680 unresolved, 1‰, target ≤50‰). Two of the five other threshold gates are real, newly-visible
GAP findings: `ExclusionRate` (999‰ vs ≤30‰, root-caused to a `tree-language` prompt-wording defect —
"prefer reroute" read as a default rather than a rare exception — fixed at the source for future
generation, not retroactively regenerated) and `NearDuplicate` (69‰ vs ≤5‰, a real content-quality
signal, named as a scoped follow-up: live corpus-wide near-duplicate suppression during generation,
never built). `HiddenFileCountMetric` (H5's own last open bullet) was also wired into the same command
for the first time — fully built and tested since H5 closed, but zero real call site until now.

**J2–J9 then advanced the same continuous session, past their own morning starting point of "J1
built, J2–J12 unstarted."** J2 (the review acceptance ladder + sampling tiers) and J3 (the
review-verdict machinery, including a real near-miss caught before it could break the shared
exactly-one-hard-gate invariant) both closed. **J4** (`diff.py`'s incremental re-review,
`record_superseded`'s provenance-supersede) closed, correcting a real cross-program misattribution
in `spec-tree-review.md` §8 along the way (`ProvenanceLedger` was never the real mechanism — the
program's own local `record_accepted` ledger was). **J5** (the species planner: roster,
mechanical-favour quota, `FavourDrift`) closed against the REAL 904-species roster, proving it
catches a live, still-unresolved blind spot (`SnorkleZombie`'s parked duplicate) the existing C#
tool's own `_`-skip convention misses. **J6** (`SpeciesUniqueness`, the deepest-mechanism marking
rule) closed. **J7** (the species-namespace affix corpus) found its own real code prerequisite
missing (no way to target `affix.species.<id>.*` at all) and built it, live-model-proven; the
6,720-affix production run itself stays correctly unscheduled. **J8** (the species generation
pipeline) closed in full — all four acceptance bullets built and proven, including a resolved
`forced_aptitude`/`TreeSpec` design question (aptitude needs no new per-node content axis; two more
real bugs found fixing `tree_slug_for`/`build_slot` for a species-shaped tree) and a real mid-run-
kill resumability proof against a full 40-node species plan. **J9**'s own real prerequisite (the
per-species orchestration caller sequencing every J5-J8 piece into one committed tree) is now built
and tested too, with a real, live-model, single-species proof-of-concept run — the 840-species
production pass itself remains correctly unscheduled, exactly matching J7's own "code vs. run" split.
**J11 and J12** (the two newest module specs' own tasks, `element-conversion` and retiring
`NodeAtom.SoulCurveId`) closed independently, in C#, with full test evidence. Only **J10** (the full
census, blocked on J9's own 840-tree corpus existing) remains genuinely unstarted.

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
| F — `squad-harness` and the measurements | The tool, A10a, S2–S4, and reconciling the tree-power model against the real pipeline | 9 (F1, F1b, F2–F8) ✅ all built + verified (F2/F3's own real production-scale sweep is a scheduled machine-time item, not a design gap) |
| G — the gate quantities | The two counters, the index, the surface, D43's seed | 8 (G1–G8) 🟡 gate-side shipped; checkpoint waits on J1's content |
| H — generation machinery and the primary+full corpus | The 24 gates, their runner, the metrics, 1680 nodes across 42 trees | 9 (H1–H9) ✅ **H1–H8 done; H9's own four acceptance bullets ALL now built + verified 2026-09-07** — generation 1677/1680 across all 42 trees (99.8%, 3 evidenced holdouts); binding capped by a disclosed cross-program pricing gap, not a code gap; **commit now proven live end-to-end** — a real server boot importing the real 42-tree bound corpus with zero refusals, after finding and fixing 3 real defects (`TreeBinder`'s `ReportWriter` never wrote the real `tree-catalog` shape its own spec mandates; `LoadAtom` needed `attachPoint`/`trigger`/`whenJson`; `IdMismatch` compared a stripped id-slug against a raw `treeId`); the gate is now genuinely MEASURED (not `NOT_MEASURED`) for the first time, and green. H8's own review pilot remains its own separate, owner-only task (a human timing 20 real cards), not one of H9's own bullets |
| I — the player surface | The wire, and the spec's levels 0 / 0b / 1 / 2 / 3 | 10 (I1–I10) ✅ owner eyeball pending |
| J — volume | The elemental, status and species corpora, the census, plus the two new atom/curve-vocabulary modules | 12 (J1–J12) 🟡 **J11/J12 ✅ built + verified 2026-09-06/07**; **J1 ✅ effectively built + verified 2026-09-07** — factory functions, server-side gate-shape fix, all 30 plans emitted, generation run to 99.8% convergence, binder run for real, commit wiring built — only the "gated" bullet's live end-to-end proof remains, blocked on an unrelated pre-existing gap, not on this task's own logic; **J2 ✅ fully built + verified the same day** — both the acceptance-number computation (`verdict.py`, Clopper-Pearson, found + fixed a real tuning-file rounding defect along the way) and the three sampling tiers (`sample.py`, tested against real `might` corpus data too) are done. **J3 ✅ also fully built + verified the same day** — all four acceptance bullets closed: `PassiveTree/ExclusionPresentation` (the gating presentation check, catching+avoiding a real near-miss that would have broken real content generation — a second `gates=True` metric would have violated §7.1's own tested "exactly one hard gate" invariant), the "nullification ships" test, `manualCorrection`'s record+rate (scoped honestly — no speculative reviewer-UI call site invented), and the committed verdict-queue artifact wired to `render_brief`'s own already-shipped `anti_motifs` parameter. The one thing named as explicitly NOT built: the orchestration that walks the ladder end to end (a CLI verb calling back into real regeneration) — the record-keeping both halves need now exists and matches, but nothing yet calls one with the other's output. **J4 ✅ also fully built + verified 2026-09-07** — `diff.py`'s `diff_tree` proves all three named bullets (a tree-wide magnitude retune is an empty human queue; an id rename triggers a full-tree review, old id included; a changed node carries its whole record, judged alongside every sibling in its tree), via a real, checked-against-committed-data design correction (`content_fields` as a parameter, since the bound catalog carries no content fields at all and is the only way to see a retune as a retune rather than as "unchanged"); `record_superseded` resolves the `provenance-supersede` risk below, scoped correctly once §8's own cross-program `ProvenanceLedger` citation was corrected to name the real, already-local, already-idempotent `record_accepted`/`nodegen/run.py` mechanism. 17 new tests, full suite green (3208 passed, same 13 pre-existing unrelated failures). **J5 ✅ also fully built + verified 2026-09-07** — the deterministic species planner: `roster.py`'s `load_roster()` (checked against the REAL `data/seed/demons/species/` tree first — 904 species today, not the spec's own stale 840 — and proven to catch the real, still-live `SnorkleZombie` parked-duplicate blind spot §2.1 names, not just a synthetic stand-in); `plan.py`'s `assign_favour_cells` (matches the spec's own Code style block, deriving the 1,728-cell joint weight table from three axis tables and `legitimateSkew` rather than requiring a hand-typed JSON table — no new tuning schema or `publish.py` rebalance needed); `FavourDriftMetric` (symmetric drift, registered the same "not in `ALL_PASSIVE_TREE_METRICS`" way H5's own metrics are, `gates=False` since no real corpus run exists yet to calibrate a tolerance against); and the species-level `unresolved` favour rate as a SECOND population under the EXISTING sole hard gate (`UnresolvedCountMetric`, extended additively — all 4 of its pre-existing tests, including the exactly-one-hard-gate registry test, pass unmodified). One real, self-caught correction during test-writing: an assumed "growing the roster never reassigns an existing species' cell" property turned out to be an unfounded overclaim (the spec never promises it, only §5.3 rule 3's node-marking prefix order has that shape) — fixed by correcting the test and the function's own docstring, not by forcing the property to hold. 42 new tests, full suite green (3250 passed, same 13 pre-existing unrelated failures). **J6 ✅ also fully built + verified 2026-09-07** — `mark_species_unique_nodes` (`species/plan.py`), a pure sort over a tree's own already-committed node list (deepest mechanism nodes, ties on branch order then `nodeKey`), with the SUBSET property itself proven by test over a 16-node fixture rather than assumed from the sort being stable; `PassiveTree/SpeciesUniqueness` (`metrics/passive_tree.py`, `gates=False`, same non-`ALL_PASSIVE_TREE_METRICS` registration as every other post-H4 metric), one reverse index feeding three findings (U1 text, U2 composition, U3 namespace — the todo's own named "two trees sharing a namespace affix" fixture passes). 17 new tests. **J7 🟡 code prerequisite built + proven 2026-09-07, the 6,720-affix production run itself NOT started** — investigation found the shared `affix-authoring` pipeline (`generate_affixes.py`) could not target `affix.species.<speciesId>.*` at all as shipped (hardcoded `ID_PREFIX`/`OUTPUT_DIR`, no `--check`, and the corpus is 10 entries today not the spec's cited two, 8 of them a Claude-reasoning stand-in from an unreachable-endpoint incident, never real model calls) — corrected the "it is a run, not a code task" framing rather than trusting it. Built an additive `--species-id` CLI flag (zero behavior change to any existing caller), decided and documented the per-species output file layout the spec's own Project structure table left silent (`data/seed/effects/affixes/species/<speciesId>.json`, mirroring its own per-species convention), and proved the whole path TWICE: 11 new tests (full suite 3278 passed) plus a real proof-of-concept run against the live local model (one resolved draw, never written to disk). The actual 6,720-call run remains its own deliberate, unscheduled decision. **J8 🟡 two of its four model-call/orchestration stages built + live-model-proven 2026-09-07** — the favour-fit stage (§3.1 step 3: one graph per species since each has its own alternate-set enum, `resolve_favour_fit`) and the codex-summary stage (§6, `resolve_codex_summaries`, shared graph across species), both proven against the real local model, not just stubbed (favour-fit: a deliberately bad offer correctly rejected in favour of an alternate; codex: 2/3 real runs resolved, 1/3 genuinely `vote_unresolved`, an honest small-sample signal flagged for J9 rather than guessed at). Two real, self-caught bugs fixed during test-writing (a channel-id regex false-positiving on "e.g.", and a "re-check after voting" branch proven to be unreachable dead code and removed). **The `forced_aptitude`/`TreeSpec` design question — RESOLVED the same continuous session, not left open**: re-reading §1's own axis comparison + §8's own atom-tag-vocabulary blocker together showed aptitude does NOT need a new per-node content axis (no tagging vocabulary exists to filter by, and the spec's own posture already treats that as a deferrable gap); `TreeSpec` gained an optional `mechanical_favour` field, a new `species_tree_spec()` factory mirrors the existing four, and `build_plan` now derives `forcedElement`/`forcedStatus` from it. **Two MORE real bugs found by actually running a species plan through the full pipeline, not stopping at TreeSpec alone**: `tree_slug_for` never lowercased (real species ids are PascalCase, unlike every prior tree id), and `nodegen.quota.build_slot`'s own `if/elif` could never force BOTH element AND status at once (a species tree's real, different shape from elemental/status trees, which force exactly one) — both found, fixed, and proven against the real 904-species corpus and the real `might.v1.json` plan reused as a stand-in shape, the same pattern H3's own elemental/status regression tests already established. A real, separate spec-citation defect also found and named (not fixed): `spec-species-tree.md`'s own "Decisions implemented" table misattributes D35, matching the exact shape of J4's earlier `ProvenanceLedger` misattribution. 41 new tests, full suite 3278→3319. **Resumability then PROVEN directly, closing J8's own last acceptance bullet, same session**: `plan_read.py` refactored (additive) to expose `load_from_dict`, feeding a real, full 40-node species plan straight into the SAME `run_language_stage` every generic tree already uses (it never branches on category at all — no new resumability machinery needed). A real mid-run-kill/resume test against that real plan proves it: 5 of 40 subjects "killed," 35 correctly resumed, zero duplicates, the final seed document complete. Three more real, self-caught TEST bugs found while proving this (a hardcoded synthetic affix id invalid against the real vocabulary; a per-call vs. per-node counter mismatch once `generate_node`'s own real 3-vote-per-node cost was confirmed live; prompt/schema keying both unsafe, call-order being the only reliable per-node signal) — none of them production bugs. **J8 ✅ CLOSED — all four acceptance bullets built and proven.** 48 new tests total, full suite 3278→3326. The top-level per-species orchestration caller (favour-fit → plan/quota → node-gen → marking → codex → one committed file) is correctly J9's own integration work, not a J8 gap — every piece it would call is real, tested, and proven. **That orchestration caller is now itself built too** (`species/generate_tree.py`'s `run_species_tree`, 3 new tests, full suite 3326→3329) and run once for real against the live local model on an actual roster species (`AbyssSwordStar`) as a genuine end-to-end proof — never committed to the real corpus, and never a stand-in for the 840-species production pass itself, which stays correctly unscheduled under J9's own "Scope: M (a run — days of machine time, not of authoring)." J10 remains genuinely unstarted, blocked on that same run existing (and stated plainly as having no
independently-buildable sub-task of its own the way J7/J8/J9 each did — it is a run over J1's/J9's own
corpora, not an authoring task). **A second real PoC run of J9's own orchestration caller, with the
`NodeKeyRefused` fix from the first run in place, completed 2026-09-07**: `AbyssSwordStar` again,
576.3s elapsed, favour-fit correctly swapped to an alternate cell this time (`Ferocity/air/shatter`),
25/40 accepted with real coherent content, codex `vote_unresolved` (an honest small-sample signal, not
a bug) — no name collision recurred (a probabilistic race, not deterministic, so its absence here is
expected, not proof the class can no longer occur). **Separately, the SAME session, wiring gaps in
`check --family PassiveTree` shared by J1/H9/H5 were found and fixed**: the command had never been
given `targets`/`tree_plans`/`nodes_by_tree`/`outcomes_by_tree`/`tree_seed_roots`, so every H4/H5
corpus-side metric (the one hard gate included) had always silently reported `NOT_MEASURED`. Fixed;
the hard gate is now genuinely measured and green (3/1680 unresolved, 1‰). Two of the five other
threshold gates are real, newly-visible GAP findings on the ALREADY-COMMITTED 42-tree corpus:
`ExclusionRate` (999‰, root-caused to a `tree-language` brief-wording defect — "prefer reroute" read
as a default rather than a rare exception — fixed at the source, `PROMPT_VERSION` bumped, NOT
retroactively regenerated) and `NearDuplicate` (69‰, named as a real, scoped, not-yet-built
live-generation-time-suppression follow-up). `HiddenFileCountMetric` (H5's own last open bullet) was
wired into the same command for the first time, closing that bullet too |

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

Tasks G1–G8. **Checkpoint G: all 42 trees reachable; an existing save shows non-zero counters. ✅
CLOSED 2026-09-08, all 3 bullets proven against a real live save** — both gate quantities shipped
and live-probed 2026-09-06 (G6); J1's own factory-function/catalog-import work (this same session)
unblocked the remaining two bullets; a real `GET /api/passive-tree/{playerId}` against a freshly
created player and the real committed 42-tree catalog shows `gateState: "wired"` for all 42 trees,
zero `unproduced` — the historical "30 trees at tier 0" defect this checkpoint exists to close is
gone against real, live data, not inferred from code.

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
| ~~Re-certifying the corpus after a tuning republish~~ — **resolved 2026-09-07**: J4's `diff.py` proves a tree-wide magnitude retune produces an **empty** human queue (40-node fixture, all-`magnitude-retune`, `human_review_queue() == ()`) | High | Every D42-style republish is now provably cheap rather than assumed cheap — the empty-queue claim is a passing test, not a design intent |
| ~~`provenance-supersede` is unbuilt and pass 2 cannot run without it~~ — **resolved 2026-09-07**: `record_superseded` built (`nodegen/run.py`), 5 tests. Scope corrected along the way — the real blocker was never the shared `ProvenanceLedger` class (§8 had misattributed it); passive-tree's own ledger is a separate, local, plain-JSON idempotence guard, so no cross-program change was needed at all | High | J9's 2–3 planned passes are unblocked whenever J9 itself starts; the prior record survives under `supersededRecord` rather than being discarded on re-review |
| The species-namespace affix bill: **6,720 authored affixes** against a shipped authored corpus of ~~two~~ 10 (stale count, corrected 2026-09-07) | High | J7 is its own task and its own run, with the cost stated here before it is scheduled. **2026-09-07: the run's own code prerequisite was found MISSING (no way to target the namespace at all) and is now built + proven** (`--species-id`, 11 tests, a real proof-of-concept model call) — the 6,720-call run itself is unblocked but still not scheduled. J6's marking rule keeps a later `speciesUniqueAffixMin` change `O(diff)` |
| `battle-tempo` is editing `BattleModels.cs` / `BattleRunState.cs` right now | Medium | R9: cite by symbol, never by line. Seventeen citations already drifted twice during the spec round. G1 and E3 both touch `BattleRunState` — the one place wave 0's "no shared files" claim does not hold, and they are sequenced accordingly |
| Review rate unknown; every hour figure rests on it | Medium | H8's 20-tree pilot, early, gates the full census rather than the whole program |
| ~~The generic corpus is generated before its gate quantities exist~~ — **superseded 2026-09-06**: both gate quantities shipped and are live-probed (G6); `R-G1` no longer refuses anything. ~~Current risk: the 30 elemental/status trees have no plan-emission tooling at all~~ — **resolved 2026-09-07 (J1)**: `elemental_tree_spec`/`status_tree_spec` are built, all 30 plans emitted, generation run to 99.8% convergence | Medium | Was J1's own named risk; J1 closed it exactly the way this row predicted — a mechanical extension of `primary_tree_spec`'s own already-generalized pattern (H9), not new design |
| Species volume (33,600 nodes, ~105,840 calls) | Medium | Last phase, resumable with a mid-run-kill test, and D41's 8-of-40 bounds the *unique* authoring to 6,720 affixes. ~~2026-09-07 (J8): a real, more specific risk found underneath this one — the resumable RUN this row promises has no ORCHESTRATION to be resumable yet, and building it needs a `forced_aptitude` concept the shared `build_plan`/`TreeSpec` module has never needed before~~ — **resolved the same session**: `TreeSpec.mechanical_favour` + `species_tree_spec()` + `build_plan`/`build_slot` fixes now correctly force a species tree's element+status from its favour lock (aptitude confirmed to need no such force at all, per §8's own atom-tag-vocabulary posture), proven against the real 904-species corpus and `might.v1.json`. **Current, narrower risk**: only the ORCHESTRATION CALLER (sequencing favour-fit → plan/quota → node-gen → codex into one resumable run) remains unbuilt; every piece it would call is now real and tested |
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
