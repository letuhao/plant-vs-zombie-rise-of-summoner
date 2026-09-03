# Spec: world-inspector

**Status: Draft — Phase 1 (Specify), awaiting owner review.** Module id `world-inspector` in the
[world-stage capability map](../world-stage-map.md). **Level 4**, depends on `world-render` and
`world-numbers`.

**Ideal:** [world-stage-ideal.md](../world-stage-ideal.md) §4.4, §4.5, §4.9, §8c.2, §8d.2, §8d.3, §8e.1.
**Catalog:** [design/11-world-stage.html](../../design/11-world-stage.html) §J (J.1–J.5), with §K's
confirms belonging to `world-confirms`.

---

## Objective

Build the **sector inspector**: the band-2 layer that opens when a sector is selected, docked to the
**left** edge beside the layer rail, carrying everything known about one sector and every verb that
acts on it — and closing again on a gesture that today does not exist.

It is the densest single entity on the stage. Type, climate, phase, intel state and its age, danger,
stability, four loam readings, four territory-component readings, a slot list, a force list,
structures with construction progress, warden state, prospecting, and the action cluster. The plate
measured **1,597px of body content in a 400px well**, with the stage behind it reporting
`scrollHeight − clientHeight = 0`. That measurement is the whole design constraint, and it is the
case GG-61 was written for.

**Success is that a full sector fits on a 1280×720 floor without the stage moving, and that no verb
is ever greyed out without saying why in words a player reads rather than a token a developer reads.**

### Why it docks left, and why that is not cosmetic

§8e.1 moved it. The inspector (380px) and the outliner (224px) were both drawn right-anchored,
claiming ~620px of a 1280px floor — a collision §8c.5 found and nobody had priced. Selection detail
now lives left; persistent empire state stays right, which is also what Stellaris, Civ VI and Total
War all do.

It docks **beside** the layer rail, not over it. The rail is `w-[92px]` at `band-hud`
(`web/fusion-rpg-web/src/shell/Rail.tsx:31`) and keeps its corner role, so §4.3's fixed-role table
survives with exactly one documented asymmetry: the left edge now has a conditional occupant.

## Design

### 1. The shell — and why `PanelShell` cannot be used as-is

`PanelShell` already satisfies half the contract and violates the other half.

| What it gives us | Line | Verdict |
|---|---|---|
| A bounded height, `max-h-[min(720px,82vh)]` | `PanelShell.tsx:81` | **Keep** — this is GG-61's bound, already correct |
| A body that is the only scrolling part, `min-h-0 flex-1 overflow-y-auto` | `PanelShell.tsx:93` | **Keep** |
| Layer-stack registration as band `panel`, with `close` owned by the stack | `PanelShell.tsx:40-44` | **Keep** — this is what makes Esc work at all |
| Radix focus trap and focus-restore-to-opener | `PanelShell.tsx:50-54`, `:66-69` | **Keep** |
| Centred geometry: `fixed left-1/2 top-1/2 w-[min(640px,92vw)] -translate-x-1/2` | `PanelShell.tsx:79` | **Wrong shape** — a dock is edge-anchored and full-height |
| A full-viewport scrim, `fixed inset-0 bg-black/50` at `band-panel` | `PanelShell.tsx:60-63` | **Wrong band** — see below |

So this module adds a **`DockShell`** beside `PanelShell` in `src/shell/`, not a copy of it: same
stack registration, same focus behaviour, same GG-61 bound, edge-anchored geometry, and a scrim that
does not cover band 1.

**The scrim point is §8d.3 and it is kit-wide, not ours.** `--band-hud: 100` and `--band-panel: 200`
(`web/fusion-rpg-web/src/theme/tokens.css:102-103`), and `PanelShell`'s overlay is `inset-0` at
`band-panel`, so today *every* stage's HUD goes dark under *every* panel. §8d.3 rules that a band-2
layer scrims the **stage**, not the HUD. **This module consumes that change; it does not make it** —
the amendment belongs in the band model and in `game-gui-principles.md`'s GG-5 table, and it affects
Sanctum and Lawn too. If it has not landed when this module builds, the inspector ships with the
scrim it is given and the defect is recorded, not worked around locally with a `z-index`.

### 2. The blocks, in order, and the field behind each

