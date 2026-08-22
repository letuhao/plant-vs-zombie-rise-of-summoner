# Implementation Plan: Game GUI refactor

**Tasks:** [game-gui-todo.md](game-gui-todo.md) — 24 tasks, 7 checkpoints.
**Spec:** the design set, which is spec-grade and is the acceptance reference:

| Reads as | File |
|---|---|
| Rules | [game-gui-principles.md](../docs/architecture/game-gui-principles.md) — GG-1…GG-60, tiered |
| Surface map | [design/information-architecture.md](../docs/design/information-architecture.md) — stages, layers, bands, keymap, motion, route migration |
| Stack + budgets | [design/tech-stack.md](../docs/design/tech-stack.md) — T1–T4, gap register, measured bundle plan |
| Visual acceptance | [design/README.md](../docs/design/README.md) — eight HTML plates |
| Module ownership | [game-gui-map.md](../docs/architecture/game-gui-map.md) — 14 modules |
| Module spec | *none written.* Approved 2026-08-22 to plan directly off the above |

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
| T14–T20 (the six surface slices) once Checkpoint D passes | T1 → T2 → T3: the shell spine |
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

## ⛔ Blocked pending the gap audit — added 2026-08-22

[design/gap-audit-2026-08-22.md](../docs/design/gap-audit-2026-08-22.md) found the design set's entity
inventory short by **29 entities** — the whole item program, the action layer, the shield stack and the
derived-stat sheet. The stage/layer model is unaffected (GG-1…GG-60, the six bands, the four stages,
the nine layers, motion and bundle all stand), and **Task 2's GG-11 keystone is unaffected** — start
there regardless.

What this plan owes:

| Task | Effect |
|---|---|
| **T15 Relics** | Scoped *Container card · Atom row · comparison*. Would have to absorb **19** missing components. **Split it, or defer item surfaces explicitly** — do not let it silently under-deliver |
| **T4 contract** | Do not seal. See the hold in [game-gui-map.md](../docs/architecture/game-gui-map.md) §Contract |
| **T18 Battle** | Wants the action card, costs, targeting, usability and the range grid — five undesigned components, not one fixture-driven stage |
| **T19 Almanac** | Wants the element matchup matrix (**two** matrices, asymmetric), dual typing and the derived-stat sheet |

**Tasks backed by shipped code are unblocked** and are the right place to keep moving: T1–T3 (shell
spine), T7 (tokens), T12 (route sweep), and the magnitude/unit work, which closes six of the audit's
eight Class-B defects by itself.

## Open questions

1. **Does the server expose unlock state?** T9's rail renders from unlock state. If no endpoint
   exists it ships as `Pending` with a reason — the sealed-contract mechanism handles it, but the
   rail will show locked entries until the server catches up. Confirm that is acceptable.
2. **Save select needs a per-save summary** (level, creatures bound, sectors held, last played).
   `GET /api/players` returns `id`, `name`, `createdUtc` today. Same treatment: `Pending`, or a
   thinner first version.
3. **Battle stage (T18) has no live backend** — the battle kernel is approved, not built. T18 builds
   against fixtures only, and its e2e is fixture-driven. Confirm that is in scope rather than
   deferred.
