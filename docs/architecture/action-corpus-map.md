# Capability map: `action-corpus`

**Status:** proposed 2026-09-03 from [action-corpus-ideal.md](action-corpus-ideal.md), whose idea phase
closed the same day (§43). **Not approved. No module spec may be written until it is** —
`seedsmith-design`'s own rule: *"capability map — approved before any module spec."*

**Program prefix:** `action-corpus`. Module specs → `docs/architecture/action-corpus/spec-<module-id>.md`;
plan → `tasks/action-corpus-plan.md` + `tasks/action-corpus-todo.md`.

---

## 1. What this program is, in one paragraph

**It generates the action seeds a creature can hold**, in three eligibility tiers — general (any
creature), family (one of 19 demon families), signature (one species) — as **seeds**, never concrete
objects. Identity is authored by a model; every magnitude, weight and duration is decided by
deterministic code and by tables the model never sees. The runtime rolls a seed into a concrete action
per player (`Instantiator`), which is Law 1 and is already built.

**It does not build the action runtime.** `ActionRow`, `ActionCompiler`, `ActionValidator`,
`ActionSetAssembler`, `LoadoutSet`, `UnlockLadder` and `ActionSeeder.Generate` all ship. This program
produces **content** for them, plus the model-free stages that make that content reviewable.

## 2. What already exists — read this before claiming a gap

| Thing | State | Evidence |
|---|---|---|
| `ActionSeeder.Generate` → `Instantiator.Draw` | **built** — atoms drawn, target shape rolled, name composed | `ActionSeeder.cs:35-73` |
| `SpeciesBasics.InnateActionId` — per **species**, nullable, validated, assembled, persisted | **built**, all four | `ActionRow.cs:87` · `ActionValidator.cs:107-115` · `ActionSetAssembler.cs:60-61` · `RpgStore.Actions.cs:549` |
| Closed vocabularies — 3 kinds · 5 categories · 8 tags · 6 target modes · 4 area shapes · 4 relations | **built** | `ActionEnums.cs`, `ActionTargetSpec.cs`, `RelationKind.cs` |
| Rung table — 10 rows, `qPower`/`qCost`/`qCd`, `structureBudget` | **built** | `data/tuning/action-rungs.v1.json` |
| `Instantiator.TryInstantiate` | **built, inert** — zero production callers | — |
| `type-weights.json` / `TypeWeights.cs` | ⛔ **do not exist** — named in `spec-action-seeding.md:173,176` | — |
| `data/seed/actions/` | 2 files, **neither loadable** by `Corpus.load` (no `kind` envelope) | — |

## 3. ⛔ Four constraints from the idea phase that shape every module below

Restated inline, because a downstream session reads this map and not its links.

1. **Seeds, not a cartesian.** An atom names a **pool**; element, tier and cell resolve at **layer 4**,
   per player, at roll time (`effect-atom-ideal.md` §W7.9). **A generated action's atoms are pool
   references.** A cell is a target, never an identity.
2. **Small-batch proof before any full run.** *"prove LLM pipeline work very well before big batch run…
   i will decide when we fully run."* Every model stage ships `--dry-run` and a small `--count`; **§17's
   call budget is a ceiling, not a plan.**
3. **The roster is 84, not 904.** Motif anchors exist for 84 species, family assignments for **53**.
   904 is the almanac row count. Per-species count is a **tunable**, so re-running later is config.
4. **C1's family-access widening is gated** (`ideal` §21.3) on three things that do not exist: a per-rung
   `powerBudget` row, a family-aware non-additive price (needs D2), and a budget check with a production
   caller. **Until all three hold, the generator emits structure-gated tiers only.**

## 4. Modules

**Nine stages, six model-free.** Ids are stable kebab-case and are referenced by every downstream spec,
task and test.