Nine blocks. The order is the plate's, and it is deliberate: identity first, then the thing that can
take the ground away from you, then the two economies, then what is on the ground, then what you can
do about it.

| # | Block | Reads | Note |
|---|---|---|---|
| 1 | **Identity header** | `SectorId`, `TypeId` (`WorldDtos.cs:66`), `Climate`, `Phase`, `DangerBand`, `Intel` (`:127`), `IntelAge` (`:132`) | Four intel states, each with an age stamp when stale |
| 2 | **The ground** | `StabilityMilli` (`:71`), `PressureMilli` (`:72`), `DevelopmentLevel`, Fracture intensity | `PressureMilli` is declared and never assigned — `Pending` until `world-wire` |
| 3 | **Next turn** | `WillReleaseNextTurn` (`:125`) plus its reason, and the *keep* / *release first* pin | §3 below — the most delicate block on the surface |
| 4 | **This sector's loam** | `LoamProduction` (`:89`), `LoamUpkeep` (`:92`), `LoamNet` (`:95`), `LoamStock` (`:114`) | Earns · costs · net · in store |
| 5 | **Its territory** | `ComponentId` (`:102`), `ComponentProduction` (`:104`), `ComponentUpkeep` (`:105`), `ComponentNet` (`:106`), `ComponentStock` (`:117`) | §4 below — the block that makes *"my empire is fine"* falsifiable |
| 6 | **Slots** | `Slots` (`:135`), each with `State`, `GuardState` (`:33`), `GuardWaveId` (`:32`), `StructureId` (`:39`) | Seven row states — §5 |
| 7 | **Forces** | `Forces` (`:138`): `Exact` (`:52`), `Strength` (`:55`), `BandName` (`:57`), `BandCeiling` (`:60`) | Yours exact; anyone else's a band |
| 8 | **Warden** | `WardenBindingId` | No DTO field; `Pending`. The verb is `world-confirms`' |
| 9 | **Dowsing** | `Prospecting.Reveal` (`IntelRecorder.cs:179`) | The stance is missing from `MovementPolicy.Stances` (`Movement/LaneCost.cs:13`) — `world-commands`' gap |
| — | **Actions** | The sector's whole verb set, every refusal carrying its reason | §6 |

**Nothing below 12px, anywhere.** XAG 101's floor is 18px at 1080p, and the plate audit found 58
raw-`px` font sizes, 52 of them at 8–9px. Every one of the nine blocks above states a fact, and a fact
below the floor is not stated. Glyph text counts as text: an intel stamp, a slot glyph and a stance
mark all meet the floor and all scale to 200%.

### 3. The pin — the deferred decision this surface exists to unblock

`spec-loam-fe.md:80` says *"the player can pin a sector as keep or mark one as release first,"* and
`:82-84` records that `spec-loam-turn` **deferred it until there was a surface to set it from. This is
that surface.** So block 3 owns the pin, and it is the first thing to cut if it proves fiddly at
Gate B — that escape clause is `spec-loam-fe.md`'s own and it stands.

**And here is the constraint that outranks it.** The engine does not let you choose. `LoamPhases`
picks the release target itself, every turn, via `LoamForecast.Weakest`
(`src/FusionRpg.Core/.../LoamPhases.cs:138`), and there is no `cede` / `release` / `abandon` command
kind — `WorldCommand.All` is `{ StandFast, Move, Clear, Claim, Stance, Sustain, Build }`
(`WorldCommand.cs:36-37`).

**So, stated as a hard rule this module obeys without exception (§8c.2, §8d.2):**

> **No copy on this surface may say "choose what to release" until `world-commands` has shipped the
> cede order.** Until then, block 3 reads *"here is what will be released next turn, and here is what
> would stop it"* — truthful, and it ships now. Plate 11 §J.1 currently draws *"Keep this ground"* and
> *"Give this up first"*; those two controls are **drawn against a verb that does not exist**, and
> shipping them as drawn is a lie the player catches on their first shortfall.

The pin's controls are therefore written against a capability flag from `world-commands`, and the
module's test suite asserts **both** states: with the cede order absent, the two controls are not
rendered at all and the forecast copy is the truthful one; with it present, they render and file a
real order.

### 4. The territory-component block is a first-class block, not a detail

