# Plan: `species-gear-chain` — implementation plan

**Spec:** 19 module specs under `docs/architecture/species-gear-chain/`, indexed by
[species-gear-chain-map.md](../docs/architecture/species-gear-chain-map.md) (audited, corrected,
owner-reviewed 2026-09-13). **Tasks:** [species-gear-chain-todo.md](species-gear-chain-todo.md) —
this pair, never the bare `tasks/plan.md`/`tasks/todo.md` (the perf stream's).

---

## Overview

Four idea-phase docs (`tier-system-ideal.md`, `species-selection-ideal.md`, `species-craft-ideal.md`,
`gear-climb-ideal.md`) were audited into one capability map and 19 module specs, then a five-agent
audit found and fixed ~35 defects across them. Nothing has been built yet — this is the first `/plan`
pass turning 19 already-detailed specs into an ordered, checkpointed task list.

The specs are unusually complete: each already carries its own Design, Tunables, Numeric types,
ActorHub gate, Testing strategy, Boundaries and Success criteria. This plan's job is **not** to
re-derive that content — it is to (a) fix the build **order** the map's corrected dependency graph
established, (b) slice each module into S/M-sized vertical tasks, (c) separate what is buildable now
from what is blocked on code that does not exist yet in *other* programs, and (d) checkpoint every
layer so a reviewer never has to trust more than one layer's worth of unverified work at a time.

## Architecture decisions

- **Follow the map's corrected 5-layer build order**, with one change found while planning (below) —
  the map's own § Corrections already records the two prior fixes (`set-species-binding` moved a
  layer, `craft-risk-ladder` split at Stage 1).
- **`rarity-promotion` moves from Layer 1 to right after `craft-risk-ladder` Stage 1**, ahead of
  `tier-propagation-contract`'s completion. Its spec builds its *own* item-side rung arithmetic
  (`IsTopRung`/`OneRungAbove` for the string-keyed item ladder) rather than depending on
  `tier-propagation-contract` — the dependency audit's finding #5 was that the original edge was
  satisfied by nothing. It only genuinely needs `craft-risk-ladder` Stage 1 to exist.
- **Two modules are named but not scheduled as active tasks**, because their build dependency is
  *other programs' unbuilt code*, verified by direct grep this session, not by a doc's status header:
  - `craft-risk-ladder` Stages 2–4 ← `deployment-hierarchy` module 7 (durability) — confirmed unbuilt:
    *"Nothing tests durability, because nothing implements it"* (`item/defect-register.md:194`).
  - `item-upgrade-tree` ← `item` module 23 `requirement-profiles` — confirmed unbuilt: zero
    `RequirementProfile` hits anywhere in `src/`; `item-map.md`'s own build order is
    *"23 → 24 → 25"*, none started.
  Neither is a pre-work gate on this plan — nothing else in the 29 active tasks depends on either.
  Named in § Deferred with the trigger that reopens each, matching what their own specs recommend.
- **No enum-slot pre-work gate.** `MutationOpKind` is ten members today; neither `Repair`
  (`deployment-hierarchy`, filed first) nor this initiative's members exist in code yet. Adding a
  member is reversible (a git-ordering question, not an irreversible collision) — sequenced by
  building `rarity-promotion` before `item-upgrade-tree`, and coordinated by filing the
  `ssot-enhancement.md` §5.3 amendment in the same commit as the code.
- **No sixth-`MaterialClass` pre-work gate**, despite the contested slot with
  `deployment-hierarchy-map.md:89`. A wrongly-named class is a **versioned migration**
  (`classes.v1→v2→v3`, `sockets.v1→v2` are the shipped precedent), not an unrecoverable collision. T28
  reads both asks and proposes one reconciled shape as a design sub-step, not a plan-wide halt.
- **`themes.v2.json` + migration is a task, not a gate.** The owner already decided this
  (2026-09-13, recorded in `spec-ladder-consistency-repair.md`); nothing is left open to ask.

## Dependency graph (phases = the map's corrected layers, with the `rarity-promotion` move above)

