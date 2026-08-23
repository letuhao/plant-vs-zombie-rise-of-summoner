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
5. **⭐ THE GATE** — after L24. ~~Owner playtest.~~ **Superseded 2026-08-23 (owner decision): the gate
   is satisfied by automated test-suite coverage of the mechanical properties the playtest brief's
   three questions rest on, not a manual ten-turn session.** See `tasks/loam-todo.md` Checkpoint 5 and
   `tests/FusionRpg.Core.Tests/World/Loam/LoamPlaytestSignalTests.cs` for the substituted evidence.
   Everything downstream is still justified by the answer — the answer just comes from the suite now.

## Open

**Every number.** Deliberately: L9's harness measures them against L17's map. Choosing them earlier is
guessing with extra steps.

---

# Plan: loam and the Fracture — the post-gate build

**Gate: PASSED via the substituted, automated verdict (`tasks/loam-todo.md` Checkpoint 5).** Owner
authorized "spec and build" for all five post-gate modules, then authorized resolving every open item
in those specs, then authorized (and received) an adversarial audit that found and fixed a real safety
bug plus five further gaps — see `docs/architecture/loam-map.md`'s post-gate section for the full
findings. **All five module specs are sealed.** This is that program's plan.

**Specs in scope (five, all sealed):** [loam/](../docs/architecture/loam/) — `spec-loam-legions`,
`spec-loam-ai`, `spec-structure-substrate`, `spec-loam-structures`, `spec-loam-texture`.

## Overview

Nineteen tasks (L25–L43), six phases, in the build order `loam-map.md` §3 already fixed:
`loam-legions → loam-ai → structure-substrate → loam-structures → loam-texture`. One deliberate,
audit-driven deviation from that order: **all five modules' new hashed `WorldState` fields land
together, first, in one task (L25)** — not spread one-per-module the way the five specs originally
each claimed their own golden move. The adversarial audit caught that as reopening a budget
`tasks/loam-plan.md`'s pre-gate section had explicitly closed at two; landing every field at once,
before any module's behavior is built on top of it, keeps this program at **one** post-gate golden
move instead of five, and avoids re-blessing hashes repeatedly as each module's fields would otherwise
land one at a time.

## Architecture decisions carried in from the specs (and their audit)

| Decision | Consequence for this plan |
|---|---|
| **One batched golden move, not five** | L25 lands every new field across all five modules at once. No later task in this plan touches `WorldCanonical` |
| **`Sustain` resolves before `LoamPhases.Pressure`; `LegionSupply`'s burn/top-up resolves after it** | Two different phase-timing requirements for two mechanisms that share one command family — L28 and L27 land in that order for a reason |
| **`SeveranceScore` is scouting-gated by design** | L30's tests must include a fog-degenerate (unscouted) case as a *pass*, not treat a near-zero score there as a bug |
| **The AI march-loam gate is a worst-contiguous-out-of-supply-run check**, not a turn-by-turn simulator | L31 builds one pass over an already-known route, per the audit's fix — nothing more elaborate |
| **`Habitability.For`'s belief overload must widen before loam-structures' waystation can use it** | L34 is two changes in one task: the overload's signature, and every existing call site updated the same time |
| **Fade contagion and Fracture surges both extend `FadePolicy.DecayFor`'s pre-clamp input, never its output** | L39/L40's single most important test is the clamp holding under both stacked at once — the bug the audit caught and this plan must prove closed, not just claim closed |
| **A homeworld-loss range-rule lockout, and a warded-sector capture releasing its binding, are both accepted risks** | L37/L42 assert these as intended behavior, not guard against them |

## Dependency graph

```
L25 post-gate state (one golden move: all five modules' fields, StructureCatalog, Rule14)
     │
     ├─→ L26 LegionSupply calculators + harness ─→ L27 wire into Pressure, retire attrition ─→ L28 Sustain
     │                                                                                            │
     │                                                            ┌───────────────────────────────┘
     ▼                                                            ▼
   Phase 7 checkpoint (loam-legions)
     │
     ├─→ L29 habitability gate ─→ L30 SeveranceScore + Sever rule ─→ L31 AI march-loam gate
     ▼
   Phase 8 checkpoint (loam-ai)
     │
     ├─→ L32 structure-substrate validated end to end (placeholder structure)
     ▼
   Phase 9 checkpoint (structure-substrate)
     │
     ├─→ L33 well ─→ L34 waystation + belief widening ─→ L35 construction ─→ L36 Lost-handling fix ─→ L37 range rule
     ▼
   Phase 10 checkpoint (loam-structures)
     │
     ├─→ L38 granary ─→ L39 contagion ─→ L40 surges ─→ L41 the Unmade ─→ L42 wardens ─→ L43 prospecting
     ▼
   Phase 11 checkpoint (loam-texture) ─→ ⭐ POST-GATE COMPLETE
```

