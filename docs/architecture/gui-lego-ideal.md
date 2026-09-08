# GUI Lego — the ideal

**Status:** **Binding design standard** for new and refactored player menus (band-2 bodies,
ActorSheet tabs, filter/inspect panels). **No React implementation** of Derived until owner
accepts hardened piece/recipe contracts + assembled surface.  
**Program id:** `gui-lego`  
**Map:** [gui-lego-map.md](gui-lego-map.md)  
**Authoring:** [gui-lego-authoring.md](gui-lego-authoring.md)  
**Design index:** [../design/gui-lego/README.md](../design/gui-lego/README.md)  
**Module specs:** [gui-lego/](gui-lego/)  
**Queue:** [gui-lego/menu-refactor-queue.md](gui-lego/menu-refactor-queue.md)  
**Tasks:** [gui-lego-plan.md](../../tasks/gui-lego-plan.md) ·
[gui-lego-todo.md](../../tasks/gui-lego-todo.md)

**Decision:** `decisions.md` **GUI Lego — menu composition (2026-09-09)**.  
**DESIGN-GATE:** UI row + **Player menus** row.

**Inputs already locked (do not reopen):**

- Dual-plane (DPLP): React chrome / pure fold / Phaser island — Phaser never owns HTTP/SignalR
  ([fe-game-foundation.md](fe-game-foundation.md)).
- Stage + layers (GG-1 / GG-5 / GG-11): menus open over the stage; band-2 hosts ActorSheet
  ([game-gui-principles.md](game-gui-principles.md),
  [design/information-architecture.md](../design/information-architecture.md)).
- ERM: screens are compositions of domain entity representations
  ([design/README.md](../design/README.md) §2).
- HTML port discipline after acceptance
  ([html-design-implementation.md](html-design-implementation.md)).
- ActorSheet shell / Derived cook IA remain owned by `actor-sheet`
  ([actor-sheet-map.md](actor-sheet-map.md)); this program designs the **composable kit**, not a
  second sheet.

---

## Which loop this extends

Not a new product loop. It is how **player menu chrome** is authored so Derived (and later other
band-2 surfaces) stop shipping as one-off ports.

| Loop / place | Role of gui-lego |
|---|---|
| Lawn / world / sanctum stages | Unchanged — pieces mount as **layers** over the stage |
| ActorSheet → Derived | First vertical slice (combat console Lego set) |
| Condition / Creatures filters | Forward reuse of chrome + lifecycle pieces |

---

## Load-bearing principles

1. **RPG features live in the RPG layer.** Menu Lego presents Hub/catalog data; it does not invent
   combat math or Unity fields.
2. **MVVM over DPLP.** Model = Hub/API/catalogs; ViewModel = pure fold; View = pieces; Commands =
   closed surface bus events (local VM or Intent).
3. **ERM is the density ladder.** Pieces declare Token / Chip / Row / Card / Panel-slice, or
   chrome / layout / gauge / lifecycle — never a private third ladder.
4. **Three registries.** Piece, theme, recipe. A surface is recipe + fold + bus — never a god TSX.
5. **Theme packs own paint.** Pieces declare slots; packs supply `css` (token vars) and `paint`
   (resolved hex for SVG). No hard-coded `--el-fire` inside piece markup.
6. **No piece fetch.** No SignalR inside a piece. No SVG `fill="var(--token)"` without a paint hex.
7. **Host is ActorPanel band-2.** Do not fork PanelShell. Dual identity (panel header +
   `identity-hd`) is an embed delta, not a defect.
8. **Buy-before-build adapters.** Lucide / recharts / motion sit behind piece payloads — payload
   stable, adapter swappable ([tech-stack.md](../design/tech-stack.md)).

---

## Phaser → menu Lego

| Phaser kernel | Menu Lego | Notes |
|---|---|---|
| Scene | Surface | One composed menu; owned recipe |
| System | Fold pipeline | Ordered Hub/API → VM slices |
| Registry | Piece + theme + recipe registries | Three boxes, not one mega-map |
| GameObject | Piece | Payload in; bus events out |
| EventBus | Surface bus | Closed catalog per surface |
| FX pool | Vfx binder | `vfxId` → CSS/`motion`; GG-32 |
| `createGame` | `bindSurface(recipe, vm, themes)` | Resolve themeRefs; validate |

This is an **analogue**, not a Phaser dependency. Menu HTTP stays React/host; Phaser stays canvas.

---

## MVVM rules

| Layer | Owns | Must not own |
|---|---|---|
| Model | `/sheet`, cook catalogs, theme JSON, Pending | Presentation |
| ViewModel | `fold*SurfaceVm` → payloads by `instanceId`, selection, `revision` | React, fetch, hex literals |
| View | Recipe mount + pieces | Fold, fetch, element color literals |
| Commands | Bus → VM selection or host Intent | Direct DTO mutation in the piece |

---

## Composition grammar (summary)

