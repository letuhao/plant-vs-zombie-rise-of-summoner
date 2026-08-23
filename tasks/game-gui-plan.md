# Implementation Plan: Game GUI refactor

**Tasks:** [game-gui-todo.md](game-gui-todo.md) — 24 tasks, 7 checkpoints.
**Spec:** the design set, which is spec-grade and is the acceptance reference:

| Reads as | File |
|---|---|
| Rules | [game-gui-principles.md](../docs/architecture/game-gui-principles.md) — GG-1…GG-61, tiered |
| Surface map | [design/information-architecture.md](../docs/design/information-architecture.md) — stages, layers, bands, keymap, motion, route migration |
| Stack + budgets | [design/tech-stack.md](../docs/design/tech-stack.md) — T1–T4, gap register, measured bundle plan |
| Visual acceptance | [design/README.md](../docs/design/README.md) — eight HTML plates, plus the entity-ladder work in [00-foundation.html](../docs/design/00-foundation.html) §C.8–C.9, §D.5–D.8, §F.3–F.4 |
| Module ownership | [game-gui-map.md](../docs/architecture/game-gui-map.md) — 14 modules |
| Module spec | [web/spec.md](../docs/web/spec.md) — **complete 2026-08-23**, all six spec-driven-development areas, owner-approved |
| Entity coverage | [design/gap-audit-2026-08-22.md](../docs/design/gap-audit-2026-08-22.md) — **closed 2026-08-23**, all 29 gaps + 8 defects fixed, nine detail-design documents written |
| Responsive + scroll | [design/responsive-and-scroll-audit-2026-08-23.md](../docs/design/responsive-and-scroll-audit-2026-08-23.md) — GG-36 range declared, GG-61 added, swept clean on all eight plates |

*Path note: `tasks/plan.md` / `tasks/todo.md` hold perf v3 and `SPEC.md` holds vfx-v3, so this
initiative uses the prefixed pair per AGENTS.md.*

---

## Overview

Convert `web/fusion-rpg-web` from twenty flat routes into four stages and eight layers, against a
sealed view contract, with English-first i18n and a 4× smaller entry chunk. Nothing about the server,
the data bus, or the Phaser projector changes in kind — what changes is what owns visibility, what
components bind to, and what ships in the entry bundle.

## How the map and this plan relate

They slice the same work two different ways, and both are needed:

- **The map's 14 modules are *ownership* boundaries** — which module owns a file, and which lands
  which of the twenty §19 checks.
- **This plan's tasks are *vertical slices*** — each one delivers something that runs and can be
  verified, usually touching three or four modules at once.

So `fe-tokens` is not a task. "The Actor ladder renders a real creature from the contract, in all
four states" is a task, and it consumes part of `fe-tokens`, `fe-contracts`, `fe-i18n` and `fe-kit`.
Foundation is built **as the first slice needs it**, not as a complete horizontal layer up front.

## Architecture decisions

Already taken and not re-litigated here — see the docs above for reasoning:

- **D1/D2** Sanctum is home; lawn, world and battle are stages, not layers. Phaser is created on
  entering the lawn stage and destroyed on leaving it, never on a panel opening.
- **T1** Contract types stay hand-written (`decisions.md:67` lock); drift is caught by shared JSON
  fixtures emitted by `FusionRpg.E2E.Tests`. The FE **view contract is sealed**, and fields no
  endpoint fills yet are `Pending<T>` carrying a player-facing reason.
- **T2** The developer tree ships in the player build, lazy, default off.
- **T3** `@xyflow/react` and `recharts` are removed.
- **T4** Preferences are `localStorage`; view state is memory + URL. Nothing server-side.

## Sequencing rationale

**The riskiest thing goes first.** Task 2 opens a panel over the live lawn and asserts the Phaser
`Game` instance is *the same object* before and after. If GG-11 cannot hold, eleven modules are
built on a false premise, and that must surface in week one — not after the Sanctum is drawn.

**The "it's a game" milestone is Checkpoint D**, not the end. Sanctum + rail + Creatures + toasts is
the point where the product stops reading as an audit tool. Everything after it is widening.

**The route sweep (T12) is early and cheap.** Moving nine diagnostic routes behind the developer gate
costs little and removes most of what makes the navigation feel wrong. Old routes stay reachable
inside the tree, so nothing is lost mid-migration.

**Old routes keep working until replaced.** No flag day. The router keeps serving `#/roster` until
the Creatures layer lands, then redirects.

## Commands

```powershell
cd web/fusion-rpg-web
npm test                     # vitest run  — baseline 292 tests, 36 files, all green
npx vitest run --coverage    # coverage; scope is rewritten in T1
npm run build                # tsc --noEmit + vite build
npm run test:e2e             # playwright, against vite preview :4173
npm run test:all             # coverage + build + e2e
```

Design plates: `start docs\design\00-foundation.html`

## Parallelization

| Can run in parallel | Must be sequential |
|---|---|
| T14, T15, T17, T18, T19, T20 (six surface slices — **T16 dropped from the original seven this phase**, 2026-08-23) once Checkpoint D passes | T1 → T2 → T3: the shell spine |
| T21–T24 (flows) once T14 and T18 land | T4 before anything binds to the contract |
| Contract fixtures (T4a) alongside token work (T7) | T13 (splitting) after the stage/layer split exists |

## Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| **GG-11 cannot hold** — a panel cannot open over the lawn without disturbing Phaser | **High** — invalidates the layer model | T2 is the second task and asserts `Game` instance identity. Fail here, cheaply |
| Contract misses fields, since it is authored before most consumers exist | Medium | Additive extension is free by rule; only rename/narrow costs a version bump |
| A `pending` field ships with a weak reason and reads as broken rather than unbuilt | Medium | T4 lands a check: every `pending` field carries a non-empty player-facing reason |
| Old and new navigation coexist and confuse the test suite | Medium | Old routes redirect the moment their replacement lands; T12 sweeps the rest |
| Coverage config still scopes 9.3% of the FE with game modules at 0% | Medium | Rewritten in T1 as part of the first shell work, not deferred |
| Removing `recharts` regresses the Progression charts | Low | T19 rebuilds the four shapes from the plates *before* T13 removes the dependency |
| `data-testid`-heavy suite fights role/name queries | Low | Migrate opportunistically per slice (gap G11), never as a big-bang pass |

## ✅ Gap audit closed, 2026-08-23 — was the blocker, now the reference

[design/gap-audit-2026-08-22.md](../docs/design/gap-audit-2026-08-22.md) found the design set's entity
inventory short by 29 entities. **All 29 are now closed** — nine detail-design documents written,
each verified against shipped code and cross-checked for arithmetic, not just cross-referenced:
[spec-magnitude-and-units.md](../docs/design/spec-magnitude-and-units.md),
[spec-item-card.md](../docs/design/spec-item-card.md),
[spec-sockets-and-sets.md](../docs/design/spec-sockets-and-sets.md),
[spec-comparison.md](../docs/design/spec-comparison.md),
[spec-inventory-and-workshop.md](../docs/design/spec-inventory-and-workshop.md),
[spec-equip-and-paperdoll.md](../docs/design/spec-equip-and-paperdoll.md),
[spec-action-layer.md](../docs/design/spec-action-layer.md),
[spec-shield-and-elements.md](../docs/design/spec-shield-and-elements.md),
[spec-derived-stat-sheet.md](../docs/design/spec-derived-stat-sheet.md).
[responsive-and-scroll-audit-2026-08-23.md](../docs/design/responsive-and-scroll-audit-2026-08-23.md)
then closed the two remaining gaps (a declared viewport contract, a rule for a dense entity's own
content — GG-61) and swept all eight plates clean.

**What this settles for the tasks that were named as at-risk:**

| Task | Then | Now |
|---|---|---|
| **T15 Relics** | Would have absorbed 19 missing components | The item card (11 blocks), sockets/sets, comparison and inventory are all designed — T15 scopes against real components, not a placeholder |
| **T4 contract** | Held from sealing — entity list was incomplete | The entity list is complete **except World's own shapes** (Sector, Contract-at-scope, Run), which stay provisional since T16 is excluded this phase (below). The contract may seal for every entity **outside** World's domain |
| **T18 Battle** | Wanted five undesigned components | Action card, costs, targeting, usability and the range grid are all designed (`spec-action-layer.md`) |
| **T19 Almanac** | Wanted the element matrix and derived-stat sheet | Both designed; the matrices were also found to be identical in content (not asymmetric as first assumed) and are drawn as one component with a diff mode |

## ⛔ World map (T16) excluded this phase — owner decision, 2026-08-23

*"Map GUI is exclude this phase, just keep it as is — we will have other plan for it because that is
huge design, should make new GUI solid foundation before we move to the map."*

**T16 is dropped from this phase's task list**, not deleted — its acceptance criteria are retained in
`game-gui-todo.md` as the starting point for its own future plan. `/world`'s current route stays
exactly as it is until that plan lands; T12's route sweep does not touch it.

**Two ripple effects, resolved, not hidden:**

- **T17 (Expeditions, Pacts) depended on T16.** Checked against
  [information-architecture.md](../docs/design/information-architecture.md): both are stage-independent
  band-2 layers with no code dependency on World. Dependency dropped to T11 alone. One real gap
  remains — Expeditions' unlock condition (*"first sector held"*) can't be live-demonstrated without a
  real sector claim, so that one demo waits; the layer itself does not.
- **T18/T22 (Battle, Deploy targeting) lose one of Battle's two entry points.** *"Commit a legion on
  the world map"* has no stage to launch from this phase. *"An expedition resolving into a fight"*
  does not require World and remains the path T18 builds and fixture-e2e-tests through.

Checkpoint F's reachability claim is corrected in `game-gui-todo.md` to state this as a fourth,
phase-scoped exception, distinct from the three permanent behavioural exceptions in IA §6 (D8).

## Open questions

1. **Does the server expose unlock state?** T9's rail renders from unlock state. If no endpoint
   exists it ships as `Pending` with a reason — the sealed-contract mechanism handles it, but the
   rail will show locked entries until the server catches up. Confirm that is acceptable.
2. **Save select needs a per-save summary** (level, creatures bound, sectors held, last played).
   `GET /api/players` returns `id`, `name`, `createdUtc` today. Same treatment: `Pending`, or a
   thinner first version. *(With T16 excluded, "sectors held" is `0`/`Pending` for every save this
   phase regardless of the endpoint — worth deciding whether to show the field at all before World
   exists, or ship it honestly at zero.)*
3. **Battle stage (T18) has no live backend** — the battle kernel is approved, not built. T18 builds
   against fixtures only, and its e2e is fixture-driven. Confirm that is in scope rather than
   deferred.