| # | Module | Owns | Model? | Depends on |
|---|---|---|---|---|
| **A-S0** | `characteristic-pool` | The closed characteristic pool, and the **species role lean** — family-level floor, deterministic derivation to differentiate, model only for the residue (A2 hybrid). Reads the demon seed; **never invents an anchor** | No | — |
| **A-S1** | `distribution-planner` | **Engine 1.** Category, pairing role, quotas, rung windows, and per-tier atom-family access sets. Owns every count the model is not allowed to choose | No | A-S0 |
| **A-P1** | `general-propose` | *"A good role-based action any creature could hold."* Role + mechanical slot only — **no anchor at all** | **Yes** | A-S1 |
| **A-P2** | `family-propose` | *"What expresses THIS family."* Family motifs, anti-motifs, themes | **Yes** | A-S1 |
| **A-P3** | `signature-propose` | *"What makes THIS ONE creature unlike its siblings."* Species motifs + element + **its family's output**, which is why it cannot be P2 with a flag | **Yes** | A-S1, **A-P2** |
| **A-S4** | `validate-heal` | Schema audit, quality gates t1–t3, bounded self-heal — **two repairs then `unresolved`**, never a silent third | mixed | A-P1, A-P2, A-P3 |
| **A-S3** | `dedup-select` | t1/t2 hash sets (**hard**); t3 LlamaIndex (**advisory only**). Pure, fixed order, index built from the round's own candidates and discarded | No | A-S4 |
| **A-S5** | `coverage-report` | Thin cells → next round's targets. **Declares closed-loop or open-loop per metric**; an open-loop metric never contributes to a pass | No | A-S3 |
| **A-S6** | `innate-picker` | Promotes one action per species to `ActionKind.Innate`. **Model-free permanently** (§34) — the innate is a free sixth slot outside `LoadoutSet.MaxSize = 5`, so choosing it is a magnitude decision Law 2 puts out of the model's reach. Ranking weights live in `data/tuning/` | No | A-S3 |

### 4.1 Modules that come along with action generating

Named here because the ideal treats them as in-scope and a map that omitted them would let them fall
between programs.

