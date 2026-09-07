# Implementation Plan: drop-tables

**Source spec:** `docs/architecture/drop-tables-map.md` (capability map, 4 modules) and its own
`docs/architecture/item/spec-rate-floor.md`, `spec-rate-authoring.md`,
`docs/architecture/world-map-runtime/spec-sector-loot-wiring.md`,
`docs/architecture/base-defense/spec-siege-loot.md`. All four specs were adversarially reviewed
(4 independent review passes) and corrected before this plan was written — see each spec's own
review annotations and `drop-tables-ideal.md` §8.

## Overview

Four modules, three programs (item, world-map-runtime, base-defense), building toward the idea doc's
two asks: a tunable floor/authoring mechanism for ultra-rare drops (0.0001%, per-entry, no rarity-
ladder change), and real drop-table integration for the two gameplay modes that have none today
(world-map's sector-clear resolver is built but never called; base-defense has no loot mechanism at
all). Party-dungeon's own rich per-room-kind generator is **not** part of this plan — it already has
an approved, unbuilt spec (`spec-dungeon-loot.md`, 2026-09-05) and builds against that, independently.

## Architecture decisions (inherited from the specs, restated so this plan doesn't drift from them)

- **The floor is a universal refusal gate, not a per-entry opt-in marker** — every drop-table entry in
  every group is checked at import against one tunable, `MinRatePerMillion`. No rarity-ladder change
  (D2).
- **The floor and the authoring tool share one tunable** (`DropRateFloorTuning`) — `rate-authoring` has
  a hard dependency on `rate-floor` for exactly this reason; there is never a second copy of
  `MinRatePerMillion`.
- **`IndependentRateEntry` (an independent, separately-streamed roll, mirroring D38's kill-drop roll)
  is the recommended default for anything meant to stay exactly rare regardless of future content
  added to the same table** — not a solved-for `Weight`, which drifts the moment a sibling entry is
  added. This is why `rate-authoring` ships before either gameplay-mode module: both should author
  their first real tables with the durable mechanism from day one, not retrofit it later.
- **World-map's real trigger is `ClaimResolver.Run` (`World/Movement/ClaimResolver.cs:79`), not
  anything Battle-adjacent** — confirmed by trace, and it happens to satisfy `DESIGN-GATE.md`'s
  "Battle never grants" rule for free, since `ClaimResolver` sits outside Battle entirely.
  `WorldSectorLootSource.TryResolve` itself needs no logic change, only a caller and one new parameter.
- **Base-defense's real trigger is NOT yet confirmed**, and per the world-map precedent, may not be
  `DistrictAssaultResolver`'s own result at all — district clears may only feed a `ClaimResolver`-style
  slot-guard precondition, with the actual grant belonging wherever that precondition is consumed. This
  is the plan's single biggest unresolved unknown; see Risks.
- **Content differentiation must be substantive, not just distinct table ids** — both gameplay-mode
  modules' acceptance criteria require at least one real axis (pool, weights, or `affix_channel`) to
  differ between at least two real tables, verified by a content-diffing test, not an id-uniqueness
  check alone (a real gap an adversarial review caught: distinct ids alone would pass every other
  stated criterion while shipping zero player-perceptible change).
- **No new player-facing surface anywhere in this plan** (D3) — the floor and the authoring mechanism
  are both backend-only; no web client, item-card, or HUD work is in scope.

## Dependency graph

```text
rate-floor (item)                                    Phase 1
    |
    +--> rate-authoring (item)                        Phase 2   [hard dependency]
             |
             +--> sector-loot-wiring (world-map)       Phase 3   [soft — call site already confirmed]
             |
             +--> siege-loot (base-defense)            Phase 4   [soft — call site UNCONFIRMED,
                                                                    the plan's real risk]
```

Phases 1 and 2 have zero external unknowns and no concurrent-session conflict risk (confirmed by fresh
`git status` at plan-writing time: neither `DropTableModel.cs` nor any file under `Items/Drops/` shows
as modified). Phase 3's call site (`ClaimResolver.cs`) is also clean. **Phase 4 touches four files
currently under another session's active edit** (`DistrictAssaultResolver.cs` `MM`,
`DistrictLayout.cs`/`BattleSeam.cs`/`DistrictAssaultPhase.cs` `M`) — re-check `git status` immediately
before starting Phase 4's tasks, not just at plan-writing time.

## Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Base-defense's real loot-grant call site turns out to be `ClaimResolver`-adjacent (like world-map), not `DistrictAssaultResolver` directly | Medium — Phase 4's design (win/loss keyed directly on `SiegeOutcomeKind`) would need reshaping, not just a different file | Phase 4's first task is explicitly an investigation, informed by Phase 3's now-real precedent (`ClaimResolver.cs`) rather than a blind second search. Not a gate — proceed, adjust the design if the investigation contradicts it, re-verify against the spec's own stated fallback |
| Another session's concurrent WIP on `DistrictAssaultResolver.cs`/`DistrictLayout.cs`/`BattleSeam.cs`/`DistrictAssaultPhase.cs` changes shape before Phase 4 starts | Medium — could invalidate Phase 4's cited line numbers or even its chosen call site | Re-run `git status` fresh immediately before Phase 4; if the files have changed materially, re-read them before writing any code against them |
| A minimal-compliance implementation of Phase 3/4's per-type/per-tier tables could ship byte-identical content under different filenames | Low if caught, otherwise defeats the whole initiative's point | Each phase's own tasks include a content-diffing test as an explicit acceptance criterion, not left to author discipline |
| `rate-floor`'s universal check could theoretically reject real, already-shipped content | Measured, not hypothetical: the narrowest real shipped multi-entry group resolves ~41,667/million against a floor of 1/million — four orders of magnitude of headroom | Phase 1's own regression task runs the check against the full real corpus before considering the module done |

## No pre-work gates in this plan

Checked against the planning skill's "Gates vs. checkpoints" rule: nothing here blocks *starting* a
phase on an external, unresolved decision. The two real unknowns (base-defense's call site, concurrent
WIP) are answerable by reading code immediately before the affected task, not by waiting on someone
else — they are the first tasks of their own phases, not gates in front of them. `affix_channel`/X4
(effect-pipeline's own module) is named as a future enhancement no task here depends on.

## Task List

Recorded in `tasks/drop-tables-todo.md`. Phases: 1 `rate-floor` (item) → 2 `rate-authoring` (item) →
3 `sector-loot-wiring` (world-map) → 4 `siege-loot` (base-defense), with a checkpoint after each.

## Open questions

None outstanding — all three of the idea doc's open questions and the one audit-surfaced gap were
resolved by the owner before this plan was written (`drop-tables-ideal.md` §7, §8). If Phase 4's
investigation finds base-defense's grant truly cannot key on `SiegeOutcomeKind` at all (not just a
different call site, but a fundamentally different signal), that is a real, new open question to bring
back before continuing Phase 4 — not something to resolve unilaterally mid-implementation.
