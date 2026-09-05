# Spec: world-render

**Status: Draft — Phase 1 (Specify), awaiting owner review.** Module id `world-render` in the
[world-stage capability map](../world-stage-map.md). **Level 3, depends on `world-shell` and
`world-contract`** — built in parallel with `world-hud`.

**Map-plane HOW superseded 2026-09-06.** React `SectorNode` **on the stage** and SVG lane drawing as
the player map are replaced by
[world-map-runtime/spec-world-map-runtime.md](../world-map-runtime/spec-world-map-runtime.md).
**This spec still owns:** channel functions, GG-27 matrices, fog intel-first, greyscale rule. The
inspector card (§A) is unchanged.

**Ideal:** [world-stage-ideal.md](../world-stage-ideal.md) §4.1, §4.2, §4.9, §8.2, §8b.6, §8c.5.
**Catalog:** [design/11-world-stage.html](../../design/11-world-stage.html) §A, §B, §C (and §D.3 for
the mid-lane marker).

---

## Objective

Draw every state the engine can actually put the map in — sectors, lanes, legions, fog, supply — on
channels that survive a greyscale print and a colour-vision deficiency. Nothing invented: every state
maps to a field on `WorldSectorDto` / `WorldLaneDto` / `WorldSlotDto`, and the catalog cites the field
behind each one.

Two rules carry this module, and both are currently broken in shipped code.

### 1. Tokens only

`features/world/` is the one directory carved out of the hex guard —
`SKIPPED_PATH_PREFIXES = ["game/", "features/world/"]` (`hexGuard.ts:27`), with the reason recorded
above it: *"the World stage, excluded this phase (T16, 2026-08-23 owner decision) — 'keep it as is'
means untouched, hex literals included, until its own plan lands"* (`:23-25`). This is that plan. The
exemption is not principled like the Phaser one beside it; it is a deferral with an expiry, and the
expiry is here.

What lives behind it: a six-entry lane palette in raw hex (`LaneEdge.tsx:11-18`), a severed-lane hex
(`:40`), an ownership hex pair on the legion marker (`:56`), and the marker's own stroke
(`LegionMarker.tsx:73`). Roughly a dozen literals, all of which have a token.

### 2. State is never carried by colour or opacity alone

GG-27 (*"status is legible from colour and shape/text, never colour alone"*) and GG-30. Three shipped
encodings fail it outright:

| Today | Where | Why it fails |
|---|---|---|
| `opacity = 0.35 + 0.65 × stability/1000` | `SectorNode.tsx:49-52` | A continuous dim is unreadable **as a value** — 38% and 9% must both stay legible, and a dimmed card is indistinguishable from a card behind a scrim |
| Slot kinds as the letters `S E M V L T ! $` | `SectorNode.tsx:29-39` | A letter is not a shape, and the map covers **9 of the 14** slot kinds |
| Danger as `"◆".repeat(n)` | `SectorNode.tsx:104` | Right channel, no number — a count with no denominator is not a reading |
| Ownership as `border-color` alone, fog as `opacity: 0.45` | `_kit/kit.css:380-383` | The kit's own `.sector` rung, superseded by plate 11's node per §8b.6 |

8–10% of male players cannot rely on red/green, and the genre's own evidence is blunt: the
most-subscribed mods for both Endless games are palette expansions, and a 2,697-subscriber ES2 mod
exists solely because two labels shared a colour.

**Success is that every fact on the map reads on at least two of shape · border treatment · pattern ·
icon · text, and that a greyscale screenshot of the stage loses nothing.**

## Design

### 1. The sector node — four orthogonal state slots, never two facts on one channel

A sector carries four independent facts at once — **who holds it**, **whether it can be kept**,
**what is on it**, **what it earns** — and each gets its own channels. A sector can be *yours* and
*fading* and *warded* and *building* and *about to be released*, and all five must read at once.