After the settlement rule, *"my empire is fine"* can be true while this component starves — the HUD's
empire total and this sector's own reach are different numbers with different answers. The wire
already carries all five fields, and `LoamGauge.tsx:28-45` already computes and warns per component
for the HUD strip. The inspector's job is the **detail** half of §8b.5's *summary up, detail down*
split: the full breakdown lives here, the strip lives in `world-hud`, and the two read the same
projection so they cannot disagree.

The block says plainly which of the two is starving. Deriving it from four numbers is what the
current map makes the player do.

### 5. Slot rows — seven states, and the two nothing has ever drawn

The row is the product of `SlotState` (`Intact` / `Claimed` / `Depleted` / `Ruined`), `GuardState`
(`Intact` / `Cleared`), and whether a structure is present and finished. **The player never sees
either enum** (GG-23). Seven rows result: empty, built, under construction with turns remaining,
guarded (with its `GuardWaveId` named as a force, not an id), cleared, depleted, ruined.

Two of the seven — depleted and ruined — are in the model and have never been drawn anywhere. They
are specified here because a list that silently cannot represent two of its own states is a defect
that only appears in a save nobody tested.

`ConstructionTurnsRemaining` has no DTO field and is `Pending`; a row that shows *"ready in 3 nights"*
cannot ship before `world-wire` projects it, and until then that row says so in player words rather
than showing a blank.

### 6. The action cluster — every refusal carries its reason, in player words

GG-55 is the rule; plate 03 §E already settled the wording — *"disabled with its reason beside it,
always"*; plate 11 §J.4 is the full verb set with the reasons written out. This module implements
that list and nothing about it is optional.

**Two properties, and the second is the one that gets lost:**

1. **Never hidden.** An unavailable verb stays in the cluster, greyed, in its place. Hiding it is
   AoW4's failure — the verb becomes unfindable and the player concludes it does not exist.
2. **The reason is visible, not a tooltip.** `ui/disabledReasonGuard.ts:57` accepts
   `title` / `aria-label` / `aria-describedby` as satisfying GG-55, and its scan
   (`:59-75`) will pass a control whose only reason is a hover string. **That guard is the floor, not
   the bar.** This module's own tests assert a *rendered sentence* beside every disabled verb — a
   hover reason is unreachable on touch, invisible to a keyboard user who has not focused it, and
   this cluster is where the player learns why the game said no.

The engine token never renders. `claim.contested`, `build.cannot-afford`, `sustain.nothing-carried`,
`path.not-contiguous`, `lane.severed`, `capacity.full`, `entity.routed` and the rest map through the
**one** translation table `world-playback` owns — the same table, not a second copy, because the
turn report and the refusal reason are the same vocabulary and two tables drift.

### 7. One dismissal gesture, applied without exception

This is §4.4's rule and it fixes a real dead end. `worldSelection.ts:29` declares
`{ type: "select-sector"; sectorId: string | null }` — the reducer accepts `null` and **nothing in the
feature ever dispatches it.** There is no close control and no `onPaneClick`. Today a sector, once
selected, cannot be deselected at all.

Four gestures, one outcome:

| Gesture | Does | Why this one |
|---|---|---|
| `Esc` | Pops exactly one layer — the inspector closes, the map keeps its camera and its selection | Amplitude state it as a manual rule; GG-6 already gives Esc fixed stack semantics |
| Right-click on the map pane | The same thing | Same rule, same sentence |
| The `✕` in the header | The same thing | Pointer users who learn neither gesture |
| Click the selected sector again | Deselects — the inspector closes with it | This is the dispatch of `select-sector: null` that has never existed |

**Esc is a shell-level ordering question, not a world special case.** `handleEscape`
(`shell/keymap.ts:125-135`) already walks the stack top-down for an Esc-dismissible band and falls
through to `emptyStackEscapeFallback` only when nothing is open; `SystemHost.tsx:26-29` claims that
fallback. So a band-2 `DockShell` on the stack is popped **before** the system menu opens, with no
change to `keymap.ts` at all — the ordering is already correct and the inspector simply has to be a
real stack entry rather than inline chrome. If it needs Esc without a rendered Dialog,
`claimStageEscape` (`keymap.ts:113-116`) already exists for exactly that.

### 8. Fog: the branch that must be on `intel`, never on emptiness

