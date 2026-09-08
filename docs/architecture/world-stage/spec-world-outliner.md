# Spec: world-outliner

**Status: Draft — Phase 1 (Specify), awaiting owner review.** Module id `world-outliner` in the
[world-stage capability map](../world-stage-map.md). **Level 4**, depends on `world-hud` (which owns
the right-edge anchor, shared with the notification rail above it).

**Ideal:** [world-stage-ideal.md](../world-stage-ideal.md) §4.3, §8c.1, §8e.1, §8e.3.
**Catalog:** [design/11-world-stage.html](../../design/11-world-stage.html) §I.1 and §I.3 — **§I.2's
map controls belong to `world-shell`**, and §I.1 carries one paragraph this spec deliberately
contradicts (see below).

---

## Objective

Build the **outliner**: the right-edge list of your legions and your held sectors, where every row
names its subject in player words, states the one or two facts that would make you act on it, and
selects and centres that subject when chosen.

**Success is that the entire map is reachable, and every subject on it actionable, without the
pointer ever touching the canvas.**

### Its justification has moved twice, and this spec carries the current one

This matters enough to state at the top, because the previous two are both still written down
somewhere and a session that reads the wrong one will build the wrong thing.

**Withdrawn (§8c.1).** The original argument was Amplitude's absence-of-an-overview friction — ES2
*"lacks an overview of any kinds that easily lists the planets and ships available on your empire"*,
and the community's *"grand circuit of your empire once every 20 turns."* That is a complaint about
**40 systems and 25 fleets**. Six sectors fit on screen at once, so the inference did not transfer —
and importing it violated §3's own *"recorded absences — do not fill these by inference"* rule.
**Plate 11 §I.1 still opens with that withdrawn argument** (*"the outliner is the price of having no
minimap"*, quoting the ES2 complaint). It is stale on that point; the plate is the drawing, this spec
is the reason.

**Current, on two narrower grounds that hold at our scale:**

1. **It is the keyboard entry point into a canvas that has none.** This is the load-bearing reason.
   Grep `web/fusion-rpg-web/src/features/world/` — 20 source and test files plus a fixtures
   directory — for `onKeyDown|keydown|Escape|tabIndex|role=` and it returns **zero hits**. The single
   accessibility affordance in the entire feature is one `aria-pressed` at `WorldPage.tsx:318`. A canvas
   needs a linear, focusable list to be reachable at all, and that is true at six nodes and at sixty.
2. **§8e.3 gives it a population.** 6–10 legions, tunable, plus up to 18 sectors.

### And that population changes its shape: it needs grouping and filtering after all

§4.3 was rewritten on 2026-09-03 to say the list was *"short by construction"* and needed neither
grouping nor filtering. §8e.3 invalidated that the same day: at 6–10 legions plus up to 18 sectors it
indexes **~28 rows**, past the point where a human scans rather than searches. Stellaris' outliner
groups and filters because that is exactly the scale at which scanning stops working.

**Plate 11 §I.1's paragraph beginning *"Why the list is short by construction"* — which concluded
*"Stellaris' outliner… groups, filters and paginates because it has to; ours does not, so it does
not"* — was superseded, and has since been repaired.** *Updated 2026-09-03: the plate now reads
"Why this list needs grouping and filtering after all" and argues this module's position. The
warning is kept as history rather than deleted, because the claim is the kind that gets
re-derived.* **Its other superseded claim is still live in the plate** — §I.1 still opens with the
withdrawn "the price of having no minimap" argument borrowed from a 40-system game. This is the second time this surface's
justification has moved, and it is the least stable part of §4 under scrutiny; carrying the history
here is cheaper than re-deriving it wrongly a third time.

**A size trigger, not a flat claim.** The argument above holds at `small` and `medium` — the only two
tiers `WorldSizeCatalog` marks available. **`world-generator` shipping a tier above `medium` (~32, ~64
or ~128 nodes) reopens this module's shape and §4.2's camera model together**, and the two must be
revisited as one: an outliner sized for 28 rows and a camera sized for ≲20 nodes fail at the same
moment, for the same reason.

## Design

### 1. Structure: two groups, a filter, and a flagged-first sort

```
┌ Your empire                                    2 · 4 ┐
│ [ needs orders ] [ fading ] [ all ]     ← filter chips│
│                                                       │
│ Legions (2)                                           │
│   Ash Column      marching · 500‰ left · 4 turns supply│
│                   ⚑ no orders                          │
│   Third Furrow    holding · 0‰ left · Verdant Reach    │
│                                                       │
│ Sectors (4)                                           │
│   Verdant Reach   +61 loam/turn · stability 980‰       │
│   Sallowfen       −12 loam/turn · stability 420‰ · fading│
│   Ashfall         −37 loam/turn · stability 90‰ · releases│
└───────────────────────────────────────────────────────┘
```

- **Legions first, then sectors.** Two groups, each with a count in its header. The header is a real
  heading, and each group is collapsible — collapsing is the cheapest form of filtering and it
  survives a rebind, a resize and a screen reader.
- **Within a group, anything flagged sorts above anything quiet.** A legion with no orders, a sector
  that will release next turn, a sector that is fading: these rise. The sort is stable below that, so
  a row does not move under the pointer for a reason the player cannot see.
- **Filter chips, not a search box.** At 28 rows a text search is the wrong instrument — the player
  does not know the name they are looking for, they know the *condition*. Three chips cover the
  conditions the game actually produces: **needs orders** (the same predicate the turn cluster
  counts), **fading** (stability falling or `WillReleaseNextTurn`), and **all**. Chips are exclusive,
  and the active one is stated in words, never by fill alone.
- **It scrolls inside its own shell.** If the list outgrows its height, the list body scrolls and the
  stage behind it does not move to compensate — GG-61, the same contract the inspector honours on the
  opposite edge.

The right edge holds the notification rail above and this beneath (§4.3's anchor table). §8e.1 moved
the inspector to the left specifically so this edge is not contested: the two were both right-anchored
and claimed ~620px of a 1280px floor.

### 2. Row types, and the field behind every fact

**Legion row.** Stance, movement remaining, supply runway, and the unresolved-orders flag.

| Fact | Family | Source | State |
|---|---|---|---|
| Stance | enum string, .NET casing | `WorldDtos.cs:182`; `march` / `scout` / `hold` from `MovementPolicy.Stances` (`Movement/LaneCost.cs:13`) | built |
| Movement left | per-mille · int | `WorldDtos.cs:183`; full march 1000‰ (`LaneCost.cs:23`), scout 500‰ (`:26`), hold 0 | built |
| Supply runway | count of turns · int | `LegionSupply.LeashTurns` (`src/FusionRpg.Core/World/Loam/LegionSupply.cs:32`) — implemented, **zero production callers, on no DTO**; it reaches a client only inside a narration string | wiring gap |
| Carried loam | whole units · long | `WorldEntity.CarriedLoam` — live state, **no DTO field** | wiring gap |
| Unresolved-orders flag | boolean | client-derived, **imported from `world-turn`'s `unresolvedLegions.ts`** | real gap |

**Sector row.** Net flow, fade risk, and the will-release warning.

| Fact | Family | Source | State |
|---|---|---|---|
| Net flow | whole units · long | `WorldDtos.cs:95` `LoamNet`, owner-only | built |
| Fade risk | per-mille · int | `WorldDtos.cs:71` `StabilityMilli`, 0–1000 | built |
| Fade pressure | per-mille · int | `WorldDtos.cs:72` `PressureMilli` — declared on the DTO and never assigned | wiring gap |
| Will release next turn | boolean | `WorldDtos.cs:125` `WillReleaseNextTurn` | built |

**The unresolved flag is imported, never re-derived.** `world-turn` owns
`unresolvedLegions.ts`; this module consumes it. It is the same fact the turn cluster counts, shown
where the player can act on it — and two derivations of one fact is how a count of 2 comes to sit
beside three flagged rows.

**Every number goes through `world-numbers` with its family declared.** Three families appear in one
row (`500‰`, `4 turns`, `+61 loam`) and they are not interchangeable.

**Every state carries a glyph and a word as well as a colour.** The current map encodes sector health
as `opacity = 0.35 + 0.65 × stability/1000` — one channel, and the weakest one. A short supply runway
loses **pips**; it does not change hue. Nothing on this surface is distinguishable by colour or
opacity alone.

**Nothing states a fact below 12px.** These rows are the densest text on the stage and therefore the
first place a designer reaches for 9px. XAG 101's floor is 18px at 1080p, and glyph text — the stance
mark, the flag, the fading drop — meets the floor and scales to 200% along with everything else.

### 3. Selecting and centring — the dispatch that has never existed

A row does two things: it selects its subject, and it centres the camera on it.

`worldSelection.ts` already carries both actions —
`{ type: "select-sector"; sectorId: string | null }` at `:29` and `select-entity` at `:30` — and
**nothing in the feature dispatches the `null` case**, which is the dead end `world-inspector` fixes
from the other side. There is no row list at all today.

Centring is a request to the camera (`world-shell`'s `viewBox` transform), never a mutation of it
from here. A row does not know the camera's implementation and does not read it back.

### 4. Focus and selection are drawn differently, on purpose

Conflating them is how a keyboard user loses their place: they arrow down four rows to look at
something, and the map has been re-centring and re-selecting under them the whole way.

- **Focus** moves with arrows and is drawn as a focus ring on the focused row. It changes nothing
  else — no selection, no camera.
- **Selection** happens on `⏎` or a click, is drawn as a filled/solid row state, and is what centres
  the camera and drives the inspector.

**The accessibility defect to avoid, named because the plate drew it.** §I.1's rows are `<div>`s with
`cursor:pointer`, **no `role`, no `tabindex`**, and a class-driven focus ring on an element that
**cannot receive focus**. That is a focus ring drawn on something the browser will never focus — the
visual of accessibility with none of it. The rows are real controls:

- The list is `role="listbox"`, rows are `role="option"` with `aria-selected`.
- **One roving `tabIndex`**: the active row is `tabIndex={0}`, every other row `tabIndex={-1}`, so the
  whole list is one tab stop and arrows move within it. No such pattern exists anywhere in the app
  today, so this module introduces it and owns it.
- Group headers are real headings, and each group's count is in its accessible name.

### 5. The keyboard model — the map's whole model, stated once

§I.3 draws the keys on the controls themselves rather than only in a settings page. This module owns
four of them:

| Key | Does |
|---|---|
| `O` | Focus the outliner |
| `↑` `↓` | Move focus between rows — **focus only** |
| `⏎` | Select the focused row and centre its subject |
| `Esc` | Hand focus back to the stage (and, with a layer open, pop that layer first — `keymap.ts:125-135` already orders this correctly) |

**The keymap trap applies here identically to `world-turn`, and the mitigation is shared.**
`registerGlobalVerb` **throws** on a duplicate (`shell/keymap.ts:45-50`), the eight rail bindings
(`layers/system/keybindings.ts:22-31`) are player-rebindable, and `conflictFor` (`:87-95`) checks a
candidate only against the other seven bindable actions — it cannot see a stage's own verbs. A player
who rebinds a rail layer to `o` would otherwise take the stage down on mount. This module registers
`O` through `world-turn`'s single `worldVerbs.ts` owner, which reports a collision in player words
instead of throwing.

## What stays out

- **Map controls.** §I.2's zoom, fit, lens picker and fog toggle are `world-shell`'s and
  `world-lenses`', despite sharing plate §I with this module. Read §I as two surfaces, not one.
- **Notifications.** The rail above this on the same edge is `world-notify`'s.
- **The unresolved derivation.** `world-turn` owns it; this consumes it.
- **Sector detail.** A row names a subject; the inspector explains it. A row that grows a fifth fact
  is an inspector escaping onto the edge.
- **Enemy legions.** The outliner lists **yours**. `WorldStateDto.Entities` carries only the viewer's
  own forces by design; anyone else's is fogged into `WorldSectorDto.Forces` at whatever detail it was
  seen, and putting a band into a list that otherwise reads as exact is how fog becomes cosmetic.


### GG-50 — this surface's volume declaration

**Tier-1 gate, and it was missing from all fifteen specs until the 2026-09-03 audit.** `ui/volumeMatrix.test.ts`
is an *exhaustive* registry — its last test is `expect(COLLECTION_SURFACES).toHaveLength(8)` — so a new
collection surface that does not register **turns a shipped test red**. Registration is not optional
paperwork; it is how this program lands without breaking CI.

| Surface | `Outliner (legion + sector rows)` |
|---|---|
| Strategy | **`render-all`** |
| Reason | Bounded by the two map tiers `WorldSizeCatalog` marks available: at §8e.3's target that is **10 legions + 18 sectors ≈ 28 rows**. Grouping and filtering exist to make 28 rows *scannable*, not to page them — the list itself renders whole. **Trigger:** `world-generator` shipping a tier above `medium` (~32 / ~64 / ~128 nodes) reopens this and §4.2's camera model together |
| Proof | The 28-row fixture this module already requires, asserted for rendered node count |

## Commands

```powershell
cd web\fusion-rpg-web
npm test                 # vitest run
npm run build            # tsc --noEmit && vite build
npm run lint
```

## Project structure

```
web/fusion-rpg-web/src/
  stages/world/outliner/
    Outliner.tsx             → listbox, groups, roving tabIndex, Esc-back-to-stage
    OutlinerFilter.tsx       → the three exclusive chips
    LegionRow.tsx            → stance · movement · runway · unresolved flag
    SectorRow.tsx            → net flow · fade risk · will-release
    outlinerModel.ts         → grouping, flagged-first sort, filter predicates — pure
    outlinerModel.test.ts
    *.test.tsx
```

`outlinerModel.ts` is pure and takes views in, rows out — the grouping and sort are testable with no
DOM, which is the same split the lawn and `worldViewModel.ts` already use.

## Code style

A row is a real option, never a clickable `div`:

```tsx
// Right.
<li
  role="option"
  aria-selected={selected}
  tabIndex={active ? 0 : -1}
  onKeyDown={onRowKeyDown}
  className={cn("wsd-row", focused && "wsd-row--focused", selected && "wsd-row--selected")}
>

// Wrong, and it is what plate §I.1 drew: a focus ring on an element that cannot take focus.
<div className="cursor-pointer focus:ring-2" onClick={…}>
```

Sorting is declared, not incidental:

```ts
/** Flagged above quiet, then stable. A row must never move for a reason the player cannot see. */
const flagged = (r: OutlinerRow) =>
  r.kind === "legion" ? r.needsOrders : r.willReleaseNextTurn || r.fading;
```

## Testing strategy

Vitest, colocated. Six groups, and group 2 is the module's whole reason to exist.

1. **Model.** Grouping, counts, flagged-first sort stability, and each filter predicate — over a
   fixture at the §8e.3 target: **10 legions and 18 sectors, 28 rows.** A fixture of 1 legion tests
   nothing this module was built for.
2. **Keyboard end to end, with no pointer at all.** `O` focuses the list; `↑`/`↓` move focus and
   assert **selection did not change and the camera was not asked to move**; `⏎` selects and centres;
   `Esc` returns focus to the stage. This is the test that proves the load-bearing justification.
3. **Focus ≠ selection.** Focus four rows down, assert exactly one `aria-selected` and that it is
   still the original row.
4. **Roving tabIndex.** Exactly one row has `tabIndex={0}` at all times, including after filtering
   changes which rows exist and after the active row is filtered away.
5. **Colour independence.** Every row state — fading, releasing, no-orders, short runway — is
   asserted by its **text or glyph**, queried by accessible name. A test that can only find a state by
   its class has not tested the rule.
6. **Roles and names.** `role="listbox"` / `role="option"`, group headings present, counts in the
   accessible name, and no bare `<div onClick>` in the module (the pattern §4 names).

## Boundaries

- **Always:** render rows as real options with a roving tabIndex; draw focus and selection
  differently; import the unresolved predicate from `world-turn`; put every number through
  `world-numbers` with its family; give every state a glyph and a word; scroll inside the shell.
- **Ask first:** adding a fifth fact to either row type — that is the inspector escaping onto the
  edge. Adding a fourth filter chip. Anything that changes `keymap.ts` or `keybindings.ts`, including
  the `conflictFor` widening `world-turn` §4 asks for. Listing anyone's legions but the player's.
- **Never:** re-derive the unresolved-orders flag. Never distinguish a row state by colour or opacity
  alone. Never move the camera on focus. Never state a fact below 12px, glyph text included. Never
  reproduce plate §I.1's *"short by construction"* claim or its withdrawn ES2 justification. Never
  scroll the stage to make room for this list.

## Success criteria

1. The whole map is reachable and every subject actionable with the keyboard alone, in a feature that
   today has zero keyboard affordances.
2. 28 rows — 10 legions, 18 sectors — group, sort flagged-first and filter, and the module's tests run
   at that size.
3. Focus and selection are visually and semantically distinct, and arrowing never selects or centres.
4. Every row state is findable by text or glyph with colour removed.
5. Rows are `role="option"` inside a `role="listbox"` with one roving tab stop; no clickable `div`
   remains.
6. The unresolved flag and the turn cluster's count come from one module and cannot disagree.
7. The size trigger is recorded in the module's own header: a tier above `medium` reopens this and the
   camera together.
8. `npm test`, `npm run build` and `npm run lint` are green.

## Open questions

**None.** §8e.1 settled the edge, §8e.3 settled the size and with it the grouping-and-filtering
question, and the two stale paragraphs in plate §I.1 are contradicted explicitly above rather than
left to be discovered.