| Group | States | Channels |
|---|---|---|
| Ownership | yours · enemy · open · contested | Top rule + border + crest + **the word**. `OwnerFactionId`, `Phase` |
| Health | anchored · fading · barren · will-release · warded · neglected · Unmade | Hatch pattern + a **numeric** root-hold meter; barren is a *flat, distinct* look, not a deeper fade; will-release is a heavy left rule + ⚠ pill + words |
| Content | 14 slot kinds × 7 slot states | **Five silhouettes** (square, circle, hexagon, diamond, octagon) group the kinds; a glyph names one. Markers: guarded ⚔, built ▲, building ⏳ + turns |
| Yield | earns · costs · net | Sign on three channels, owner-only, whole loam units via `world-numbers` |

**Barren is the one that must not be a shade of fading.** `SectorNode.tsx:43-48`'s own comment already
has the reasoning right — *"barren ground can never be kept no matter what that number says this turn,
and a player who reads it as 'just another shade of fading' will make the wrong call every time"* —
and then implements it as an opacity branch. The reasoning survives; the encoding does not.

**Density ceiling and drop order.** The fully-populated node is the ceiling, and §4.2's zoom rule
binds here: at map zoom the slot row and the flags row drop first; **ownership, health and net never
drop.** Each zoom tier is a strict superset of the legibility below it — simplifying is allowed,
removing a fact only the other tier shows is not.

### 2. Lanes — six kinds × five states, stacked as separate channels

Kind and state are orthogonal and both must read: a warded, hazardous ley lane is drawable and reads
as all three.

| Kind | Treatment | Rule it encodes |
|---|---|---|
| `corridor` | Solid | Carries supply and pressure both ways |
| `rift` | Dashed | A hole, not a road — the default tear |
| `ley` | Twin rails | Element-typed; a matching banner marches cheaper |
| `deep` | Solid, marked no-supply | Passable, carries **no supply** |
| `one-way` | Arrowheads, always | Drawn direction only, no supply |
| `gated` | Long dashes + 🔒 at midpoint | Shut until its key or boss clears — `GateKeyId` non-null |

States stack on top: **Open** (the absence of everything else) · **Severed** (a real **gap** plus ✕ —
never a faded line, which reads as *"far away"*) · **Warded** (twin rails + shield + `WardLevel` as a
number, *"ward 3"*, never a %) · **Hazardous** (fine dots + ☠ + the printed chance;
`HazardMilli` 400 renders 40%).

**Width is stroke weight; length is a printed number.** A thin line must look like a chokepoint before
anyone reads a tooltip. Length is never encoded as line length — the layout is authored, so the drawn
distance means nothing. `strokeWidthFor` (`LaneEdge.tsx:24-26`) already does the width half correctly
and carries over unchanged.

### 3. Legion markers, including the mid-lane case

Ownership reads as **three shapes before three colours**. Position is either in-sector or a fraction
of the way down a lane at `LaneProgressMilli`, and the mid-lane case is the one with a real
implementation constraint attached.

`LegionMarker` animates along the lane's own `<path>` — `getElementById` (`:46`) →
`getTotalLength()` (`:50`) → `getPointAtLength()` (`:55`), writing `transform` in a
`requestAnimationFrame` loop so a marching legion costs **zero** React re-renders. **The technique
survives the library removal; the id contract does not** — `pathId` is documented as *"the `<path>`
element id React Flow gives this lane's edge"* (`:17-18`). `world-shell` declares the replacement
scheme in `stageIds.ts`; **this module must render lane paths that honour it**, or markers silently
stop moving with no error.

Enemy strength is a **band unless surveyed**: `Strength` is meaningful only when `Exact` is true and
is zero otherwise, so a distant force renders as `BandName` + `BandCeiling` — *"A host — plan for
2,400"* — and never as `Strength 0`. That is a fog feature, not a limitation, and `ForceView` is
shaped so the wrong rendering is not expressible.

### 4. Fog — four treatments, and the render branches on `intel`

The server already answers this in four derived states (`IntelLadder.StateOf`,
`FactionIntel.cs:133-140`), with `FreshTurns = 5` (`:131`) as the Scouted/Rumored boundary. The client
renders one of them well and the rest as a question mark.

