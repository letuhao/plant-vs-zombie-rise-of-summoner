# Capability map: lawn-interactive

**Status:** PLAN written 2026-09-07 — map + seven module specs + prefixed plan/todo. **No
IMPLEMENT** until owner accepts [tasks/lawn-interactive-plan.md](../../tasks/lawn-interactive-plan.md).

**Visual:** [12-lawn-stage.html](../design/12-lawn-stage.html). **Audit:**
[lawn-interactive-audit-2026-09-06.md](../research/lawn-interactive-audit-2026-09-06.md).
**Design landing:** [spec-lawn-interactive.md](../design/spec-lawn-interactive.md).
**Plan / tasks:** [lawn-interactive-plan.md](../../tasks/lawn-interactive-plan.md) ·
[lawn-interactive-todo.md](../../tasks/lawn-interactive-todo.md). Never `tasks/plan.md` /
`tasks/todo.md`.

**Sibling programs (dependencies, not owned here):**

| Program | Role |
|---|---|
| [actor-sheet-map.md](actor-sheet-map.md) | Character sheet opened from dock / commanders / creatures |
| [actor-hud-map.md](actor-hud-map.md) | Band B per-unit glance (shipped); click opens dock, not a third HUD |
| DPLP / lawn projector | Board observe + Intent; FE never owns sim |

> **Why a separate program.** ActorSheet is the panel. Lawn interactive is the **stage chrome**
> (match strip, cell stack, occupancy dock, spawn tray, commander combat book). Bundling them into
> actor-sheet violated program boundaries and skipped a capability map for the lawn ideas.

---

## What this program is

Player-facing lawn stage layers and Phaser cell presentation so a mid-wave player can see stacks,
inspect occupants, field uniques, and enqueue commander orders **without leaving the lawn stage**
(GG-1 / GG-11). RPG layer readouts + Intent only — not a rewrite of PvZ `Plant` fields.

---

## Modules

| Module id | Responsibility | Depends on | Spec |
|---|---|---|---|
| `actor-collection` | One shared Actor Row/Card list widget (density, GG-50, GG-51 query). Consumers: Creatures, cell dock, spawn tray — never a private list copy | — | [spec-actor-collection.md](lawn-interactive/spec-actor-collection.md) |
| `lawn-occupant-adapt` | Map living lawn occupants (unique Bound + general Wave) → `ActorRow` / sheet bind props. Generals never enter `?sel=` as instanceId | — | [spec-lawn-occupant-adapt.md](lawn-interactive/spec-lawn-occupant-adapt.md) |
| `lawn-match-hud` | Band-1 match strip: `pvz.*` sun bank, wave/clock/phase, commander + aura chips, deployed unique chips, connection, Field CTA. Extends [commander-surface/spec-lawn-hud-chip.md](commander-surface/spec-lawn-hud-chip.md) | `actor-collection` (chips) | [spec-lawn-match-hud.md](lawn-interactive/spec-lawn-match-hud.md) |
| `cell-stack` | Phaser Band-0: draw every living occupant in a cell with stable overlap; `+K` overflow; unique pip. Per-unit HUD stays actor-hud | lawn projector | [spec-cell-stack.md](lawn-interactive/spec-cell-stack.md) |
| `cell-occupancy-dock` | Band-2 reserved **left** column; camera shrinks so 12 columns stay visible; rows from adapt; select → push ActorSheet (GG-10) | `actor-collection`, `lawn-occupant-adapt`, actor-sheet | [spec-cell-occupancy-dock.md](lawn-interactive/spec-cell-occupancy-dock.md) |
| `spawn-tray` | Band-2 fielding tray via ActorCollection + existing SpawnTargeting ghost; no `typeId` typing | `actor-collection`, DPLP Intent | [spec-spawn-tray.md](lawn-interactive/spec-spawn-tray.md) |
| `commander-action-bar` | Band-1 off-board combat book (1–9): pick order → board target → enqueue Intent. HoMM3 combat-hero pattern; not adventure map spells | `spec-action-layer.md` chrome | [spec-commander-action-bar.md](lawn-interactive/spec-commander-action-bar.md) |

Shared React kit for action slots / costs stays in `spec-action-layer.md` — this program composes it.

---

## Build order

```text
actor-collection ──┬── lawn-match-hud
                   ├── spawn-tray
                   └── cell-occupancy-dock
lawn-occupant-adapt ──┘
cell-stack                    (parallel with React chrome after projector hooks exist)
commander-action-bar          (after Intent targeting mute rules with dock/sheet)
```

ActorSheet and actor-hud must be **spec-approved** before dock “open sheet” / HUD-click-to-dock wiring
is tasked — they are external dependencies, not modules of this map.

---

## Acceptance (program-level)

> Mid-wave on lawn (Fusion optional for observe; Intent needs injector): player sees stacked
> occupants without click; opens left dock without losing the Phaser Game; drills into ActorSheet
> for a Bound unique; fields a unique via tray without typing `typeId`; enqueues one commander order
> via the combat book. Generals show Wave chrome and locked unique affordances (GG-17). No new
> `#/actor/:id` route. Commander never drawn as a lawn tile.

---

## Explicitly not in this program

- ActorSheet tab bodies / runtime catalogs — [actor-sheet-map.md](actor-sheet-map.md)
- Band B token layout / VFX — actor-hud / vfx programs
- Action corpus authoring, A10 range math on FE
- Promoting Crazy Dave → unique Commander (owner Vocabulary decision)
- Siege / world-map adventure book
- Rewriting PvZ Plant fields

---

## Plan / tasks

Prefixed pair (written): [lawn-interactive-plan.md](../../tasks/lawn-interactive-plan.md) ·
[lawn-interactive-todo.md](../../tasks/lawn-interactive-todo.md). **IMPLEMENT only after owner
accepts the plan.** Never `tasks/plan.md` / `tasks/todo.md`.
