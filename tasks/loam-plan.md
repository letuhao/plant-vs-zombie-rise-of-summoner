# Plan: loam and the Fracture — the pre-gate build

**Paths used:** `tasks/loam-plan.md` + `tasks/loam-todo.md`. The bare `plan.md`/`todo.md` pair holds
**Perf v3** and was not touched (AGENTS.md parallel-programs convention).

**Design:** [empire-economy-ssot.md](../docs/architecture/empire-economy-ssot.md) — what holds ·
[economy-principles.md](../docs/architecture/economy-principles.md) — the tests ·
[loam-map.md](../docs/architecture/loam-map.md) — build order and audit.
**Specs in scope (six, all sealed):** [loam/](../docs/architecture/loam/) — `loam-model`, `loam-calc`,
`loam-turn`, `loam-maps`, `loam-ai-survival`, `loam-fe`.

**Gate: PASSED — specs sealed and build authorized 2026-08-23.** Work may start at L1.

## Overview

The empire holds ground by keeping it **real**. Loam pays for that; the Fracture takes it back. This
plan builds only as far as the **⭐ gate** — the point at which the owner can play the mechanic and
decide whether the rest of the program should exist at all.

Twenty-four tasks, five phases. Post-gate modules (`loam-legions`, `loam-ai`, `structure-substrate`,
`loam-structures`, `loam-texture`) are deliberately unplanned.

## Architecture decisions carried in from design

| Decision | Consequence for this plan |
|---|---|
| **Loam first, structures fifth** (map A10) | `SlotTypeDef.Yields` already exists and has one reader; a rootbed slot can seep with no structure model. Nothing here builds `StructureId` |
| **Pool per connected component** | `TerritoryComponents` is the load-bearing function and lands early (L6). It is **not** `SupplyGraph.ConnectedSectors` — different seeds, different question |
| **No distance multiplier** (A3) | One multiplier in `LoamUpkeep`: intensity. Plus the faction handicap |
| **The fade is its own enforcement** | No admission rule for claiming barren ground. A warning entry instead |
| **Handicap, never cheat** | `WorldFaction.UpkeepHandicapMilli`, hashed, replayed, announced in the report |
| **Two golden moves, both budgeted** | L2 (canonical + template minimum) and L15 (`RulesetVersion` 4). A third needs explaining |

## Slicing note — one deliberate deviation

The skill asks for vertical slices. **Phase 2 is deliberately horizontal**: pure calculators wired to
nothing. That is the pattern that worked in the AI program, where W25–W34 built every evaluation table
against hand-built fixtures and checkpoint 9 was literally *"the tables exist and still nothing has an
opinion."* It caught real defects cheaply, and mutation testing then found five vacuous tests that
coverage had called 100%.

Each calculator is still a complete slice of its own — model → function → fixture → test → mutants.
Phase 3 is the vertical slice that makes them real.

## Dependency graph

```
L1 rootbed catalog ─┐
                    ├─→ L2 fields+canonical+template ─→ L3 validation ─→ L4 persistence ─→ L5 fog
                    │                                                                        │
                    └────────────────────────────────────────────────────────────────────────┤
                                                                          ┌──────────────────┘
   L6 TerritoryComponents ─→ L7 production+upkeep ─→ L8 balance+fade+habitability
                                    │                          │
                                    └──→ L9 harness ←──────────┘   L10 mutants   L11 A5 benchmark
                                              │
   L12 Production phase ─→ L13 Pressure phase ─→ L14 sector loss ─→ L15 RulesetVersion 4
                                                                            │
   L16 size catalog ─→ L17 two-hearths ─→ L18 teaching properties ─→ L19 fixture
                                                                            │
   L20 Abandon rule ─→ L21 handicap ────────────────┐
                                                     ├─→ ⭐ GATE
   L22 DTO ─→ L23 overlay ─→ L24 gauge ──────────────┘
   (the FE strand hangs off L15, not off the AI — parallel, rejoining only at the gate)
```

L11 (the A5 benchmark) has no dependencies and can run at any point — it measures shipped code.

## Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| **A third golden move** | Med | Two are budgeted and isolated (L2, L15). L1 is catalog-only and provably moves nothing; L3's validation moves nothing. If a third appears, stop and find out why |
| **The AI-memory hypothesis (A4) fails** | **Low** (was High) | Tested at L20. If a stateless goal function oscillates, the fix is to pass the policy **its own orders from turn N−1** — already stored in `rpg_world_commands`, already hashed, already the replay input. No new state, no coupling of the save format to the AI, no boundary to ask about |
| **`TerritoryComponents` merged with `SupplyGraph`** | High | They answer different questions and are one careless refactor from being wrong for both. Stated in the spec, asserted by a test in L6 |
| **A vacuous test suite** | High | Mutants ship **with** the module (L10), on a **verified-green baseline** — an earlier "all 22 caught" was false because a concurrent stream had `Core` uncompilable |
| **Tuning against a map that cannot teach** | High | Phase 4 precedes Phase 5. W37 warned `first-light` would under-exercise the AI and it did |
| **The gate judged against a missing reward layer** | Med | The playtest brief is written into `spec-loam-maps.md` and says so explicitly |
| **Silent `int` overflow in upkeep** | Med | Two multipliers + "divide once" reaches `int.MaxValue` at legal inputs and wraps to **negative upkeep**, which reads as free territory rather than as a crash. **quantities typed `long`** so the expression promotes with no cast to forget (a cast must be remembered every time; a type need not), one division, boundary test in L7 |
| **`two-hearths` bloats `WorldTemplateCatalog`** | Low | Split per template past ~700 lines; a file that is two authored maps is a file nobody reviews |

## Checkpoints

1. **State exists** — after L5. One golden move, `RulesetVersion` unchanged.
2. **Nothing has an opinion** — after L11. Every calculator proven on paper; **no golden moved**.
3. **Ground can be lost** — after L15. `RulesetVersion` 4, replay byte-identical.
4. **The map can teach** — after L19. Every teaching property asserted.
5. **⭐ THE GATE** — after L24. Owner playtest. Everything downstream is justified by the answer.

## Open

**Every number.** Deliberately: L9's harness measures them against L17's map. Choosing them earlier is
guessing with extra steps.