| # | Module | Owns | Depends on |
|---|---|---|---|
| **A-M1** | `movement-payload` | The **RPG-layer half** of a movement action — the buff, status or tempo effect. Legal today, works with the game closed. This is what makes a movement action *standalone-first* | — |
| **A-M2** | `lawn-reposition` | The **lawn enrichment half** — `decisions.md` *"Lawn position write"*, **DRAFTED not built**. ONE guarded entry point (*move actor to cell*) through `EntityApply`, single writer, `guard-single-writer.ps1` extended, record-then-drain. **⚠️ `Hud/` needs an explicit exemption** — the ADR's enumeration originally missed `Hud/ActorHudPool.cs:170,225,243` | **E33** (`OnActivate` on the lawn), A-M1 |
| **A-T1** | `type-weights` | The per-species generation anchor `spec-action-seeding.md` names and **which does not exist**. A demon type is a weight vector over the five shipped action categories — *"inventing a third vocabulary is the exact defect the atom program exists to stop"* | A-S0 |
| **A-E1** | `eligibility-axis` | ⭐ **The founding gap, and the highest-damage orphan the coverage audit found.** `scope` (`general`/`family`/`species`) + `scopeKey` on the action row, and the candidate-set query `general ∪ family(mine) ∪ species(mine)`. **`ActionRow` has no such field today** — its only `scope` is `ActionScopeRow`'s *effect* scope, an unrelated concept. Without it the corpus is content nothing can read, and `UnlockLadder` stays reachable only from tests | — |
| **A-G1** | `tier-access-gate` | **Makes decision C1 enableable.** §21.3's gate was named by five specs and owned by none: a per-rung `powerBudgetMilli` (derived from shipped `qPowerMilli` × `poolRolls`, never a new curve), a budget check with a **production caller**, the caps-register entry §5 constraint 2 promised, and `reaction`/`restriction` reporting **`undetectable` rather than `0`**. ⛔ Does **not** enable C1 — assertion 2 stays blocked on effect-atom's D2 | A-S1 |
| **A-R1** | `resource-ownership` | §30 task 0.4, marked ✅ with its deliverable absent. The 18-row ownership table and the generator that emits 216 edges from it, so *"add a resource id and its edges appear"* becomes true rather than promised. **First emission must reproduce `aptitudes.v5.json` byte-for-byte** | — |
| **A-U1** | `rung-semantics` | **Makes the rung ladder mean one thing.** Three findings that are one question: `StructureBudgetGuard` reads the **authored** `Rung` while `effectiveRung` is derived per holder (so the clamp never reaches the guard — **and the guard is correct; the specs' inference is wrong**); `minRung` is an unpriced second curve whose rung-5 floor costs a first-ever unlock **3.6×**; and `cap: 10` is **two meanings in one number**. Splits it into `heldCap`/`rungCap` at equal values, behaviour-neutral by construction | — |
| **A-C1** | `corpus-loader` | Make `data/seed/actions/` loadable — the two files there today are **silently skipped** by `Corpus.load` for want of a `kind` envelope. Model-free, tiny, and it gates every round-trip test | — |

## 5. Dependency graph and build order

```
  A-E1 ────────────────────────────────────► (a corpus becomes holdable AT ALL)
  A-C1 ────────────────────────────────────► (round-trip tests possible)
  A-R1 ────────────────────────────────────► (resource edges generated, not hand-published)
  A-S1 ─► A-G1                              (two of C1's three gates; C1 stays disabled)

  A-S0 ─► A-T1
    │
    └──► A-S1 ─┬─► A-P1 ─┐
               ├─► A-P2 ─┼─► A-S4 ─► A-S3 ─┬─► A-S5 ─► (round n+1 targets)
               │    └──► A-P3 ─┘           └─► A-S6 ─► data/seed/actions/
               └─► (rung windows, family-access sets)

  A-M1 ──────────────────────────────────► A-M2   (needs effect-atom E33)
```

**Build order, model-free first — this is the load-bearing sequencing rule, not a preference:**

```
A-E1 · A-C1 · A-S0 · A-T1 · A-S1 · A-G1 · A-R1 · A-S5 · A-S3 · A-S6   ← ten modules, ZERO tokens
                                    │
                                    └─► A-S4 ─► A-P1 ∥ A-P2 ─► A-P3
```

**By the time the first token is spent**, the pool, the plan, the metrics, the dedup and the innate
picker are all inspectable against real data, and the only unknown left is the judgement itself.
**A-P1 and A-P2 run in parallel; A-P3 waits on A-P2.**

## 6. Checkpoints

- **✅ Checkpoint 0 — an action can be held.** A-E1 lands: `candidates(actor)` returns general ∪ family ∪ species, ordinally sorted, and a null `scopeKey` matches **only** general. **Nothing else in this program matters until this passes.**
- **✅ Checkpoint 1 — the corpus round-trips.** A-C1 lands: a file written to `data/seed/actions/` loads
  back. Today both files there are invisible.
- **✅ Checkpoint 2 — the anchor exists and is measured.** A-S0 + A-T1. **The role lean is reported with
  its separation**, so §34's deferred question (*how sharply does it separate species?*) is answered with
  data rather than assumed.
- **✅ Checkpoint 3 — the plan is reviewable with no model.** A-S1 emits quotas, rung windows and
  family-access sets for the **real 84-species roster**, and A-S5 reports coverage over them.
- **✅ Checkpoint 4 — a smoke batch proves quality.** Small `--count` through A-P1/A-P2/A-P3 → A-S4 →
  A-S3. **Metrics, defects found, defects fixed.** Per §W7.10 the owner decides whether a full run
  happens; this checkpoint is the evidence for that decision and **the program does not schedule past
  it**.
- **✅ Checkpoint 5 — an innate is picked deterministically.** A-S6 over the smoke batch: same input →
  same pick, ties broken on id, a species with no eligible action gets `null`.

## 7. Cross-program dependencies

| Needs | From | State |
|---|---|---|
| `OnActivate` raised on the lawn | **effect-atom E33** | ⛔ **A-M2 is blocked on it** — the only hard cross-program block |
| Channel pools (L2) | **effect-atom E30** | A generated action's atoms reference pools |
| Binding production | **effect-pipeline module 4** `instance-producer` | `effect_binding` has **zero rows**; without it the corpus is authored into a runtime nothing reaches |
| Species anchors (motifs, family, theme) | **seedsmith D2/D5** | 84 motif, **53 family**. Rarity for the rest is unspecced |
| Rung window in the caps register | **A-G1** (was: power) | `ssot-power-scale.md` §11 has **no row** for it; §5 constraint 2 promised one |

## 8. What stays out

- **The action runtime.** Shipped. This program authors content for it.
- **A second roll.** `Instantiator` is the roll. Law 1.
- **Magnitudes chosen by a model.** Law 2, enforced by schema audit, never by review.
- **The 820 unrostered species.** Generate against 84; the per-species count is a tunable, so growth is
  a re-run.
- **`RendezvousLane` / link-strikes.** Built, tested, **zero production callers**, gated behind a
  default-off `BattleModeProfile.RendezvousEnabled`. A wiring question owned elsewhere.

## 9. Success criteria

1. **Every generated action is a seed** — enums and pool references, no magnitudes.
2. **A rerun over unchanged inputs is byte-identical**, proven by hash, with provenance recording the
   model id, prompt version and candidate-set hash.
3. **Tests never call a model.** The transport is stubbed so it *raises*.
4. **`unresolved` is a legal outcome** — 1-1-1 votes never silently take the first option.
5. **Coverage is reported before it is claimed.** A-S5 names thin cells; no round declares success
   against a metric it did not evaluate.
6. **The smoke batch is the gate.** No full run without the owner's decision on its evidence.
