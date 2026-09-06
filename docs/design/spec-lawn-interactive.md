# Lawn interactive GUI — shared components and the character sheet

**Status:** Design draft, 2026-09-06. **Not a build authority.** Player-surface landing for lawn
stage chrome. **Architecture capability map (SPECIFY):**
[lawn-interactive-map.md](../architecture/lawn-interactive-map.md) · module specs under
[lawn-interactive/](../architecture/lawn-interactive/). ActorSheet tabs are a **sibling** program
([actor-sheet-map.md](../architecture/actor-sheet-map.md)) — do not implement lawn chrome from this
file alone, and do not park lawn work inside `tasks/actor-sheet-*`. **Audit fold same day** — five
perspectives (UX, DPLP, character-sheet systems, GG-9 FE, demon vocabulary). Findings that changed
the draft are in §16; do not implement from the first pass of plate E aptitude names or the
adventure-spell label.

**Where it lives:** `docs/design/` on purpose. This is a *player surface* contract — same folder as
`spec-derived-stat-sheet.md`, `spec-equip-and-paperdoll.md`, `spec-action-layer.md`. Visual
acceptance: [12-lawn-stage.html](12-lawn-stage.html). Architecture locks it must not fight:
[game-gui-principles.md](../architecture/game-gui-principles.md),
[fe-game-foundation.md](../architecture/fe-game-foundation.md),
[information-architecture.md](information-architecture.md) §2.3.

**Loop this extends:** [the-loops.md](../guide/the-loops.md) place **1. Lawn — first core**. It also
surfaces spine A (power), B (demons on the board), C (gear on a unique). It does not invent a
parallel pitch and it does not make the lawn the whole war. Multi-perspective audit:
[lawn-interactive-audit-2026-09-06.md](../research/lawn-interactive-audit-2026-09-06.md).

---

## Pre-proposal checklist (DESIGN-GATE §5)

```
[x] Subsystems: player GUI, lawn projector (DPLP), unique actor, actor hub / derived stats,
    resources, actions, demon vocabulary (general vs unique vs commander).
[x] Read this session: game-gui-principles.md · information-architecture.md · design/README.md ·
    fe-game-foundation.md (to RT-15 + stage lifetime) · decisions.md Game GUI / Resource /
    UniqueActor / Lawn projector rows (grep + section) · spec-derived-stat-sheet.md ·
    spec-action-layer.md · spec-equip-and-paperdoll.md · spec-magnitude-and-units.md (spine) ·
    actor-hud-ideal.md · resource-hub-ssot.md §1–§2 · unique-actor-runtime.md §1–§3 ·
    demon-system-map.md Vocabulary · the-loops.md · the-lawn.md · software-architecture.md §1–§3 ·
    plates 04, 08, 10, 11 headers · ActorPanel.tsx and tab bodies against CODE.
[x] decisions.md Game GUI lock held: one stage, layers over it, bands, no new top-level route.
[x] Factual claims cite file:line. Code beats plate 08 comments.
[ ] Constraint tests not run — this pass is design only; "would break goldens" is not claimed.
[x] No §2 invariant contradicted. Observe ≠ control, no FE prediction, deltas not absolutes.
[x] Propagation of *this draft*: README plate index, IA §2.3 HUD row, plate 04/08 supersede notes,
    research audit log. Capability map `actor-sheet-map.md` carries a successor banner, not a retire.
```

Honest gap: `actor-hub-ssot.md` and `action-ideal.md` were not read cover-to-cover this session.
Derived-stat and action *presentation* contracts in `docs/design/` were. If a later pass finds a
channel or action membership this file missed, that is a step-1 ERM defect — fix the inventory, do
not add a one-off to the lawn screen (`design/README.md` §2.2 step 6).

---

## 0. Load-bearing principles (restated, not linked)

A downstream session reads this file. These constrain every component below:

1. **The lawn is a stage.** Panels open over it; they never unmount the Phaser `Game` (GG-1, GG-11).
2. **The RPG observes past events and sends Intent.** The FE never owns Unity/Server sim, never
   paints a living occupant before Admit, never predicts procs (DPLP RT-04 / RT-15, GG-15).
3. **RPG features live in the RPG layer.** Cell occupancy, resources, actions, trees, gear are RPG
   readouts and Intent chrome. They are not a rewrite of PvZ `Plant` fields.
4. **Commander never fights.** Commander and Patron are off-board aura roles
   (`demon-system-map.md` Vocabulary). The lawn order bar is this-match Commander's
   **combat book** — HoMM3 *combat* hero spells while stacks fight (hero is not a hex, one
   portrait off-field). It is **not** the adventure-map book (View Air / Dimension Door); those
   verbs belong to the world-map Orders loop. Do not draw the commander as a lawn tile.
   **Today** the commander identity in code is Crazy Dave (`CommanderId`). Vocabulary 2026-09-06
   says a unique demon; that promotion is an owner decision (§14), not two hotbars.
