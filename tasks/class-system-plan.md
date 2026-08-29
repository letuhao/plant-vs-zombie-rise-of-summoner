# Implementation Plan: Class system (primary stats)

**Program:** `class-system` · **Map:** [docs/architecture/class-system-map.md](../docs/architecture/class-system-map.md)
**Specs:** `docs/architecture/class-system/spec-<module-id>.md` (12) · **Status: AUTHORIZED 2026-08-26 -- owner's /goal directive commands execution of this plan to completion; supersedes the earlier "not authorized to build" header, which was never flipped after that directive landed.**

**Task list:** [tasks/class-system-todo.md](class-system-todo.md). The bare `tasks/plan.md` / `todo.md`
pair is the perf stream's and is untouched ([AGENTS.md](../AGENTS.md)).

### 0.1 The tuning ladder — three stages, and only the last is real

**The owner's framing from the start of this program, and the plan has to be honest about which stage
it reaches:**

> *"In a real fight there are many things that cannot be controlled by math — random functions,
> combination, timing. We need the simulator and statistical learning to resolve it by fine-tuning the
> tuning config. **The simulator is just a POC. A real system needs tuning based on real data.**"*

| Stage | What it is | What it can decide | Built in |
|---|---|---|---|
| **1. Closed form** | Arithmetic. No RNG, no trials | Whether the model is **self-consistent**, and whether a coefficient change moves the matrix | Phase 4 |
| **2. Simulator** | RNG, combination, timing — what the math cannot express | Whether the closed form is **wrong**. It falsifies; it does not balance | Phase 8 |
| **3. Real system run** | The whole game, all mechanisms, real play | **Whether the game is balanced.** The only source of real data | **Phase 9 — and it cannot start until the complete build** |

> **Phases 0–8 produce a system that is correct and instrumented. They do not produce a balanced one,
> and this plan must never claim they do.** Stage 2 fits the model to the simulator — a **correctness**
> activity. Stage 3 fits the config to reality — the **balance** activity, and it needs mechanisms this
> program deliberately does not build (actions, passives, skills, items — map §5).

**What this program owes stage 3 is the loop, not the answer.** Emit → collect → aggregate → fit →
publish, built and proven on simulated runs, so that the day real runs exist the pipeline already
works. **A tuning loop first exercised on real data is a tuning loop debugged during the one window
where the data matters.**

---

## 1. Overview

Ship the **twelve primary stats** — the RPG-side aptitudes — from "a word in a design doc" to "a number
an actor holds that both combat engines agree on", then make balance a CI assertion rather than a
periodic exercise.

**The player has no class.** Points go anywhere at one price; classes survive only as Zomboss AI
patterns. So the thing being built is not a class system in the usual sense — it is **an allocation
space and the machinery that turns a point in it into a channel value**.

**54 tasks · 10 phases · 10 checkpoints · no task larger than M (≤5 files).** One external dependency
(`aspect-scope`, owned by the demon program).

---

## 0. ⛔ The founding constraint: this system is built and tuned BY DATA

**Not a risk. Not a preference. The operating principle every other section answers to.**

> **A human cannot help.** There is no GUI, no operator, and nobody who can watch a fight, read a
> console table, eyeball a balance number, or sign off a value. **Every number in this system is
> decided by measurement, and every criterion is a command that exits non-zero on failure.**

Three consequences, and they shape the whole plan:

1. **The build must emit before it can be judged.** A guard cannot assert what the runtime never says
   out loud — so **Phase V comes first** and instruments the system before anything asserts on it.
2. **A judgement that cannot be mechanised is not an acceptance criterion.** It becomes a logged number
   with a threshold, or it is dropped. Two were dropped rather than faked (§6a).
3. **Real balance data does not exist yet, and cannot be faked by a simulator.** §0.1.

---

## 2. Scope — all twelve modules