| State | Treatment | Also |
|---|---|---|
| `Watched` | Full clarity, live eye badge, exact force counts | `IntelAge` 0 |
| `Scouted` | Doubled border + parchment wash + a **dated stamp** | Static facts kept; forces replaced by an explicit *"who stands here is not known"* strip |
| `Rumored` | Ragged dashed border + torn wash + *"hearsay"* | Same facts, visibly older |
| `Unknown` | **Not a card at all** — a different silhouette | No name, no fields, nothing that could be mistaken for data |
| *(control)* unowned but Watched | Dashed border for ownership; no wash, no stamp, no doubled edge | Fog and ownership never share a channel |

**Static facts are remembered and shown; dynamic facts are hidden.** Civ VI's sub-rule, sharper than
"old vs new", and it matches what `IntelRecorder` already stores: name, type, climate, danger,
development, slots, structures and ownership survive; forces, guard state and marching legions do not.
The "not known" strip is **explicit**, never an empty gap — an empty gap reads as *"nobody is there"*.

**The rule that will otherwise produce a silent, symptomless bug:** an unknown sector serialises every
field at its record default (`WorldEndpoints.cs:271-277` returns a `WorldSectorDto` carrying only
`SectorId`, `Intel`, `Phase`, `LayoutX`, `LayoutY`). On the wire it is byte-identical to a known
sector that happens to be zeroed — same `DangerBand` 0, same empty `Slots`, same empty `Forces`. **A
renderer that branches on "is this empty?" draws a real, poor, zero-danger sector as unexplored.** It
branches on `intel` first, and only then looks at anything else.

### 5. The legibility check §8.2 owes, and the map it must be run on

§8.2 decided stale fog errs toward **distinctness**: remembered ground must be unmistakably
remembered. That is the right call and it is not reopened. Its known cost is Civ VI's — a strong
treatment can make the map harder to *plan on*, which produced a named complaint thread. The wash is
capped at 13% (Scouted) / 18% (Rumored) over the panel for exactly that reason, and it sits **under**
the content layer, never over it.

**So the check this module owes is not "can you tell them apart?" but "can you still plan a march
against them?"** — and §8c.5 already priced one half of it: at 13% the wash puts `--muted` at 3.98,
below AA, for the state line, the ownership word and the *"who stands here is not known"* strip.
**Stale-node body text promotes from `--muted` to `--text`** (9.34 / 8.13, both AA), which keeps
everything the distinctness decision bought.

**Run it on `medium`, not `first-light`.** `first-light` is six sectors and was reshaped precisely
because one march lit the whole map — `l-ash-verdant` became `l-black-verdant` in W41, and even after
the fix Dave holds at *"4 of 6 known across 14 turns"* (`world-map-program.md:48`). A map with two
unknown sectors and almost nothing stale cannot test a stale-fog treatment. `two-hearths` (16 sectors)
is the bed.

### 6. Lifeline and supply overlays

Two overlays, both derived from graph properties the player cannot see by looking:

- **Lifeline** — the dashed amber halo + ◈ + a sentence naming the cost: *"losing this splits your
  empire (2 sectors cut off)"*. `Lifeline` / `LifelineCost` are server-computed and **opt-in**: the
  reconnection sweep is `O(holdings⁴)`, so `WorldEndpoints.cs:51` gates it behind `?lifelines=true`
  rather than paying for it every frame. It becomes the first map **lens** rather than a boolean prop
  — `world-lenses` owns the picker; this module owns the drawing.
- **Supply** — the connected block that is actually fed, derived from lanes that carry supply plus
  `ComponentId`. A sector outside it draws crossed-out with the words *"cut off"*.

### 7. The type floor, which applies to glyphs too

XAG 101 sets **18 px at 1080p** (36 px at 4K), measured as body height, and *"players should be able
to resize text up to 200 percent… without the loss of content, functionality, or meaning."* Scaled to
the declared 720p floor by the standard's own ratio, that is **12 px**.

