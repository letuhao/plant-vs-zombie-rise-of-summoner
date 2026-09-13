# Plan: `species-gear-chain` — implementation plan

**Spec:** 19 module specs under `docs/architecture/species-gear-chain/`, indexed by
[species-gear-chain-map.md](../docs/architecture/species-gear-chain-map.md) (audited, corrected,
owner-reviewed 2026-09-13). **Tasks:** [species-gear-chain-todo.md](species-gear-chain-todo.md) —
this pair, never the bare `tasks/plan.md`/`tasks/todo.md` (the perf stream's).

**Revision 2 (2026-09-13):** a three-agent `/plan`-audit pass found and fixed 6 structural defects
in revision 1. See § Round-2 corrections. 34 tasks now, up from 29 — the increase is one genuine
scope addition the owner approved (a durability slice pulled forward from `deployment-hierarchy`
module 7), not padding.

**Revision 3 (2026-09-13):** a full sweep of every open question across all 19 specs, presented to
the owner for sealing. Four real decisions made (one — creature drops carrying equipment, not just
materials — reverses this plan's own recommendation); the rest of the open-questions catalogue is
tracked in § Open questions (owner-sealable, none blocking). One new task (**T18b**) and one new
success criterion (**3a** on `creature-drop-tables`) resulted; see § Round-3 decisions.

---

## Overview

Four idea-phase docs were audited into one capability map and 19 module specs, then a five-agent
audit fixed ~35 defects across them, then this plan turned the 19 specs into an ordered, checkpointed
task list, then a second three-agent audit fixed 6 more defects **in the plan itself** — the
map→task translation did not fully inherit the rigor the module-level audit already established.

The specs are unusually complete: each already carries its own Design, Tunables, Numeric types,
ActorHub gate, Testing strategy, Boundaries and Success criteria. This plan's job is **not** to
re-derive that content — it is to (a) fix the build **order**, (b) slice each module into S/M-sized
vertical tasks, (c) separate what is buildable now from what is blocked on other programs' unbuilt
code, and (d) checkpoint often enough that a reviewer never has to trust more than a few tasks' worth
of unverified work at a time.

## Round-2 corrections (2026-09-13, three-agent `/plan` audit)

| # | Defect in revision 1 | Fix |
|---|---|---|
| 1 | ⛔ **`craft-risk-ladder`'s own spec states Stage 1 may not ship alone** without (a) stage 2's decay, (b) a restore verb, or (c) an explicit owner verdict accepting a temporary hard stop (`AGENTS.md`'s no-hard-progression-ceiling rule) — the plan shipped none of the three | **Owner chose (a)/pull-forward, 2026-09-13.** A minimal slice of `deployment-hierarchy` module 7 (durability storage + derivation + workbench repair, **not** field touch-up or death-drop decay) is now built inside this plan. This fully un-defers `craft-risk-ladder` Stages 2–4 — see the new Phase 2 tasks and § Cross-program pull-forward below |
| 2 | ⛔ **`craft-executor-completion`'s own spec header says "Depends on: nothing... runs beside `rarity-promotion`, not after it"** — the plan sequenced it behind `rarity-promotion` anyway, needlessly stalling 14 stranded recipes | Moved to Phase 1 (T14–T15); dependency corrected to none. The map's own build order had the same error and is fixed too |
| 3 | ⛔ **The "contested sixth `MaterialClass`" claim was a misreading.** `deployment-hierarchy-map.md:89` says *"shard-leg material class **at high rungs**"* — reusing the existing `Shard` class, not proposing a new one. There is no collision | Struck from the map, the plan's Risks table, and `species-materials`' task |
| 4 | **`gem-tier`'s single task covered only 3 of 10 spec success criteria** — the upcycle verb, `recipegen` content, and the mirror-reconciliation test were silently dropped, not deferred | Split into two tasks (T8, T9); the second covers the missing half |
| 5 | **`wave-species-roll` and `wild-species-spawn` both claimed to share a `CreatureAdmission` policy type, but only one spec's code sample actually calls it** — a live inconsistency between two governing specs, not just a task-sequencing risk | New tiny task (T5) creates the shared type first; T6/T7 both depend on it and are corrected to call it, overriding the one spec's stale inline sample |
| 6 | **`set-species-binding`'s tasks omitted the C# runtime side** (`FusionRpg.Data` importer, `ItemSeedValidator` closure check) the spec's own Objective ("make the field **queryable**") requires | Added to T28 |

Also fixed, lower severity: T2/T3 now explicitly sequenced (both touch
`anchor/schema.py`); Phase 1 and Phase 3 gained interior checkpoints (skill guidance: every 2–3
tasks, not once per phase); several tasks' Acceptance Criteria restored qualifiers the coverage audit
found dropped (e.g. "proven across a shuffled catalog order," "no shipped `rarityGrant` row edited").

## Round-3 decisions (2026-09-13, full open-questions sweep)

The owner reviewed every open question across all 19 specs in one sitting. Four produced a real
change; the rest are recorded in § Open questions below, sealable at any time, none blocking.

| # | Decision | Consequence |
|---|---|---|
| 1 | `gem-tier`'s upcycle stays **same-family only** (not cross-family) | Spec's own recommendation confirmed; no task change |
| 2 | `wild-species-spawn`'s members take their **species' own `P(Θ)`**, not the flat `UnmadeMemberHp` | New task **T18b** (Phase 2) — kept out of T7 (Phase 1) to avoid pulling a Phase-1 task behind a Phase-2 dependency; T7 ships with the flat value as a named interim |
| 3 | ⭐ **`creature-drop-tables` carries `Equipment`-kind drops in v1, not materials-only** — **reverses this plan's own recommendation** | New success criterion 3a; `DropEntryKind.Equipment` is already a built mint arm (`LootMintAt.cs:77-87`), so this is not a new equipment-roll design — only a `thetaContent` input, shared with E3a's species-rung derivation for the shard |
| 4 | The species-cost multiplier **applies to `elevate`**, not just enhance/temper | No new task — `elevate` must resolve through the same shared cost-resolution function every verb uses (T26 confirms this); T32 (Phase 4) wires the multiplier into that function once, and `elevate` inherits it automatically |

## Cross-program pull-forward — read before Phase 1/2's durability tasks

Building `craft-risk-ladder` Stages 2–4 requires *some* form of durability, which is
`deployment-hierarchy` module 7's territory, not this initiative's. The owner approved building a
**minimal slice** of it here rather than deferring the whole risk ladder or inventing a parallel
mechanism:

- **Built here:** storage (`durability_max`/`durability_current` columns), derivation
  (`DurabilityTable.Build` over `class`/`rarity`/`tags`), the at-zero enforcement filter, and
  **workbench-only** repair (`RepairPolicy.Resolve`, the `Repair` `op_kind`/`CraftOperation` members,
  the destruction-on-repair-attempt chance) — all built **exactly as
  `deployment-hierarchy/spec-item-durability-repair.md` §1/§2/§5/§6 already specify**, not a parallel
  invention.
- **Not built here, still deployment-hierarchy's own future work:** field touch-up (needs
  `party-dungeon`'s `PackGrid`, itself unbuilt), death-drop extra decay (needs `corpse-cache`,
  unbuilt), commander-pouch parity (D6).
- **This is filed back into `deployment-hierarchy-map.md`** so that program's own eventual plan does
  not duplicate the slice — see the todo's task notes.
- **Consequence:** `craft-risk-ladder` Stages 2–4 are **no longer deferred**. The only module still
  in § Deferred is `item-upgrade-tree`, blocked on `item` module 23, which this pull-forward does not
  touch.

## Architecture decisions

- **`rarity-promotion` sits after `craft-risk-ladder` Stage 1 and after the durability pull-forward's
  `Repair` op_kind lands** (not merely after Stage 1, as revision 1 said) — it needs the next enum
  ordinal after `Repair` claims the eleventh slot, and both land inside this same plan now, so the
  ordering is enforced by task dependency, not by hoping two separate programs coordinate.
- **`craft-executor-completion` runs in Phase 1**, per its own spec's correction — it needs zero enum
  members and nothing else in this plan blocks it.
- **`item-upgrade-tree` remains the sole deferred module** — `item` module 23
  `requirement-profiles` has zero implementation (`grep -rn "RequirementProfile" src/` → 0 hits) and
  nothing in the active 34 tasks depends on it.
- **No enum-slot pre-work gate**, beyond the ordering above. `MutationOpKind`/`CraftOperation` slot
  additions are reversible (git-level ordering), not an irreversible collision.
- **`themes.v2.json` + migration is a task, not a gate** — the owner already decided this.

## Dependency graph (phases; sub-groups mark interior checkpoints)

```
Phase 1 — foundations, 15 tasks, 3 sub-checkpoints
  1a: tier-propagation-contract(a,b) · threat-band-fill · socket-allowance-by-kind
  1b: CreatureAdmission · wave-species-roll · wild-species-spawn · gem-tier(a,b)
  1c: craft-risk-ladder(Stage1) · durability-slice(a) · enhance-track-wiring(a,b)
      · craft-executor-completion(a,b)

Phase 2 — 11 tasks
  ladder-consistency-repair(a,b) ← tier-propagation-contract
  species-magnitude-synth ← threat-band-fill
  delve-species-wiring(a,b) ← threat-band-fill, CreatureAdmission
  socket-combat-wiring(a,b) ← gem-tier(a)
  durability-slice(b) ← durability-slice(a)
  craft-risk-ladder(Stage2-3) ← craft-risk-ladder(Stage1), durability-slice(a)
  rarity-promotion(a,b) ← craft-risk-ladder(Stage1), durability-slice(b)

Phase 3 — 5 tasks
  set-species-binding(a,b) ← ladder-consistency-repair
  creature-drop-tables(a,b,c) ← species-magnitude-synth + one selection module

Phase 4 — 1 task
  species-cost-shaping ← set-species-binding

Phase 5 — 2 tasks
  species-materials(a,b) ← species-cost-shaping, creature-drop-tables

Deferred — the only remaining one
  item-upgrade-tree ← rarity-promotion, craft-risk-ladder, item module 23 (UNBUILT)
```

18 modules (17 fully + `craft-risk-ladder` complete via the pull-forward) active across 5 phases, 34
tasks. `item-upgrade-tree` is the sole deferral.

## Task list

Full per-task detail is in [species-gear-chain-todo.md](species-gear-chain-todo.md). Index:

### Phase 1 — foundations
- **1a:** T1–T2 `tier-propagation-contract` · T3 `threat-band-fill` · T4 `socket-allowance-by-kind`
- *(Checkpoint 1a)*
- **1b:** T5 `CreatureAdmission` (new) · T6 `wave-species-roll` · T7 `wild-species-spawn` ·
  T8–T9 `gem-tier`
- *(Checkpoint 1b)*
- **1c:** T10 `craft-risk-ladder` Stage 1 · T11 `durability-slice` a (pulled forward) ·
  T12–T13 `enhance-track-wiring` · T14–T15 `craft-executor-completion`
- *(Checkpoint — Phase 1)*

### Phase 2
T16–T17 `ladder-consistency-repair` · T18 `species-magnitude-synth` · **T18b** `wild-species-spawn`
HP wiring (new, owner decision) · T19–T20 `delve-species-wiring` · T21–T22 `socket-combat-wiring` ·
T23 `durability-slice` b · T24 `craft-risk-ladder` Stage 2–3 · T25–T26 `rarity-promotion`
- *(Checkpoint — Phase 2)*

### Phase 3
T27–T28 `set-species-binding` · T29–T31 `creature-drop-tables`
- *(Checkpoint — Phase 3)*

### Phase 4
T32 `species-cost-shaping`
- *(Checkpoint — Phase 4)*

### Phase 5
T33–T34 `species-materials`
- *(Checkpoint — Phase 5 / Complete)*

## Deferred

| Item | Why | Trigger |
|---|---|---|
| `item-upgrade-tree` (E5, armour successor edge) | `item` module 23 `requirement-profiles` has zero implementation; also needs `rarity-promotion` and an 11th `CraftOperation` member — now taken by `Repair` (T23) | Module 23 ships |

## Risks and coordination (not gates)

| Risk | Impact | Mitigation |
|---|---|---|
| T2 and T3 both edit `tools/seedsmith/seedsmith/adapters/creatures/anchor/schema.py` | Low — different fields, but same file | Land T2 first within sub-checkpoint 1a; rebase T3 if both are in flight |
| Durability-slice columns (T11) and `craft_potential` columns (T10) both `ALTER TABLE effect_instance` | Low — idempotent additive migrations | Both in sub-checkpoint 1c; review together at that checkpoint |
| Building a slice of `deployment-hierarchy` module 7 inside this initiative | Medium — a second program's territory | Filed back into `deployment-hierarchy-map.md`; scope is explicitly bounded (no field touch-up, no death-drop decay) |
| `species-rank` (unlisted 19th `creature-seed` module) overlaps T3/T6 | Low — a display axis, not a build blocker | Filed in `creature-seed-map.md`'s asks table already |
| `creature-yield.v1.json` created independently by T30 and T34 | Low — sequenced (T30 before T34) | T34 confirms the file exists with T30's shape before adding to it |
| Golden fixture drift from T6 (wave roll) or T3 (threatBand rewrite) | Medium if it happens | Each spec states which goldens should be unaffected; a moved golden is investigated, never auto-re-blessed |

## Open questions (owner, each answerable — not blocking)

1. Does `RaiseResolver.SpeciesFor`'s own fix (a second ~6-species selection site,
   `spec-wild-species-spawn.md` Open question 3) ride with T7 or ship as its own follow-up?
   Recommendation: its own follow-up.
2. `species-materials`' per-species material count (1–2, or 0 for general creatures) — resolved
   during T34.
3. `species-cost-shaping`'s threshold rung value — resolved during T32.
4. Durability-slice's repair cost curve (`repairRatioMilli`, starting at the siege 600‰ precedent or
   its own value) — resolved during T23, a balance question, not a blocking one.