An unknown sector serialises **every field at its record default**
(`src/FusionRpg.Server/WorldEndpoints.cs:269-277` returns a `WorldSectorDto` carrying only
`SectorId`, `Intel`, `Phase`, `LayoutX`, `LayoutY`). On the wire it is indistinguishable from a zeroed
known one **except by `Intel`**. Every block above branches on `Intel`, never on a zero or an empty
list.

The four states are `Unknown` / `Rumored` / `Scouted` / `Watched`, decided by
`FactionIntel.StateOf` (`src/FusionRpg.Core/.../FactionIntel.cs:133-140`) with `AgeOf` at `:143-144`.
Each gets a distinct header treatment, and the stale two carry `IntelAge` **in turns, stated in
words** — *"4 nights old"*, not a bare integer. A `Rumored` sector's slot list is empty *by design*
(`WorldDtos.cs:134-135`: *"empty unless the viewer has actually stood here"*), so the panel says
*a glimpse sees no slots* rather than drawing an empty list that reads as *nothing here*.

`Scouted` shows ground, buildings and remembered ownership; it shows **no forces** — Civ VI's
static-vs-dynamic line, and a remembered army is a lie waiting to happen.

## What stays out

- **The confirms.** Commit-a-legion, bind-a-warden and the abandon warning are `world-confirms`'
  band-3 layers. This module renders the verb that opens them.
- **Targeting.** `March here` reaches past this sector, so its route preview and range overlay are
  `world-targeting`'s, drawn **on the map**. The inspector holds only the sector's own verbs.
- **The magnitude renderer.** `world-numbers` owns it, including the `CostMilli` trap. Every number
  in every block above goes through it with an explicit family.
- **The translation table.** `world-playback` owns it. This module is a consumer.
- **The cede order itself.** `world-commands`. See §3 — this module's obligation is to not draw it
  early.
- **The band-1 scrim fix.** §8d.3 is a kit-wide band-model change.


### GG-50 — this surface's volume declaration

**Tier-1 gate, and it was missing from all fifteen specs until the 2026-09-03 audit.** `ui/volumeMatrix.test.ts`
is an *exhaustive* registry — its last test is `expect(COLLECTION_SURFACES).toHaveLength(8)` — so a new
collection surface that does not register **turns a shipped test red**. Registration is not optional
paperwork; it is how this program lands without breaking CI.

| Surface | `Sector inspector — slot rows` |
|---|---|
| Strategy | **`render-all`** |
| Reason | Bounded by authored sector content: the highest `SlotIndex` across both shipped templates is **3**, i.e. four slots. `SectorTypeCatalog.AllowedSlotTypes` constrains which *kinds* a sector may hold, not how many — so this bound is content, not structure, and moves only when `world-generator` authors wider sectors |
| Proof | A maximal-sector fixture, which this module's GG-61 test already requires |

| Surface | `Sector inspector — force rows` |
|---|---|
| Strategy | **`render-all`** |
| Reason | Bounded by how many entities can co-locate in one sector — at most one per faction in practice, and `first-light` peaks at 1. Even at §8e.3's target the list is single-digit. Enemy entries are **bands**, not per-unit rows, so an opposing army does not lengthen it |
| Proof | The contested-sector fixture in this module's force-row tests |

## Commands

```powershell
cd web\fusion-rpg-web
npm test                 # vitest run — includes disabledReasonGuard and the shell-height fixture
npm run build            # tsc --noEmit && vite build
npm run lint
```

## Project structure

```
web/fusion-rpg-web/src/
  shell/
    DockShell.tsx              → edge-anchored band-2 shell; bounded height, internal scroll
    DockShell.test.tsx
  stages/world/inspector/
    SectorInspector.tsx        → the shell + block order
    IdentityHeader.tsx         → four intel states, the age stamp
    GroundBlock.tsx
    NextTurnBlock.tsx          → WillReleaseNextTurn + the pin, gated on the cede order
    SectorLoamBlock.tsx
    ComponentBlock.tsx         → the territory readings
    SlotRow.tsx                → seven states
    ForceRow.tsx               → exact vs band
    WardenBlock.tsx
    DowseBlock.tsx
    ActionCluster.tsx          → every disabled verb with its rendered reason
    *.test.tsx
```

Nothing here imports a REST DTO — `world-contract`'s widened guard is what proves it.

## Code style

A block is a pure function of one `SectorView` and renders nothing it was not given. The intel branch
is explicit and appears once, at the top of each block that needs it, never as a truthiness check on
a value.