```
Phase 1 (parallel-safe, no dependencies)
  tier-propagation-contract · threat-band-fill · wave-species-roll · wild-species-spawn
  socket-allowance-by-kind · craft-risk-ladder(Stage 1) · gem-tier · enhance-track-wiring

Phase 2
  ladder-consistency-repair  ← tier-propagation-contract
  species-magnitude-synth    ← threat-band-fill
  delve-species-wiring       ← threat-band-fill
  socket-combat-wiring       ← gem-tier
  rarity-promotion           ← craft-risk-ladder(Stage 1)          [moved up — see decisions]

Phase 3
  set-species-binding        ← ladder-consistency-repair
  creature-drop-tables       ← species-magnitude-synth + any one of {wave-species-roll,
                                wild-species-spawn, delve-species-wiring}
  craft-executor-completion  ← rarity-promotion

Phase 4
  species-cost-shaping       ← set-species-binding

Phase 5
  species-materials          ← species-cost-shaping, creature-drop-tables

Deferred — not scheduled, named with a trigger
  craft-risk-ladder Stages 2-4  ← deployment-hierarchy module 7 (durability), UNBUILT
  item-upgrade-tree              ← rarity-promotion + craft-risk-ladder + item module 23
                                    (requirement-profiles), UNBUILT
```

18 modules active across 5 phases, 29 tasks. `item-upgrade-tree` and `craft-risk-ladder`'s Stages 2–4
are the 19th/partial module, deferred.

## Task list

Full per-task detail (acceptance criteria, verification, dependencies, files, size) is in
[species-gear-chain-todo.md](species-gear-chain-todo.md). Index:

### Phase 1 — foundations (parallel-safe)
- T1–T2: `tier-propagation-contract`
- T3: `threat-band-fill`
- T4: `wave-species-roll`
- T5: `wild-species-spawn`
- T6: `socket-allowance-by-kind`
- T7: `craft-risk-ladder` (Stage 1 only)
- T8: `gem-tier`
- T9–T10: `enhance-track-wiring`

### Checkpoint — Phase 1

### Phase 2
- T11–T12: `ladder-consistency-repair`
- T13: `species-magnitude-synth`
- T14–T15: `delve-species-wiring`
- T16–T17: `socket-combat-wiring`
- T18–T19: `rarity-promotion`

### Checkpoint — Phase 2

### Phase 3
- T20–T21: `set-species-binding`
- T22–T24: `creature-drop-tables`
- T25–T26: `craft-executor-completion`

### Checkpoint — Phase 3

### Phase 4
- T27: `species-cost-shaping`

### Checkpoint — Phase 4

### Phase 5
- T28–T29: `species-materials`

### Checkpoint — Phase 5 / Complete

## Deferred — named, not gated, each with its trigger

| Item | Why it is not in Phases 1–5 | Trigger |
|---|---|---|
| `craft-risk-ladder` Stages 2–4 (durability decay, break, repair-loss) | `deployment-hierarchy` module 7 has zero implementation (`item/defect-register.md:194`) | Module 7 ships |
| `item-upgrade-tree` (E5, armour successor edge) | `item` module 23 `requirement-profiles` has zero implementation; also needs `rarity-promotion` (T18–19) and an 11th `CraftOperation` member | Module 23 ships |

## Risks and coordination (not gates)

| Risk | Impact | Mitigation |
|---|---|---|
| `MutationOpKind`/`CraftOperation` slot ordering vs. `deployment-hierarchy`'s own filed asks | Low — reversible git-level reorder | Sequence `rarity-promotion` (T18) before any `item-upgrade-tree` work; file the ssot amendment in the same commit as the code |
| Sixth `MaterialClass` contested with `deployment-hierarchy-map.md:89` | Medium — a wrongly-scoped class needs a versioned migration later | T28 reads both asks first and proposes one shape; `classes.v1→v3` is the shipped precedent for this exact repair |
| `species-rank` (unlisted 19th `creature-seed` module) overlaps `threat-band-fill`/`wave-species-roll` | Low — a display/rank axis layered on top, not a build blocker | Filed in `creature-seed-map.md`'s asks table already; re-check before T3/T4 if that module has moved |
| `creature-yield.v1.json` created independently by T23 and T29 | Low — same initiative, sequenced (T23 before T29 per Phase 3→5) | T29 confirms the file exists with T23's shape before adding to it |
| Golden fixture drift from T4 (wave roll) or T3 (threatBand rewrite) | Medium if it happens — signals the isolation claim in the spec was wrong | Each spec states which goldens should be unaffected and why; a moved golden is investigated, never auto-re-blessed |

## Open questions (owner, each answerable — not blocking)

1. Does T5's `RaiseResolver.SpeciesFor` fix (the second ~6-species selection site, Open question 3 in
   `spec-wild-species-spawn.md`) ride with T5 or get filed as its own follow-up? Recommendation: its
   own follow-up — different blast radius (a player's own legions, not the wild map).
2. `species-materials`' per-species material count (1–2, or 0 for general creatures) — balance data,
   resolved during T29, not before.
3. `species-cost-shaping`'s threshold rung value — balance data, resolved during T27.