5. **General vs unique is an identity axis, not a UI theme.** A cell can hold engine-spawned
   general demons (species stats only, no `instanceId`) and player unique demons (`UniqueActor`).
   One collection, one sheet; the sheet *locks* what a general cannot have (GG-17), it does not
   fork into two sheets (GG-9).
6. **Six actor resources, one set, labels at display.** `hp` `stamina` `hunger` `spirit` `qi`
   `poise`. Plant `hunger` reads Sun; that is **not** the match-scoped `pvz.*` sun bank. HUD shows
   the bank; the sheet shows the pool.
7. **Numbers carry a unit class.** No bare derived magnitude (GG-46). `spec-magnitude-and-units.md`.
8. **Endless grind, one ladder.** No hard ceiling drawn as a full bar that implies "done." Caps on
   magnitudes are soft or throw. Contests read `Θ`; magnitudes read `P(Θ)`.
9. **GG-60:** under a live wave, legibility beats ornament. The sheet may be almanac-framed; the
   HUD and action bar may not.

---

## 1. Why this exists

Plate 04 drew a lawn *stage chrome* (sun, wave, commander chip, transport) and a panel-over-board
proof. Plate 08 drew a six-tab Actor *door*. Plate 10 drew per-unit HUD. None of them is a
**lawn monitor you can play**: click a stacked cell, page the occupants, open a real character
sheet, spawn a unique onto a cell, enqueue off-board orders, read match resources.

The shipped `ActorPanel` is the defect the owner named. It is not a character sheet. Verified
against code, not the plate:

| What a character sheet shows | What `ActorPanel` does today |
|---|---|
| Portrait + paperdoll + identity | `ActorFrame` initial + badges (`ActorPanel.tsx:107-138`) |
| Live resource gauges (all six) | **Absent from `ActorView`** (`types.ts:575-591` — no resources, no statuses, no actions) |
| Primary stats as named aptitudes | Progression tab: **commander-scope** allocation, labels are **raw aptitude ids** (`ProgressionTab.tsx:35-46,83-91`) |
| Derived combat, six states, attribution | `channelSummary` unconditionally pending; live list prints **`channelId`** (`DerivedStatsTab.tsx:27-31,54-56`); "Open full sheet" is **disabled** (`:65-72`) |
| Equipped actions + costs | Placeholder Strike/Firebolt/Guard + aura slots (`ActionsTab.tsx:9-14,116-119`) |
| Passive trees for *this* specimen | `PassivesTab` is **player-id** trees, not `instanceId` (`PassivesTab.tsx:65-67`) |
| Fifteen-slot paperdoll | Gear tab: empty or "doesn't render it yet" (`GearTab.tsx:9-27`) |
| Current statuses + shield | Overview `PendingNote` for standing / element / shield (`ActorPanel.tsx:181-196`) |

Plate 08 said the Overview tab was "mostly already drawn" and later tabs would fill in. The
implementation treated that as permission to ship a tab bar over stubs. **That cost is already
spent and it is the wrong shape.** Tabs partition a no-scroll floor. Each closed catalog has a
tab that lists every row (plate 13). Number-box primaries and a packed landing are both refused.

CreaturesLayer has the 24/240 *volume policy* and is **list-only UniqueActor** master-detail
(`CreaturesLayer.tsx:17-24`). It is a *consumer* of the collection widget, not the widget to extract.
Lawn cell click and unique-spawn must **not** grow a third list, and they cannot drop
`VirtualCreatureList` onto occupants that have no `instanceId`. `ActorListPickerPanel` is a
narrow scope-picker over `ActorRow` only — no grid, no paging, no sheet, and it treats
`instanceId` as `targetPtr`. LawnPage spawn is a **numeric `typeId` box** (`LawnPage.tsx:570-572`)
— GG-23 / GG-24 fail.

---

## 2. Shared component inventory

**Rule:** a screen is bands + shells + ladders + clusters (`design/README.md` §2.2 step 6). If the
lawn needs a new *entity representation*, that is an ERM hole — fix the ladder. If it needs a
repeating *control group*, that is a cluster. Feature code never owns a private actor list or a
private actor sheet (GG-9).

### 2.1 Canonical shared (used on lawn *and* at least one other stage)

These two are the ones the request named. They are the only new *entity* surfaces. Everything else
on the lawn is a cluster that *composes* them.

