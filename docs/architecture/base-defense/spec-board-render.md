# Spec: `board-render`

**Module 16 of 21 · level 8 · depends on `siege-board` · [base-defense-map.md](../base-defense-map.md)**
**Status:** spec, 2026-09-04.
**Largest single module in the program.** Measured: `src/stages/world` is **6,902 LOC**, the Phaser
island under `src/game` is the second-largest. Budget this at world-stage scale, not as a reuse.

---

## Objective

**Extract a generic grid-rendering layer from the lawn's Phaser island, so a siege board can be drawn
without cloning it.**

The FE has exactly one Phaser integration and it is lawn-shaped throughout: `createLawnGame`,
`LawnWorldScene`, `PtrEntityRegistry` keyed on lawn ptr semantics, and lawn geometry baked into the
scene rather than passed in.

**Success looks like:** one board layer that both the lawn and the siege configure differently, and a
lawn that renders byte-identically after the extraction.

---

## What already exists (verified at HEAD, 2026-09-04)

**Built.**

- `src/game/createLawnGame.ts` — game construction, **lawn-specific by name and by content**.
- `src/game/scenes/LawnWorldScene.ts`, `BootScene.ts`.
- `src/game/entities/PtrEntityRegistry.ts` — entity registry, keyed on **lawn ptr** semantics.
- `src/game/systems/PickSystem.ts` — picking, against lawn geometry.
- `src/game/systems/SyncFromModelSystem.ts`, `ActorHudDisplay.ts`, `fx/FxPool.ts`.
- `src/stages/world/` — 6,902 LOC across `hud/`, `inspector/`, `playback/`, `render/`, `targeting/`.
  **The subdirectory shape to copy** — it is the repo's own worked answer to "how is a stage
  organised".
- Three stages built: `lawn`, `sanctum`, `world`.
- `src/shell/railState.ts:31` — `currentStageId: "sanctum" | "world" | "lawn" | "battle"`. **`battle`
  is declared and unbuilt**, which Gate 0 confirmed and which the `decisions.md` amendment names as
  its own third cost.

**Real gap.** No generic board layer. Every piece above assumes the lawn.

---

## The contract

### 1. Five extractions, each with a "byte-identical lawn" acceptance

| From | To | The change |
|---|---|---|
| `createLawnGame.ts` | `createGame({ scenes, width, height })` | scenes are **injected**, not imported. `createLawnGame` becomes a thin caller |
| `LawnWorldScene` geometry | `GridSpec` **passed in** | rows/cols/cell size are constructor input, never imported constants |
| `PtrEntityRegistry` | `EntityRegistry<TKey>` | generic over key type; the lawn keeps ptr keys, the siege uses actor keys |
| kind → sprite | a **caller-supplied mapping** | the layer knows nothing about demons, plants, zombies or walls |
| `PickSystem` | `pickCell(spec, pointer) → GridPos \| null` | pure function of spec and pointer |

**Each extraction lands with the lawn rendering byte-identically.** This is five reversible steps, not
one large refactor — and it is why this module is one module rather than five: the extractions share a
single acceptance criterion and would otherwise each need their own.

### 2. The camera bridge — the one genuinely new piece

There is a pure `Camera` model on one side and a Phaser camera on the other. The bridge is
bidirectional and is where a naive implementation creates a feedback loop:

```ts
/**
 * Binds the pure Camera model to a Phaser camera.
 *
 * The hazard is a loop: model → phaser → change event → model → phaser. Broken by making the model
 * authoritative and treating Phaser's camera as a pure output — Phaser never writes back. Input
 * (drag, wheel, pinch) is read from the POINTER and applied to the model, which then drives Phaser.
 * One direction of authority, always.
 */
export function bindCamera(model: Camera, cam: Phaser.Cameras.Scene2D.Camera): () => void;
```

The returned function is the unbind. **Not optional** — a scene torn down without unbinding leaks a
listener per siege, and a player who opens ten sieges in a session has ten live cameras.

### 3. Rendering the board

Four layers, drawn in a fixed order so a structure never hides a unit:

```
terrain → structures → units → overlays (range, path preview, cover)
```

**Terrain is drawn once to a render texture and cached**, invalidated only when the `GridSpec`
changes — which `district-layout`'s stability contract (S1–S4) guarantees is rare. Redrawing a static
grid per frame is the obvious first implementation and it is the one that makes a large board stutter.

### 4. Structures render as structures