| # | Module | Phase | Spec |
|---|---|---|---|
| 1 | `primary-stats` | 1 | [spec-primary-stats.md](../docs/architecture/class-system/spec-primary-stats.md) |
| 2 | `unit-class-close` | 1 | [spec-unit-class-close.md](../docs/architecture/class-system/spec-unit-class-close.md) |
| 3 | `distribution-reconcile` | 1 | [spec-distribution-reconcile.md](../docs/architecture/class-system/spec-distribution-reconcile.md) |
| 4 | `poise-resource` | 1 | [spec-poise-resource.md](../docs/architecture/class-system/spec-poise-resource.md) |
| 5 | `aptitude-tuning` | 2 | [spec-aptitude-tuning.md](../docs/architecture/class-system/spec-aptitude-tuning.md) |
| 6 | `aptitude-resolve` | 2–3 | [spec-aptitude-resolve.md](../docs/architecture/class-system/spec-aptitude-resolve.md) |
| 7 | `deterministic-core` | 4 | [spec-deterministic-core.md](../docs/architecture/class-system/spec-deterministic-core.md) |
| 8 | `balance-guard` | 5 | [spec-balance-guard.md](../docs/architecture/class-system/spec-balance-guard.md) |
| 9 | `point-economy` | 6 | [spec-point-economy.md](../docs/architecture/class-system/spec-point-economy.md) |
| 10 | `guard-economy` | 7 | [spec-guard-economy.md](../docs/architecture/class-system/spec-guard-economy.md) |
| 11 | `zomboss-patterns` | 7 | [spec-zomboss-patterns.md](../docs/architecture/class-system/spec-zomboss-patterns.md) |
| 12 | `residual-fit` | 8 | [spec-residual-fit.md](../docs/architecture/class-system/spec-residual-fit.md) |
| — | **`aspect-scope`** | **external** | [demons/spec-aspect-scope.md](../docs/architecture/demons/spec-aspect-scope.md) — **demon program owns it** |

---

## 3. Architecture decisions this plan builds on

Taken 2026-08-26 unless noted. None is reopened by this plan.

| | Decision |
|---|---|
| 1 | **Free build** — no player class; classes are Zomboss patterns (2026-08-25) |
| 2 | **An aptitude is a SOURCE, not a registered channel** — `share` normalises over the actor's own total, so a granted aptitude would dilute the other eleven |
| 3 | **Allocation is the sum of four scopes**, commander smallest → unique largest; `share` taken on the **sum** |
| 4 | **PS-3** — contests read a `Θ`-free share, magnitudes read `P(Θ)`. **One implementation, owned by `aptitude-tuning`** |
| 5 | **Win rate is the metric** — never fight length, damage or kill time; never under a clock |
| 6 | **Two criteria, different standing** — termination **HARD/blocking**; dominance **SOFT/reporting with coverage** |
| 7 | **The seam is `IActorStatSubsystem`**, not `ClassStatPlugin` (wrong pipeline) |
| 8 | **The composers stay separate** — battle takes `ChannelMods`, the way `StarPolicy` already feeds progression stats in |
| 9 | **`poise` is the sixth resource**; `ReciprocalPoints` + `AptitudePoints` join the `UnitClass` ledger |
| 10 | **`aspect-scope` belongs to the demon program**; `point-economy` ships 3-of-4 scopes until it lands |
| 11 | **Cap-parity in `ChannelMods` belongs to `battle-adoption`, external to this program** — same shape as decision 10. `BattleStatComposer`'s `ChannelMods` consumption applies no cap at all, for any producer, and `spec-aptitude-resolve.md §8` forbids this program from changing that composer's logic. Found 2026-08-27 building P2.6's cross-composer proof: every one of the twelve aptitudes, at full share, disagrees between engines on any `SumIncreased`-capped channel (`status.resist.*` etc.) — latent until `point-economy` (Phase 6) gives a player something to fund, real the day it does. Written up in `docs/architecture/combat/spec-battle-adoption.md` (three resolution options, undecided) and `.remember/now.md`. **This program's own gates check what this program's own modules control** — a `ChannelMods` cap producer this program does not own being wired up is not this program's to build or block on, the same way `aspect-scope` not landing does not block `point-economy` shipping three of four scopes.
| 12 | **G3 (the `atk` double-count guard) is a forward-looking safeguard, not a same-day tuning fix.** `spec-aptitude-resolve.md §2a.1`'s "red today" claim was traced 2026-08-27 (P3.2) against the live dispatch (`ConditionalOverlayCombatMath.Finalize`) and does not hold: overlay mode strictly replaces vanilla's computation per hit, never adds to it, and never reads `EntityFinal.Atk` — so `Might`/`Ferocity` funding both `combat.power.omni` and `progression.bonus.atk` does not compound in the shipped overlay pipeline today. The rule stays (G3 stays in `guard-class-system.ps1`, `data/tuning/aptitudes.v1.json` stays as `class-system-ideal.md §4` originally designed it) because it protects against a real future case — `battle-adoption`'s own (unbuilt) mapping table would make `BattleActorSetup.Atk` the exact double-counted input the guard already names. G3 reporting red on the shipped file is the correct, permanent state until `battle-adoption` ships or the design changes — same carve-out shape as decision 11, and it does not block Checkpoint 3.

