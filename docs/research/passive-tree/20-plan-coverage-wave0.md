# Plan coverage audit — four wave-0 specs against `passive-tree-todo.md`

**Status:** research, 2026-09-05. Read-only audit. Nothing in `src/`, `tools/`, `tests/`, `data/`,
the specs or the plan was changed.

**What was audited:** `tasks/passive-tree-plan.md` and `tasks/passive-tree-todo.md` (27 tasks, 6
checkpoints, phases A–F) against `docs/architecture/passive-tree/spec-tree-plan.md`,
`spec-gate-counters.md`, `spec-mechanism-wiring.md` and `spec-squad-harness.md`.

**Method.** For each spec: enumerate its numbered rules (`R-*`, `G*`, `A*`, `C*`, `P-*`, its `S1`–`S4`
stages, its test tables), its Structure / Testing / Boundaries sections where they describe work, and
its Decisions-implemented rows that imply an implementation. Then find the task that would *build*
each one. A requirement covered only by a checkpoint bullet or only by the standing verification
block is **PARTIAL** — a checkpoint verifies, it does not build.

**Headline.** The plan's spine (phase A) is well matched to `spec-tree-plan.md`'s **runtime** half and
to `spec-gate-counters.md`. The gaps cluster in three places, and all three are real:

1. **`data/tuning/passive-tree.v1.json` does not exist and no task creates it.** Verified:
   `ls data/tuning/ | grep passive` returns nothing. All four specs name keys in it —
   `spec-tree-plan.md` §Tunables, `spec-gate-counters.md` §13 (P3 says so outright: *"does not exist
   yet and is named by every module in this program"*), `spec-squad-harness.md` §6,
   `spec-mechanism-wiring.md` §7 (which names it as somebody else's file). Neither does anything
   create `data/tuning/passive-tree-targets.v1.json`, which `spec-tree-plan.md` §8 needs before the
   quota algorithm can run.
2. **`spec-squad-harness.md` is a five-stage module served by one task.** B4 delivers §10.1 (A10a)
   and roughly one line of §5. S1's own core — the 91/23 rosters, the three columns, the two
   artifacts, the determinism hash, the two-stage resolution, the coverage block — is unscoped, and
   S2/S3/S4 have no task at all. S4 is the one the spec calls *claimed, not optional*, and it is the
   evidence `tree-plan`'s headline *"no tree is OP"* rests on.
3. **`spec-tree-plan.md` gets three tasks and it owns eleven checkable rules.** `R-A1`, `R-M1`,
   `R-M2`, `P-1`, `P-2`, `C1`, `R-G1`, `R-G2`, `PassiveTree/TreeEqualValue`,
   `archetype_shapes_actually_differ` and the two roster mirrors have no home. A1 covers the ladder,
   the budget column and the ids well; nothing covers the corpus-level invariants.

---

## 1. `spec-tree-plan.md` — 9 covered · 6 partial · 9 missing · 1 contradiction

| # | Requirement | Status | Task, or the gap |
|---|---|---|---|
| TP-1 | §1 rootless 40-node topology, 20 per branch, corpus stated as a function of the roster | COVERED | **A1** |
| TP-2 | §1 graph invariants `G1`–`G9`; no node at tier 1 has a parent; no cross-branch edge | PARTIAL | A1 emits the topology; no task names `G1`–`G9` or `no_node_has_a_parent_at_tier_one`. §Testing lists both as their own tests |
| TP-3 | §2 `req(t) = k·t(t+1)/2`, `W/req = b/k` at every tier | COVERED | **A1** ("`W/req = b/5` at all ten tiers") |
| TP-4 | §2 `R-G0` — `ladder.gateCurrency` is `aptitudePoints`, anything else exits 3 | COVERED | **A1** |
| TP-5 | §3 tier budget column sums to 1000‰, round half up, residual to the deepest tier | COVERED | **A1** |
| TP-6 | §3 `C1` — `Σ budgetPoints` identical across **all** `n` trees, `Σ off == Σ def` | PARTIAL | A1 emits one tree, so `C1` is unassertable there; D4 emits twelve but its acceptance never names `C1`. This is the module's headline property |
| TP-7 | §3 `archetype_shapes_actually_differ` — strongest node differs ≥ 2× across archetypes | MISSING | The inverse guard on D15. Without it "equal value" can silently collapse into "equal shape" |
| TP-8 | §3 `nodes[].budgetShareMilli` is authoritative; binder drops `tierWeight`/`weightTotal` (R4) | COVERED | **A4**, explicitly |
| TP-9 | §3.1 `R-A1` — reward-per-skill-point spread bounded at **every** tier; `archetypes[].rewardPerPointMilli[]` emitted; `archetype.rewardSpreadMaxRatioMilli` | MISSING | The spec calls this *"§3.1's missing test"* and says `C1` structurally cannot catch it |
| TP-10 | §3.2 `PassiveTree/TreeEqualValue` — registered beside `QuotaDrift`/`CellOccupancy`, run at `--emit` and `--check` | MISSING | The spec claims this module *owns* it precisely because it was a gate every spec cited and none built |
| TP-11 | §4 the mechanism ramp `archetypes[].mechNodes[]`; `R-M1` (deepest tier 100% mechanism); `R-M2` (monotone) | MISSING | Also the interface `tree-language` consumes as an exact per-tier count. Nothing in D1/D2 mentions it |
| TP-12 | §4 `nodeClass` derived, re-derived at stage 3, refused on mismatch | PARTIAL | Implied by D2's property set and A4's binding; no task names the re-derivation refusal |
| TP-13 | §5.1/§5.2 one-branch denominator; `P-1` derivation guard; `P-2` rounding guard at tier counts 1..40 | MISSING | A2's *"a `kMicro` over the ceiling"* is `tree-catalog`'s **load-path** check — a different check on a different artifact |
| TP-14 | §6 the thirteen-axis property vocabulary, every count read at load, no hardcoded roster count | COVERED | **D2** ("the plan emits the closed property set before any node text exists") |
| TP-15 | §6/§Repro a missing mirror refuses (exit 2) — and the two mirrors this module **owes**: `data/seed/statuses/roster.json`, `data/seed/atoms/vocabulary.json` | MISSING | Verified absent: `data/seed/statuses/` does not exist, `data/seed/atoms/` holds only `fx-*.json`, `generated/`, `trait-critical-hunter.json`. §9 items 1–2 call them blockers on emitting a complete plan |
| TP-16 | §6 `conversionState` axis emitted; zero budget to conversion nodes | COVERED | **A4** + the non-blocking-asks row |
| TP-17 | §7 the plan emits `gateQuantity`, `gateIndexKind` and `gateState` per tree, and never resolves them | PARTIAL | Phase C builds the counters; nothing emits `gateState` into the plan or defines the checked-in evidence row it is derived from |
| TP-18 | §7.1 `R-G1` — stage 2 exits 3 when asked to generate content for a `pending` tree | MISSING | F1's *"only after their gate quantities are live"* is a schedule note in prose. `R-G1` is a refusal in code, and the spec's whole point is that it *"keeps the schedule honest"* rather than relying on discipline |
| TP-19 | §7.1 `R-G2` — `trees[]` ordered by `generationWave` then ordinal; the wave derived from `gateState` | PARTIAL | D4 does the primary-first *behaviour*; nothing emits the ordering or derives the wave |
| TP-20 | §8 quota cells: `largest_remainder_count`, step-5 return-to-pool, `permittedIds` → schema `enum` | COVERED | **D2** (quota + return to pool) and **D1** (permitted values are the schema enum) |
| TP-21 | §Node ids grammar `skill.<treeSlug>-<branch>-t<tier>-<nodeKey>`, minted once, read back, refuses to re-mint | COVERED | **A1**, all three halves |
| TP-22 | §Repro byte-identical emit, `planHash`, canonical JSON, `emittedUtc` excluded, `--diff` | PARTIAL | D4 covers re-emit identity and no re-mint. `planHash`, the canonical-JSON rules, and the `--diff` command are unscoped |
| TP-23 | §Tunables — `data/tuning/passive-tree.v1.json` and `data/tuning/passive-tree-targets.v1.json` with this module's keys | MISSING | Neither file exists; no task creates either |
| TP-24 | §9 items 3–4 — an `ssot-power-scale.md` §10 row for `req(t)` and a §11.10 authored-depth row, both **before this module ships** | MISSING | A named success criterion. `guard-power.ps1` provably cannot catch the absence (its checks key on a parameter named `level`/`lvl`/`index`; `req(t)`'s is `t`) |
| TP-25 | §Project structure — the planner is a **seedsmith adapter**, `tools/seedsmith/seedsmith/adapters/trees/plan/*.py`, reusing `largest_remainder_count`, the corpus loader and the CLI exit codes | ⛔ CONTRADICTED | **A1** names `**Files:** tools/PassiveTreeGen/ (new)`. The spec is explicit: *"The planner is a seedsmith adapter, not a new tool"*, and its §Commands are `python -m seedsmith trees plan --emit/--check/--diff` |

---

## 2. `spec-gate-counters.md` — 10 covered · 4 partial · 8 missing

Phase C covers the counting rules, the persistence, the index transform and the registry well. It
does **not** cover the three shipped-code prerequisites, the tunables, the surface, or the two SSOT
rows.

| # | Requirement | Status | Task, or the gap |
|---|---|---|---|
| GC-1 | §2.1 (a)–(d): outbound, landed, fresh, distinct host, never self | COVERED | **C1**, verbatim |
| GC-2 | §2.1 ownership is decided at spawn — charmed/hypnotised actors do not launder credit | PARTIAL | Not named in C1. `hypno` and `charm_pulse` put an enemy on the player's side, so this is a farm, not an edge case |
| GC-3 | §2.2 one credit per element component on a direct landed hit; DoT pulses excluded | COVERED | **C2**, verbatim |
| GC-4 | §2.2b `Outcome == Applied` **and** `AppliedAmount != 0`; a fully-absorbed hit earns nothing | PARTIAL | C2's acceptance and verification cover the pulse rule only. §11 test 7 is the pipeline's deliberate zero-delta miss-telemetry parity |
| GC-5 | §3 the index is the square-root transform; `c = 23`; tier-10 parity within 5% from tier 4 up | COVERED | **C3** ("Tier 10 opens within 5% of the primary tree's Θ from tier 4 up") |
| GC-6 | §6/§9 integer-only index (no `Math.Sqrt`, no `double`), division-based predicate, survives a count at `long.MaxValue` | PARTIAL | Only the standing `audit-overflow.py` line. §11 tests 3 and 4 are named tests the spec asks for |
| GC-7 | §4.1 `rpg_gate_counter`, sparse, raw counts only, no cap, `owner_kind`/`owner_key` | COVERED | **C1** |
| GC-8 | §4.2 SQL only in `RpgStore.GateCounters.cs`, a partial slice sharing `_gate`, `EnsureHotSchema` and `Reset()` | PARTIAL | `guard-dal` is in the standing block; the `Reset()`-participation test the spec asks for is unnamed |
| GC-9 | §4.3 in-memory accumulator, 5 s + match-end flush, one batched transaction, no hot-path write | COVERED | **C1** |
| GC-10 | §5.2 `IGateQuantitySource` + `GateQuantityRegistry`, answer in aptitude-point-equivalents, no `AptitudeAllocation` constructed | COVERED | **C3** |
| GC-11 | §5.2 the tier-0 reason field — *no aptitude allocated yet* vs *this quantity has no producer* | MISSING | The registry is what answers the second. Not named in C3, and A6 does not carry it either |
| GC-12 | §12 exclusive registration per family, throws naming both owners, no combine path | COVERED | **C2**. The composition-root guard test the spec asks for is unnamed |
| GC-13 | §7 **P1** — `DamageOrigin origin = DirectHit` defaulted parameter on `DamageApplyPipeline.Apply`, plus the pulse construction sites | MISSING | C2's acceptance *depends* on it and no task builds it. The spec flags this as the one file crossing another wave-0 module's surface (`BattleRunState`, also `mechanism-wiring` G2) |
| GC-14 | §7 **P2** — a new `OnFreshApplication` property on `StatusRuntime`; `OnApplied` untouched | MISSING | C1's fresh-vs-refresh rule is undeliverable without it |
| GC-15 | §7 **P3** / §13 — the `gateCounters` tuning block: five keys, T5 refusal on a missing key, refuse on a rate divergence with no `rateDivergenceWhy` | MISSING | §11 tests 13 and 14 |
| GC-16 | §6 two `ssot-power-scale.md` §10.2 rows at ordinals **29 and 30**, and the row-count line at `:587` moved with them | MISSING | Verified: the highest ordinal today is 28 and `:587` reads *"27 rows today"*. Success criterion 7. `guard-power.ps1` cannot detect the absence — the parameter is `count` |
| GC-17 | §10 `src/FusionRpg.Server/GateCounterEndpoints.cs` — `POST /api/gate-counters/credit`, `GET /api/gate-counters/{playerId}` for `tree-surface` | MISSING | Not in phase C and not in E1–E4 |
| GC-18 | §10 injector subscription — both counters wired where the status runtime already is (`EffectRuntime.cs:59,69`) | MISSING | Implied by C1/C2; the injector is a separate assembly with its own guard-test convention |
| GC-19 | §16 OQ1 / D43 cold start from a proxy, one-time, stamped, never for a new player | COVERED | **C4** |
| GC-20 | §15 criterion 9 — the lawn's per-hit cost unchanged within probe noise | MISSING | No task's verification runs a probe |
| GC-21 | §11 test 9 — crediting never moves `GrandTotal()` or any of the twelve `Share()` values | COVERED | **C3**, and correctly demanded as an executable test |
| GC-22 | §15 criterion 2 — all 39 generic trees reachable above tier 0 | COVERED | **Checkpoint C** |

---

## 3. `spec-mechanism-wiring.md` — 6 covered · 4 partial · 9 missing

**The G1–G4 scoping matches.** G4 is excluded in the spec (`definitions.md` §14.2 is a design law, not
an oversight) and no task widens `stat.derived`'s trigger set. The 17th atom kind is excluded in §9
and appears in the plan only as a non-blocking ask whose default — *the binder refuses conversion
nodes* — is exactly what A4 builds. **Nothing has been silently added or silently dropped at the
gap level.** The gaps are inside G1 and G3, and in the propagation the spec demands.

| # | Requirement | Status | Task, or the gap |
|---|---|---|---|
| MW-1 | G1 — the fourth `IActorStatSubsystem` in Core, own `SubsystemId`, opt-in delegate | COVERED | **B1** |
| MW-2 | G1 — `src/FusionRpg.Injector/Stats/LiveStatusMods.cs`, the `CheatState.cs` `liveStatuses:` argument, and `StatusDerivedWiringGuardTests` | MISSING | The injector cannot host a test project, which is why the spec specifies a text guard. B1 names neither |
| MW-3 | G1 — extract `IsDerivedChannel` from `StatusStatPayload`, and **refuse `more` on a derived channel at parse** | MISSING | There is no `More` on the derived side; the spec's own mutation set targets exactly this |
| MW-4 | G1 — the seam test with the three-subsystem **falsifier** arm | COVERED | **B1**'s *"a test that fails against `main` and passes after"* is that falsifier |
| MW-5 | G1 — two stacks withdraw independently; contributions name the status instance; empty delegate contributes nothing | PARTIAL | B1's acceptance names composition and registration only |
| MW-6 | G2 — one `RecomposeDerived` per actor per round; idempotent; goldens **run** not reasoned about | COVERED | **B2**, including the deliberate re-bless clause |
| MW-7 | G3 — the contribution fold on `ActorDerivedLookup`, wired into **both** `SimEffectHost` **and** `FoundationHarness` | PARTIAL | B3 says *"a `stat.derived` atom binds and evaluates in Sim"*. `tools/CombatSim` drives `FoundationHarness`, not `SimEffectHost` — miss it and the harness still reads a bare pinned snapshot |
| MW-8 | G3 — a `BindContext(RuntimeId.Sim)` call site so a bind is actually attempted | PARTIAL | Implied by B3's *"binds"*; the spec makes it step 3 of four because flipping the cell alone *"authorizes nothing"* |
| MW-9 | G3 — `Full` vs `Partial` decided **from the built executor** by exercising all four derived ops; the cell moves **last** | MISSING | The spec is emphatic: a fold on `OverlayAdd` honours `Flat`/`Increased` and not `Replace`/`Flag`, so *"the honest first landing is `Partial`"*. B3 does not carry the decision or the four-op test (A6) |
| MW-10 | G3 — `IlvlTierLadderTests.cs:87` moves with the cell | MISSING | B3 names `AtomKindRegistryTests` only. The other assertion goes red on the same edit |
| MW-11 | G3 — amend `decisions.md:106`'s *"Sim stays `None`"* in the same change | COVERED | **B3**, explicitly |
| MW-12 | §3/§10 propagation — the `actor-hub-ssot.md` §6 registry row at order **400** (A9), and `atom-catalog-ssot.md`'s `stat.derived` runtime row | MISSING | Both are "in the same change" items under evidence rule 6 |
| MW-13 | A7 — `KindCount == 16`, `TriggerCount == 13`, `AttachPointCount == 7` unchanged; `stat_derived_still_refuses_every_trigger` stays green | PARTIAL | The plan adds no kind or trigger, which is the substance. No task asserts the counts, and `DESIGN-GATE.md` §1's atom row has gone stale on them twice |
| MW-14 | §9 — no budget to conversion nodes until a reviewed 17th kind; the binder refuses one | COVERED | **A4** + the non-blocking-asks row, and the default matches the spec |
| MW-15 | A10 — direction, 3.0pp effect size, ≤ 1.0pp half-width, selectivity `ΔW_spread ≥ 2 × ΔW_corner`, three verdicts, UNRESOLVED holds the gate | COVERED | **B4** + **Checkpoint B**, which correctly makes UNRESOLVED stop the phase |
| MW-16 | §11.1 A10b — the shipped vehicle, needing G1 + G2 **plus a Battle status → `BattleDerivedModifierLedger` producer that no module's modified-files table contains** | MISSING | Neither a task nor an ask. The spec says it is *"not scoped here"*, so the plan is the place it becomes visible |
| MW-17 | §12 OQ1 — does the L2b resist path read status-granted resist channels? *"Owner call, because it changes shipped resist math"* | MISSING | Not in the non-blocking-asks table |
| MW-18 | §12 OQ2 — `aura-skill` T13's ack on the per-round-recompose split | MISSING | Needs that program's acknowledgement; B2 lands the change regardless |
| MW-19 | §6 Mutation — `mutate.ps1` over the new subsystem; the `IsDerivedChannel` and `TryParseOp` mutants | MISSING | The standing verification block names coverage/guards but not mutation |

---

## 4. `spec-squad-harness.md` — 2 covered · 2 partial · 12 missing · 1 contradiction

**Answer to the brief's question: one task is not enough. This spec needs four.** B4 delivers §10.1
and about one line of §5. The module the spec describes is a new `tools/SquadHarness` project with
eight modes, two rosters, three columns, two artifacts, a determinism hash, twenty named tests and a
four-stage plan (S1–S4). None of S1's core, and none of S2/S3/S4, has a task.

| # | Requirement | Status | Task, or the gap |
|---|---|---|---|
| SH-1 | §10.1 A10a — four arms, `D` as a difference of differences, its own half-width, the selectivity bar, PASS/FAIL/UNRESOLVED | COVERED | **B4**, accurately |
| SH-2 | §1.2 the two rosters — 91 duel builds (12 + 66 + 12 + 1) and 23 squad builds, both from the one shared corner-shape helper | MISSING | Without them there is nothing for the four arms to be built from, and no `Every_squad_has_exactly_six_actors` |
| SH-3 | §2 squad-vs-squad primary; `--opponent wave` as a separately-reported mode | MISSING | |
| SH-4 | §3 elements neutralised, plus the `coverage` block naming every axis the run did not exercise | MISSING | *"A measurement without its coverage is a claim"* |
| SH-5 | §4 the tree model — `req(t)`, `W(T)`, `H`, `F` per actor; D25's ownership cost in S2 | MISSING | |
| SH-6 | §5 the three columns (`duelClosedForm` / `duelTrials` / `squadTrials`), `orderingByColumn`, `transfers`, `_scope-transfer.json`, `_squad-scope.json` | PARTIAL | B4's *"the 1v1 baseline is shown beside it"* is one column of three and no artifact |
| SH-7 | §6 the keys it settles — `concentration.fmaxMilli`, `concentration.wMilli`, `soulTrack.thetaPerSoulLevelMilli`, D28's four credit rules | MISSING | These are named by `tree-plan` and `tree-resolve` and taken as given. Nothing produces them |
| SH-8 | §7 determinism: `seed(a,d,k)`, common random numbers, `long`/`checked` counts, no `float`, no `double` in the hash | PARTIAL | B4's verification is *"same seed, same numbers"* |
| SH-9 | §8 stalemates leave the denominator; a high-stalemate cell is refused, not scored | COVERED | **B4** ("Stalemate cells refused, not scored") |
| SH-10 | §9.1 the SHA-256 determinism hash with provenance blanked; `A_second_process_reproduces_the_hash`; parallel/serial agreement | MISSING | The module's hard requirement, asserted three ways in the spec |
| SH-11 | §9.2 two-stage screening (3,000) then `--refine` (40,000) only on cells inside their own half-width; the *"cannot separate"* wording for doc 16's Θ ≈ 300 crossover | MISSING | B4 asks for a half-width but not the machinery that reaches 1.0pp |
| SH-12 | §10 the six blocked mechanism classes, and the A10a/A10b split, named in `coverage` | MISSING | So an A10a pass is never reported as an A10b pass |
| SH-13 | §11 **S2** — `concentration` and `crossunlock` modes, `Fmax × w × Θ`, D25 folded in | MISSING | |
| SH-14 | §11 **S3** — the soul track taught to the model; Θ ∈ {100, 150, 200, 300, 400, 600} | MISSING | Doc 16: `w` is the load-bearing late-game parameter and is unmeasurable until the model has both tracks |
| SH-15 | §11 **S4** — the `budget` mode, D15's marginal-value-per-budget-point evidence. *"Claimed, not optional"* | MISSING | The spec says **no other module is scoped to produce it**, and `tree-plan`'s *"no tree is OP"* rests on it |
| SH-16 | §Project structure — `tools/SquadHarness/` (eleven files) + `tests/FusionRpg.SquadHarness.Tests/`, explicitly **not** a single top-level `Program.cs` because that shape is untestable | ⛔ CONTRADICTED | **B4** names `**Files:** tools/HybridViability/, tools/CombatSim/`. Both are exactly the untestable single-`Program.cs` shape the spec rejects by name, and neither exists as a home for `DeterminismTests` |
| SH-17 | §14 three owner questions — mirror squads vs authored waves for the verdict; whether the shipped allocation shape is measured too; whether D15's rule changes | MISSING | None appears in the plan's non-blocking-asks table |

---

## 5. Ranked missing tasks, in the todo's own format

Ordered by what unblocks the most, earliest. Paste-ready.

---

### 1 — A0: the two tuning files, created once, with every wave-0 key

**Spec:** `spec-tree-plan.md` §Tunables, `spec-gate-counters.md` §7 P3 and §13.
**Description:** `data/tuning/passive-tree.v1.json` does not exist and is named by every module in
this program. Create it with the standard `_meta` header and this module set's keys —
`tierLadder.reqScalePoints`, `budget.treeTotalPoints` (flagged **UNMEASURED**, D42),
`budget.branchSplitMilli`, `potency.maxNodeShareMilli`, `potency.minTerminalWidth`,
`potency.bandEdgesMilli[]`, `mechanism.rampStartMilli`, `mechanism.rampEndMilli`,
`archetype.rewardSpreadMaxRatioMilli`, `exclusion.targetShareMilli`, `archetypeAssignment`,
`designTarget.thetaAllIn`, and the whole `gateCounters` block. Also create
`data/tuning/passive-tree-targets.v1.json` with the `quotas.*`, `legitimateSkew.rows` (empty) and
`gates.*` rows §8 needs. **T4 applies from the moment the file exists: never hand-edit, republish
`v{n+1}`.**
**Acceptance:**
- [ ] Both files exist with `_meta`, and every key carries its unit in its name (R2/T6)
- [ ] No built-in defaults — a missing key is a load rejection naming it (T5)
- [ ] `gateCounters.statusMasteryRatePoints` defaulting to the Aspect rate, and a divergence refusing
      without `gateCounters.rateDivergenceWhy`
- [ ] `budget.treeTotalPoints` and `treeShareMilli` carry an `UNMEASURED` marker and a `_note`
- [ ] None of the superseded spellings appears anywhere: `concentration.fmax`, `concentration.w`,
      `ladder.kPoints`, `soulThetaWeight`, `mechanism.floorMilli`, `mechanism.capMilli`
**Verification:** a typed-view load test per module block; a text test asserting no superseded key.
**Depends on:** none. **Scope:** S. **Files:** `data/tuning/passive-tree.v1.json`,
`data/tuning/passive-tree-targets.v1.json`.
*(Blocks A1, A4, C1, C3 — every one of them reads a key from a file that is not there.)*

---

### 2 — A0b: the two roster mirrors `tree-plan` owes

**Spec:** `spec-tree-plan.md` §6, §9 items 1–2, §Reproducibility.
**Description:** `data/seed/statuses/roster.json` (21) and `data/seed/atoms/vocabulary.json`
(7 attach points / 16 kinds / 13 triggers, 11 authorable) do not exist. Same `--check`/`--emit`
contract as `tools/ElementEnumGen`, so a drift between the mirror and the shipped registry is a
failing check rather than a stale file.
**Acceptance:**
- [ ] Both mirrors emit, and `--status-check` / `--atom-vocab-check` exit non-zero on drift
- [ ] Every count is read and counted, never typed — `roster_counts_are_read_never_typed` greps this
      module's source for a bare `12`, `6`, `21`, `53`, `16`, `13`, `7`
- [ ] A missing mirror is `EXIT_CANNOT_RUN` naming the file, never an empty axis
**Verification:** delete a mirror in a temp tree; the planner exits 2 naming it.
**Depends on:** none. **Scope:** S. **Files:** `tools/ElementEnumGen/`, `data/seed/statuses/`,
`data/seed/atoms/`.
*(Blocks A1's `propertyVocabulary` emit.)*

---

### 3 — B0: `squad-harness` S1 — the tool, the rosters, the three columns

**Spec:** `spec-squad-harness.md` §1.2, §2, §3, §5, §7, §8, §9, §Project structure, §Testing.
**Description:** The measurement tool B4 rides on. `tools/SquadHarness/` as its own project with a
thin `Program.cs` over referenceable types — **not** a single top-level `Program.cs`, which the spec
rejects by name because it cannot be tested and determinism is this module's hard requirement. The
91-build duel roster and the 23-squad roster from one shared corner-shape helper; the mode table;
modes `duel`, `squad`, `transfer`, `verify`; the seeded trial loop with common random numbers; the
determinism hash; both artifacts.
**Acceptance:**
- [ ] `transfer` prints three columns and one derived word; `transfers` is `false` whenever an
      ordering rests on a gap inside its own half-width
- [ ] The same command in two processes produces the identical determinism hash; shuffling the roster
      moves no surviving cell; parallel and serial agree by hash
- [ ] The duel roster is proven to be `tools/HybridViability`'s same 91 builds by **constructing**
      them, never by asserting the number 91; every squad has exactly six actors
- [ ] Stalemates leave the denominator; a cell over the flag threshold reports `lowConfidence`
- [ ] Elements are neutral in every generated setup, and `coverage` names every unexercised axis plus
      §10's six blocked mechanism classes
- [ ] Zero files changed under `src/`, `data/`, or `tests/` outside its own test project
**Verification:** `dotnet test tests/FusionRpg.SquadHarness.Tests`; `verify --seed …` twice.
**Depends on:** none. **Scope:** L. **Files:** `tools/SquadHarness/` (new),
`tests/FusionRpg.SquadHarness.Tests/` (new), `docs/research/passive-tree/_scope-transfer.json`,
`_squad-scope.json`.
*(B4 currently has no tool to run in. This is its prerequisite, and it also answers D33.)*

---

### 4 — A1b: the corpus-level plan invariants — `C1`, `R-A1`, `R-M1/M2`, `P-1/P-2`

**Spec:** `spec-tree-plan.md` §3, §3.1, §3.2, §4, §5.2, §Testing.
**Description:** A1 proves one tree. These are the properties that only exist across the corpus and
across the ladder, and they are the ones the spec says the endpoint check structurally cannot catch.
Includes the mechanism ramp `archetypes[].mechNodes[]` — the interface `tree-language` consumes as an
exact per-tier count — and registering `PassiveTree/TreeEqualValue` beside `QuotaDrift` and
`CellOccupancy` so `tree-review` reads it through the same registry.
**Acceptance:**
- [ ] `C1`: `Σ budgetPoints` identical across all `n` trees, `Σ off == Σ def` in each
- [ ] `archetype_shapes_actually_differ`: the strongest node differs by ≥ 2× across the archetype set
- [ ] `R-A1`: `W(t)/cost(N_a(t))` walked at **every** tier as an exact integer ratio, refused above
      `archetype.rewardSpreadMaxRatioMilli` (6000‰), and exactly 1000‰ at `t == tierCount`;
      `archetypes[].rewardPerPointMilli[]` emitted so the 6.0× tier-2 gradient is visible in a diff
- [ ] `R-M1` (`mechNodes[tierCount] == w[tierCount]`) and `R-M2` (monotone `mechShareMilli`)
- [ ] `P-1` recomputes `potency.maxNodeShareMilli` from the **emitted** `tierCount` and
      `minTerminalWidth`; `P-2` finds no rounded share above the derived maximum at tier counts 1..40
- [ ] `PassiveTree/TreeEqualValue` runs at `--emit` and `--check`, refuses naming tree/branch/tier/node,
      and never clamps
- [ ] The two deleted tests stay deleted — `no_node_exceeds_the_potency_ceiling` and
      `every_shipped_archetype_is_admissible` compare a construction against its own supremum
**Verification:** a hand-authored fourth archetype that widens the gradient is refused naming the tier
and the two archetypes.
**Depends on:** A1. **Scope:** M.

---

### 5 — C0: the two shipped-code prerequisites the counters need

**Spec:** `spec-gate-counters.md` §7 P1 and P2.
**Description:** C1's fresh-vs-refresh rule and C2's DoT exclusion are both undeliverable without a
change in `src/` that no task currently owns. **P1:** a defaulted `DamageOrigin origin =
DamageOrigin.DirectHit` parameter on `DamageApplyPipeline.Apply`, with only the pulse construction
sites passing anything. **P2:** a new `OnFreshApplication` property on `StatusRuntime`, fired only
when the upsert added a new instance — `OnApplied` is single-assignment with three assigning sites,
one of which chains by hand, so its signature does not move.
**Acceptance:**
- [ ] The origin defaults, so every existing call site is zero lines changed
- [ ] `OnApplied`'s signature and all three assigning sites are untouched
- [ ] A refresh fires `OnApplied` and does **not** fire `OnFreshApplication`
- [ ] Battle's pulse site passes `DamageOrigin.StatusPulse` — **cite by symbol** (R9); this file is
      under concurrent edit and `mechanism-wiring` G2 also modifies it
**Verification:** a reapply loop fires one fresh event; a `wither` pulse train reports `StatusPulse`.
**Depends on:** none. **Scope:** S. **Files:** `src/FusionRpg.Core/Combat/DamageApplyPipeline.cs`,
`src/FusionRpg.Core/Status/StatusRuntime.cs`, `src/FusionRpg.Core/Battle/BattleEngine.cs`.
*(Sequence before C1/C2, and coordinate with B2 — this is the one file the map's "nothing in wave 0
touches another wave-0 module's files" claim does not cover.)*

---

### 6 — B1a: G1's injector half and the parse refusal

**Spec:** `spec-mechanism-wiring.md` §3, §4.1 sub-decisions 3 and 4, §6.
**Description:** B1 lands the Core subsystem. The half that makes it reach a live actor is the
injector adapter, and the half that stops a wrong number shipping looking correct is the parse
refusal.
**Acceptance:**
- [ ] `LiveStatusMods.For` reads the live `EffectRuntime.Status` static inside `try/catch` and returns
      empty on failure, mirroring `GrantedDerivedAtoms.cs`
- [ ] `CheatState.cs` passes `liveStatuses:` alongside the existing `boundDerivedAtoms:` argument
- [ ] `StatusDerivedWiringGuardTests` — a text guard, because the injector cannot host a test project
- [ ] `IsDerivedChannel` extracted to one public predicate, read by both the parser and the subsystem
- [ ] `more` on a derived channel is refused **at parse** with a named error, never coerced to `Flat`
- [ ] `mutate.ps1` over the subsystem: the always-true `IsDerivedChannel` mutant and the `Flat`
      default-arm mutant are both caught
**Verification:** `dotnet test tests/FusionRpg.Guard.Tests`; the two named mutants die.
**Depends on:** B1. **Scope:** S.

---

### 7 — B3a: G3 finished — both hosts, the four-op decision, the cell last

**Spec:** `spec-mechanism-wiring.md` §4.3, §6, §11 A5/A6.
**Description:** B3 flips the Sim cell. The spec's four-step order says the cell moves **last**, and
two of the earlier steps are not in B3's acceptance. `tools/CombatSim` drives `FoundationHarness`,
not `SimEffectHost`, so folding only one host leaves the harness reading a bare pinned snapshot.
**Acceptance:**
- [ ] `Sim_folds_bound_derived_contributions_onto_the_pinned_snapshot` passes on **both**
      `SimEffectHost` and `FoundationHarness`
- [ ] A `BindContext(RuntimeId.Sim)` call site exists, so a bind is actually attempted
- [ ] `The_four_derived_ops_decide_Full_versus_Partial` — the cell reads what the fold honours. A fold
      on `OverlayAdd` honours `Flat`/`Increased` only, so `Partial` is the honest first landing
- [ ] `IlvlTierLadderTests.cs:87` moves with the cell, alongside `AtomKindRegistryTests`
**Verification:** the harness scores a `stat.derived` node end to end; `guard-power` green.
**Depends on:** B1. **Scope:** M.

---

### 8 — A1c: `R-G1`, `R-G2` and the reproducibility contract

**Spec:** `spec-tree-plan.md` §7, §7.1, §Reproducibility, §Testing.
**Description:** The generation gate that keeps the corpus schedule honest without relying on
discipline, plus the `--check`/`--diff`/`planHash` contract.
**Acceptance:**
- [ ] Every tree emits `gateQuantity`, `gateIndexKind` and `gateState` (`carrier`|`pending`) from a
      checked-in evidence row
- [ ] `R-G1`: stage 2 exits 3 naming the tree and the missing quantity when asked to generate for a
      `pending` tree. `--emit` on a `pending` tree stays free
- [ ] `R-G2`: `trees[]` ordered by `generationWave` then roster ordinal; the wave is **derived** from
      `gateState`, never hand-assigned
- [ ] `planHash` over the canonical manifest minus `_provenance`, plus the sorted per-tree hashes;
      `emittedUtc` excluded
- [ ] Canonical JSON: sorted keys, 2-space indent, `\n`, UTF-8 no BOM — byte-identical on a
      Windows/Linux round trip
- [ ] `--diff` reports budget deltas, archetype reassignments, quota-cell moves and — the one that
      matters under D24 — ids added, removed or re-minted
**Verification:** flip one hashed input byte; `--check` exits 1 naming the first differing path.
**Depends on:** A1, A0b. **Scope:** M.

---

### 9 — C5: the gate-counter surface and its injector wiring

**Spec:** `spec-gate-counters.md` §10, §15 criterion 9.
**Description:** The counters are invisible without a read path, and `tree-surface` needs one.
**Acceptance:**
- [ ] `POST /api/gate-counters/credit` takes the batched flush; `GET /api/gate-counters/{playerId}`
      returns counts, index **and** equivalents
- [ ] Both counters subscribed in the injector where the status runtime is already wired
- [ ] The lawn's per-hit cost is unchanged within probe noise — a credit is an in-memory increment
- [ ] The tier-0 reason is distinguishable on the wire: *no aptitude allocated yet* versus *this
      quantity has no producer*
**Verification:** a `probe-perf.ps1` window before/after shows no per-hit regression.
**Depends on:** C3. **Scope:** M. **Files:** `src/FusionRpg.Server/GateCounterEndpoints.cs`,
`src/FusionRpg.Injector/Effects/EffectRuntime.cs`.

---

### 10 — X1: the SSOT and registry rows this wave owes

**Spec:** `spec-tree-plan.md` §9 items 3–4; `spec-gate-counters.md` §6; `spec-mechanism-wiring.md`
§3, §10.
**Description:** Four documents owe a row, and **no guard can detect any of them missing** —
`guard-power.ps1` keys on a parameter named `level`/`lvl`/`index`, and these are `t` and `count`.
Each spec names its row as a shipping condition.
**Acceptance:**
- [ ] `ssot-power-scale.md` §10 carries the `req(t)` cost-ladder row (row 6's `XpToNext` precedent)
- [ ] `ssot-power-scale.md` §11.10 carries the authored-depth content-breadth row
- [ ] `ssot-power-scale.md` §10.2 carries rows **29** and **30** for the mastery ladder and the
      count→equivalents read, and the row-count line at `:587` moves with them (today: 27, highest
      ordinal 28)
- [ ] `actor-hub-ssot.md` §6 carries `status.timed | 400 | session bag | timed derived from live statuses`
- [ ] `atom-catalog-ssot.md`'s `stat.derived` runtime row reflects the new Sim cell
- [ ] `DESIGN-GATE.md` §1's atom row still reads 7 / 16 / 13, verified by counting
**Verification:** re-grep each file after the edit (evidence rule 6).
**Depends on:** A1, B3, C3. **Scope:** S.

---

### 11 — B4b: `squad-harness` S2–S4

**Spec:** `spec-squad-harness.md` §11, §6.
**Description:** The three staged measurements after S1. **S2** — `concentration` and `crossunlock`
modes, D25's ownership cost folded in, producing `concentration.fmaxMilli` and D28 evidence.
**S3** — the soul track taught to the model so `concentration.wMilli` becomes measurable, swept over
Θ ∈ {100, 150, 200, 300, 400, 600}, producing `soulTrack.thetaPerSoulLevelMilli`.
**S4** — the `budget` mode: D15's marginal win share per budget point across the duel roster.
**S4 is not optional** — no other module in the program is scoped to produce the evidence
`tree-plan`'s *"no tree is OP"* rests on.
**Acceptance:**
- [ ] Every proposed value is a number and a half-width. The harness **writes no `data/tuning` value**
- [ ] `1000` (no multiplier at all) is inside the `fmaxMilli` sweep, because D5 is provisional
- [ ] The Θ ≈ 300 crossover reports *"cannot separate"* in those words when it cannot, and the
      artifact never presents that as a refutation of a closed-form result
- [ ] S4 reports marginal value per budget point with the same half-widths every other cell carries
**Verification:** `verify` covers every new mode by enumerating the mode table.
**Depends on:** B0, A6. **Scope:** L.

---

### 12 — X2: four owner asks the plan does not currently carry

**Spec:** `spec-mechanism-wiring.md` §12; `spec-squad-harness.md` §14.
**Description:** Add to the plan's non-blocking-asks table, each with the spec's own recommendation as
the default so nothing blocks:

| Ask | Default if unanswered | Resolver |
|---|---|---|
| Does the L2b resist path read status-granted resist channels after G1? | Contribute everything including `status.resist.*`, with a dedicated feedback-path test | Owner — it changes shipped resist math |
| Does `mechanism-wiring` take `aura-skill` T13's live-toggle scope? | Take the per-round recompose only; leave the toggle to T13 | `aura-skill`'s ack |
| Is the transfer verdict scored against mirror squads or authored waves? | Mirror squads decide; waves reported beside | Owner |
| Does D15's rule change once S4's budget evidence lands? | Keep the equal-budget rule | Owner, after S4 |

Also record **A10b's unowned prerequisite**: a Battle status → `BattleDerivedModifierLedger`
producer. `BattleStatusSpec` carries no `StatMods` and `BattleDerivedModifierLedger.Add` has one
caller. G1 and G2 are necessary and not sufficient, and no module's modified-files table contains it.
**Scope:** XS.

---

## 6. Fully covered, verified

These need no change. Named so the list of gaps is not mistaken for a list of everything.

- **A1** covers `spec-tree-plan.md`'s ladder, its per-mille budget column, `R-G0`'s gate currency,
  and all three halves of the node-id scheme — mint once, read back, refuse to re-mint. The id rule
  is the one the whole `O(diff)` re-review cost model rests on, and the task states it correctly.
- **A4** reads `budgetShareMilli` and explicitly not `tierWeight`/`weightTotal`, which is R4 and the
  3.25× defect §3 documents. It also carries the conversion-node refusal with the 17th-kind reason,
  matching `spec-mechanism-wiring.md` §9 exactly.
- **A6** reads **aptitude points** for `req(t)`, not skill points — the single most-confused line in
  the program, and the todo has it right.
- **B1** and **B2** are accurate to G1's and G2's Core halves, including B1's falsifier arm and B2's
  refusal to assert golden-neutrality without running the suite (evidence rule 4).
- **B4 + Checkpoint B** carry A10 with a direction, an effect size, a half-width and three verdicts,
  and Checkpoint B correctly makes **UNRESOLVED hold the gate exactly as FAIL does**. That is the one
  thing `spec-mechanism-wiring.md` §11.1 says not to soften, and the plan did not soften it.
- **C1, C2, C3, C4** cover `spec-gate-counters.md`'s counting rules, the flush window, the index
  transform, the tier-10 parity bar, the exclusive registry, the `GrandTotal()` invariant as an
  executable test, and D43's cold-start seed. This is the best-covered spec of the four.
- **The plan's non-blocking asks and its gates table are right.** Checking A10 before the corpus
  spend, treating the first shipped catalog as the one irreversible point, and defaulting the 17th
  atom kind to *the binder refuses conversion nodes* all match the specs.
- **G4 and the 17th atom kind are scoped out correctly.** No task widens `stat.derived`'s trigger set
  and no task adds a kind. Nothing was silently added or silently dropped at the gap level.

---

## 7. What the plan claims that its spec does not support

Four, all small but all real.

1. **Checkpoint B: *"`treeShareMilli` and `budget.treeTotalPoints` re-derived from real data and
   republished (D42)"*.** Nothing in phase B produces that data. `treeShareMilli` is `tree-binder`'s
   dial; `budget.treeTotalPoints` is `tree-plan`'s, and its own open question 1 says it *cannot be
   measured until trees actually carry power in the resolver*. The evidence that would move either is
   `squad-harness` **S4**, which no task schedules. The plan's risk table repeats the claim —
   *"phase B produces the data to correct it."* B4 measures the Erosion differential and nothing else.
2. **B4's `Files:` line names `tools/HybridViability/` and `tools/CombatSim/`.**
   `spec-squad-harness.md` §Project structure specifies `tools/SquadHarness/` plus its own test
   project, and rejects the single-top-level-`Program.cs` shape of those two tools **by name**,
   because determinism is the module's hard requirement and an untestable tool cannot carry
   `DeterminismTests`.
3. **A1's `Files:` line names `tools/PassiveTreeGen/ (new)`.** `spec-tree-plan.md` §Project structure
   opens with *"The planner is a **seedsmith adapter, not a new tool**"*, gives eight file paths under
   `tools/seedsmith/seedsmith/adapters/trees/plan/`, and its §Commands are all
   `python -m seedsmith trees plan …`. The reason matters: a new tool grows a second copy of
   `largest_remainder_count`, which is the carefully-written integer algorithm §8 depends on.
4. **B4 `Depends on: B3 for the mechanism arm`.** `spec-mechanism-wiring.md` §11.1 says A10a needs
   **neither G1 nor G3**, and that *"G3 is off A10's critical path entirely, because `squad-harness`
   resolves over `BattleEngine`"* — G3 is the Sim path. `spec-squad-harness.md` §10.1 agrees: the
   static Erosion runs *"with no new wiring"*. A10a's four arms **are** the corner/spread arms; there
   is no separate mechanism arm. As written, the dependency puts G3 in front of the checkpoint that
   opens `tree-language --write`, which is a cost the specs say the program does not have to pay.

---

## 8. Design-gate checklist

```
[x] Subsystems identified: passive tree (four wave-0 modules), stats/ActorHub, status, Battle,
    the atom layer, tunables, the power ladder.
[x] Read this session, in full: docs/DESIGN-GATE.md, tasks/passive-tree-plan.md,
    tasks/passive-tree-todo.md, spec-tree-plan.md, spec-gate-counters.md,
    spec-mechanism-wiring.md, spec-squad-harness.md, and passive-tree-map.md (modules, build
    order, the two sequencing rules).
[x] Every factual claim cites a spec section or a file path.
[x] Verified against the repo, not against prose: data/tuning/ has no passive-tree file;
    data/seed/statuses/ does not exist; data/seed/atoms/ holds only fx-*.json, generated/ and
    trait-critical-hunter.json; tools/ has no SquadHarness and no PassiveTreeGen but does have
    seedsmith, HybridViability, CombatSim and ElementEnumGen; ssot-power-scale.md's highest
    ordinal is 28 and its row-count line at :587 reads 27.
[x] Read the section, not the line - notably spec-mechanism-wiring.md §11.1's A10a/A10b split,
    which is what makes finding 7.4 a real cost rather than a style note.
[x] No manufactured uncertainty. Every requirement the plan covers is marked COVERED and dropped.
[ ] I ran no build, no test and no guard. This is a read-only coverage audit and no build is
    authorized; nothing here reports a test result. The three "does not exist" claims were
    checked by listing the directories, and are stated as such.
[x] Nothing here contradicts a §2 invariant. The report proposes no design - it names tasks the
    specs already specify and that the todo does not yet carry.
[x] This file is the only artifact. No spec, plan, todo, src/, tools/, tests/ or data/ file was
    touched, and no git write command was run.
```