`combatant-kind`'s discriminator reaches the FE through the siege view model. A structure shows an
**HP bar and no initiative marker** — it never appears in the turn order, so a turn-order UI element
on it would be a lie the player then has to unlearn.

### 5. Accessibility and the shell rules apply

`game-gui-principles.md`'s GG rules bind here as everywhere:

- Keyboard cell navigation (arrows + confirm), not mouse-only.
- Every board action reachable without a pointer.
- The board is **stage band**, so HUD and panels layer over it and Esc pops one layer.
- Respect `prefers-reduced-motion` for camera transitions.

### 6. Entry-chunk budget

`decisions.md`'s Game GUI row fixes an entry-chunk budget of **≤180 KB gz** (against 713 KB today).
**The board layer must be lazily loaded** with the siege stage, not pulled into the entry chunk.

Phaser is large. If the siege stage is statically imported anywhere reachable from the entry point,
this module regresses a budget the GUI program has already committed to — a cross-program cost, and
the kind that is discovered late.

---

## Tunables

FE presentation constants (cell pixel size, camera limits, animation durations) are **not** game
balance and do not belong in `data/tuning/`. They live in a module-local constants file, which is
[tunables-ssot.md](../tunables-ssot.md)'s own distinction: a number a *balance* pass would change is a
tunable; a number a *visual design* pass would change is not.

## Numeric types

TypeScript. Cell coordinates and indices are **integers** — assert with `Number.isInteger` at the
board boundary. Pixel positions may be fractional (they are presentation only and never feed back into
a game decision), but a fractional *cell* coordinate is a bug and must throw rather than round.

## Boundaries

**Always:** pass `GridSpec` in, never import it · lawn byte-identical after every extraction step ·
unbind the camera on teardown · lazy-load the board layer · keyboard parity.

**Ask first:** replacing Phaser · changing `PtrEntityRegistry`'s public shape (the lawn depends on it).

**Never:** import lawn constants into the generic layer · let Phaser's camera write back to the model ·
redraw static terrain per frame · statically import the siege stage from the entry chunk · give a
structure an initiative marker.

---

## Testing

`web/fusion-rpg-web/src/**/*.test.ts(x)`, plus the existing Playwright setup.

| Test | Asserts |
|---|---|
| `Lawn_renders_identically_after_each_extraction` | **the gate**, five times — one per extraction step |
| `createGame_accepts_injected_scenes` | |
| `Grid_spec_is_passed_not_imported` | source scan: the generic layer imports no lawn module |
| `Entity_registry_is_generic_over_key` | ptr keys and actor keys both |
| `pickCell_maps_pointer_to_cell` | including out-of-bounds → `null` |
| `Camera_bridge_does_not_loop` | drive the model, assert exactly one Phaser write |
| `Camera_unbind_removes_every_listener` | the leak |
| `Terrain_texture_is_cached_and_invalidated_on_spec_change` | not per frame |
| `Layers_draw_in_order` | a structure never occludes a unit |
| `Structures_show_hp_and_no_initiative_marker` | |
| `Keyboard_navigation_reaches_every_cell` | GG accessibility |
| `Reduced_motion_is_respected` | |
| `Board_layer_is_not_in_the_entry_chunk` | **a bundle-size assertion**, so the 180 KB budget cannot regress silently |
| `Fractional_cell_coordinate_throws` | |

## Success criteria

1. The lawn renders byte-identically after all five extractions.
2. The generic layer imports nothing lawn-specific — proven by scan.
3. The camera bridge has no feedback loop and unbinds cleanly.
4. Terrain is cached.
5. Full keyboard parity.
6. The board layer is lazily loaded and the entry chunk is unchanged — asserted by a bundle test.

## Open questions

**One, and it is a scoping decision the owner should make.** `railState.ts:31` already declares a
`battle` stage that has never been built. Does `board-render` serve **both** `battle` and `siege`, or
only `siege`?

Serving both would retire a declared-but-unbuilt stage id and is architecturally cleaner — the
amendment's own third cost is precisely that approving `siege` leaves *two* declared-and-unbuilt ids
rather than one.

**Recommendation: build for `siege` only, but keep the layer generic enough that `battle` can adopt it
later.** `battle` has no spec, no owner and no timeline; designing this module against a stage nobody
has specified means guessing at requirements twice over. Generic-but-unused is free here because the
layer is generic anyway — that is the module's whole point.
