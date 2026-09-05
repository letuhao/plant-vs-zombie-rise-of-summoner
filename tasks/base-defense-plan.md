# Base defense — implementation plan

**Program:** `base-defense` (the siege stage) · **29 modules** · 46 owner decisions across eleven rounds.
**Map:** [docs/architecture/base-defense-map.md](../docs/architecture/base-defense-map.md) — the index.
**Specs:** [docs/architecture/base-defense/](../docs/architecture/base-defense/) — one per module id.
**Ideal:** [base-defense-ideal.md](../docs/architecture/base-defense-ideal.md) · **Audit:**
[_completeness-audit.md](../docs/architecture/base-defense/_completeness-audit.md) (four passes).
**Tasks:** [base-defense-todo.md](base-defense-todo.md).

**Status:** plan, 2026-09-05, updated 2026-09-05. Execution is under way — see
[base-defense-todo.md](base-defense-todo.md) for what is actually built. Gate 0 through CP1 (Levels
0–2) are done and evidenced there; Level 3 is next. **All three gates this plan carried are resolved
(§7)** — none are currently blocking any task in the program.

---

## 1. What this plans, and what it deliberately does not

**Plans:** all 29 module specs, their ordering, the three gates, and the checkpoints between phases.

**Does not plan:** time estimates. There is no basis for them here and an invented one becomes a
commitment. What this gives instead is **module count per level, which modules are small, and which
single module is the largest** — enough to sequence without pretending to schedule.

**Does not restate the specs.** Each spec already carries its own contract, tunables, numeric types,
boundaries, test list and success criteria. This plan carries what a spec cannot: **order, gates,
parallelism, and the risks that span modules.**

---

## 2. Two families, one join

```
THE SIEGE (1–22)              engine → world seam → board → AI → FE
STRUCTURE CONTENT (23–29)     schema → corpus → catalog → instantiate/planner → pipeline → metrics
```

They meet at **exactly one point**: `structure-catalog-import` (c2) replaces the four hand-authored
`StructureCatalog` rows the siege family ships against. **Everything else is independent.**

Two consequences worth planning around:

- **They can be worked in parallel by different sessions** without merge contention — different
  namespaces, different test projects, different failure modes.
- **The content family is model-free until c4.** Schema, corpus, catalog-import, instantiate and
  planner spend **zero tokens**. That is deliberate: *"a parse, a table, a schema and a dump produce
  real value with zero tokens spent, and they make the expensive stage's inputs reviewable."*

---

## 3. Build order and gates

Verified mechanically: **no cycles, every dependency at a strictly earlier level.** Re-run that check
(todo task **V1**) after any module moves.

```text
GATE 0   inventory current + determinism guard extended     ← part 1 done, part 2 pending

0.   battle-clock-profile · siege-supply · world-graph-diff      (parallel, no deps)
1.   siege-board
2.   siege-pathing · district-layout · siege-seam                (parallel)
3.   structure-state · combatant-kind                            (parallel)  ⛔ the golden landing
3b.  siege-objective
GATE A   the seam holds · zero world goldens moved

4.   siege-positions · siege-waves · siege-obstacles             (parallel)
5.   siege-cover · siege-construction                            (both consume obstacles)
6.   siege-economy · siege-ai
7.   siege-resolver         ⭐ PLAYABLE AND CI-PROVABLE HERE, WITH NO FE
7b.  siege-engagement
GATE B   a siege resolves deterministically · resolver at BOTH call sites

8.   board-render
8b.  siege-stage · battle-stage                                  (parallel)

CONTENT FAMILY — parallel with all of the above
c0.  structure-schema
c1.  structure-corpus
c2.  structure-catalog-import        ← the only join with the siege family
c3.  structure-instantiate · structure-planner                   (parallel)
c4.  structure-pipeline              ⭐ the FIRST model call in the program
c5.  structure-metrics
```

### The three gates, and what each is actually for