**And the requirement §8c.5 sharpens: *"the text contained inside icons and glyphs should also meet
the minimum default text size"*, and glyphs must scale with text to 200%.** This module encodes a
great deal in glyphs — slot silhouettes, the danger lozenge, force chips, the fog stamp — so the floor
applies to all of them, not only to prose.

Against `tokens.css:64-66`: `--text-2xs` is 0.625rem (10px) and `--text-xs` is 0.6875rem (11px), both
**below** the floor; `--text-sm` is 0.75rem (12px), at it. **No fact-bearing glyph or label on the map
uses `--text-2xs` or `--text-xs`.** And because the tokens are `rem`, a 200% browser scale doubles
them — which is the property a raw `px` literal would destroy, inverting the hierarchy.

`--faint` is **decorative only, never body text** by its own token comment
(`docs/design/_kit/tokens.css:22`); it computes 3.22 on `--panel`. No state, count or label on the map
uses it.

## What stays out

- **The camera, the extent and the DOM id scheme.** `world-shell` owns them; this module renders into
  them and honours the lane-path ids.
- **The HUD.** `world-hud` owns band 1.
- **Number formatting.** `world-numbers` owns every magnitude on a node — this module composes them.
- **The lens picker and lens exclusivity.** `world-lenses`. This module draws what a lens asks for.
- **Targeting, routes and range overlays.** `world-targeting` (plate §E).
- **The inspector.** `world-inspector` — this module draws the node, not its detail panel.
- **Art.** Framed chrome and the designed placeholder (GG-58); illustration is D10's "later".

## Commands

```powershell
cd web\fusion-rpg-web
npm test                 # vitest — the hex guard and the state matrices run here
npm run build
npm run lint
```

```powershell
# The exemption this module retires, re-read rather than remembered:
rg -n "SKIPPED_PATH_PREFIXES" web\fusion-rpg-web\src\theme\hexGuard.ts
```

## Project structure

```
web/fusion-rpg-web/src/
  stages/world/render/
    SectorNode.tsx           → ownership · health · content · yield, four independent slots
    SectorNode.test.tsx      → the state matrix, no library mock
    sectorChannels.ts        → state → {shape, border, pattern, glyph, word} — pure
    sectorChannels.test.ts
    Lane.tsx                 → 6 kinds × 5 states, stacked
    laneChannels.ts
    LegionMarker.tsx         → moved, tokens only, same rAF technique
    Fog.tsx                  → four treatments + the "not known" strip
    fog.test.tsx             → branches on intel, never on emptiness
    SupplyOverlay.tsx        → envelope + cut-off marks
    LifelineOverlay.tsx      → halo + ◈ + the cost sentence
web/fusion-rpg-web/src/theme/hexGuard.ts   → `features/world/` prefix removed
```

## Code style

Channel assignment is a pure function; the component is a thin renderer. That is what makes a
greyscale assertion and a state matrix possible without a browser.

```ts
/**
 * Every fact reads on at least two channels (GG-27). `colour` is never
 * returned alone, and there is no `opacity` field — a dim is not a value.
 */
export type Channels = {
  shape: SectorShape;
  border: BorderTreatment;
  pattern: Pattern | null;
  glyph: string | null;
  /** The word. Always present, always player vocabulary (GG-23). */
  word: string;
  token: ColorToken;
};

/**
 * Fog is read first, and from `intel` — never from emptiness. An unknown sector
 * serialises every field at its record default (WorldEndpoints.cs:271-277), so it is
 * byte-identical on the wire to a zeroed known one.
 */
export function channelsFor(sector: SectorView): Channels {
  if (sector.intel === "Unknown") return UNKNOWN_SILHOUETTE;
  // …
}
```

## Testing strategy

Vitest, colocated, with two matrices and one guard. The greyscale assertion is the one that would
catch a regression a reviewer would not.

1. **State matrices, exhaustive** — every ownership × health combination for the sector, every
   kind × state pair for the lane. Table-driven, so an added state fails the test until it is drawn.
   The repo already has this shape in `ui/fourStatesMatrix.test.ts` and `ui/diffStateMatrix.test.ts`.