L32 (structure-substrate) has no behavioral dependency on `loam-ai`, only on L25's state — it is placed
after Phase 8 purely to match `loam-map.md`'s own declared build order, not because anything blocks it
earlier.

## Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| **A second golden move sneaks in anyway** | High | L25 lands every field for all five modules at once; every later task's own checklist item is "no `WorldCanonical` change," not "one more move, reason recorded" |
| **The decay-clamp bug the audit found gets rebuilt from the original (wrong) wording** | High | L39/L40's acceptance criteria quote the exact fix (`spec-loam-texture.md`'s "Resolved" text) rather than re-deriving it from the design source, which still describes the pre-fix framing in prose |
| **`Sever` judged "broken" because it scores near-zero in an early-game fixture** | Med | L30's test plan requires the fog-degenerate case as a named, passing test — a reviewer must not "fix" this later without re-reading why it is accepted |
| **`Habitability.For`'s belief-overload widening breaks something touching it that isn't `loam-structures`** | Med | L34 greps every call site of the belief overload before changing its signature, the same discipline this whole program used for the G-C exemption re-proof |
| **Construction's activation-turn off-by-one gets rebuilt ambiguous again** | Med | L35's acceptance criterion states the exact turn (`BuildTurns`-th decrementing pass, same turn) rather than "eventually" |
| **The two long-run regression properties silently break** | High | `AbandonRuleTests`'s 100-turn survival test and `TwoHearthsCampaignTests`'s 60-turn campaign are a named acceptance criterion on **every** phase checkpoint below, not just the final one |

## Checkpoints

6. **`loam-legions` built** — after L28. Leash is legible and reproducible; bearers change it, plain
   headcount does not; attrition fully retired; `Sustain` and the burn/top-up pass resolve in the
   audit-corrected order; both long-run regression properties still pass.
7. **`loam-ai` built** — after L31. Habitability collapses `Total` for barren ground without
   suppressing `SeveranceScore`; `Sever` fires/declines correctly including the accepted fog-degenerate
   case; the AI never marches a route that exhausts a legion's leash; both regression properties pass.
8. **`structure-substrate` built** — after L32. Catalog validates at static init; `Rule14` fires and
   declines; DTO round-trips; no behavior yet, by design.
9. **`loam-structures` built** — after L37. Well multiplies, waystation grants zero-yield habitability,
   construction genuinely gates both until its exact completion turn, loss during construction ruins
   the structure via new code (not assumed pre-existing behavior), the range rule fires/declines against
   unmodified `two-hearths`; both regression properties pass.
10. **`loam-texture` built, ⭐ POST-GATE COMPLETE** — after L43. All six mechanics pass their named
    tests; the decay-clamp stacking test (contagion + surge, already-severe deficit) proves the
    ceiling holds; a fully-warded, unpayable component applies no fade and does not crash; `two-hearths`
    has its `Wild` faction row; both regression properties still pass; all four guard scripts green;
    mutation testing extended to cover every new calculator this program's slice added.

## Open

Every harness-scheduled constant this program's five specs deferred rather than guessed:
`CarryPerBearer`, `BurnPerMember` (L26); the `Sever` threshold (L30); `WellYieldMultiplierMilli`,
well/waystation `CostMilli`/`BuildTurns`, `WaystationRangeHops` (L33–L37); `GranaryCostMilli`/
`CapacityBonus`, `ContagionPressurePerTurn`/`MaxPressureMilli`, `SurgeDecayMultiplierMilli`, Unmade
spawn rate/strength (L38–L43). Same discipline as the pre-gate slice: each is measured against its
own spec's stated target when its task lands, not chosen here.
