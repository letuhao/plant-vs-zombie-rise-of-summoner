# Plan: `species-gear-chain` — implementation plan

**Spec:** 19 module specs under `docs/architecture/species-gear-chain/`, indexed by
[species-gear-chain-map.md](../docs/architecture/species-gear-chain-map.md) (audited, corrected,
owner-reviewed 2026-09-13). **Tasks:** [species-gear-chain-todo.md](species-gear-chain-todo.md) —
this pair, never the bare `tasks/plan.md`/`tasks/todo.md` (the perf stream's).

**Revision 2 (2026-09-13):** a three-agent `/plan`-audit pass found and fixed 6 structural defects
in revision 1. See § Round-2 corrections. 34 tasks now, up from 29 — the increase is one genuine
scope addition the owner approved (a durability slice pulled forward from `deployment-hierarchy`
module 7), not padding.

**Revision 3 (2026-09-13):** a full sweep of every open question and blocked item across all 19
specs, presented to the owner for sealing. Eight real decisions made, the largest being a second
pull-forward — `item` module 23 `requirement-profiles` — that **fully un-defers `item-upgrade-tree`**.
**The deferred list is now empty.** 38 tasks, up from 34 (T18b, T35–T38). See § Round-3 decisions.

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

## Round-3 decisions (2026-09-13, full open-questions and blocked-items sweep)

The owner reviewed every open question and every blocked item across all 19 specs in one sitting.
Eight produced a real change; the rest are recorded in § Open questions below, sealable at any time,
none blocking.

| # | Decision | Consequence |
|---|---|---|
| 1 | `gem-tier`'s upcycle stays **same-family only** (not cross-family) | Spec's own recommendation confirmed; no task change |
| 2 | `wild-species-spawn`'s members take their **species' own `P(Θ)`**, not the flat `UnmadeMemberHp` | New task **T18b** (Phase 2) — kept out of T7 (Phase 1) to avoid pulling a Phase-1 task behind a Phase-2 dependency; T7 ships with the flat value as a named interim |
| 3 | ⭐ **`creature-drop-tables` carries `Equipment`-kind drops in v1, not materials-only** — **reverses this plan's own recommendation** | New success criterion 3a; `DropEntryKind.Equipment` is already a built mint arm (`LootMintAt.cs:77-87`), so this is not a new equipment-roll design — only a `thetaContent` input, shared with E3a's species-rung derivation for the shard |
| 4 | The species-cost multiplier **applies to `elevate`**, not just enhance/temper | No new task — `elevate` must resolve through the same shared cost-resolution function every verb uses (T26 confirms this); T32 (Phase 4) wires the multiplier into that function once, and `elevate` inherits it automatically |
| 5 | `socket-combat-wiring`'s insert binds at the **host's existing role**, socket index carried in the SourceId | Confirms the spec's own recommendation; no task change |
| 6 | `socket-combat-wiring` ships **arm 1 alone, then arm 2** (combination/resonance grants as a follow-on) | Confirms the spec's own recommendation; T21/T22 stay scoped to arm 1 |
| 7 | `enhance-track-wiring` grants follow the **authored track below +20, a fixed stride above it** | Confirms the spec's own recommendation; no task change |
| 8 | ⭐⭐ **`item` module 23 `requirement-profiles` is pulled forward**, exactly as the durability slice was — it already has a complete, approved spec with zero code. **This fully un-defers `item-upgrade-tree`.** Its non-armour successor spine is a new authored `successorOf` field per base type (weapon/offhand/jewel), never derived from the class ladder | New tasks **T35–T38** (Phase 1: the resolver/evaluator/tuning; Phase 3: the upgrade executor + `successorOf`). **The deferred list is now empty.** Filed into `item-map.md` |

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
Phase 1 — foundations, 17 tasks, 3 sub-checkpoints
  1a: tier-propagation-contract(a,b) · threat-band-fill · socket-allowance-by-kind
  1b: CreatureAdmission · wave-species-roll · wild-species-spawn · gem-tier(a,b)
  1c: craft-risk-ladder(Stage1) · durability-slice(a) · enhance-track-wiring(a,b)
      · craft-executor-completion(a,b) · requirement-profiles-pullforward(a,b)

Phase 2 — 12 tasks
  ladder-consistency-repair(a,b) ← tier-propagation-contract
  species-magnitude-synth ← threat-band-fill
  wild-species-spawn HP wiring ← wild-species-spawn, species-magnitude-synth
  delve-species-wiring(a,b) ← threat-band-fill, CreatureAdmission
  socket-combat-wiring(a,b) ← gem-tier(a)
  durability-slice(b) ← durability-slice(a)
  craft-risk-ladder(Stage2-3) ← craft-risk-ladder(Stage1), durability-slice(a)
  rarity-promotion(a,b) ← craft-risk-ladder(Stage1), durability-slice(b)

Phase 3 — 7 tasks
  set-species-binding(a,b) ← ladder-consistency-repair
  creature-drop-tables(a,b,c) ← species-magnitude-synth + one selection module
  item-upgrade-tree(a,b) ← rarity-promotion, craft-risk-ladder(Stage2-3), requirement-profiles-pullforward

Phase 4 — 1 task
  species-cost-shaping ← set-species-binding

Phase 5 — 2 tasks
  species-materials(a,b) ← species-cost-shaping, creature-drop-tables

Deferred — none
```

**19 modules, all active, across 5 phases, 38 tasks. The deferred list is empty** — both external
blockers (`deployment-hierarchy` module 7, `item` module 23) were resolved by pulling a minimal,
already-designed slice of each forward.

## Task list

Full per-task detail is in [species-gear-chain-todo.md](species-gear-chain-todo.md). Index:

### Phase 1 — foundations
- **1a:** T1–T2 `tier-propagation-contract` · T3 `threat-band-fill` · T4 `socket-allowance-by-kind`
- *(Checkpoint 1a)*
- **1b:** T5 `CreatureAdmission` (new) · T6 `wave-species-roll` · T7 `wild-species-spawn` ·
  T8–T9 `gem-tier`
- *(Checkpoint 1b)*
- **1c:** T10 `craft-risk-ladder` Stage 1 · T11 `durability-slice` a (pulled forward) ·
  T12–T13 `enhance-track-wiring` · T14–T15 `craft-executor-completion` ·
  **T35–T36** `requirement-profiles-pullforward` (new, pulled forward)
- *(Checkpoint — Phase 1)*

### Phase 2
T16–T17 `ladder-consistency-repair` · T18 `species-magnitude-synth` · **T18b** `wild-species-spawn`
HP wiring (new, owner decision) · T19–T20 `delve-species-wiring` · T21–T22 `socket-combat-wiring` ·
T23 `durability-slice` b · T24 `craft-risk-ladder` Stage 2–3 · T25–T26 `rarity-promotion`
- *(Checkpoint — Phase 2)*

### Phase 3
T27–T28 `set-species-binding` · T29–T31 `creature-drop-tables` ·
**T37–T38** `item-upgrade-tree` (new, un-deferred)
- *(Checkpoint — Phase 3)*

### Phase 4
T32 `species-cost-shaping`
- *(Checkpoint — Phase 4)*

### Phase 5
T33–T34 `species-materials`
- *(Checkpoint — Phase 5 / Complete)*

## Deferred

None. See § Round-3 decisions #8.

## Risks and coordination (not gates)

| Risk | Impact | Mitigation |
|---|---|---|
| T2 and T3 both edit `tools/seedsmith/seedsmith/adapters/creatures/anchor/schema.py` | Low — different fields, but same file | Land T2 first within sub-checkpoint 1a; rebase T3 if both are in flight |
| Durability-slice columns (T11) and `craft_potential` columns (T10) both `ALTER TABLE effect_instance` | Low — idempotent additive migrations | Both in sub-checkpoint 1c; review together at that checkpoint |
| Building a slice of `deployment-hierarchy` module 7 inside this initiative | Medium — a second program's territory | Filed back into `deployment-hierarchy-map.md`; scope is explicitly bounded (no field touch-up, no death-drop decay) |
| `species-rank` (unlisted 19th `creature-seed` module) overlaps T3/T6 | Low — a display axis, not a build blocker | Filed in `creature-seed-map.md`'s asks table already |
| `creature-yield.v1.json` created independently by T30 and T34 | Low — sequenced (T30 before T34) | T34 confirms the file exists with T30's shape before adding to it |
| Golden fixture drift from T6 (wave roll) or T3 (threatBand rewrite) | Medium if it happens | Each spec states which goldens should be unaffected; a moved golden is investigated, never auto-re-blessed |
| Building a slice of `item` module 23 inside this initiative | Medium — a second program's territory, same shape as the durability slice | Filed back into `item-map.md`; scope is explicitly bounded to the spec's own v1 (no activation, no resource charging, no set reconciliation) |

## Open questions — full sweep, sealed 2026-09-13

Every open question across all 19 specs was reviewed in one sitting (see § Round-3 decisions for the
8 that changed something). **Everything not listed below is sealed as its spec's own stated
recommendation** — each spec already carries that recommendation as its adopted design, so there is
nothing left to override unless you want to revisit a specific one by name.

**Genuinely still open — balance/content values with no number yet, none blocking, each resolved
inside its own task:**

1. `RaiseResolver.SpeciesFor`'s own fix (a second, ~6-species-reachable selection site) — ships as
   its own follow-up task, not folded into T7. *(Not yet its own task — file when picked up.)*
2. `species-materials`' per-species material count (1–2, or 0 for general creatures) — T34.
3. `species-cost-shaping`'s threshold rung value — T32.
4. Durability-slice's repair cost curve (`repairRatioMilli`) — T23.
5. `gem-tier`'s upcycle drain (`upcycleInputPerOutput: 3`, unchanged unless play says otherwise) —
   T9.
6. `gem-tier`'s `rung` input for `forge-gem` pricing (the output tier feeds it) — T9, one line of
   tuning prose.
7. `gem-tier`'s ladder width (`[2..4]` today; whether `gemgen` ever authors t1/t5) — not this
   initiative's call; named so it is not silently assumed settled.
8. `enhance-track-wiring`'s per-ordinal milestone tier ladder (starting shallow) — T13.
9. `item-upgrade-tree`'s potential consumption on upgrade, and whether the successor inherits the
   used fraction — T37.
10. `rarity-promotion`'s potential consumption on promotion vs. a plain temper — T26.

Nothing on this list blocks a phase. Each is a tuning value or a small follow-up task, decided by
whoever picks up the owning task, using the recommendation already in that task's spec unless you
say otherwise.