---

## 4. Dependency graph

```text
C0 gates ─────────────────────────────────────────────────────┐
                                                              ▼
primary-stats ─┐
unit-class-close ──► distribution-reconcile   (adjacent, shared consumer readings)
poise-resource ─┤
               └──► aptitude-tuning  (owns AptitudeReadFunctions)
                         ├─► aptitude-resolve ──┐
                         └─► deterministic-core ┴─► balance-guard
                                                       ├─► point-economy ──┐  ⛔ scope 3 external
                                                       ├─► guard-economy   ├─► residual-fit
                                                       └─► zomboss-patterns┘
```

---

## 5. Slicing strategy — why these phases

**The modules are units of ownership; the phases are units of delivery, and they are not the same
shape.** Building module-by-module would mean a type with no config, then a config with no resolver,
then a resolver with nothing to resolve — three phases before anything is observable.

**So the spine is one vertical:** *one aptitude becomes a channel value both engines agree on*
(Phase 2). Everything before it is the plumbing that vertical needs; everything after widens it.

| Phase | Delivers | Observable when done |
|---|---|---|
| **0** | The two `decisions.md` gates | Building is legal |
| **V** | **The instruments** — `--json` emit, a program guard, a prove script, three baselines, five runtime metrics | Every later phase has something to assert **against** |
| **1** | Vocabulary + honest plumbing | The twelve exist; nothing in the stat path is silently inert |
| **2** | **The first vertical** | `Might` → `combat.power.omni`, resolved identically on both engines |
| **3** | Widen to twelve | Every aptitude feeds every live channel it should |
| **4** | The closed form | A matchup is predicted in microseconds |
| **5** | The guard | Balance is a CI assertion |
| **6** | The economy | A player has points to spend — **first golden move** |
| **7** | Guard economy + patterns | BASTION can win; Zomboss is readable |
| **8** | Residual fit **against the simulator** | The model is falsifiable and the tuning loop works |
| **9** | **Tune on real data** | The config answers to real runs — **gated on the complete build** |

**Phases 1 and 3 are deliberately horizontal-looking** and that is correct: Phase 1 is *reconcile*
work over a shipped stat path (every task is "make this existing thing honest"), and Phase 3 is
*widening* a proven vertical. Neither invents a path.

---

## 6. Phases and tasks

Full task bodies with acceptance criteria and verification are in
[class-system-todo.md](class-system-todo.md). This section is the index and the reasoning.

### Phase 0 — Gates (2 tasks)

### Phase V — Verification infrastructure (5 tasks)

See §6a. **Nothing after this phase is verifiable without it.**

