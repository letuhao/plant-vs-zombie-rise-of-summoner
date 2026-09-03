# Spec: world-hud

**Status: Draft — Phase 1 (Specify), awaiting owner review.** Module id `world-hud` in the
[world-stage capability map](../world-stage-map.md). **Level 3, depends on `world-shell` and
`world-contract`** — built in parallel with `world-render`.

**Ideal:** [world-stage-ideal.md](../world-stage-ideal.md) §4.3, §8b.5, §8b.7, §8c.5, §8c.6, §8d.3,
§8e.1.
**Catalog:** [design/11-world-stage.html](../../design/11-world-stage.html) §G.1–§G.4.

---

## Objective

Band 1: anchored to the viewport, present at every zoom, **never scrolling**, with corner roles fixed
for the life of the stage.

This module owns three things and one of them reaches outside the program:

1. **The frame** — the corner-role contract every other level-4 module docks into.
2. **The top strip** — the loam **summary**, the turn number, the calendar, and the
   **component-split state**.
3. **§8d.3 — band 1 is exempt from a band-2 scrim.** A kit-wide amendment to GG-5's band table that
   changes the Sanctum and the Lawn too, and must be written into
   [game-gui-principles.md](../game-gui-principles.md), not only recorded here.

**Success is that at 1280×720 every corner is populated, no decision is split across two of them, the
HUD stays legible with an inspector open, and nothing in band 1 ever scrolls.**

## Design

### 1. The corner-role table, and why stability is the whole rule

This is Amplitude's lesson learned from both directions at once: they are removing their "Divided UI"
because it was *"frustrating for players that didn't know what part of the screen to look at"*, while
EL2 players name Endless Legend 1's *"strict division into corners"* as what made it accessible.
Both are true. **Per-corner role stability is right; splitting one decision across two corners is what
failed.**

| Anchor | Owns | Built by |
|---|---|---|
| **Top strip** | Empire loam summary — income · upkeep · **net** · stock-against-capacity. Turn number and the **calendar** | **this module** |
| **Top-left** | The layer rail — identical on every stage (`Rail.tsx:31`, a `w-[92px]` icon column) | shell, unchanged |
| **Right edge** | The notification rail, and beneath it the outliner | `world-notify`, `world-outliner` |
| **Bottom-right** | The turn cluster | `world-turn` |
| **Bottom-left** | Map controls: zoom · fit · lens picker · fog | `world-shell`, `world-lenses` |
| **Left edge** | **The sector inspector when one is open** (§8e.1) | `world-inspector` |

**The left edge is the one asymmetry, and §8e.1 priced it honestly.** The inspector (380px) and the
outliner (224px) were both right-anchored, claiming ~620px of a 1280px floor. Docking the inspector
left resolves the collision and matches the genre convention (Stellaris, Civ VI and Total War all put
the selected-entity panel and the outliner on opposite edges). **It docks beside the layer rail, not
over it** — the rail is a ~92px icon column and keeps its corner role, so the fixed-role rule survives
with exactly one conditional occupant.

**Screen budget: chrome ~27%, map ~73% at 1280×720.** 27% is deliberately inside the *measured* band
for shipped RTS (25–40%), not the *remembered* one designers quote (10–25%). Budgeting to the
remembered number is how a HUD ends up needing a scroll it was never supposed to have.

**And nothing in band 1 scrolls, at any zoom.** The genre's four replacements for scrolling are all
available on this stage: move the camera, swap the lens, open a bounded panel with its own single-axis
scroll (GG-61), or page.

### 2. The loam summary strip — summary up, detail down

§8b.5 **amends** `spec-loam-fe.md`'s sealed 2026-08-23 decision, and the amendment is already written
into that spec at `loam/spec-loam-fe.md:156`: the gauge lives in **both** places — a compact
income · upkeep · net · stock strip in the stage HUD, and the full per-component breakdown in the
world panel.