| Gate | After | Proves | Cost of skipping |
|---|---|---|---|
| **0** | before level 1 | The inventory is current, and `Core/Battle`/`Core/Effects` are under the determinism guard | Every siege module lands inside an unguarded tree; you audit code instead of preventing it |
| **A** | after 3b | The world/combat seam carries a board and **zero world goldens moved** | Level 4+ builds on a seam that may already have moved a hash nobody has noticed |
| **B** | after 7b | A siege resolves deterministically, resolver at **both** call sites, feature-absence structural | The FE is built on a resolver whose replay disagrees with itself |

**⭐ Level 7 is the milestone that matters.** A siege is playable and provable in CI **with no FE at
all**. Everything after it is presentation. If the program is ever cut short, cut it after 7b.

---

## 4. Vertical slicing — the rule, and how it applies here

The skill's rule is *one complete path per task, never a horizontal layer.* For this program a
**module is already close to a vertical slice** (contract → code → tests → guards). So the slicing
rule bites *inside* modules:

**Correct (vertical):** *"`Withdrawn` survives the seam: field on `BattleSideOutcome`, handled in
`BattleApplication.Apply`, asserted by a test that a withdrawing entity keeps its orders."*

**Wrong (horizontal):** *"add all new fields to all seam records"*, then later *"handle all new fields
in `BattleApplication`."* The second half is where the rout-penalty bug would hide.

**Every task in the todo names its acceptance, its verification command, and its files.** A task
touching more than ~5 files is a task that should have been two.

---

## 5. Risk register — what spans modules

Ordered by cost of discovering it late.

| # | Risk | Where it bites | Mitigation, already specced |
|---|---|---|---|
| **R1** | **The golden-locked landing.** `structure-state` + `combatant-kind` are the only modules that touch hashed state | Level 3 | **One batched landing**, conditional canonical rows (`faction-scope` precedent), and a triage pass shared with anyone else moving `RulesetVersion`. ✅ **Coordination check CLEARED by owner 2026-09-05 — no other in-flight program has a `RulesetVersion` bump queued. Level 3 is unblocked.** |
| **R2** | **`decisions_json` has no writer.** Decision 46's pause cannot resume without it, and per `DecisionTrace`'s own comment the boot sweep may **overwrite a played result with an AI re-resolve** | Level 8b — but the risk is **live today**, for every played battle | Owned by `spec-interactive-turns.md` (T10), **not this program**. **Non-blocking for base-defense** (owner decision 2026-09-05): tracked here as a cross-program follow-up, raised independently with that program when convenient — it does not gate any base-defense task, including Level 8b's own tasks |
| **R3** | **Both resolver call sites.** Wiring only `RpgStore.WorldTurns.cs:509` makes every re-derived turn report disagree with what happened — and it looks like a UI bug | Level 7 | `siege-resolver`'s first success criterion, plus a **source-scan test** so it cannot regress silently |
| **R4** | **Mode collapse in an invention pipeline.** Majority vote does not catch it | c4 | Hand-author the corpus **first** (c1) so the model extends a real distribution; n-gram guard as a **review queue, never a gate** |
| **R5** | **Entry-chunk budget** (≤180 KB gz vs 713 KB today). Phaser is large | Level 8 | Lazy-load the board layer; `npm run check:bundle` as a gate, not a review note |
| **R6** | **`board-render` is the largest single module** — measured, `stages/world` is 6,902 LOC | Level 8 | Five extractions, **each landing with the lawn rendering byte-identically**. Five reversible steps, not one refactor |
| **R7** | **Determinism drift** — an unordered enumeration or an unstated tie-break reproduces on one machine and not another | 1, 2, 6, 11 | 10,000-run assertions in `siege-pathing`, `siege-ai`, `siege-cover`, `district-layout`; Gate 0's guard extension underneath all of them |