2. **No state on colour or opacity alone** — for every state, `channelsFor` returns at least two
   non-colour channels, and **no code path sets an opacity that varies with a value**. Asserted over
   the matrix, not spot-checked. This is the direct replacement for `SectorNode.tsx:49-52`.
3. **Fog branches on `intel`** — the decisive test: a fixture sector with `intel: "Watched"` and every
   other field at its type default renders as a **known, poor, zero-danger sector**, not as
   unexplored; and an `intel: "Unknown"` sector renders the silhouette even when the payload is
   otherwise identical. Without this the bug ships with no error and no symptom.
4. **The static/dynamic table** — on a `Scouted` sector, terrain, climate, slots, structures and
   remembered ownership render; forces, guard markers and lane-borne legions do not, and the
   *"not known"* strip is present rather than a gap.
5. **The hex guard** — passes with `features/world/` removed from `SKIPPED_PATH_PREFIXES`. The guard
   is the test; the deletion of the exemption is the assertion.
6. **The type floor** — no fact-bearing map label or glyph resolves to `--text-2xs` or `--text-xs`,
   and none uses `--faint`. A scan over the render tree, in the same spirit as
   `ui/disabledReasonGuard.ts`.
7. **The lane-path id contract** — every rendered lane exposes the id `world-shell`'s `stageIds`
   declares, so `LegionMarker` finds it. Shared with `world-shell`'s own assertion, deliberately: it
   is the one contract that spans both modules and breaks silently.
8. **The stale-fog legibility check** — a manual pass on `two-hearths`, recorded with its result, not
   a checkbox: can a march still be planned against a `Rumored` and a `Scouted` sector? §8.2 chose
   distinctness knowingly; this is where the price is paid deliberately.

## Boundaries

- **Always:** give every state two non-colour channels; branch on `intel` before anything else;
  render magnitudes through `world-numbers`; use tokens; keep channel assignment pure and the
  component thin; print a band and a ceiling for an inexact force.
- **Ask first:** any change to the four fog treatments themselves — §8.2 is an owner decision and the
  wash caps are the price it set. Also any zoom tier that drops a fact rather than simplifying one,
  which reopens §4.2's superset rule.
- **Never:** encode a state in opacity or hue alone. Never write a hex literal. Never render a faded
  line for a severed lane (it reads as *"far away"*). Never render `Strength` when `Exact` is false.
  Never use `--faint`, `--text-2xs` or `--text-xs` for a fact. Never infer fog from an empty payload.
  Never import a REST DTO into `stages/`.

## Success criteria

1. Every state in plate §A, §B and §C renders, and the state matrices are exhaustive rather than
   sampled.
2. **A greyscale screenshot of the stage loses no fact** — every state still readable from shape,
   pattern, glyph and word.
3. No opacity anywhere varies with a value; `SectorNode.tsx:49-52`'s formula has no successor.
4. The 14 slot kinds all render, on five silhouettes plus a naming glyph — replacing the 9-of-14
   ASCII letters at `SectorNode.tsx:29-39`.
5. Fog renders four distinct treatments plus the unowned-but-Watched control, and a test proves the
   render branches on `intel` rather than on emptiness.
6. `hexGuard.ts`'s `features/world/` exemption is deleted and the guard passes.
7. No fact-bearing label or glyph on the map uses `--text-2xs`, `--text-xs` or `--faint`.
8. Legion markers still animate along lanes after the library removal, proven by the shared path-id
   test.
9. The stale-fog legibility check is run on `two-hearths` and its result recorded — including if it
   fails.
10. `npm test`, `npm run build` and `npm run lint` are green.

## Open questions

**None.** §8.2 decided the fog treatment and §8c.5 priced it; §8b.6 decided the kit's `.sector` rung
yields to plate 11's node; §4.2 decided the zoom rule. Two items the plate left marked *"still open"*
are **owned rather than open**: the far-zoom drop order is fixed above (slots and flags first,
ownership/health/net never), and supply-as-envelope-versus-per-lane is a rendering choice inside this
module's boundary that the non-convex case decides on its own — an envelope that cannot enclose the
territory draws per-lane.