The amendment is safe for the reason the original decision was made: `resource-hub-ssot.md` §4
requires a surface carrying two scopes to separate them by scope, and **this strip carries only empire
scope** — neither the lawn's `pvz.*` sun bank nor an actor's pools. The rule forbids mixing scopes on
one surface, not showing empire scope on a stage HUD.

| Reading | Family | Field | State |
|---|---|---|---|
| Income | whole loam · `long` | `WorldSectorDto.LoamProduction` (`WorldDtos.cs:89`), summed client-side | built |
| Upkeep | whole loam · `long` | `LoamUpkeep` (`:92`) — after intensity and handicap | built |
| Net | whole loam · `long` | `LoamNet` (`:95`) — the number the abandonment decision is about | built |
| Stock — numerator | whole loam · `long` | `LoamStock` · `ComponentStock`, owner-only | built |
| Stock — **denominator** | whole loam · `long` | `LoamPhases.EffectiveCapacity` (`LoamPhases.cs:58`) | **wiring gap — no DTO field** |
| Turn | count · `int` | `WorldHeaderDto.CurrentTurn` (`WorldDtos.cs:12`) | built |

**The denominator we do not have, drawn honestly.** `EffectiveCapacity` is computed at
`LoamPhases.cs:58` and used internally at `:39`, and it is never projected. So the stock slot reads
`1 140 / ? loam` rather than a bar that would lie about its fullness. **The correct response is not to
infer a capacity on the client** — deriving loam numbers in TypeScript is what `spec-loam-fe.md`
forbids outright. `world-wire` projects it; until then the field is `Pending<T>` with a
player-readable reason, and the strip says so in words.

Every magnitude here renders through `world-numbers` — a flow carries its period, a stock carries its
denominator or a stated reason for its absence, and no number is unlabelled.

### 3. The calendar is a calendar, not a season

§8b.7, and the plate has not caught up: §G.1 and §G.2 still draw a **Season · Long Wither** slot. That
slot has no field behind it and no season concept in the turn engine. **This module follows the
decision, not the drawing.**

What is real, and it is far better prepared than "a new mechanic" suggests:
[`TurnCalendar.cs`](../../../src/FusionRpg.Core/World/Turn/TurnCalendar.cs) runs a complete clock — a
turn is a day, `DaysPerWeek` days a week, `WeeksPerMonth` weeks a month (`:22-24`), rolled purely from
`(turn, seed)` with per-boundary derived RNG streams (`:41`, `:48`), every rate already tunable
(`SpecialWeekChanceMilli`, `SpecialMonthChanceMilli`, `PlagueChanceMilli`, `:27-29`). The rolls reach
the client as `calendar` report entries (`TurnReportKinds.Calendar`, `TurnReport.cs:7`).

The file's own comment states the deferral: *"Wave 1 records the rolls; the economic effects land with
sector-development, which is the module that owns growth"* (`:6-7`). So seasons are the **effects half
of an existing deterministic clock** and belong to `sector-development`.

**`world-hud` draws the slot; it does not invent the mechanic.** The slot shows the week and month
with their flavour. When `sector-development` lands seasons, the slot gains a subject; it does not
change shape.

### 4. The component-split state is first-class, not a detail

After the settlement rule, *"my empire is fine"* can be false while half of it starves. The empire net
can read `+24 loam/turn` and be telling the truth while one of the two halves of that territory is
dying. §8c.6 lists the pooled-component economy as load-bearing: `TerritoryComponents` makes the
empire N *purses*, not N sectors, so at turn 80 with fourteen sectors the player manages two or three
decision objects. **This is the correct primary readout and must not be traded down to a per-sector
list.**

The wire already carries it — `ComponentId`, `ComponentProduction`, `ComponentUpkeep`, `ComponentNet`,
`ComponentStock`, all owner-only — and `LoamGauge.tsx` already computes it, its own comment naming the
reason: *"split into a row per component once territory is split, because 'my empire is fine' can be
false while half of it starves"* (`:6-8`). **What it has never had is a place on screen that does not
scroll away.**

