# Plan: lawn-interactive program

Source: [lawn-interactive-map.md](../docs/architecture/lawn-interactive-map.md) · Design landing:
[spec-lawn-interactive.md](../docs/design/spec-lawn-interactive.md) · Specs under
[lawn-interactive/](../docs/architecture/lawn-interactive/).

Task list: [lawn-interactive-todo.md](lawn-interactive-todo.md). Prefixed pair only — never
`tasks/plan.md` / `tasks/todo.md`.

**Status:** PLAN written 2026-09-07 — awaiting owner review before IMPLEMENT. Do not start T0 until
this plan is accepted.

---

## Overview

Player-facing lawn stage chrome: shared `ActorCollection`, occupant adapt, match HUD, Phaser cell
stacks, left occupancy dock, unique spawn tray, and off-board commander combat book — so a mid-wave
player can monitor stacks, inspect, field uniques, and enqueue orders without leaving the lawn
(GG-1 / GG-11). Observe + Intent only; no PvZ field rewrite.

**Seven specs → thirteen tasks · 5 checkpoints.** Vertical slices; FE/Phaser only except Intent
enqueue paths that already exist.

---

## Spec coverage (every module has tasks)

| Module id | Spec | Tasks |
|---|---|---|
| `actor-collection` | [spec-actor-collection.md](../docs/architecture/lawn-interactive/spec-actor-collection.md) | T1, T10, T11 |
| `lawn-occupant-adapt` | [spec-lawn-occupant-adapt.md](../docs/architecture/lawn-interactive/spec-lawn-occupant-adapt.md) | T2 |
| `lawn-match-hud` | [spec-lawn-match-hud.md](../docs/architecture/lawn-interactive/spec-lawn-match-hud.md) | T4 |
| `cell-stack` | [spec-cell-stack.md](../docs/architecture/lawn-interactive/spec-cell-stack.md) | T3 |
| `cell-occupancy-dock` | [spec-cell-occupancy-dock.md](../docs/architecture/lawn-interactive/spec-cell-occupancy-dock.md) | T5, T6 |
| `spawn-tray` | [spec-spawn-tray.md](../docs/architecture/lawn-interactive/spec-spawn-tray.md) | T7 |
| `commander-action-bar` | [spec-commander-action-bar.md](../docs/architecture/lawn-interactive/spec-commander-action-bar.md) | T8, T9 |

Cross-cutting InteractionMode / mute / Esc precedence: T5 (dock mute), T7 (spawn exception), T8
(full table). HUD → dock (actor-hud sibling): T12. Design §10 focus/safe-area/reduced-motion: **T13**.

### Design-landing idea → task matrix

| Design § | Idea | Tasks |
|---|---|---|
| §2.2 / §4 | ActorCollection | T1, T10, T11 |
| §4.5 | Occupant adapt | T2 |
| §5 | CellStack + tile confirm | T3, T5, T8 |
| §8 | LawnMatchHud (sun, wave/**clock**/phase, connection, Field, …) | T4 |
| §6 | SpawnTray / no typeId | T7 (+ Field from T4) |
| §7 / §10.1 | Commander book + ActionTargeting | T8, T9 |
| §9 | Stage bands / left dock | T5, T6 |
| §10 | Focus, safe area, GG-36, reduced motion | **T13** |
| §11 | Retire player KeyValue / typeId inspect dump | **T6** |
| plate 10 / HUD | Click → dock | T12 |
| §12 | Presentation tokens | T0 |

---

## Architecture decisions

1. **One collection widget** — Creatures / dock / tray / scope picker consume `ActorCollection`; no
   private list forks (GG-9).
2. **Adapt before rows** — never feed raw `LawnViewModel` into `ActorRow`; generals never in `?sel=`.
3. **Left reserved dock** — camera shrinks; all **12** columns stay visible (not right overlay).
4. **Phaser owns stack paint** — React does not duplicate sprites (`cell-stack`).
5. **Intent only** — spawn and orders ack immediately; FE is not an A10 range oracle; do not say
   "cast."