| Id | Player name | Entity / density | Consumers (must stay one implementation) | Today |
|---|---|---|---|---|
| **`ActorCollection`** | Creature list | Actor **Row** and **Card**, with density toggle, GG-50 volume, GG-51 query state | Creatures layer · **cell occupancy dock** · **unique spawn tray** · scope picker (replace `ActorListPickerPanel`'s private list) | **Real gap as a widget** — CreaturesLayer has the 24/240 *policy* and is list-only UniqueActor master-detail; it is a *consumer*, not the implementation to lift. Fusion/Expedition/Commanders lists are later consumers, not v1 |
| **`ActorSheet`** | Character sheet | Actor **Panel** — no-scroll floor, eight tabs, complete catalogs on plate 13 | Creatures inspect · Commanders inspect · **cell occupant drill-in** | **Real gap in shape** — `ActorPanel` is a tab bar over stubs. Plate 13 is the inventory. Filename stays; export alias `ActorSheet` |

### 2.2 Lawn clusters (travel together on this stage; not new entities)

| Id | Player name | Band | Job |
|---|---|---|---|
| **`LawnMatchHud`** | Match strip | 1 | Sun *bank*, wave/clock/phase, commander + aura chips, deployed uniques, connection. Extends plate 04 §A / `spec-lawn-hud-chip.md`. |
| **`CellStack`** | Occupants on a tile | 0 (Phaser) | Draw every living occupant in the cell with a stable overlap offset so the player can *see* a stack without clicking. Per-unit HUD stays plate 10. |
| **`CellOccupancyDock`** | This cell | 2 | **Reserved left column** (rail side), one width token. The Phaser camera **shrinks** so all **12** columns stay on screen — including spawn lanes 9–11. Do not overlay the right edge. Select a row → **`ActorSheet`**. Occupants adapt through §4.5 before they become `ActorRow` |
| **`SpawnTray`** | Field a creature | 2 | Unique demons eligible to deploy, through **`ActorCollection`**. Confirm cell via existing `SpawnTargeting` ghost (DPLP InteractionMode). No `typeId` typing. |
| **`CommanderActionBar`** | Orders | 1 | Off-board **combat book** (1–9). HoMM3 combat-hero pattern: pick order → target on the board → **enqueue Intent**. Uses `spec-action-layer.md` action card / cost cluster / refusal. FE does not run A10 range math on the PvZ lawn |

### 2.3 Already specified — compose, do not redraw

| Piece | Owner | Lawn use |
|---|---|---|
| Actor Token / Chip / Row / Card | `00-foundation.html` §G · `src/ui/actor/*` | Collection densities; HUD chips |
| PanelShell / DialogShell | GG-5 / GG-61 | Sheet and docks: **no page scroll**; list regions only |
| Resource meter `(id, label, value, max, polarity)` | `00-foundation` · resource-hub | Condition tab + HUD only for match-scoped stocks |
| AptitudeTile | plate 13 | Twelve primary stats — name, share, Reading, +/−, inspector (no dialog) |
| StatRow | plate 13 | Derived family: lexicon name, number, spark, `?` |
| InspectSplit | plate 13 | Left dock / right reading. Not a stack push |
| LeftoverBar | plate 13 | Unspent points + Reset + Confirm |
| ShieldLayer | plate 13 · shield runtime | Up to 3 instances, not an aggregate bar |
| StatusInstanceRow | plate 13 · StatusRuntime | Live bag / catalog / mastery segs |
| ElementMasteryCell | plate 13 · gate-counters | Six concrete elements; omni not a slot |
| Status token / chip | plate 10 + status-ssot | Cell stack glance + sheet live strip |
| Action slot + cost cluster + refusal | `spec-action-layer.md` | Action bar *and* sheet equipped-action row |
| Paper-doll, 15 roles, frame vocabulary | `spec-equip-and-paperdoll.md` | Kit → Gear |
| Derived-stat sheet, 6 states, combat matrix | `spec-derived-stat-sheet.md` | Derived tab · StatRow · InspectSplit |
| Per-unit lawn HUD | plate 10 · `actor-hud-ideal.md` | On-canvas glance; click opens dock, not a third HUD |
| Commander HUD chips | `spec-lawn-hud-chip.md` | Match strip; tap may open **same** `ActorSheet` with "this match" banner |

### 2.4 Explicitly not components

| Temptation | Why it is refused |
|---|---|
| `LawnActorSheet` vs `RosterActorSheet` | GG-9. Role prop (`creature` / `commander` / `lawn-bound`) changes chrome, not the sheet |
| `CellDemonList` copy of CreaturesLayer | The whole point of `ActorCollection` |
| Per-cell React overlay of names | Phaser owns the board (DPLP). Overlap is `CellStack`. Chrome is React docks |
| Commander drawn on a lawn tile | Vocabulary: commander never fights |
| Sun meter on the character sheet that reads the match bank | Wrong scope. Bank = HUD; `hunger` = actor |
| Numeric `typeId` spawn | GG-23 / GG-24. Recognition from `ActorCollection` |
| A new top-level `#/actor/:id` route | GG-1 / GG-8. Sheet is `?panel=…&sel=<instanceId>` over the current stage. Generals never enter `sel` |

---

## 3. `ActorSheet` — the character sheet

### 3.1 Tabs partition a no-scroll floor (owner, 2026-09-06 evening)

Plate 12 §E packed doll + twelve + meters + standing + live combat onto one landing. That forced
either a scene scrollbar or truncated catalogs (six invented names, one Shield bar). **Tabs are
required.** Condition is the glance. Every closed list has a tab that enumerates it.

Complete inventory (implement from this, not from memory):
[13-actor-sheet.html](13-actor-sheet.html).

```
┌ Header (flex:none): portrait · name · species · side · level · two concrete elements · Fielded/Wave · Esc ┐
├ Tab strip (flex:none): Condition · Aptitudes · Derived · Shield · Status · Elements · Kit · Paths         ┤
├ Pane (flex:1; min-height:0; overflow:hidden) — list-scroll OR inspector body may overflow-y               ┤
├ Aptitudes leftover footer (flex:none): leftover count · Reset · Confirm                                  ┤
└ Footer when commander-role: Set default / Defend the lawn                                                 ┘
```

**No page scroll on the lawn stage or this shell.** GG-61 still owns *list* scroll inside a bounded
region (derived channels, 24-status catalog, elemental dump, Paths lattice). HUD, dock chrome,
AptitudeTile grid, 3 shield slots, 15 doll wells, 6 element cells, 6 resource meters do **not**
scroll.

**GG-10 / GG-63:** cell → dock (push 1) → sheet (push 2). AptitudeTile and StatRow fill an
**in-pane inspector** — they do not push a dialog. Confirm on the leftover footer is the decision
control. Paths lattice may push once more (`spec-tree-surface.md`) — that is the cap.

### 3.2 Who the sheet can bind

| Bind | Identity shown | What is live | What is locked (GG-17, with reason) |
|---|---|---|---|
| **Unique, Cold** (roster, not on a board) | `instanceId`, species, rarity | Aptitudes (unique scope), gear, trees, action loadout | Live HP/status/cell — *"not on a board"* |
| **Unique, Bound** (lawn `ptr` ↔ `instanceId`) | Same + this-cell | All of the above **plus** observe HP/shield/status/resources | Nothing extra. Observe may lag (RT-14); show *"binding catching up"* not a blank |
| **General, living** (engine plant/zombie, no specimen) | Species, side, level-band from pinned `progression.power` | Observe HP/status; species bloodline tree **read-only** | Gear, unique tree, action loadout, rename, release — *"this is a wave [species], not a fielded specimen"* |
| **Commander role** | Same unique sheet | Aura slot + "leads this match / next run" banner | Combat participant chrome. Commander is not a tile. Footer: Set default / Defend the lawn (plate 09) |

`ptr` is never a player-facing id (GG-23). Bound unique is titled by display name + **Fielded**.
General by species name + **Wave** (plant or zombie). Do not write **Wild** — that word is capture /
wild-join (`demon-system-map.md`). Engine generals include plants the player sun-planted; they are
still wave troops, not specimens.

### 3.3 Tab inventory — every closed list has a home

| Tab | No-scroll chrome | List-scroll region |
|---|---|---|
| **Condition** | HP+shield radial · 6 coloured pool meters · 5-axis Standing radar+bars · status glyphs | none |
| **Aptitudes** | 12 **AptitudeTile** with +/− · leftover footer · Confirm | inspector reading (right) |
| **Derived** | StatRow list, one collapse group open, Show unchanged | inspector: unit, compose, cap or not, sources |
| **Shield** | 3 coloured radials (instances). Empty well dashed | omni shield StatRows; full matrix lives on Derived |
| **Status** | StatusGlyph grid · segs Live / Catalog / Mastery | inspector reading |
| **Elements** | 6 mastery radials · lock-chips for omni/aspect/family | inspector + mastery StatRows |
| **Kit** | segs Loadout (5+1 ActionSlot) / Gear (15 doll wells, locked≠empty) | none (15 wells flex) |
| **Paths** | species bloodline + shared corpus panes | lattice when that surface is open |

**AptitudeTile** is a shared component (GG-9): name, share, `Reading`, +/− stepper, leftover footer,
Confirm. Click fills InspectSplit. It is not `statgrid` number boxes and not a nested dialog.

**Shield** is layered instances (max 3, `shield.v1.json`). Aggregate-only “Shield 120/200” is a HUD
glance, not the sheet. Noun is Shield, never Ward.

**Status stacks ≠ status mastery.** Live bag is `StatusRuntime` (no stack-count field; ember Coexist;
elemental family mutex; nerve is party stacks). Mastery is player-lifetime `status_applied.<id>` gate
counters over the same 24 ids.

**Element mastery** is player-lifetime `element_mastery.<id>` over the six concrete elements. Built.
**Aspect-scope is REVERTED** — show species `ElementPrimary`/`Secondary`, lock “six aspects” with that
reason. **Demon family** has no canonical vocabulary in `src/` — lock, do not invent.

### 3.4 What this retires

- Plate 08 Overview-as-PendingNotes.
- Plate 12 §E “landing is the whole character” (truncated catalogs).
- History / Promote tabs (`actor-sheet-map.md`).
- Player-path KeyValue dumps (`LawnPage.tsx:754-767`). GG-41 may keep a developer inspector.
- actor-sheet-shell “Overview unchanged + six tabs”. Filename `ActorPanel.tsx` stays; layout is plate 13.

### 3.5 Four states (GG-17) — every tab

| State | Shows |
|---|---|
| Loading | Shell + pulse wells; no fake Emberling |
| Empty | Collection empty is the collection; a tab with a closed list of zero live rows still shows the catalog / locked empty |
| Error | What failed + Retry. Stage behind stays up (GG-14) |
| Locked | General-demon locks as in §3.2, reason on the control (GG-55) |

---

## 4. `ActorCollection`

### 4.1 One widget

Props (conceptual): `source`, `density: list | grid`, `query` (search, side, sort), `page` /
window, `selection`, `onSelect`, `empty`, `lockedReason`.

Densities use the existing rungs: **Row** in list, **Card** in grid. No third card.

### 4.2 Volume (GG-50) — already decided in CreaturesLayer, now *the* policy

| Magnitude | Behaviour |
|---|---|
| ≤ 24 | Render all |
| 25–240 | Window / virtualize |
| > 240 | Search-first; grid starts empty until query narrows |

Cell occupancy is almost always ≤ 24 (a stacked tile). Spawn tray is roster-sized — the 24 / 240
cutoffs matter there. **Declare once in `ActorCollection`, not again in the dock.**

### 4.3 Sources

| Source | Rows are | Select does |
|---|---|---|
| **Cell** | Living occupants on `(lane, column)` from `LawnViewModel` (content SSOT) | Opens `ActorSheet` for that occupant |
| **Roster deployable** | Unique demons legal to field this match (not expedition-locked, not already Bound) | Enters `SpawnTargeting` with that `instanceId` |
| **Creatures layer** | Full roster | Opens `ActorSheet` Cold |
| **Scope picker** | Candidates for target / unique | Sets picker value (replaces `ActorListPickerPanel` body) |

Paging chrome is identical. Query state survives close/reopen of the dock within the stage session
(GG-51). Cell source may reset query when the *cell* changes — that is a new collection, not a
reopen.

### 4.4 Mixed general + unique in one cell

Each row shows the actor rung **plus** a chip: **Fielded** (unique Bound) vs **Wave** (general plant
or zombie). Sorting default: fielded first, then plants, then zombies — overridable. The player came
here to find *their* specimen in a pile.

Dock rows carry a thin **HP sliver** (observe). Collection rows on Creatures (Cold) omit it.

### 4.5 Occupant → collection adapter (required before the dock)

`ActorView` today requires `instanceId` (`types.ts:575-591`). Cell occupants include generals with
none. **Do not** feed `LawnViewModel` rows straight into `ActorRow`.

The adapter produces a collection row: display name, species, side, rarity-or-none, Fielded/Wave,
observe HP fraction, optional `instanceId`. Unique Bound rows may open `ActorSheet` by `instanceId`.
General rows open the same sheet bound to a **session-scoped observe handle** that dies with the
occupant (generation ≠ ptr lifetime; do not invent a fourth durable id).

`ActorCollection` is a **new widget**. CreaturesLayer, the dock, the spawn tray, and the scope picker
**consume** it. Do not copy `VirtualCreatureList` and call GG-9 done.

---

## 5. Cell display and overlap (`CellStack`)

**Need:** monitor without click. A 2-wide, 3-deep stack on one tile must still read as a stack.

| Rule | Detail |
|---|---|
| Plane | Phaser only (DPLP). React does not duplicate sprites |
| Overlap | Stable offset (e.g. 6–8 px down-right per occupant after the first), depth = layout order already used by `layoutGrid` |
| Cap visible | Structural cap on *drawn* extras (not a progression ceiling): show N sprites + a `+K` pip when over N. N is a tunable in presentation tokens, not a Core cap |
| HUD | Plate 10 stack on the **topmost** occupant; others keep a thinner HP/status sliver if the unit HUD program turns slivers on — default off v1 (`actor-hud-ideal.md`) |
| Bound pip | Fielded uniques carry plate 10's identity pip on the **sprite**, including under-stack — monitor without click |
| Pick | Click hits topmost occupant (existing `PickSystem`) **or** empty-ish cell chrome → dock with the full list. Keyboard confirm on a focused cell opens the dock, not only the top occupant (so overlap is not a mouse-only trap — GG-21). Today's `LawnWorldScene` confirm emits an **occupant**; implementation retargets confirm to the **tile** when InteractionMode is inspect |
| Selection chrome | Existing ring (`StatusFx` / FxPool). Dock open ⇔ cell selected |

This is glance. Numbers stay in the sheet (GG-60, actor-hud v1: no full numeric readout on-canvas).

---

## 6. Spawn unique onto a selected cell (`SpawnTray`)

Flow (existing FSM, honest chrome):

1. Player selects a cell (dock may already be open) or opens Field from the HUD.
2. `SpawnTray` lists deployable **uniques** via `ActorCollection` (grid default).
3. Choose one → `enterSpawnTargeting` (`interactionMode.ts`). Ghost on cell (already in
   `LawnWorldScene`). Ack immediately (GG-15, M9).
4. Confirm → Intent only. Occupant appears when observe fold says so. Rejection → toast + ghost
   clears (GG-16, GG-54).
5. Illegal (phase Idle/Ending, expedition lock, already Bound) → control disabled **with reason**
   (GG-55). `canEnterSpawnTargeting` already gates phase (`interactionMode.ts:21-24`).

**Forbidden:** `typeId` number field, debug spawn on the player tray (GG-40 — debug spawn stays
developer tree). Side toggle plant/zombie on the player tray is wrong for uniques: the specimen
already has a side.

General-demon debug inject remains a developer surface.

---

## 7. Commander action bar (HoMM3 combat-hero book, off the hex)

### 7.1 What it is

HoMM3 combat lets a **hero who is not a stack on the hex** spend a spell book while stacks fight.
Adventure-map spells (View Air, Dimension Door) are a different book; in this repo they belong to
the **world-map Orders** loop. Our Commander **never enters the lawn as a unit**. The lawn bar is
the combat-hero analogue, 1–9, no round lock:

- Caster: **this-match Commander**. Today that identity in code is Crazy Dave (`CommanderId`).
  Vocabulary 2026-09-06 wants a unique demon; until that promotion, do not draw a second hotbar.
- Verbs: actions whose membership is `spec-action-layer.md` / action-ideal. Summon-to-cell is an
  action. A passive aura is not. Example verbs (Sunfall, Lane bolt) are **combat orders onto the
  lawn**, not adventure travel.
- Target: cell, occupant, lane, side, or self-off-board, per the action's **declared target kind**.
  **FE does not run battle A2 range.** Highlights of lagged observe are chrome only. Server /
  injector re-resolves. Do not say the player "casts."
- Payer: each slot's cost cluster names the **stock** — match `pvz.*` sun bank, or an actor pool
  (`qi` / Yang, etc.) on the commander. Unlabeled `40` is a GG-46 fail.
- Authority: enqueue Intent; paint outcome on observe (GG-15).

### 7.2 Layout

Band 1, **bottom-center**, above safe-area inset. Does not cover the rail. Keys `1`–`9` as already
reserved for stage hotbar (`information-architecture.md` §5).

Each slot = `ActionSlot`: icon, player name, cost cluster, cooldown veil, unaffordable / refused
reason (GG-55). Selected slot + board targeting highlight (cell `is-range` / `is-target` from
plate 04 grammar) is **observe chrome**, not a FE legality oracle.

Unaffordable is not silent opacity: cost chips go `bad`, reason on focus.

### 7.3 Pause / GG-18 / targeting

Opening the dock or sheet **does not pause Unity**. Overlay pause is F10 / `overlay-spec.md`
§Pause while away. The match clock stays on the HUD; the wave still runs.

When `CellOccupancyDock` or `ActorSheet` is open, GG-18 mutes **board arrows and board confirm**.
Today `isLawnKeyboardMuted` is only the LawnStage GG-11 proof panel — dock and sheet must join that
gate. Pointer clicks on the board still change the inspect cell (monitor), except in order /
spawn targeting (table below).

v1: no enqueue-from-sheet.

### 7.4 Empty / unbuilt corpus

If the commander has no authored actions yet, the bar shows locked slots with *"Orders unlock with
the action corpus"* — not fake Strike/Firebolt. Do not hide the bar; GG-44 locked-visible.
Do not name a slot **Ward** — that word is aptitude `Bulwark`'s cousin and a paper-doll role.

---

## 8. `LawnMatchHud` — what we can collect

Band 1, top edge, safe-area inset. Plate 04 §A is the skeleton. This section only names **stocks
and readouts that already have an observe path or an honest pending**.

| Cluster | Shows | Scope | Do not confuse with |
|---|---|---|---|
| **Sun bank** | Match `pvz.*` sun | Match | Actor `hunger` (Sun on plants) |
| **Wave** | Wave index + next-in | Match | |
| **Phase** | Starting / InMatch / Paused / Ending | Match | UniqueActor Cold phase |
| **Commander** | This-match leader + aura **display name from the corpus** (chip spec uses Might-class auras; do not label a chip "Sun Blessing" if that reads as Patron) | Snapshot at `board.start` | Changing default mid-run (`spec-lawn-hud-chip.md`); Patron |
| **Deployed** | Bound unique chips — **`ActorChip`**, not custom `#typeId` spans over every living plant | Match bindings | The full roster; engine generals |
| **Souls (session)** | Souls earned *this run* if the ledger already attributes lawn kills | Run | Wallet on Sanctum HUD |
| **Transport** | Pause / speed | Overlay pause contract | |
| **Selection** | Focused cell coords in player words ("Lane 3 · Column 5") | UI | |

If a stock cannot be collected yet, omit the cluster or show locked-with-reason. Do not invent a
second sun.

No ornament (GG-60). HUD stays interactive over a panel scrim (GG-5 amendment). Sheet-over-Phaser
is **not** overlay pause; Unity keeps running.

---

## 9. Stage composition and stack

```
Band 1  LawnMatchHud (top)     CommanderActionBar (bottom)     rail
Band 0  Phaser lawn + CellStack  (camera inset by reserved dock width; 12 columns visible)
Band 2  CellOccupancyDock (reserved LEFT column)  and/or  SpawnTray (same column)
        ActorSheet (PanelShell, bounded, over dock or replacing it)
Band 3  confirm (release, lethal action opt-in)
Band 4  Intent ack / reject toasts
```

**Dock vs sheet:** dock is the list; sheet is the selected occupant. Opening the sheet does not
destroy the dock's query (GG-12). Esc pops sheet then dock. **Esc while an order is armed cancels
the order first**, then pops.

URL (`GG-8`) — `sel` is **`instanceId` only**:

```
#/lawn/{matchKey}
#/lawn/{matchKey}?cell=3,5
#/lawn/{matchKey}?cell=3,5&sel=<instanceId>
#/lawn/{matchKey}?panel=creatures          existing layer over lawn
```

General-demon inspect is in-memory, dies with the occupant, **not in the address bar**. Do not
invent a fourth durable id. Do not put `ptr` in the URL.

---

## 10. Input, focus, viewports (game-ui-ux)

| Concern | Contract |
|---|---|
| Layout | HUD corners anchored; **dock reserved, not overlaid**; board is the remaining center showing 12 columns. No absolute pixel stage chrome |
| Scale | GG-36: 1280×720 floor, 1440×900 reference, 1920×1080 headroom. Height-scale HUD |
| Safe area | Inset HUD and action bar; board may bleed |
| Focus | Dock open → first collection row. Sheet open → declared landing stop (name / first meter). Order targeting → cell focus |
| Mouse + focus | Coexist; hover does not clear keyboard focus |
| Reduced motion | Instant docks; keep M9 press ack (GG-32) |

### 10.1 InteractionMode — inspect vs spawn vs order

`SpawnTargeting` already exists. Add **`ActionTargeting`**. Occupied-cell `selectOccupant` **must not
clobber** an armed order.

| Mode | Arrows | Enter | Space | Esc | Click occupied cell |
|---|---|---|---|---|---|
| **Inspect** (default) | Cell focus (muted if dock/sheet open — GG-18) | Open occupancy dock for the **tile** | **Pause** (`information-architecture.md` §5). Never confirm | Pop sheet, then dock | Open / retarget dock |
| **SpawnTargeting** | Ghost cell (board arrows live; spawn tray is band 2 so GG-18 would mute — **exception:** spawn targeting keeps board arrows) | Enqueue spawn Intent | Pause | Cancel targeting | Confirm spawn on that cell, do not open dock |
| **ActionTargeting** | Target cell / occupant per declared kind | Enqueue order Intent | Pause | Cancel armed order (**before** System / pop) | Select target; **do not** `selectOccupant` into inspect |

`wireKeyboardNav` today treats Space as confirm. Implementation drops that. Confirm is Enter only.

Band 2 spawn tray would mute board arrows under a naive GG-18. Spawn targeting is the exception in
the table so the player can still walk the ghost.

Playwright later: 1280×720 / 1440×900 / 1920×1080, `scrollWidth <= clientWidth`, sheet
`clientHeight` cap (GG-61), **12 cells visible** with dock reserved.

---

## 11. Built / wiring / real gap

| Piece | Bucket | Evidence |
|---|---|---|
| Lawn stage + Phaser island + GG-11 panel | Built | `LawnStage.tsx`, `createLawnGame`, kernel destroy |
| Cell pick → `lawn:select` | Built | `PickSystem.ts`, keyboard nav + mute (audit fix) |
| Occupant list in inspector (ad-hoc) | Wiring | `LawnPage.tsx` selection; not `ActorCollection` |
| Unique bind inspect | Wiring | `useUniqueActor` + KeyValue engine fields (`LawnPage.tsx:754+`) — player path retires; GG-41 dump may stay |
| Spawn Intent + ghost | Wiring | FSM + Intent; player UI is `typeId` input |
| Actor ladder Token…Panel | Wiring | `src/ui/actor/*`; Panel landing is stubs |
| Creatures volume list | Built (policy only) | `CreaturesLayer.tsx` — **consumes** `ActorCollection`; not a lift |
| `ActorListPickerPanel` | Wiring | Row-only; compose `ActorCollection` after adapter + GG-50; do not pass `instanceId` as `targetPtr` |
| Derived sheet / paper-doll / action corpus on sheet | Real / specified | specs exist; `ActorView` fields pending; gear empty |
| Per-unit HUD | Specified + partial | plate 10 / actor-hud program |
| Commander chips | Specified | `spec-lawn-hud-chip.md` |
| Commander action bar | Real gap | No off-board combat book; battle plate 04 bar is the wrong stage |
| Cell overlap draw | Wiring / real | Occupants exist; stack offset + `+K` pip + unique pip not a specified cluster |
| `ActorView` resources/status/actions | Real gap | `types.ts:575-591` |
| `ResourceView` missing `poise` | Wiring (stale contract) | `types.ts:640` vs resource-hub six |
| `ActionTargeting` | Real gap | Inspect and spawn only today; Space still confirms |
| Occupant → `ActorRow` adapter | Real gap | `ActorView` requires `instanceId` |

---

## 12. Tunables (presentation only)

Any number a balance pass would change for *feel of chrome* lives in FE tokens / a small
`data`-adjacent presentation file — **not** Core Policy. Candidates:

- Cell stack offset px, max sprites before `+K`
- Dock width (one token; reserved column)
- Action bar slot count (structural 9 from keymap — comment if not tunable)
- Collection page size inside the 25–240 window

Action *costs*, cooldowns, sun bank rules stay in existing `data/tuning/` owners. This GUI does
not invent a power curve.

---

## 13. Deliberately not decided

- Discharging Decision 40 / SiegeBoardScene (out of lawn GUI).
- Pixel-golden for lawn paint (command-list oracle stays).
- Whether commander tap on HUD opens `ActorSheet` v1 or waits (`spec-lawn-hud-chip.md` optional).
- Capture / blessing / trophy chrome (WIP on `the-lawn.md`) — no fake clusters.
- Making `PassivesTab` two-track (species bloodline + shared corpus) — Paths *requires* it; that is
  actor-sheet work, called out as a dependency.
- Commanders list private `CommanderRow` and Pacts opening AptitudesLayer — not v1 `ActorCollection`
  consumers until they retire those rows.
- Promoting `CommanderId` from Crazy Dave to a unique demon (Vocabulary 2026-09-06). Until then the
  order bar is Dave's combat book; a unique Commander would be ineligible to spawn.

---

## 14. Open questions (owner)

Already decided in this file (not re-asked):

- Cell click **always** opens the occupancy list, even for count = 1.
- General Paths = **read-only species tree**; shared corpus locked.

Still owner:

1. **Commander identity.** Keep Crazy Dave as this-match commander (current `CommanderId`) until
   unique-demon Commander ships, **or** promote now? Recommendation: **keep Dave for v1 lawn GUI**;
   do not draw two bars. When a unique takes the role, they leave the spawn tray.

---

## 15. Next

Owner review of this landing **and** the architecture map/specs/plan:

- [lawn-interactive-map.md](../architecture/lawn-interactive-map.md)
- [lawn-interactive/](../architecture/lawn-interactive/) (`spec-actor-collection` …
  `spec-commander-action-bar`)
- Prefixed plan/todo: [lawn-interactive-plan.md](../../tasks/lawn-interactive-plan.md) ·
  [lawn-interactive-todo.md](../../tasks/lawn-interactive-todo.md) (T0–T13; idea coverage matrix)
- Sibling ActorSheet: [actor-sheet-map.md](../architecture/actor-sheet-map.md) (eight tabs +
  catalogs; expand/join for 268 channels) — plan parked until Draft approved
- Sibling Band B HUD catalog-token amend: [actor-hud-plan.md](../../tasks/actor-hud-plan.md)
  **H1–H3** (ideal §4.1 — resolve `hudToken`/`color`; delete `StatusInitials` / hash RGB). Lawn T12
  owns click→dock wiring; HUD owns token resolve.

Visual: [12-lawn-stage.html](12-lawn-stage.html) + **[13-actor-sheet.html](13-actor-sheet.html)**.

**IMPLEMENT lawn only after owner accepts the lawn plan.** Do not implement from number boxes or
from the first-pass plate E. ActorSheet plan stays parked behind its own Draft gate.

---

## 16. Audit fold (2026-09-06)

Five perspectives: UX (game-ui-ux), DPLP observe≠control, character-sheet systems, GG-9 FE
duplication, demon / HoMM3 vocabulary. Consensus: **architecture split is right; first-pass plate
contradicted closed vocabularies. Do not graduate, do not implement from plate E as first drawn.**

What this fold changed:

| Finding | Correction in this file |
|---|---|
| Right overlay hid spawn columns on a 12-col lawn at 1280×720 | Reserved **left** column; camera shrinks; 12 columns stay visible |
| Inspect / targeting / Space / Enter collided | §10.1 `ActionTargeting`; Space = pause; Enter confirms; Esc cancels order first |
| `?sel=` for generals | `sel` = `instanceId` only |
| "Range is real here" / "confirm casts" | Enqueue Intent; no FE A2; do not say cast |
| Invented six aptitude names | Closed twelve (`spec-primary-stats.md`) |
| Standing three bars / wrong labels | Five `definitions.md` axes; stale `PowerCategory` called out |
| Adventure-spell label | Off-board **combat** book |
| Yours / Wild | Fielded / Wave (Wild = capture) |
| ActorCollection as Creatures extract | New widget; Creatures consumes; occupant adapter required |
| Two ActorSheet programs | This landing **supersedes** actor-sheet-shell; map carries a banner |
| KeyValue only named at `:754` | Player path vs GG-41 split |
| Duplicate HP / Vitality | One HP pool; hub label HP |
| Sheath filled at Lv 14 | Unlock ladder honest (sheath = 24) |
| Sun Blessing / Ward slot name | Corpus aura name; no Ward order |
| Overlay-pause vs sheet | Unity keeps running; F10 is overlay pause |

Verdicts (research log): UX fix-before-review · DPLP revise before `/spec` · character sheet do not
implement from first plate E · GG-9 does not hold as extract-and-wire · demon/HoMM3 do not graduate.
