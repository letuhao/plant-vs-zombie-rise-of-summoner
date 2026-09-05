# Capability map: world map runtime

**Status:** Map + module spec drafted 2026-09-06, **strengthened same day after coverage audit** —
**pending owner review**. Implementation plan drafted same day:
[tasks/world-map-runtime-plan.md](../../tasks/world-map-runtime-plan.md) ·
[tasks/world-map-runtime-todo.md](../../tasks/world-map-runtime-todo.md).
**No build until the plan (and the spec defaults it implements) are approved.**

**Program id:** `world-map-runtime`.

**Ideal it implements:** [world-map-runtime-ideal.md](world-map-runtime-ideal.md) (owner pick
2026-09-05: Phaser map island + existing React HUD).

**Visual catalog:** [design/11-world-stage.html](../design/11-world-stage.html) **§O** (map pin,
LOD, dual-plane, focus, safe-area). Inspector card remains §A / §J.

**Does not reopen:** [world-map-program.md](world-map-program.md) turn engine, [world-stage-map.md](world-stage-map.md)
HUD / inspector / commands / playback, recruitment, fog *rules*.

---

## What this program is

The **map plane** of the world stage: how the rift is *seen* and *aimed at*. Sectors as compact pins,
lanes as typed strokes, a live camera, fleets on the graph, detail in the existing inspector. Not a
flowchart of 192px cards. Not a second simulation.

It replaces the SVG `viewBox` host that [tech-stack.md](../design/tech-stack.md) T3 named after
dropping `@xyflow/react`, and it replaces `world-render`'s **map drawing** (React `SectorNode` on
the stage) **and** map-plane overlay drawing (range, supply, lifeline, lens fills). Channel
functions, HUD, and the inspector card stay.

Loops: **World map — adventure** and **World stage — empire building**
([the-loops.md](../guide/the-loops.md) §4–§5).

---

## Why one module, not four

Host, pin factories, and the scene share one dataflow and are not independently shippable: an empty
canvas is not a player feature, and pins without a host have nowhere to live. They are **build
slices** of one module (`world-map-runtime`), ordered below. Token snapshot is a slice of host boot,
not a product.

| Slice | Responsibility | Depends on |
|---|---|---|
| **Host** | `createWorldGame`, `WorldGameHost`, `world:*` bus, CSS-token snapshot, `modelSeq`, destroy checklist | existing `createGame`, `allocGameGeneration` |
| **Objects** | Sector pin / lane / force factories from `sectorChannels` / `laneChannels` / `fogTreatments` (fog-on-pin density); registry | Host types; channel modules (stay Phaser-free; `game/` may import them) |
| **Scene** | `WorldMapScene`, Phaser camera (incl. edge-scroll), zoom LOD, pick with chrome occlusion, **all** lens/route/range/supply/lifeline/blocked drawing | Host + objects |

**Build order:** Host → Objects → Scene.

**Module spec:** [world-map-runtime/spec-world-map-runtime.md](world-map-runtime/spec-world-map-runtime.md).

---

## What it depends on (other programs)

| Provider | What we consume |
|---|---|
| `world-contract` | `SectorView` / `LaneView` / `ForceView` / `SlotView`, `adaptWorldState` (no wire `Revision` on state) |
| `world-render` (pure half) | `sectorChannels`, `laneChannels`, `fogTreatments`, `slotSilhouettes` — **not** `SectorNode.tsx` or React overlays on the stage |
| `world-shell` | `StageHost`, no page scroll, Esc/right-click, corner HUD mounts — **not** the SVG camera |
| `world-hud` / `world-inspector` (left dock) / `world-turn` / `world-lenses` (picker) / `world-targeting` (pure `worldSelection`) | Chrome and command UI. Lens *drawing*, route *drawing*, supply/lifeline *drawing* move onto the canvas |
| `world-numbers` | Net loam chip at detail zoom |
| DPLP / [fe-game-foundation.md](fe-game-foundation.md) | Dual-plane locks, generation-scoped bus, destroy checklist, RT mapping, "no real `Phaser.Game` in unit tests" |

---

## What this map does not contain

- A second Phaser.Game while the lawn stage is current (GG-1, D2).
- A Phaser HUD / UIScene (React already owns chrome).
- A `WorldBootScene` in v1 (placeholders live in `WorldMapScene.create`).
- `@xyflow/react` on the player map.
- Minimap on `small` / `medium`.
- Turn-engine or wire-shape changes (including adding `Revision` to `WorldStateDto`).
- A new GG-50 collection-surface row (outliner already covers volume).

---

## Assumptions — correct these now

1. Plate 11 **§O** (circle pin + ownership ring, unknown = diamond, strict-superset LOD) is the visual
   contract this spec implements. If §O's three drawing calls are rejected, stop and revise the plate
   before code.
2. Phaser **4.2.1** in this repo is **Scenes + GameObjects + lite systems**, matching
   `LawnWorldScene.ts` — not a greenfield ECS framework.
3. `world:*` events live beside `lawn:*` in `game/EventBus.ts`; they do not join the `LawnBusEvent`
   union.
4. Arrow-key pan is **React → bus → Phaser** so GG-18 still holds when a layer owns input. Pointer
   drag, edge-scroll, wheel, and pin click are Phaser. `W` is not pan.
5. T3's *xyflow off the entry chunk* survives; T3's *therefore SVG hook* is amended in `decisions.md`
   / `tech-stack.md` as a follow-up doc task, not a silent rewrite.
6. **Import law copies the lawn:** `game/world` may import Phaser-free channel modules under
   `stages/world/render/`; it must not import React, `lib/bus`, or `*Dto`.
7. **Dirty flag is host `modelSeq`**, not `WorldHeaderDto.revision` and not a new wire field.
8. **One Phaser camera** owns pins, lanes, forces, fog-on-pin, and every map-plane overlay.
9. **Inspector docks left** (`spec-world-inspector` §8e.1); pick ignores that rectangle + the 92px
   rail. Plate §O's right-side composed mock does not override the dock.
10. **Fog-on-pin** drops density (no forces strip on the disc); identity of the four intel states
    survives. Owner sign-off on that table is part of this review.

---

## Open questions

1. Plate §O three drawing calls — assumed; overturn on the plate first.
2. T3 HOW sentence in `tech-stack.md` / `decisions.md` — doc follow-up, Ask-first before editing.
3. Later slice: move channels to `src/lib/world-view/` so `game/` never imports `stages/` — not v1.

Detail and locks live in the [module spec](world-map-runtime/spec-world-map-runtime.md).