6. **Combat book ≠ adventure book** — lawn bar is HoMM3 combat-hero pattern; world-map Orders stay
   elsewhere. Commander never drawn as a tile.
7. **`pvz.*` sun bank on match HUD only** — actor `hunger` stays on ActorSheet Condition.
8. **No `#/actor/:id`** — `?cell=` + optional `?sel=<instanceId>` for Bound uniques only.

---

## Dependency order

```text
T0 tokens
T1 ActorCollection ──┬── T4 LawnMatchHud
                     ├── T7 SpawnTray
                     └── T5 Dock ── T6 sheet/URL
T2 adapt ────────────┘
T3 CellStack                 (parallel after T0; needs projector layout)
T8 ActionTargeting/mute ── T9 CommanderActionBar
T10 Creatures consume · T11 scope picker · T12 HUD→dock
T13 focus / safe-area / reduced-motion (after T5+T9 chrome exists)
```

---

## External deps — reversible defaults (not hard gates)

Per planning skill “Gates vs. checkpoints”: do **not** freeze the lawn plan on ActorSheet catalog
approval or Dave→unique Commander Vocabulary.

| Dependency | Default while sibling unfinished | Later pass |
|---|---|---|
| ActorSheet eight-tab catalog era | Dock/sheet push opens **existing** `ActorPanel` / `ActorSheet` export with honest pending chrome | Re-bind when actor-sheet IMPLEMENT unlocks |
| Commander identity | **Crazy Dave** (`CommanderId`) — one bar | Vocabulary promotion → unique leaves spawn tray |
| Action corpus empty | Locked-visible 1–9 slots with corpus reason (GG-44) | Fill when corpus authors actions |
| actor-hud click → dock | T12 after dock exists; Band B tokens stay actor-hud | — |

Irreversible none for this FE chrome program. Dual-load / game binary patches remain forbidden by
repo hard boundaries (not lawn-specific gates).

---

## Tasks

### T0: Presentation tokens (dock / stack / collection page)

**Description:** Land FE presentation tokens for dock width, cell-stack offset px, max sprites
before `+K` (structural draw cap — comment), and collection page size inside 25–240. Not Core Policy.

**Acceptance criteria:**
- [ ] Tokens documented once; stack `+K` N commented as structural, not progression ceiling
- [ ] No balance numbers moved into Core

**Verification:** Token file / CSS vars present; unit or snapshot of defaults.

**Dependencies:** None · **Scope:** S · **Files:** `web/.../ui/tokens` or lawn presentation module;
spec §12 candidates.

---

### T1: `ActorCollection` widget (`actor-collection`)

**Description:** Shared list/grid over Row/Card with GG-50 volume and GG-51 query; Fielded/Wave chip
slot; optional HP sliver prop.

**Acceptance criteria:**
- [ ] Densities list|grid only; ≤24 all / 25–240 window / >240 search-first
- [ ] Query API stable for dock session reopen
- [ ] No third card shape; no `typeId` chrome

**Verification:** `npm test -- --run src/ui/actor/ActorCollection`

**Dependencies:** T0 (page size token optional) · **Scope:** M · **Files:**
`web/.../ui/actor/ActorCollection.tsx` (+ test); compose `ActorRow` / `ActorCard`.

---

### T2: Occupant → row adapt (`lawn-occupant-adapt`)

**Description:** Pure map from living lawn occupants to collection/sheet bind props; session observe
handle for generals; Bound keeps `instanceId`.

**Acceptance criteria:**
- [ ] Unique Bound → `instanceId` + Fielded; general → Wave, no `instanceId`
- [ ] No `ptr` in player-visible fields; URL helper refuses general `sel`
- [ ] Handle invalidates when occupant leaves/dies

**Verification:** `npm test -- --run adaptOccupant` (or landed path)

**Dependencies:** None (parallel with T1) · **Scope:** S · **Files:**
`web/.../ui/lawn/adaptOccupant.ts` (+ test).

---

### Checkpoint A — foundation list

- [ ] T1 + T2 tests green; `npm run build` clean for touched packages
- [ ] Owner skim: volume policy + Fielded/Wave + no general `sel`
- [ ] Review before dock/tray consume the widget