Full contract: [gui-lego/spec-composition.md](gui-lego/spec-composition.md).

- Parents expose **named slots**. Children fill slots — no illicit wrappers under CSS `>`.
- **Recipes** are JSON trees (`docs/design/gui-lego/recipes/`). Changing IA = edit recipe.
- Every mount has stable **`instanceId`**. Selection is surface-level `selectedInstanceId`.

---

## Theme taxonomy

| kind | Example ids | Consumers |
|---|---|---|
| `element` | omni, fire, ice, air, earth, light, dark | chips, rows when expand=element |
| `status-category` | omni, dot, cc, contagion | status rails / rows |
| `resource` | hp, stamina, … | resource rails |
| `action-category` | attack, defense, … | Other rail |
| `rarity` | (item ladder) | future Relics |
| `side` | plant, zombie | identity / shell tint |
| `cook-tab` | elements, status, resources, other | primary rail |
| `neutral` | — | shell, search, foot |

**Resolver precedence:** explicit `themeRef` on payload > inferred from channel/variant > `neutral`.

**Pack channels:**

- `css` — CSS custom properties on piece root (extend `_kit` / `src/theme`, GG-29).
- `paint` — resolved hex for SVG/canvas (fixes black-donut class).
- `vfx` — ids only; binder maps; reduced motion → instant (GG-31/32).

Glyphs: `glyphRef` → catalog `hudToken` / `icon` / CatalogIcon — fallback text, never `idWords`
(GG-58).

---

## Closed bus catalog — Derived surface v1

| Event | Payload | Sink |
|---|---|---|
| `derived.search.set` | `{ query: string }` | VM |
| `derived.showUnchanged.set` | `{ value: boolean }` | VM + localStorage policy |
| `derived.tab.set` | `{ tabId: string }` | VM |
| `derived.variant.set` | `{ variantId: string }` | VM |
| `derived.channel.select` | `{ channelId: string }` | VM |
| `derived.retry` | `{}` | Host Intent / refetch |

Expanding this list is a reviewed ideal change.

---

## Six render states (unchanged SSOT)

`active | default | capped | stub | no-producer | unregistered` live on **payload**
(`channel-row.state`, inspect). Pieces style via theme + state class. They do **not** re-derive caps.
Do not invent a third classification
([design/spec-derived-stat-sheet.md](../design/spec-derived-stat-sheet.md)).

---

## ViewModel fold

Contract: [gui-lego/spec-derived-surface-vm.md](gui-lego/spec-derived-surface-vm.md).

`foldDerivedSurfaceVm(input) → DerivedSurfaceVm` — pure; formats magnitudes (GG-46); maps Pending
to lifecycle payloads; never invents numbers.

---

## Per-piece contract checklist

Every `spec-<piece-id>.md` + HTML draft must include:

1. ERM kind + rung  
2. Structure (landmark tree; `>` ancestors)  
3. Payload schema (`phase`: ready|loading|empty|error|pending)  
4. Theme slots  
5. Data flow (bind in; bus out)  
6. Focus (GG-19)  
7. Motion / reduced-motion (GG-31/32)  
8. Magnitude: `valueRaw` + `valueText` (VM owns format)  
9. Empty/error child  
10. ≥2 sample themes in the HTML page where themeable  

---

## Explicit bans

- CV-only Derived CSS/SVG patches as the product strategy.
- Hard-coded element/status colors or VFX inside pieces.
- Piece-level fetch / SignalR.
- God surface TSX that bypasses recipe.
- Expanding all nine IA layers before Derived Lego acceptance.
- Replacing ERM with a private density system.
- SVG `fill="var(--token)"` without a **paint** hex channel.
- Forking PanelShell / ActorPanel as a second sheet host.

---

## Relationship to stop-gap Derived

`web/fusion-rpg-web/src/ui/actor/derived/*` is stop-gap. After owner acceptance, a later
implementation stream composes recipes — it does not keep patching the port.

Visual reference for the first surface remains
[derived-combat-console.html](../design/derived-combat-console.html) until Lego drafts supersede
regions piece-by-piece.

---

## Owner review gates

1. This ideal + [gui-lego-map.md](gui-lego-map.md)  
2. Composition + [recipes/derived-console.json](../design/gui-lego/recipes/derived-console.json)  
3. Piece index + reuse matrix  
4. VM fold contract  
5. HTML drafts: theme swap (fire↔ice) + lifecycle states  
6. Owner accepts or rejects boundaries  
7. **Then** separate React stream  

---

## DESIGN-GATE §5 checklist (this document)

- [x] UI row docs cited (GG, IA, design README ERM, DPLP, actor-sheet, html-design-implementation)
- [x] Stats/derived six-state SSOT respected (no third classification)
- [x] No Phaser owning menu HTTP; no second sim
- [x] Honest: design-only; React not authorized
- [x] Caps/tunables: N/A (presentation kit; no progression ceilings)