`decisions.md` rows for the class system and for `poise`. **AGENTS.md makes an architecture change
that locks behaviour a hard boundary**, and no class-system row exists. The row text is already drafted
in map §2aa, so this is review-and-land, not authoring.

### Phase 1 — Vocabulary and honest plumbing (14 tasks)

The four front modules. **Every task here is additive or corrective; none changes a value**, so the
phase gate is *zero goldens moved*.

Two findings shape it:
- **11 of 29 `unitClass: null` families have no reader**, so they get documented nulls, not classes.
- **`Θ` is zero on the overlay path** and 47 of 84 aptitude channels are outside the battle
  known-channel set — the resolver cannot work until both are fixed.

### Phase 2 — The first vertical (6 tasks)

The config, the read functions, the subsystem, the `ChannelMods` producer, and the test that proves
both engines agree — **on one aptitude**. Small on purpose: it proves the whole path before twelve
aptitudes make a failure hard to localise.

### Phase 3 — Widen to twelve (4 tasks)

All edges live. Includes the **`atk` double-count** (`Might` *and* `Ferocity` both feed
`combat.power.omni` and `progression.bonus.atk` — red on the shipped config today) and retiring
`RpgProgressionSubsystem`'s level-scaled stub, which allocation supersedes.

### Phase 4 — The closed form (6 tasks)

Ported from `tools/CombatSim`, carrying its four paid-for corrections. **It calls the shipped combat
functions and adds only the expectation** — a closed form that re-implements them is a second combat
SSOT.

### Phase 5 — The guard (3 tasks)

Two halves, wired with **different standing**. The failure mode nothing else catches: both blocking →
never lands; both advisory → the one unfixable defect becomes a warning nobody reads.

### Phase 6 — The economy (4 tasks) — ⚠️ first golden move

Budgets, persistence, respec. **Nothing before this moves a golden** because nobody has an allocation.

### Phase 7 — Guard economy and patterns (5 tasks)

`poise` cost/regen/riposte, and the nine Zomboss patterns. **Re-runs the termination invariant** —
a new recovery source is exactly what could break it.

### Phase 8 — Residual fit against the simulator (7 tasks)

Its **first two steps are fixed, not open**: make elements live, then make `stamina` bind. Both are
repairs to the instrument; fitting before them means fitting against a distortion.

**This phase does not balance the game** (§0.1). It proves the model is falsifiable and **builds the
tuning loop that Phase 9 runs for real**, exercised end to end on simulated runs.

### Phase 9 — Tune on real data (5 tasks) — gated on the complete build