Six states, from plate §G.4, and the empty and the collapsed ones matter as much as the alarm:

| State | Behaviour |
|---|---|
| One component | The split row **collapses entirely**. There is nothing to say and it says nothing |
| Split, both solvent | The split is stated; **no alarm**. Split is a fact, starving is the event — conflating them trains the player to ignore the row |
| Split, one starving | The starving row raises the alarm; the solvent one does not |
| Both starving | Both raise it independently. The empire net also turns — two facts, both true, neither derived from the other |
| No territory | **A sentence, not four zeroes.** A row of zeroes reads as a broken feed and the player cannot tell the difference |
| Many components | Starving parts sort to the top and are **never folded**; solvent parts collapse into a summary row past two. **Maximum three rows**, which is what keeps the strip a fixed height at the 720p floor |

**Four channels, and colour is the fourth:** a glyph, a plain sentence, a thick left rule, then the
tint. Any one of the first three carries the state alone.

### 5. §8d.3 — band 1 is exempt from a band-2 scrim, and this is kit-wide

A band-2 layer scrims the **stage**, not the HUD. This fixes §8c.5's finding and honours §4.3's
*"anchored, present at every zoom."*

**The defect, with its numbers:** `.scrim` is `z-index: var(--band-panel)` (`_kit/kit.css:401`), and
the band tokens are `--band-hud: 100` against `--band-panel: 200` (`_kit/tokens.css:140-142`; the
shipped web mirrors them at `theme/tokens.css:101-106`). So the scrim covers the HUD. Opening any
inspector drops `--text` on the rail from 14.08:1 to **2.12:1**, and the turn cluster's blocker reason
to **1.50:1** — the one sentence explaining why the player cannot end their turn, rendered
unreadable by the panel that raised it.

**Three consequences, and the second is the one that gets skipped:**

1. **It is a change to a Tier-1 rule's mechanics.** GG-5's band table is what makes stacking and input
   *"mechanical rather than per-screen judgement calls"* (`game-gui-principles.md:113-130`). Adding
   *"band 1 is not scrimmed by band 2"* is an amendment **to that table**, and it lands in
   `game-gui-principles.md` — recording it only in a world-stage spec is how a kit-wide rule becomes a
   world-stage quirk.
2. **The shipped web has not built the defect yet, which is the reason to fix it now rather than
   later.** `theme/tokens.css:12` declares `--color-scrim` and there is no `.scrim` class in
   `web/fusion-rpg-web/src` at all; the Lawn's own chrome comment records *"no scrim, the board stays
   fully visible and interactive"* (`LawnPage.tsx:1075`). So the defect lives in the design kit and
   **will be faithfully reproduced** by the first web panel that scrims. Amending the rule before that
   costs one line; amending it after costs every scrimmed surface on three stages.
3. **The plate owes the drawing that would have caught it.** No figure shows the HUD and an open
   inspector together — §8c.5 notes that one missing composition would have surfaced this, the
   380px + 224px right-edge collision, and the GG-61-with-HUD question at once. §8e.1 has now unblocked
   it.

**The rule, stated so it can be implemented and tested:** a band-2 scrim covers band 0 only. Band 1
sits above it and stays fully legible and interactive. Band 3 and above scrim everything below,
unchanged — a dialog is a decision and the HUD is not a competing one.

### 6. Redundancy is not division

The blocker's notification and the turn cluster's reason line say the same sentence in two places on
purpose. **No decision is split across two corners:** the loam net and the component that cannot pay
it are both in the top strip; the unresolved count and the button it would block are both in the
bottom-right. That is the test to apply to every future addition to this frame.

### 7. The type floor applies to the HUD, and the HUD is where it bites hardest

