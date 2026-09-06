# Lawn interactive GUI — shared components and the character sheet

**Status:** Design draft, 2026-09-06. **Not a spec to build against until owner review.** No code
authorized.

**Where it lives:** `docs/design/` on purpose. This is a *player surface* contract — same folder as
`spec-derived-stat-sheet.md`, `spec-equip-and-paperdoll.md`, `spec-action-layer.md`. Visual
acceptance: [12-lawn-stage.html](12-lawn-stage.html). Architecture locks it must not fight:
[game-gui-principles.md](../architecture/game-gui-principles.md),
[fe-game-foundation.md](../architecture/fe-game-foundation.md),
[information-architecture.md](information-architecture.md) §2.3.

**Loop this extends:** [the-loops.md](../guide/the-loops.md) place **1. Lawn — first core**. It also
surfaces spine A (power), B (demons on the board), C (gear on a unique). It does not invent a
parallel pitch and it does not make the lawn the whole war.

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
[ ] Propagation after owner review: README plate index, IA §2.3 HUD row, plate 04/08 supersede notes.
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
4. **Commander never fights.** Commander and Patron are off-board aura roles on a unique demon
   (`demon-system-map.md` Vocabulary). The lawn action bar is the *summoner's / this-match
   commander's* Intent list onto the board — HoMM3 *adventure* spellbook, not a combat hero on a
   hex. Do not draw the commander as a lawn tile.
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
sheet, spawn a unique onto a cell, cast off-board actions, read match resources.

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
spent and it is the wrong shape.** A character sheet's landing view *is* the character. Tabs are
depth (GG-26), not the place the character is hiding.