**Cannot start until every mechanism exists** — actions, passives, skills, items (map §5). Until then
**4%** of the aptitude edges point at channels nothing reads (⛔ CORRECTED 2026-08-27 from a stale 28% —
P8.4 shipped a re-runnable census, `scripts/audit-reader-census.py`; live count is 6 families / 18 of
486 edges, mostly because `resource.max`/`resource.regen` gained a reader this session for hp/stamina
specifically, `class-system-todo.md` P8.4's own evidence), so a fit over those specific channels would
still freeze noise — a smaller gap than previously stated, not a closed one.

The loop is the deliverable this program owns: **real run → emitted metrics → aggregate → fit →
publish `v{n+1}`**, reusing the shipped telemetry shape (injector → `POST /api/perf` ring buffer →
`probe-*.ps1` → `_baseline-*.json`) rather than inventing a second one.

---

## 6a. Verification infrastructure — Phase V, and why it is first

**The plan's first draft failed its own standard.** It contained steps like *"triaged before, not
after"*, *"do the nine cycle?"* and *"ready for owner review"* — every one of which needs a human to
look at something. With no GUI and no operator, those are not verification steps; they are hopes.

Phase V builds five instruments, each following a pattern the repo already ships:

| | Instrument | Existing precedent |
|---|---|---|
| **V1** | `CombatSim --json` for `predict` / `trinity` / `marginal` | its own `--csv/--out`; the perf stream's `_baseline-*.json` |
| **V2** | `scripts/guard-class-system.ps1` | the eight shipped `guard-*.ps1`, incl. `guard-stat-pairs.ps1`'s planted-violation proof |
| **V3** | `scripts/prove-aptitude.ps1` → `_prove-aptitude.json` | `scripts/prove-overlay-combat.ps1`'s `-OutJson` shape |
| **V4** | three checked-in baselines (residual, dominance, goldens) | `docs/research/perf/_baseline-*.json` |
| **V5** | runtime metrics: `progression.power` at resolve, `poiseRegen`/`peerDamage` per round, `stamina` cost-vs-regen | `PerfProbe` + `FUSIONRPG_PERF` kill switch |

**V5 is the one that would otherwise be missed.** Three acceptance criteria — *`Θ` is non-zero*,
*`r < 1`*, *`stamina` binds* — are statements about a running fight. Without emitted numbers they can
only be checked by watching one, which is exactly what is unavailable. **A guard cannot assert what the
runtime never says out loud.**

**Two judgements were dropped rather than faked.** *"The dominance severity restated as a measurement,
not a verdict"* became: the emitted `coverage` block must be populated and name the live element axis —
a schema assertion. *"If still red, it is a finding with an owner"* became: the test asserts **the
record exists**, never that the number is good.

---

## 7. Checkpoints

| # | After | Gate |
|---|---|---|
| **0** | Phase 0 | Both `decisions.md` rows landed and reviewed |
| **1** | Phase 1 | Twelve ids closed, collision-guarded · allocation round-trips · every `null` carries a reason · `Θ` non-zero on both paths · no registered contributor silently empty · six resources register · **zero goldens moved** |
| **2** | Phase 2 | **One aptitude resolves identically through `DerivedComposer` and `BattleStatComposer`** · contest read `Θ`-free · magnitude ∝ `P(Θ)` · one `AptitudeReadFunctions` implementation · **zero goldens moved** |
| **3** | Phase 3 | All twelve live · no aptitude reaches `atk` twice (claim re-verified against the shipped overlay pipeline first — found 2026-08-27 to need this, see `class-system-todo.md` P3.2) · stub bonus curve retired or inventoried · resolver matches `tools/CombatSim` · **zero goldens moved**. **Revised 2026-08-27 (decision 11):** "prove-aptitude.ps1 exit 0 across all twelve" means across every channel `battle-adoption`'s shipped `ChannelMods` pipeline can currently carry without a cap — the `SumIncreased`-capped channels (`status.resist.*` etc.) are decision 11's external, tracked-separately gap and do not block this checkpoint or anything phased after it |
| **4** | Phase 4 | Residuals within recorded band (core ≤2.4% max; all axes ≤7.7% max) · `Θ`-invariance **exact** · 144 corners in microseconds · no combat formula re-implemented |
| **5** | Phase 5 | Termination **blocking and green**; dominance **reporting, red, with coverage printed** · milliseconds in CI, no seed, no flake |
| **6** | Phase 6 | Four budgets sum · respec always available and priced · **goldens re-blessed once, triaged before not after** · `guard-dal.ps1` green |
| **7** | Phase 7 | `poise` costs on commit and drains on absorb · `r < 1` · riposte scales · **termination re-run and green with poise live** · nine patterns resolve, none self-cancelling, none exceeding the player budget |
| **8** | Phase 8 | Elements live and dominance re-measured **as a measurement, not a verdict** · `stamina` binds · `_meta.measurable` accurate · every fit has a dated research note |

---

## 8. Sequencing constraints

**⚠️ Phase 6 vs the battle stream's T5.** `point-economy` is the first task that moves a battle golden.
`T5 kernel-adoption` is a **byte-identical freezer**, and `decisions.md`'s *Golden ordering across
streams* says **"freeze first, move last"** — a mover inside a freezer's window destroys its proof.

> **Decided 2026-08-26: land before T5 opens.** The battle map records T5 as specced and **nothing
> built**, so the window is not open. Phases 0–6 proceed now; if T5 starts first, Phase 6 waits for its
> gate to pass.

**Phase 1's `unit-class-close` → `distribution-reconcile` order is not arbitrary** — the second
consumes the first's consumer readings rather than repeating them over the same families.

**`aspect-scope` is not on this critical path.** `point-economy` ships three scopes and lights up the
fourth when the demon program delivers.

**Decision 11's cap-parity gap is not on this critical path either — added 2026-08-27.** The module
graph (§4) never made `deterministic-core` (Phase 4) depend on `aptitude-resolve`'s full cross-composer
proof — only on `aptitude-tuning`'s config being stable, which it is (shipped, Phase 2). The
phase-checkpoint gate (§7) had been read as stricter than that graph actually requires: "Checkpoint 3
before anything in Phase 4" does not mean "every clause of Checkpoint 3's gate, including one this
program does not own the fix for." So Phase 4 onward proceed once Phase 3's *own* work
(P3.1's registration half, P3.3, P3.4, and P3.2 once its claim is re-verified) is done, without waiting
on `battle-adoption` to resolve decision 11. Checkpoint 3 itself stays open on that one clause until
`battle-adoption` closes it — tracked, not faked, not silently dropped, and not a reason to stop
advancing the rest of the plan.

---

## 9. Risks

| Risk | Impact | Mitigation |
|---|---|---|
| **A seam claim made from a declaration rather than from what flows through it** | High — **this recurred three times in the spec pass alone** (`ClassStatPlugin` ×2, `BattleStatComposer`) | Every seam task carries "read the consumer at `file:line`" in its acceptance. Phase 1's seam-coverage guard makes inert-vs-wired mechanical |
| Phase 1 discovers more reader-less families than 11 | Medium — shrinks what `unit-class-close` can deliver | Already measured; the census is a script, re-runnable |
| Phase 4's residuals exceed band after porting | Medium — the closed form is `balance-guard`'s only input | The four corrections have a test each; residuals are pinned as the spec |
| Phase 6's golden re-bless overlaps T5 | High — destroys another program's proof | §8. Land before T5 opens, or wait for its gate |
| `progression.bonus.*` stub fires before Phase 3 retires it | Low — latent (no host passes the level delegate) | P3.3 retires it; P1.13 inventories it meanwhile |
| Coefficients ship unbalanced after Phase 8 | **Expected, not a risk** | §0.1 — stage 2 cannot balance. The soft guard is red by design and reports rather than blocks; balance is Phase 9's, on real data |
| **The tuning loop is first exercised on real data** | **High** | Phase 9's pipeline is built and proven on simulated runs in Phase 8, so stage 3 debugs coefficients rather than plumbing |

---

## 10. What this plan does not do

- **Does not tune.** T7 forbids landing a refactor and a rebalance together. Phase 8 is the only tuning
  phase, and it publishes `v{n+1}` rather than editing in place.
- **Does not close the reservations.** **4%** of aptitude edges point at reader-less families (⛔
  CORRECTED 2026-08-27 from a stale 28% — see Phase 9 section above); that is the action/passive/skill
  layer's to fill, named in map §5.
- **Does not build `aspect-scope`.** External.
- **Does not promote the soft criterion to hard.** A product decision, one line when it arrives.
- **Does not require an operator.** No step opens a game, watches a fight, or reads a console table.
- **Does not balance the game.** Phases 0–8 make it correct and instrumented; §0.1's stage 3 balances
  it, and that needs mechanisms this program does not build.

---

## 11. Open

**None blocking.** Five `[~]` boxes remain across the twelve specs: three are "nothing is built yet"
(unavoidable before Phase 1), two are invariant clarifications in `aptitude-tuning`. The one genuinely
undecided input — the aspect tier's point source — was decided 2026-08-26 as `element_mastery`.
