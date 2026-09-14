# Implementation Plan: `species-rank` (creature-seed)

**Spec:** `docs/architecture/creature-seed/spec-species-rank.md` (approved) · **Parent ideal:** `docs/architecture/gameplay-tiers-ideal.md` · **Tasks:** `tasks/creature-seed-todo.md` (this initiative's pair; the sibling-spanning `seed-to-concrete` plan is a different initiative and is not touched).

## Overview

Land per-species cosmetic rank end-to-end: a tuning grid maps resolved `(threatBand, rarity)` to a closed 10-value rank; seedsmith derives it as a `DERIVED` anchor field; Core expands, persists, and serves it on both hosts; fusion gates and display payloads read it at pass-through defaults (zero behavior change). Follow-up gates (expedition, wave, Cage) land separately, each with its own pass-through proof.

## Architecture Decisions

- **Diagonal-default 100-cell grid** (owner-decided over the ideal's 10-row shape): off-diagonal divergence later costs a cell edit, no migration; threat selects row, rarity selects column; rank never enters a magnitude.
- **Separate `CreatureRank` enum mirroring rarity ids** (owner-confirmed principle: each rank axis owns its closed vocabulary); bare-ordinal comparisons forbidden by a new guard test.
- **Python `"unresolved"` → C# null at the boundary** (`Rarity` nullability untouched); null maps to bottom **at each gate** — no `AtLeast(null,…)` crash path.
- **No recompute on promotion** (rank is species-identity, promotion is specimen-state); preview mirrors every enforcing gate.
- **No pre-work gates.** Nothing here is irreversible: tuning is versioned, regen is committed-diffable, gates land pass-through, and every slice is reviewable after the fact. Checkpoints review done work; nothing blocks starting.

## Dependency Graph

```text
creature-rank.v1.json ──┬── CreatureRankLadder (helpers + guard)
                        │
anchor schema/derive/runner ──► anchor regen ──► AnchorRow/Expander
                                                        │
concrete triad + SeedReader ──► DAL persist ──► catalog/Mapper/Injector
                                                        │
                                      ┌─────────────────┴─────────────────┐
                                      ▼                                   ▼
                          fusion gates + preview               display payloads ──► quality line
                              (parallel with T9)                   (parallel with T8)
                                      │                                   │
                                      └─────────────────┬─────────────────┘
                                                         ▼
                        follow-ups: expedition / wave / Cage (each independent)
```

Bottom-up, sliced vertically: each task below leaves the system working and tested. T1+T2+T3 are parallelizable once the vocab ids (T1) are fixed; T4 needs T3; T5→T6→T7 sequential (corrected 2026-09-12 — todo has a single Task 5, no a/b split); T10–T12 independent of each other after T7.

**T8/T9 ordering — resolved 2026-09-12 (owner): parallel-safe.** This line previously read
"T8→T9 sequential" and the dependency graph below showed `fusion gates + preview ──► display payloads`,
contradicting `creature-seed-todo.md` Task 9's "Task 8 parallel-safe." Confirmed: the two tasks' file
sets are disjoint (`RpgStore.Fusion.cs`/`FusionEndpoints.cs`/`CreatureRecipeCatalog.cs` vs.
`CreatureEndpoints.cs`/`RpgStore.Creatures.cs`/`SlotFilter.cs`) — no code dependency forces the order.
The graph below is corrected to match; both may build in either order once Task 7 lands.

## Risks and Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Null rank crashes a gate (`AtLeast(null,…)`) | Med | Explicit null→bottom mapping per gate, tested (T8) |
| Scoped rerun leaves stale ranks | Med | Staleness test at the merge path (T3) |
| Preview/enforce divergence on fusion gates | High | Parity tests proving identical verdicts (T8) |
| `descriptions.py` KeyError (missing entry) | Low | Named explicitly in T3 acceptance |
| Generated-file churn masks real diffs | Low | Byte-identical rerun test; regen is one commit |
| `data/generated/creatures/**` regen attempted before the C# model can carry rank | Med | Moved to Task 6 in the todo (2026-09-12 fix) — was mis-sequenced onto Task 4, which only has the seedsmith derive as a dependency |
| `creature-seed-map.md`'s module table goes stale (no module 19 entry) the moment this lands | Low | Doc-only task added to the todo's Gap audit section (GAP-4) |

## Open Questions

None — spec assumptions decided; micro-choices (display names, off-diagonal cells, floors above bottom) are tuning content for implementation review.