---

### T3: Phaser cell stack paint (`cell-stack`)

**Description:** Stable overlap offsets in `layoutGrid` / LawnWorldScene; `+K` pip; Bound unique pip
under stack; topmost keeps actor-hud; keyboard confirm on focused cell opens **tile** dock path
(retarget inspect confirm from occupant-only to tile — GG-21).

**Acceptance criteria:**
- [ ] Phaser-only sprites; no React name overlay on cells
- [ ] N + `+K` correct; unique pip when Bound under stack
- [ ] Structural N commented
- [ ] Inspect keyboard confirm opens dock for the **tile** (full list), not topmost-only

**Verification:** Focused lawn/layout tests; manual fixture stack if needed

**Dependencies:** T0 · **Scope:** M · **Files:** `LayoutGridSystem.ts`, `LawnWorldScene.ts`,
`PickSystem.ts` as needed.

---

### T4: Match strip (`lawn-match-hud`)

**Description:** Band-1 top strip: `pvz.*` sun, wave/**clock**/phase, commander + aura chip, Bound
unique `ActorChip`s, **connection**, transport, selection coords, **Field CTA** → SpawnTray;
omit/lock missing stocks. Chip SSOT:
[spec-lawn-hud-chip.md](../docs/architecture/commander-surface/spec-lawn-hud-chip.md).

**Acceptance criteria:**
- [ ] Sun bank never labeled as actor hunger
- [ ] Deployed chips = Bound uniques only (ActorChip)
- [ ] Wave clock cluster when observe provides it; omitted when silent
- [ ] Connection cluster honest when Fusion optional
- [ ] Field CTA opens SpawnTray (no `typeId`)
- [ ] No invented second sun / fake aura strings

**Verification:** `npm test -- --run LawnMatchHud`

**Dependencies:** T1 (chips) · **Scope:** M · **Files:** `web/.../ui/lawn/LawnMatchHud.tsx`;
wire into lawn stage / page.

---

### Checkpoint B — monitor without click

- [ ] Stack visible without click; match strip honest stocks (incl. Field + connection)
- [ ] Tests for T3–T4 green
- [ ] Review before dock/spawn chrome

---

### T5: Cell occupancy dock + camera inset (`cell-occupancy-dock`)

**Description:** Band-2 reserved **left** column; camera shrinks for 12 columns; rows = T2→T1;
cell click always opens list (count=1 included); tile confirm opens dock; GG-18 mute joins
dock/sheet; no Unity pause.

**Acceptance criteria:**
- [ ] Left reserved token; not right overlay
- [ ] Count=1 still shows collection; query persists across sheet push, resets on cell change
- [ ] `isLawnKeyboardMuted` true when dock/sheet open
- [ ] Tile confirm / cell click opens dock (GG-21)

**Verification:** Dock unit tests; mute test; layout note for 12-col (Playwright later OK)

**Dependencies:** T1, T2, T0 · **Scope:** M · **Files:** `CellOccupancyDock.tsx`, `focusGate.ts`,
lawn stage composition, camera inset.

---

### T6: Sheet push + URL + retire player KeyValue (`cell-occupancy-dock` + actor-sheet default)

**Description:** Row select → push ActorSheet (GG-10). URL `?cell=` + `?sel=<instanceId>` Bound
only. **Default:** existing `ActorPanel` / export until actor-sheet catalog era unlocks. **Retire
player-path** KeyValue / engine-field dump on lawn inspect (design §11); GG-41 developer inspector
may remain on a developer tree.

**Acceptance criteria:**
- [ ] Esc pops sheet then dock; no `#/actor/:id`
- [ ] Generals inspect in-memory only
- [ ] Stage Phaser Game stays mounted
- [ ] Player inspect path no longer dumps KeyValue engine fields as the primary sheet substitute

**Verification:** Dock/sheet integration tests; URL encode/decode unit tests; grep LawnPage player
path for retired KeyValue inspect chrome