Creatures layer already has list/grid + paging (`CreaturesLayer.tsx:17-24`, GG-50 tiers 24 / 240).
Lawn cell click and unique-spawn must **not** grow a third list. `ActorListPickerPanel` is a
narrow scope-picker over `ActorRow` only — no grid, no paging, no sheet. LawnPage spawn is a
**numeric `typeId` box** (`LawnPage.tsx:570-572`) — GG-23 / GG-24 fail.

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
| **`ActorCollection`** | Creature list | Actor **Row** and **Card**, with paging, list/grid toggle, GG-50 volume, GG-51 query state | Creatures layer · **cell occupancy dock** · **unique spawn tray** · scope picker (replace `ActorListPickerPanel`'s private list) · Fusion parents · Expedition dispatch | **Wiring gap** — CreaturesLayer has the volume policy; picker and lawn do not call it |
| **`ActorSheet`** | Character sheet | Actor **Panel** — *landing is the sheet*, tabs are depth | Creatures inspect · Commanders inspect · **cell occupant drill-in** · Pact bound-demon · (later) Delve party, not this plate | **Real gap in shape** — `ActorPanel` exists as a tab shell over stubs; this document *replaces the landing*, it does not add a seventh tab |

### 2.2 Lawn clusters (travel together on this stage; not new entities)

| Id | Player name | Band | Job |
|---|---|---|---|
| **`LawnMatchHud`** | Match strip | 1 | Sun *bank*, wave/clock/phase, commander + aura chips, deployed uniques, connection. Extends plate 04 §A / `spec-lawn-hud-chip.md`. |
| **`CellStack`** | Occupants on a tile | 0 (Phaser) | Draw every living occupant in the cell with a stable overlap offset so the player can *see* a stack without clicking. Per-unit HUD stays plate 10. |
| **`CellOccupancyDock`** | This cell | 2 | Right dock: this cell's occupants through **`ActorCollection`**. Select a row → **`ActorSheet`**. Not a second list widget. |
| **`SpawnTray`** | Field a creature | 2 | Unique demons eligible to deploy, through **`ActorCollection`**. Confirm cell via existing `SpawnTargeting` ghost (DPLP InteractionMode). No `typeId` typing. |
| **`CommanderActionBar`** | Orders | 1 | Off-board action hotbar (1–9). HoMM3 adventure-spell pattern: pick order → target on the board → Intent. Uses `spec-action-layer.md` action card / cost cluster / refusal. |

### 2.3 Already specified — compose, do not redraw

| Piece | Owner | Lawn use |
|---|---|---|
| Actor Token / Chip / Row / Card | `00-foundation.html` §G · `src/ui/actor/*` | Collection densities; HUD chips |
| PanelShell / DialogShell | GG-5 / GG-61 | Sheet and docks scroll *inside* the shell |
| Resource meter `(id, label, value, max, polarity)` | `00-foundation` · resource-hub | Sheet landing + HUD only for match-scoped stocks |
| Status token / chip | plate 10 + status-ssot | Cell stack glance + sheet live strip |
| Action slot + cost cluster + refusal | `spec-action-layer.md` | Action bar *and* sheet equipped-action row |
| Paper-doll, 15 roles, frame vocabulary | `spec-equip-and-paperdoll.md` | Sheet landing (read) + Gear depth (equip) |
| Derived-stat sheet, 6 states, combat matrix | `spec-derived-stat-sheet.md` | Sheet **Combat** depth, not the landing |
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
| A new top-level `#/actor/:id` route | GG-1 / GG-8. Sheet is `?panel=…&sel=` over the current stage |

---

## 3. `ActorSheet` — the character sheet

### 3.1 The shape (landing *is* the character)

Classic sheets (Diablo character, BG3 overview, HoMM3 hero screen) put portrait, body, numbers,
gear, and current condition on **one view**. Depth (spellbook, skill tree, full stat dump) sits
behind that view. Plate 08 inverted this: six tabs, empty Overview. **This plate inverts it back.**

```
┌ Header: portrait · name · species · rarity · side · level · element pips · live status chips · Esc ┐
├──────────────┬─────────────────────────────┬─────────────────────────────┤
│ Paper-doll   │ Primary + resources         │ Live combat                 │
│ 15 slots     │ 12 named aptitudes          │ HP / shield (observe)       │
│ frame vocab  │ 6 resource meters           │ statuses, cell, unique/gen  │
│ read-only    │ 5-axis Standing             │ type progression (species)  │
│ on landing   │                             │                             │
├──────────────┴─────────────────────────────┴─────────────────────────────┤
│ Equipped action slots (same ActionSlot grammar as the lawn bar)          │
├──────────────────────────────────────────────────────────────────────────┤
│ Depth tabs (not the character): Combat sheet · Paths · Loadout · History │
└──────────────────────────────────────────────────────────────────────────┘
```

**GG-61:** the shell is height-bounded to the GG-36 floor (720 CSS px). The landing grid scrolls
inside the body if 15 slots + six meters overflow. The depth tab that hosts ~385 derived channels
*always* scrolls inside the sheet; the page behind never does.

**GG-10:** cell → dock (push 1) → sheet (push 2). Combat / Paths / Loadout are tabs, not a third
push. Passive lattice *inside* Paths may push once more (already specified in `spec-tree-surface.md`)
and that is the cap.

### 3.2 Who the sheet can bind

| Bind | Identity shown | What is live | What is locked (GG-17, with reason) |
|---|---|---|---|
| **Unique, Cold** (roster, not on a board) | `instanceId`, species, rarity | Aptitudes (unique scope), gear, trees, action loadout | Live HP/status/cell — *"not on a board"* |
| **Unique, Bound** (lawn `ptr` ↔ `instanceId`) | Same + this-cell | All of the above **plus** observe HP/shield/status/resources | Nothing extra. Observe may lag (RT-14); show *"binding catching up"* not a blank |
| **General, living** (engine plant/zombie, no specimen) | Species, side, level-band from pinned `progression.power` | Observe HP/status; species primary / general tree **read-only** | Gear, unique tree, action loadout, rename, release — *"this is a wild [species], not one of yours"* |
| **Commander role** | Same unique sheet | Aura slot + "leads this match / next run" banner | Combat participant chrome. Commander is not a tile. Footer: Set default / Defend the lawn (plate 09) |

`ptr` is never a player-facing id (GG-23). Bound unique is titled by display name; general by
species name + a "wild" / "wave" chip.

### 3.3 Landing blocks — required, not optional

Each block names its data shape. Components bind to the shape, not to an enumerated list
(`design/README.md` §2.3).

| Block | Binds to | Render rule |
|---|---|---|
| **Identity** | display name, species name, rarity rung, side, level, frame | Art contract GG-58. Missing art = designed placeholder with side + element, never a broken image |
| **Element** | up to two concrete slots; never offer `omni` as a type | Tokens from the element ladder |
| **Live status** | status chips from observe (Bound/general) or empty (Cold) | Same chips as plate 10. Empty Cold: omit the tray, do not write "no statuses" as if they were missing |
| **Paper-doll** | 15 `role_id`s, labels from `(role, frame)` | `spec-equip-and-paperdoll.md`. Empty slot is a dashed well with the *player* slot word (muzzle, not `armament-primary`). Landing is inspect; equip is Loadout tab |
| **Resources** | six meters, faction labels | Registry shape `(id, label, value, max, polarity)`. Include **`poise`**. ⚠ `ResourceView` in `types.ts:640` still lists five ids — that contract is stale vs resource-hub; the sheet must not ship the stale five |
| **Primary stats** | twelve aptitudes, **display names**, unique-scope shares | Never print the aptitude *id* as the label. Commander-scope replica is a footnote ("includes commander spread"), not the only editor, and not on a general demon |
| **Standing** | five-axis power vector | GG-48: scalar may sort a list; it is not the only number on the sheet |
| **Live combat** | observe HP, shield stack, cell (row/col as *board words*, not `row`/`col` in copy) | Lawn-bound only. GG-46 on shield magnitudes |
| **Equipped actions** | loadout slots | Same `ActionSlot` as the stage bar. Empty slot dashed + unlock reason (`loadout.slots`). Costs as resource chips with faction labels |

### 3.4 Depth tabs (behind the landing)

| Tab | Opens | Must not become |
|---|---|---|
| **Combat** | Full derived-stat sheet (`spec-derived-stat-sheet.md`) — combat *matrix* first, six states, GG-49 attribution | A dump of `channelId` strings |
| **Paths** | Specimen (unique) or species (general) tree. Reuse the existing Passives surface **parameterized by actor**, not a second tree UI | Player-wallet trees with no specimen (today's `PassivesTab`) shown as if they were this demon's |
| **Loadout** | Paper-doll **editor** + action slot assignment + comparison (GG-47) | A second gear page in Relics that does not deep-link here |
| **History** | Specimen XP, runs this one fought, fusion lineage | A developer event log |

Auras (commander) stay on the commander-role landing / Loadout, not mixed into a fake action grid
of Strike/Firebolt (`ActionsTab.tsx` current mix).

### 3.5 Four states (GG-17) — every sheet, including landing

| State | Landing shows |
|---|---|
| Loading | Shell chrome + pulse wells in the three columns; no fake Emberling |
| Empty | Does not apply to a bound id; collection empty is the collection's problem |
| Error | What failed + Retry. Stage behind stays up (GG-14) |
| Locked | General-demon locks as in §3.2, with the reason on the control (GG-55) |

### 3.6 What this retires

- Plate 08 §B "Overview is the landing, mostly already drawn" as the implementation contract.
  Plate 08's *tab inventory* (progression, derived, actions, passives, gear) is absorbed into
  landing + the four depth tabs above.
- `ActorPanel` Overview-as-PendingNotes as the accepted actor surface.
- Any lawn inspector KeyValue of `typeId` / `ptr` / `Cold phase` (`LawnPage.tsx:754-767`).

`ActorPanel` the *module* may keep its filename; its **layout contract** is this section.

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

Each row shows the actor rung **plus** a chip: **Yours** (unique Bound) vs **Wild** (general).
Sorting default: uniques first, then plants, then zombies — overridable. The player came here to
find *their* demon in a pile.

---

## 5. Cell display and overlap (`CellStack`)

**Need:** monitor without click. A 2-wide, 3-deep stack on one tile must still read as a stack.

| Rule | Detail |
|---|---|
| Plane | Phaser only (DPLP). React does not duplicate sprites |
| Overlap | Stable offset (e.g. 6–8 px down-right per occupant after the first), depth = layout order already used by `layoutGrid` |
| Cap visible | Structural cap on *drawn* extras (not a progression ceiling): show N sprites + a `+K` pip when over N. N is a tunable in presentation tokens, not a Core cap |
| HUD | Plate 10 stack on the **topmost** occupant; others keep a thinner HP/status sliver if the unit HUD program turns slivers on — default off v1 (`actor-hud-ideal.md`) |
| Pick | Click hits topmost occupant (existing `PickSystem`) **or** empty-ish cell chrome → dock with the full list. Keyboard confirm on a focused cell opens the dock, not only the top occupant (so overlap is not a mouse-only trap — GG-21) |
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

## 7. Commander action bar (HoMM3 adventure spells)

### 7.1 What it is

HoMM3 lets a **hero who is not a stack on the hex** spend spell points on the adventure map
(View Air, Dimension Door) and, in combat, spend a separate book while stacks fight. Our Commander
**never enters the lawn as a unit**. The bar is therefore the adventure-map analogue:

- Caster: this-match Commander (unique, off-board) — identity already on `LawnMatchHud`.
- Verbs: actions whose membership is `spec-action-layer.md` / action-ideal (anything that costs
  resource or time and needs a cooldown). Summon-to-cell is an action. A passive aura is not.
- Target: cell, occupant, lane, side, or self-off-board, per the action's target rule. **With no
  board, range checks pass** (A2 §4) — the lawn *has* a board, so range is real here.
- Authority: enqueue Intent; paint outcome on observe (GG-15).

### 7.2 Layout

Band 1, **bottom-center**, above safe-area inset. Does not cover the rail. Keys `1`–`9` as already
reserved for stage hotbar (`information-architecture.md` §5).

Each slot = `ActionSlot`: icon, player name, cost cluster, cooldown veil, unaffordable / refused
reason (GG-55). Selected slot + board targeting highlight (cell `is-range` / `is-target` from
plate 04 grammar).

Unaffordable is not silent opacity: cost chips go `bad`, reason on focus.

### 7.3 Pause / GG-18

Picking an action that needs a board target: top layer is still the stage (bar is HUD, does not
block). Keyboard: digits pick slots; arrows move cell focus (`wireKeyboardNav` + mute gate when a
**panel** is open). Confirm casts. Esc cancels targeting, does not pop a panel that is not open.

When `CellOccupancyDock` or `ActorSheet` is open, GG-18: stage arrows are muted (already
`isLawnKeyboardMuted`). The bar may remain visible (HUD above scrim, 2026-09-04 amendment) but
slots do not steal keys until the panel pops — unless the action is explicitly "cast from the
sheet" later. v1: no cast-from-sheet.

### 7.4 Empty / unbuilt corpus

If the commander has no authored actions yet, the bar shows locked slots with *"Orders unlock with
the action corpus"* — not fake Strike/Firebolt. Do not hide the bar; GG-44 locked-visible.

---

## 8. `LawnMatchHud` — what we can collect

Band 1, top edge, safe-area inset. Plate 04 §A is the skeleton. This section only names **stocks
and readouts that already have an observe path or an honest pending**.

| Cluster | Shows | Scope | Do not confuse with |
|---|---|---|---|
| **Sun bank** | Match `pvz.*` sun | Match | Actor `hunger` (Sun on plants) |
| **Wave** | Wave index + next-in | Match | |
| **Phase** | Starting / InMatch / Paused / Ending | Match | UniqueActor Cold phase |
| **Commander** | This-match leader + aura | Snapshot at `board.start` | Changing default mid-run (`spec-lawn-hud-chip.md`) |
| **Deployed** | Bound unique chips | Match bindings | The full roster |
| **Souls (session)** | Souls earned *this run* if the ledger already attributes lawn kills | Run | Wallet on Sanctum HUD |
| **Transport** | Pause / speed | Overlay pause contract | |
| **Selection** | Focused cell coords in player words ("Lane 3 · Column 5") | UI | |

If a stock cannot be collected yet, omit the cluster or show locked-with-reason. Do not invent a
second sun.

No ornament (GG-60). HUD stays interactive over a panel scrim (GG-5 amendment).

---

## 9. Stage composition and stack

```
Band 1  LawnMatchHud (top)     CommanderActionBar (bottom)     rail
Band 0  Phaser lawn + CellStack
Band 2  CellOccupancyDock (right, ~360px)  and/or  SpawnTray
        ActorSheet (PanelShell, bounded, over dock or replacing it)
Band 3  confirm (release, lethal action opt-in)
Band 4  Intent ack / reject toasts
```

**Dock vs sheet:** dock is the list; sheet is the selected occupant. Opening the sheet does not
destroy the dock's query (GG-12). Esc pops sheet then dock.

URL (`GG-8`):

```
#/lawn/{matchKey}
#/lawn/{matchKey}?cell=3,5
#/lawn/{matchKey}?cell=3,5&sel=<instanceId|occupant-key>
#/lawn/{matchKey}?panel=creatures          existing layer over lawn
```

Occupant-key for a general demon cannot be `instanceId`. Use a generation-scoped observe key the
player never sees; the URL may use a non-durable token that dies with the match (document in
implementation). Do not put `ptr` in the address bar as a player-facing string.

---

## 10. Input, focus, viewports (game-ui-ux)

| Concern | Contract |
|---|---|
| Layout | HUD corners anchored; docks column; board is the remaining center. No absolute pixel stage chrome |
| Scale | GG-36: 1280×720 floor, 1440×900 reference, 1920×1080 headroom. Height-scale HUD |
| Safe area | Inset HUD and action bar; board may bleed |
| Focus | Dock open → first collection row. Sheet open → declared landing stop (name / first meter). Action targeting → cell focus |
| Keyboard | Arrows on board when no blocking panel; `1`–`9` bar; Enter confirm; Esc pop / cancel targeting |
| Mouse + focus | Coexist; hover does not clear keyboard focus |
| Reduced motion | Instant docks; keep M9 press ack (GG-32) |

Playwright later: 1280×720 / 1440×900 / 1920×1080, `scrollWidth <= clientWidth`, sheet
`clientHeight` cap (GG-61).

---

## 11. Built / wiring / real gap

| Piece | Bucket | Evidence |
|---|---|---|
| Lawn stage + Phaser island + GG-11 panel | Built | `LawnStage.tsx`, `createLawnGame`, kernel destroy |
| Cell pick → `lawn:select` | Built | `PickSystem.ts`, keyboard nav + mute (audit fix) |
| Occupant list in inspector (ad-hoc) | Wiring | `LawnPage.tsx` selection; not `ActorCollection` |
| Unique bind inspect | Wiring | `useUniqueActor` + KeyValue engine fields (`LawnPage.tsx:754+`) |
| Spawn Intent + ghost | Wiring | FSM + Intent; player UI is `typeId` input |
| Actor ladder Token…Panel | Wiring | `src/ui/actor/*`; Panel landing is stubs |
| Creatures volume list/grid | Built | `CreaturesLayer.tsx` — must become `ActorCollection` |
| `ActorListPickerPanel` | Wiring | Row-only; should compose `ActorCollection` |
| Derived sheet / paper-doll / action corpus on sheet | Real / specified | specs exist; `ActorView` fields pending; gear empty |
| Per-unit HUD | Specified + partial | plate 10 / actor-hud program |
| Commander chips | Specified | `spec-lawn-hud-chip.md` |
| Commander action bar | Real gap | No off-board action hotbar; battle plate 04 bar is the wrong stage |
| Cell overlap draw | Wiring / real | Occupants exist; stack offset + `+K` pip not a specified cluster |
| `ActorView` resources/status/actions | Real gap | `types.ts:575-591` |
| `ResourceView` missing `poise` | Wiring (stale contract) | `types.ts:640` vs resource-hub six |

---

## 12. Tunables (presentation only)

Any number a balance pass would change for *feel of chrome* lives in FE tokens / a small
`data`-adjacent presentation file — **not** Core Policy. Candidates:

- Cell stack offset px, max sprites before `+K`
- Dock width
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
- Making `PassivesTab` specimen-scoped (Paths tab *requires* it; that is actor-sheet work, called
  out as a dependency, not designed twice here).

---

## 14. Open questions (owner)

Answerable. Not manufactured.

1. **Dock default on cell click:** always open the occupancy list, even for a single occupant, or
   skip the list and open `ActorSheet` when count = 1? (Recommendation: **always list** — overlap
   and "I thought I clicked the other one" are the failure; one extra click on a singleton is
   cheap.)
2. **Action bar caster:** this-match Commander only, vs summoner-Dave plus commander? (Recommendation:
   **Commander only**, HUD already names them; Dave is the save, not a second hotbar.)
3. **General-demon Paths:** show the species tree read-only on wild units, or hide Paths until
   unique? (Recommendation: **read-only species tree** — Almanac teaching in place, GG-45.)

---

## 15. Next

Owner review of this file + [12-lawn-stage.html](12-lawn-stage.html). Then `/spec` only if this
graduates — capability map / module specs would be a `lawn-interactive` (or actor-sheet retarget)
program, **not** `tasks/plan.md`.