**R2 is raised, not blocking.** It is someone else's module, it is a live defect today, and the siege
only makes it visible — but "someone should raise this" is not a reason for base-defense's own build
plan to stop. It is tracked and will be raised with that program independently of this plan's progress.

---

## 6. Checkpoints

A checkpoint is a **stop-and-verify**, not a status update. Six.

| CP | After | Verify |
|---|---|---|
| **CP0** | Gate 0 | Guard extended; `EffectBag.cs:180` finding recorded and fixed; full suite green |
| **CP1** | Level 2 | Seam widened, board exists, pathing deterministic — **zero goldens moved anywhere** |
| **CP2** | Gate A (after 3b) | The one golden landing is in; world goldens byte-identical **unblessed**; win condition and field cap exist |
| **CP3** | Level 6 | Cover, construction, economy and AI green; AI provably RNG-free and float-free |
| **CP4** | Gate B (after 7b) | ⭐ **A siege plays and resolves in CI with no FE.** Both call sites wired; multi-turn loop works |
| **CP5** | Level 8b | Both stages ship; lawn byte-identical after five extractions; entry chunk unchanged; **zero declared-but-unbuilt stage ids** |
| **CPc** | c5 | Corpus generated, idempotent by hash, metrics declare closed/open, no numeric field anywhere |

---

## 7. Owed before the levels that need them — resolved 2026-09-05, none block the build

Both items below were originally written as hard pre-work gates: stop and wait for an answer before
starting the level. The owner's 2026-09-05 ruling on both (see `tasks/base-defense-todo.md`'s Level 3
header) is that **a coordination or approval question is not, by itself, a reason to halt a build plan
that can otherwise proceed** — this repo already has a tunable/reversible-default pattern
(`docs/architecture/tunables-ssot.md`) for exactly this shape of uncertainty, and a gate that blocks
shipping becomes a burden rather than a safety net. Both are now resolved or reframed accordingly:

1. ✅ **`RulesetVersion` coordination** (R1) — **CLEARED.** Owner confirmed directly 2026-09-05: no
   other in-flight program has a `RulesetVersion` bump queued. Level 3's batched landing may start.
2. **A `decisions_json` writer** (R2) — **reframed as non-blocking.** It is still someone else's module
   (`spec-interactive-turns.md`, T10) and still a live defect worth raising — but it no longer gates any
   base-defense task, Level 8b included. Raised as a cross-program follow-up, tracked here, not waited on.
3. **`world-graph-diff` step 1 is a measurement, not a build** — and it may conclude the diff is
   unnecessary. Record that outcome either way; an unanswered prerequisite is how it became nobody's.
   (This one was never a hard gate — it is a task with its own acceptance criteria, already closed;
   listed here only for the historical record of what this section originally tracked.)

---

## 8. Where the work concentrates

Not a schedule — a shape, so sequencing decisions are informed.

| Level | Modules | Note |
|---|---|---|
| 0 | 3 | **Two are small.** `battle-clock-profile` moves two fields + adds one row; `siege-supply` is a predicate split |
| 1–2 | 4 | `siege-pathing` is determinism-heavy for its size |
| 3–3b | 3 | ⛔ **The only golden-locked landing.** Batch it |
| 4–5 | 5 | `siege-cover` is the mechanically densest — four multipliers + an action-system change |
| 6–7b | 4 | ⭐ Ends at the playable milestone |
| 8–8b | 3 | **`board-render` is the single largest module in the program** |
| c0–c5 | 7 | Model-free until c4; c1 is hand-authoring ~36 rows |

**Start here:** Gate 0 part 2 is **one line** (`WorldDeterminismGuardTests.cs:144`), is **expected to go
red** on `EffectBag.cs:180`, and depends on nothing. It is the smallest change in the program that
produces real information.

---

## 9. Coverage audit — run 2026-09-05, and how to re-run it