```tsx
// Right: the wire cannot distinguish unknown from zeroed, so the branch is on intel.
if (sector.intel === "Unknown") return <UnknownGround sectorId={sector.sectorId} />;

// Wrong, and it is the defect WorldEndpoints.cs:269-277 guarantees:
if (sector.slots.length === 0) return <UnknownGround … />;   // a Rumored sector also has none
```

A disabled verb is one shape, never two:

```tsx
<ActionRow
  verb="Build a Well"
  disabledReason={reasonFor("build.cannot-afford", { carried: 180, cost: 200 })}
/>
// renders the sentence beside the control, and sets aria-describedby to the same node.
// A `title`-only reason satisfies disabledReasonGuard.ts:57 and fails this module's own test.
```

## Testing strategy

Vitest, colocated. Six groups, and the first two are the ones the module exists for.

1. **The GG-61 fixture.** A maximal sector — every block populated, 8 slots, 4 forces, a warden, a
   construction in progress — rendered at the 1280×720 floor. Assert: the shell's own height never
   exceeds its bound; the **body** scrolls (`scrollHeight > clientHeight`); and the stage element
   behind it has `scrollHeight − clientHeight === 0`. This is the assertion the plate's 1,597px
   measurement demands, and it is the whole point of the module. It runs at 1280×720, 1440×900 and
   at 200% text scale.
2. **Dismissal, all four gestures.** Esc pops exactly one layer and the map keeps its camera and
   selection; right-click on the pane does the same; the `✕` does the same; clicking the selected
   sector dispatches `select-sector: null` and the inspector unmounts. Plus the ordering test:
   **with the inspector open, Esc does not open the system menu.**
3. **Disabled-with-reason.** For every verb in plate 11 §J.4's refusal table, assert a *rendered*
   sentence — queried by text, not by `title` — and assert no engine token appears in the accessible
   name or the visible text.
4. **Intel branching.** Four sectors, one per state, sharing an identical zeroed payload. Assert the
   four render differently and that the `Unknown` case is reached without reading a field other than
   `intel`.
5. **The cede embargo.** With the cede capability absent, assert the pin controls are **not in the
   document** and the copy contains neither "choose" nor "release first"; with it present, assert both
   render and file an order. This test is what keeps §8c.2's finding from re-entering by drift.
6. **Slot rows, all seven.** Including depleted and ruined, which nothing has drawn before.

## Boundaries

- **Always:** branch on `intel`; render a disabled verb's reason as text; declare the shell's bound
  and let the body scroll; put every number through `world-numbers` with its family; keep the stage's
  camera, selection and subscriptions across an open/close cycle (GG-11).
- **Ask first:** any change to `PanelShell` itself — ten shipped surfaces bind to it, and `DockShell`
  exists so this module need not touch it. Any addition to the block list beyond the nine above. Any
  copy that implies a verb the engine does not have.
- **Never:** say "choose what to release" before `world-commands` ships the cede order. Never render
  an engine token on this surface. Never let a `Rumored` sector's empty slot list read as *nothing
  here*. Never scroll the stage to make room. Never write a `z-index` — the band classes are the only
  stacking vocabulary (GG-5). Never state a fact below 12px, glyph text included.

## Success criteria

1. A maximal sector renders at 1280×720 with the shell inside its bound, the body scrolling, and the
   stage measuring `scrollHeight − clientHeight === 0`.
2. All four dismissal gestures work, `select-sector: null` is dispatched for the first time in the
   feature's life, and Esc with the inspector open never reaches the system menu.
3. Every verb in §J.4's table renders its reason as visible text; no engine token appears anywhere on
   the surface.
4. The nine blocks render from `SectorView` alone, with each unwired field carrying a
   player-readable `pending` reason rather than a blank or a zero.
5. The territory-component block shows a starving reach while the empire total is positive — the
   case §4.3 calls first-class.
6. The pin is absent-and-truthful without the cede order, present-and-real with it, and a test
   asserts both.
7. `npm test`, `npm run build` and `npm run lint` are green.

## Open questions

**None.** §8e.1 decided the dock, §8d.2 decided the cede order and §8d.3 decided the scrim; the only
thing left conditional — whether the pin renders — is resolved by a capability check with both
branches specified and tested above.