**Dependencies:** T5 · **Scope:** M · **Files:** LawnPage / stage URL helpers, ActorSheet open path.

---

### Checkpoint C — inspect stack

- [ ] Mid-wave: open left dock, drill Bound unique, Wave locks honest
- [ ] 12 columns still the acceptance target (manual or Playwright — non-blocking follow-up OK)
- [ ] Player KeyValue inspect dump retired (T6)
- [ ] Review before spawn/order

---

### T7: Spawn tray — no player `typeId` (`spawn-tray`)

**Description:** Band-2 Field tray via ActorCollection (grid default) → `enterSpawnTargeting` →
Intent; retire player-path numeric `typeId` on LawnPage; spawn targeting keeps board arrows
(GG-18 exception); confirm does not open dock. Opened from match HUD Field (T4) or with cell selected.

**Acceptance criteria:**
- [ ] Zero numeric `typeId` fields on player spawn tray
- [ ] Illegal/Bound disabled with reason (GG-55)
- [ ] Ghost + Intent path reused; reject clears ghost + toast

**Verification:** `npm test -- --run SpawnTray`; grep player tray for typeId input gone

**Dependencies:** T1, T5 composition (shared column); T4 Field CTA · **Scope:** M · **Files:**
`SpawnTray.tsx`, `LawnPage.tsx`, `interactionMode.ts` as needed.

---

### Checkpoint D — field a unique

- [ ] Field unique via collection → ghost → Intent without typing typeId
- [ ] T7 tests green
- [ ] Review before combat book

---

### T8: InteractionMode — ActionTargeting + Esc/Space (`commander-action-bar` foundation)

**Description:** Add `ActionTargeting`; Space = pause (not confirm); Enter confirms; Esc cancels
armed order before dock/sheet pop; occupied click does not `selectOccupant` into inspect while
order armed; dock/sheet mute already from T5; inspect Enter opens **tile** dock (with T3/T5).

**Acceptance criteria:**
- [ ] Mode table matches design landing §10.1
- [ ] SpawnTargeting still keeps board arrows
- [ ] Inspect Enter → tile dock; ActionTargeting click does not clobber into inspect
- [ ] No enqueue-from-sheet in v1

**Verification:** `npm test -- --run interactionMode` (+ focusGate if touched)

**Dependencies:** T5 mute baseline · **Scope:** M · **Files:** `interactionMode.ts`,
`interactionMode.test.ts`, keyboard wiring in `LawnWorldScene` / LawnPage.

---

### T9: Commander action bar UI (`commander-action-bar`)

**Description:** Band-1 bottom-center 1–9 `ActionSlot` + cost cluster + refusal; empty corpus =
locked-visible; costs name stock (`pvz.*` or actor pools); Intent enqueue; no Ward slot name; Dave
v1 one bar.

**Acceptance criteria:**
- [ ] No fake Strike/Firebolt when corpus empty
- [ ] Unaffordable shows reason; FE does not claim range legality
- [ ] Commander never drawn on a lawn tile

**Verification:** `npm test -- --run CommanderActionBar`

**Dependencies:** T8 · **Scope:** M · **Files:** `CommanderActionBar.tsx`; compose action-layer
slots; stage Band 1.

---

### Checkpoint E — combat book

- [ ] Arm slot → target → Intent; Esc cancels order first
- [ ] T8–T9 tests green
- [ ] Review before consumer polish

---

### T10: CreaturesLayer consumes `ActorCollection` (`actor-collection` consumer)

**Description:** Creatures master-detail uses the shared widget; keep 24/240 policy in one place
(widget), not a second copy.

**Acceptance criteria:**
- [ ] Single import of ActorCollection; no forked VirtualCreatureList policy
- [ ] Cold roster open sheet still works

**Verification:** CreaturesLayer tests updated/green

**Dependencies:** T1 · **Scope:** M · **Files:** `CreaturesLayer.tsx` (+ test).

---

### T11: Scope picker body → `ActorCollection` (`actor-collection` consumer)

**Description:** Replace `ActorListPickerPanel` private list with ActorCollection; never pass
`instanceId` as `targetPtr`.