The first draft of the todo was checked only by *"is every spec linked?"*. **That is not coverage.**
A proper check measures two axes, and it found real gaps on both.

| Axis | Method | First run | After fixes |
|---|---|---|---|
| **A — success criteria** | Extract each spec's numbered success criteria, pull the distinctive identifiers, assert each appears somewhere in the todo | **13 unmatched** | **0** |
| **B — contract sections** | Count `### N.` sections per spec against tasks in that module's todo block; flag where `tasks×2 < sections` | **1 module** (`siege-ai`: 15 sections, 6 tasks) | **0** |

**Thirteen tasks added.** The gaps clustered where a spec had grown after its tasks were written:

- **`siege-ai`** — four missing: the named validity filter (§5.20 rule 2), the **stated** retarget latency
  (rule 3), the replacement vocabulary for an emplacement (rule 5), and the **auto-versus-played dial**.
- **`siege-obstacles`** — `RequiresLineOfSight`'s first reader, the Emplacement, and the no-directional-cover
  assertion.
- **`siege-cover`** — mechanic 5 proving cover **and** obstruction vanish **together**, plus asserting the
  absences (no dodge grant, and the vocabulary budget **not** spent twice).
- **`siege-supply`** — `ConnectedSectors` staying **uncached**, which is a success criterion and was in no
  task.
- Plus `SlotState.Ruined`'s first reader, `DevelopmentLevel` defaulted / board never persisted,
  both-sides-one-path reinforcement, pre-battle-and-in-battle-one-path, and `Resolve`'s throw behaviour.

**One finding was a SPEC defect, not a todo gap:** `spec-battle-stage.md` wrote the route as
`#/battle/{battleId}` in its contract and `#/battle/{id}` in its success criteria. Fixed in the spec.

### Re-run it after editing any spec

Both checks are short scripts over `## Success criteria` blocks and `### N.` headings. **Axis A has a
known false-positive class**: it matches *qualified* identifiers literally, so a task saying
`WorldCanonical` will not match a criterion saying `WorldCanonical.Write`. When it flags something,
**check whether the concept is covered before adding a task** — the fix is often to name the exact
identifier in the existing task, which is better writing anyway, or to correct the spec.

Together with **V1** (the acyclic-graph check, which found four ordering errors reading had missed),
these are the three checks that keep this plan honest. **None of them is a judgement call.**

---

## 10. Standing rules for every task

Binding, from `AGENTS.md` / `CLAUDE.md` — restated because a downstream session reads this file:

- **A gate must be answerable, or it isn't a gate — it's a stall.** Ruling made 2026-09-05 after §7's
  two "owed" items sat as hard pre-work blockers with no path to resolution: before writing a gate that
  halts starting a level, check whether the uncertainty could instead ship behind a tunable/configurable
  default and get corrected in a later balance pass (this repo's own convention —
  `docs/architecture/tunables-ssot.md`). Reserve an actual pre-work halt for the one case that is not
  reversible after the fact — two writers about to collide on the same hashed/versioned state with no
  way to detect or repair the collision later (that is what R1 protects, and it resolves by asking one
  question with a yes/no answer). An approval, a coordination check, or "raise this with another team"
  is a **tracked, non-blocking follow-up**, not a stop condition on the whole plan.
- **No git writes.** Ever. Draft a message; the owner commits.
- **`long` for every magnitude**, never `float`, widen before multiplying, divide by 1000 last exactly
  once, overflow **throws**.
- **No balance number in code.** `data/tuning/<domain>.v{n}.json`.
- **No hard progression ceilings.** A board/runtime cap is exempt **and must say so in a comment.**
- **One power ladder.** Contests read `Θ`; magnitudes read `P(Θ)`. No private `f(level)`.
- **Run the boundary guards** before finishing injector/Core/Data work.
- **`dotnet test` with plain `>` redirection, never piped through `tail`** — a pipe breaks output
  capture and the run looks dead. Confirmed twice on this machine.