XAG 101's **18 px at 1080p** (36 px at 4K), measured as body height, scales to **12 px** at the
declared 720p floor — and the standard requires text to resize to **200% without loss of content,
functionality, or meaning**, glyphs included. Against `theme/tokens.css:64-66`, `--text-2xs`
(0.625rem = 10px) and `--text-xs` (0.6875rem = 11px) are below that floor; `--text-sm` (0.75rem =
12px) is at it. **No reading in the top strip — figure, label, unit badge or split-row sentence — uses
`--text-2xs` or `--text-xs`.** §8c.5's sharpest finding is that the GG-46 unit-family badge, the
Tier-1 gate's entire on-screen expression, was drawn as an 8px superscript.

Nothing here uses `--faint`, which its own token comment restricts to *"decorative only, never body
text"* (`_kit/tokens.css:22`) and which computes 3.22 on `--panel`.

## What stays out

- **The turn cluster's behaviour.** `world-turn` owns End Turn's states, the unresolved count, the
  blocking classes and the force-end hatch. This module owns the anchor it occupies.
- **Notifications and the outliner.** `world-notify` and `world-outliner`. Same relationship.
- **The inspector.** `world-inspector` owns the left-docked shell; this module owns the corner-role
  contract that makes room for it and the guarantee that opening it does not dim band 1.
- **Number formatting and the modifier ledger.** `world-numbers`.
- **Projecting effective capacity, or the calendar's economic half.** `world-wire` and
  `sector-development` respectively.
- **Seasons.** Not this program's design work (§8b.7).

## Commands

```powershell
cd web\fusion-rpg-web
npm test
npm run build
npm run lint
npm run test:e2e         # the 1280×720 / 1440×900 sweep
```

```powershell
# The band model this module amends, re-read rather than remembered:
rg -n "band-hud|band-panel" web\fusion-rpg-web\src\theme\tokens.css docs\design\_kit\tokens.css
rg -n "scrim" docs\design\_kit\kit.css
```

## Project structure

```
web/fusion-rpg-web/src/
  stages/world/hud/
    WorldHud.tsx             → the band-1 frame and the six anchors
    WorldHud.test.tsx
    TopStrip.tsx             → income · upkeep · net · stock · turn · calendar
    TopStrip.test.tsx
    ComponentSplit.tsx       → the six states, max three rows
    ComponentSplit.test.tsx
    componentSplit.ts        → grouping + sort + fold — pure
    calendarLabel.ts         → week/month + flavour from `calendar` report entries
  theme/
    kit.css / band model     → band-2 scrim covers band 0 only
docs/architecture/game-gui-principles.md   → GG-5's band table gains the band-1 exemption
docs/design/_kit/kit.css                   → `.scrim` z-index corrected at the source
```

## Code style

The frame is layout; the readings are data. Grouping, sorting and folding live in a pure module so
the six split states are testable without rendering.

```ts
/**
 * Band 1. Anchored to the viewport, never scrolling, present at every zoom.
 * Sits ABOVE a band-2 scrim (§8d.3) — a panel dims the stage, never the HUD.
 */
export function WorldHud(props: { readings: LoamSummaryView; turn: number; calendar: CalendarView }): JSX.Element;

/**
 * Max three rows, and it is not cosmetic: a fixed-height strip is what keeps
 * band 1 off the scroll at the 720px floor. Starving parts sort first and are
 * never folded; solvent parts past the second collapse into one summary row.
 */
export const MAX_SPLIT_ROWS = 3;

/** Split is a fact; starving is the event. Conflating them trains the player to ignore the row. */
export function splitRows(components: ComponentView[]): SplitRow[];
```

## Testing strategy

Vitest plus one Playwright sweep. The contrast assertion is the one that proves §8d.3 rather than
asserting it in prose.

1. **The six split states** — one component collapses to nothing; split-and-solvent states the split
   without an alarm; one starving alarms once; both starving alarm independently; no territory renders
   **a sentence, not zeroes**; many components sorts starving first, folds solvent past two, and never
   exceeds `MAX_SPLIT_ROWS`. Table-driven over `componentSplit.ts`, no DOM needed.