**Acceptance criteria:**
- [ ] Picker uses shared widget; no instanceId-as-ptr
- [ ] Grid/list densities available as needed

**Verification:** Picker tests

**Dependencies:** T1 · **Scope:** S · **Files:** ActorListPickerPanel (or successor).

---

### T12: Per-unit HUD click → occupancy dock (sibling actor-hud)

**Description:** Band B click opens dock for that cell (not a third HUD). Tokens stay actor-hud
program.

**Acceptance criteria:**
- [ ] Click HUD → dock with that tile’s occupants
- [ ] No second inspect HUD invented

**Verification:** Lawn stage / HUD click test or documented wire

**Dependencies:** T5 · **Scope:** S · **Files:** actor-hud click handler → lawn select/dock open.

---

### T13: Stage chrome — focus, safe area, reduced motion (design §10)

**Description:** Apply design §10 contracts across Band 1–2 chrome already landed: GG-36 floor
viewports, safe-area inset on match HUD + action bar, focus landing (dock → first row; sheet →
declared stop; order targeting → cell), reduced motion = instant docks (GG-32) while keeping M9 press
ack. Does not invent new panels.

**Acceptance criteria:**
- [ ] Match HUD and commander bar respect safe-area inset
- [ ] Dock open focuses first collection row; sheet open focuses declared landing stop
- [ ] `prefers-reduced-motion` → instant dock/sheet (no ornamental slide); ack feedback remains
- [ ] Documented GG-36 reference sizes; no absolute-pixel-only stage chrome

**Verification:** Unit/focus tests where practical; manual pass at 1280×720

**Dependencies:** T5, T9 (chrome present); T4 strip · **Scope:** M · **Files:** lawn stage layout,
`LawnMatchHud`, `CommanderActionBar`, `CellOccupancyDock`, focus helpers.

---

### Checkpoint F — program acceptance

- [ ] Map acceptance paragraph satisfied (stack, dock, sheet Bound, spawn no typeId, one order)
- [ ] All T0–T13 acceptance criteria checked
- [ ] No `#/actor/:id`; commander not a tile; generals Wave-locked
- [ ] Owner playtest mid-wave (Fusion optional for observe; Intent needs injector)

---

## Out of scope

- ActorSheet tab bodies / runtime catalogs ([actor-sheet-plan.md](actor-sheet-plan.md))
- Band B token layout / VFX (actor-hud / vfx)
- Action corpus authoring; FE A10 range
- Promoting Crazy Dave → unique Commander (non-blocking follow-up)
- Siege / world-map adventure book
- Rewriting PvZ Plant fields
- Pixel-golden lawn paint

---

## Risks

| Risk | Impact | Mitigation |
|---|---|---|
| Dock hides spawn columns | High | Left reserve + camera shrink; 12-col checkpoint |
| ActorSheet unfinished blocks dock | Med | T6 default = existing ActorPanel |
| InteractionMode clobber (spawn/order/inspect) | High | T8 table + tests before T9 polish |
| Creatures extract mistaken for GG-9 | Med | T1 new widget; T10 consumes |
| Plan mode / bare `tasks/plan.md` | Process | Prefixed paths only |

---

## Non-blocking follow-ups

- Actor-sheet catalog-era sheet quality behind dock push
- Commander unique promotion (Vocabulary) — Dave default until then
- Playwright matrix 1280 / 1440 / 1920 for dock + 12 cells
- Commander HUD chip → sheet (optional per chip spec)
- Fusion/Expedition as later ActorCollection consumers

---

## Verification commands (IMPLEMENT)

```powershell
cd web\fusion-rpg-web
npm test -- --run src/ui/actor/ActorCollection
npm test -- --run src/ui/lawn
npm test -- --run src/features/lawn/interactionMode
npm run build
# Optional later: Playwright viewport checks from design §10
```

---

## Open questions (do not stall IMPLEMENT)

1. **Commander chip → sheet in v1?** Default: deferred (chip spec optional). Resolver: owner.
2. **Exact stack N / dock width?** Ship T0 defaults; tune as presentation follow-up.