2. **Four channels, colour fourth** — every alarm row exposes a glyph, a sentence and a rule, and the
   test asserts the state is still identifiable with the tint removed.
3. **Band 1 above a band-2 scrim** — mount the HUD, open a band-2 layer, and assert the HUD's computed
   `z-index` is above the scrim's and that its text is not composited under it. This is §8d.3's gate;
   without it the amendment is prose and the 2.12:1 regression returns the first time someone adds a
   scrim.
4. **Nothing in band 1 scrolls** — at 1280×720 and 1440×900, no band-1 element has scroll overflow in
   either axis, and the strip's height is unchanged across all six split states (which is what
   `MAX_SPLIT_ROWS` buys).
5. **The corner-role contract** — every anchor has exactly one occupant and no anchor's occupant
   changes as a function of a band-2 layer, except the left edge, which is the one declared
   conditional occupant.
6. **The denominator is honest** — with `EffectiveCapacity` unprojected, the stock reads its
   `Pending` reason rather than a fabricated bar, and **no test fixture makes the client derive it**.
7. **The calendar slot** — renders week and month with flavour from `calendar` report entries, and
   contains no season vocabulary. A guard against re-importing the plate's uncorrected §G.1 label.
8. **Type floor** — no top-strip reading resolves to `--text-2xs`, `--text-xs` or `--faint`, and the
   strip survives a 200% text scale without clipping or reordering.

## Boundaries

- **Always:** keep corner roles fixed; keep both halves of a decision in the same corner; render every
  magnitude through `world-numbers`; state the split when it exists and stay silent when it does not;
  render a missing wire field as a `Pending` reason in player words.
- **Ask first:** **the GG-5 band-table amendment.** It is a Tier-1 rule and it changes the Sanctum and
  Lawn stages, so it is a `game-gui-principles.md` edit with owner sign-off, not a world-stage
  implementation detail. Also any addition to the corner-role table — a new occupant is a permanent
  change to a contract five other modules dock into.
- **Never:** let band 1 scroll, at any zoom or any viewport in the declared range. Never let a band-2
  scrim dim it. Never derive a loam number in TypeScript — the server computes all of them. Never
  render a season; the mechanic does not exist. Never show four zeroes for an empty empire. Never
  split one decision across two corners. Never use `--faint`, `--text-2xs` or `--text-xs` for a
  reading.

## Success criteria

1. At 1280×720 every anchor is populated and nothing in band 1 scrolls; chrome stays at roughly 27%
   of the frame.
2. The top strip renders income · upkeep · net · stock, each with its unit family, and the stock's
   missing denominator renders a player-readable `Pending` reason rather than an inferred bar.
3. Turn and calendar render from `WorldHeaderDto.CurrentTurn` and `calendar` report entries; **no
   season vocabulary appears anywhere.**
4. All six component-split states render, capped at three rows, with starving sorted first and never
   folded, and the empty state is a sentence.
5. **A band-2 layer does not scrim band 1**, asserted by a test — and the rule is written into GG-5's
   band table in `game-gui-principles.md` and fixed at the source in `_kit/kit.css`, not only recorded
   in this spec.
6. The left edge is the inspector's only conditional occupant, and it docks beside the 92px rail
   rather than over it.
7. No reading uses `--text-2xs`, `--text-xs` or `--faint`, and the strip survives a 200% text scale.
8. `spec-loam-fe.md`'s amendment (`:156`) and this module's implementation agree — summary here, full
   breakdown in the panel.
9. `npm test`, `npm run build` and `npm run lint` are green.

## Open questions

**None.** §8b.5 decided summary-up/detail-down and the amendment is already written into
`spec-loam-fe.md`; §8b.7 decided calendar-not-season; §8d.3 decided the scrim exemption and named it
kit-wide; §8e.1 decided the left edge. The plate's remaining `Season` label is a known drawing defect
against a recorded decision, not an open question — it is listed under **Never** so it cannot be
re-imported.
