# Spec: world-numbers

**Status: Draft — Phase 1 (Specify), awaiting owner review.** Module id `world-numbers` in the
[world-stage capability map](../world-stage-map.md). **Level 2, depends on `world-contract`** — and
deliberately **not** on `world-shell`: a magnitude renderer needs no stage, which is what lets it be
built in parallel and reused by the inspector, the HUD and the playback rail.

**Ideal:** [world-stage-ideal.md](../world-stage-ideal.md) §4.10, §8c.5, §8c.6.
**Catalog:** [design/11-world-stage.html](../../design/11-world-stage.html) §M, §M.1.
**Unit SSOT:** [design/spec-magnitude-and-units.md](../../design/spec-magnitude-and-units.md).

---

## Objective

Two things, and the second is the harder one.

1. **No world number reaches a screen without its unit family.** GG-46 is a Tier-1 hard gate and its
   testable form is already written down: *"a magnitude renderer that requires an explicit unit
   family and refuses without one; golden tests per family."*
2. **"Why did my net income drop?" is answerable from the interface.** GG-49, and the answer is the
   **modifier ledger** — nested, lockable, and arithmetic all the way down.

### The renderer already exists, and that changes the shape of this module

This is the finding that should be at the top rather than discovered halfway through.
`web/fusion-rpg-web/src/i18n/magnitude.ts:15` is `formatMagnitude(m: Magnitude, locale)`, and its own
comment states the gate: *"No overload accepts a bare `number` — that omission is the GG-46 guard."*
`Magnitude.unit` is a required field of a closed union (`contract/types.ts:28-44`), twelve classes,
governed by [spec-magnitude-and-units.md](../../design/spec-magnitude-and-units.md) §3 and its own
exhaustiveness check (`magnitude.ts:44`).

So **`world-numbers` is not a new renderer. It is an extension of a sealed one**, and every class it
adds is a governed contract change with a named precedent: `ladderIndex` (2026-08-24),
`aptitudePoints` and `reciprocalPoints` (2026-08-26) were each proposed and owner-authorised the same
day, and the union edit is recorded in that spec. Forking a second renderer for the world would
reproduce, in one program, exactly the defect that document exists to stop.

**Success is that a world magnitude cannot be rendered without a declared family, the four families
that need one are added to the SSOT rather than beside it, and a net figure expands into rows that add
up to it.**

## Design

### 1. The four world families, mapped onto the twelve classes that exist

Three of the four already have a home. One does not, and two need a correction the world found.

| World family | Wire | Members | Existing class | Verdict |
|---|---|---|---|---|
| per-mille `int` | `int` | `stabilityMilli`, `pressureMilli`, `fractureIntensityMilli`, `hazardMilli`, `laneProgressMilli`, `movementRemaining` | `perMilleRatio` | **Reuse, with one correction** — see §2 |
| whole loam units | `long` | every `loam*` and `component*` reading, `carriedLoam`, structure costs | — | **A new class is needed** — see §3 |
| counts / indices | `int` | `dangerBand`, `developmentLevel`, `intelAge`, `wardLevel` | `count` | Reuse; `dangerBand` needs a denominator, which is a context part, not a class |
| enum-as-string | `string` | `"Watched"`, `"Held"`, `"Warband"` | — | **Not a magnitude at all** — see §4 |

### 2. `perMilleRatio` renders the world's intensity wrong, and the bug is arithmetic

`formatPerMille` (`magnitude.ts:66`) treats a per-mille value as a **delta over 1000**:
`case "more"` returns `×${(1 + value / 1000).toFixed(2)}` (`:69-70`). That is correct for a stat
modifier, where `+400‰ more` means ×1.40.

The world's `FractureIntensityMilli` is **absolute**: `1000` is the neutral point, and
`WorldSectorDto.FractureIntensityMilli` even defaults to `1000` (`WorldDtos.cs:80`). Plate §M requires
`1400 → "The Fracture runs at ×1.40 here"`. The shipped `more` formatter renders **×2.40**.

This is a real, verified mismatch and it needs a decision, not a special case at the call site:

- **Recommended:** an `op` of `"absolute"` on `Magnitude` for per-mille values where 1000 is neutral,
  rendering `×(value / 1000)`. `op` is already the field that distinguishes `flat` / `increased` /
  `more` for exactly this reason, and adding a fourth arm keeps one formatter.
- **Rejected:** dividing at the adapter so the wire's 1400 becomes a 400 delta. That is a derived
  number computed in TypeScript, and it is the class of thing `spec-loam-fe.md` forbids outright.

Two smaller per-mille rules the world adds, both from plate §M:

- **Trim a trailing zero.** `StabilityMilli 240` reads *"Hold on the ground 24%"*, not `24.0%`. The
  shipped `flat` arm always emits one decimal.
- **Round away from zero at the display boundary.** A small non-zero per-mille must never render as
  `0%` — *"a real penalty never vanishes."* Rounding happens **once**, at the boundary, in the same
  direction the engine rounds.

`movementRemaining` deserves its own line because its name says nothing about its unit at all: it is
per-mille of one turn's march budget, so `750` is *"¾ of a march left"*, never `750 movement`.

### 3. Whole loam units need a class, and the name of the field is not the family

There is no class for a `long` count of a resource. `gameUnits` is the nearest and is wrong for two
reasons: its ledger row requires a `channel` (the arena — *"`+12 fire power` is 12 damage on a fire
component"*), and loam is not a derived channel; and it always renders signed (`magnitude.ts:50-55`),
which is right for a net figure and wrong for a stock reading.

**Proposed: a `loamUnits` class**, rendering:

- a **stock** against its denominator — `12 / 300 loam`;
- a **flow** with its sign carried by an arrow *and* a minus *and* colour — `▼ −10 loam` — never
  colour alone (GG-27/GG-30);
- a **period** on every flow, because *"Net −10"* is a bare magnitude: ten of what, over what?

**And the trap this class exists to make unrepresentable.** Four fields named `…Milli` hold whole
loam units:

| Field | Declared | Proof it is whole loam |
|---|---|---|
| `StructureDef.Cost` | `StructureCatalog.cs:26` (`long`) | compared against `CarriedLoam` at `BuildResolver.cs:101`, subtracted at `:115` |
| `LoamPolicy.WellCost` | `LoamPolicy.cs:106` (`long`) | feeds `StructureDef.Cost` |
| `LoamPolicy.WaystationCost` | `LoamPolicy.cs:109` (`long`) | same |
| `LoamPolicy.GranaryCost` | `LoamPolicy.cs:126` (`long`) | same; and `GranaryCapacityBonus` alongside it has no `Milli` in its name and is the **same unit** |

(All four fields above were named `…CostMilli` until world-map W57 (2026-09-05) renamed them off the
misleading suffix — no value changed, only the name. The table below describes the pre-rename
naming trap as the reason the renderer must not trust a field's own suffix; it is now a historical
example rather than a live defect, and the same argument still holds for any future field.)

A renderer trusting the suffix prints *"A Well costs 0.2 loam"* and the player cannot understand why a
legion carrying 180 is refused. **The repair in the model is a rename; declaring the family at the
projection boundary is what stops the bug reaching a screen in the meantime.** This module owns the
second half, and it must not special-case the four names — the whole point is that the name is never
consulted.

**One numeric note that belongs in this module and nowhere else.** `Magnitude.value` is a TypeScript
`number` — an IEEE double, integer-exact to 9,007,199,254,740,992, the `double` row of the overflow
table in `CLAUDE.md`. That is enough for any loam magnitude the ladder can produce, **on a display
path only**. The FE never computes a loam number (`spec-loam-fe.md` is explicit; the server already
computes every one), so no magnitude here ever re-enters arithmetic that reaches the server.

### 4. Enums are not magnitudes, and the failure mode is silent

`intel === "watched"` never matches; the wire says `"Watched"`. `"rumoured"` never matches; the wire
says `"Rumored"`, American spelling (`FactionIntel.cs:133-140`). Neither throws. Every sector quietly
renders as unknown — a bug with no error and no symptom except a map that looks fogged.

So enum handling is **an exhaustive lookup table with a loud default**: an unmapped value is a
development-time failure, not a blank cell. This is the same discipline `formatMagnitude`'s
`const exhaustive: never` (`magnitude.ts:44`) already applies to unit classes, applied to the world's
four enum surfaces (intel, phase, ownership, force kind).

### 5. The modifier ledger — GG-49, and its rows are a function signature

The ledger expands a net figure into what produced it. Plate §M.1 draws it; this module builds it.

**Its rows are not a design choice.** They are exactly the five arguments of

```csharp
LoamUpkeep.For(int garrisonMembers, int developmentLevel, int dangerBand, int intensityMilli, int handicapMilli)
```

(`LoamUpkeep.cs:40`), in that order: garrison, development, danger, intensity, handicap.

**There is no calendar term in that signature, and the plate drew one anyway.** An earlier §M.1 draft
had a sixth row, *"this month is heavier ×1.15"*, with no field behind it — corrected 2026-09-03 to
the faction upkeep handicap, which is real, is a declared balance lever, and which the engine already
narrates as `loam.handicap:1150`. It is worth restating here because the defect was drawn in the one
section whose subject is not lying about numbers, and a spec written from the uncorrected plate would
reintroduce it.

**The arithmetic is the design.** Reading down the column must reproduce the total exactly:
`10 + 8 + 10 + 9 = 37`, `×1.40 = 52`, `×1.15 = 60`, `50 − 60 = −10`. A ledger whose rows do not add up
to its total is worse than no ledger, because the player will trust it.

**Depth is capped at three levels** — level 1 is the hovered figure, levels 2 and 3 are the operands
and their own composition. A fourth level would be the tuning file, and *"why is a garrison 2 loam a
head"* is a balance question, not a provenance one.

**WCAG 2.1 SC 1.4.13 (Level AA) — content on hover or focus.** All three obligations are met
explicitly, and the fourth row below is what makes the ledger usable by the half of players who do not
hover:

| Obligation | How |
|---|---|
| **Dismissible** | Esc closes the ledger without moving the pointer, and without closing the inspector under it. The ✕ does the same for pointer users |
| **Hoverable** | The pointer travels from the number onto the ledger and through it without it vanishing — which is the whole point, since levels 2 and 3 are inside it |
| **Persistent** | It stays until dismissed, until the pointer leaves both trigger and ledger, or until the underlying value changes. It never times out |
| **Keyboard (not 1.4.13, but owed)** | Focus the number, press Enter: the ledger opens locked and its expandable rows are in the tab order |

The **lock gesture** is Shift by default; dwell-time and middle-click are alternatives, and which one
is **a player setting, not a constant**.

### 6. The dependency this module cannot resolve on its own

**The per-operand breakdown is not on the wire.** `WorldSectorDto` carries totals only —
`LoamProduction` (`WorldDtos.cs:89`), `LoamUpkeep` (`:92`), `LoamNet` (`:95`) — and the projection
assigns exactly those three (`WorldEndpoints.cs:332-334`). There is no garrison term, no development
term, no danger term, no intensity or handicap multiplier on any DTO.

`PressureMilli` is the sharper case: it is **declared** on the DTO (`WorldDtos.cs:72`) and **never
assigned** by the projection. A field that exists in the type and is always zero on the wire is worse
than a missing one, because it looks wired.

**That projection is `world-wire`'s, and this module states the dependency rather than working around
it.** Until it lands, the ledger's operand rows are `Pending<T>` with a player-readable reason
(`world-contract` §2) — *"the breakdown for this number isn't reported yet"* — never a blank row and
never a client-side derivation. Deriving loam in TypeScript is forbidden outright by
`spec-loam-fe.md`, and it would also break §8c.6's load-bearing property that the warning and the act
cannot disagree.

### 7. What we can and cannot reuse from the one GG-49 precedent

`ui/actor/ChannelContributions.tsx` is the repo's existing provenance component, and it is honest
about being simple. It is **single-level**, prints the raw `sourceId` as its label (an engine token on
a player surface — GG-23), renders a bare signed integer with no unit, and sits at `--text-2xs`
(10px, `tokens.css:64`). Its backing type `DerivedContribution` (`contract/types.ts:171-175`) is a
flat `{sourceId, op, value}` triple with no room for nesting.

**So the world ledger is a different component, and saying so is cheaper than a failed reuse.** What
transfers is the discipline in its comment — *"never a fabricated grid: an empty list means the
channel genuinely had no contributor, not a loading state"* — which is the same distinction
`Pending<T>` draws and which §6 above depends on.

## What stays out

- **The stage.** No camera, no layout. This module renders numbers and one popover.
- **Filling the operand fields.** `world-wire` projects them; this module declares them `Pending` and
  says why in player words.
- **The turn-report translation.** `world-playback` owns the 21 prefixes and 37 drop reasons; this
  module owns only the numbers inside those sentences.
- **The HUD strip and the inspector blocks.** `world-hud` and `world-inspector` compose these
  renderers; they do not reimplement them.
- **Renaming `CostMilli` in the model.** That was the real repair and it was a C# change on the
  `world-wire` / engine side, done by world-map W57 (2026-09-05: `StructureDef.Cost`,
  `LoamPolicy.WellCost`/`WaystationCost`/`GranaryCost`/`SoulConduitCost`/`ExtractorCost`/
  `HatcheryCost`). This module makes the name irrelevant to the client regardless.

## Commands

```powershell
cd web\fusion-rpg-web
npm test                 # vitest run — the unit-family goldens live here
npm run build
npm run lint
```

```powershell
# The trap, re-proved from the source rather than remembered:
rg -n "CostMilli" src\FusionRpg.Core\World
```

## Project structure

```
web/fusion-rpg-web/src/
  contract/types.ts          → `UnitClass` gains "loamUnits"; `Magnitude.op` gains "absolute"
  i18n/
    magnitude.ts             → the two new arms, in the existing switch — not a second renderer
    magnitude.test.ts        → a golden per family, world families included
  ui/world/
    LoamFigure.tsx           → stock / flow / period, sign on three channels
    PerMilleFigure.tsx       → hold, intensity, hazard, march remaining
    BandFigure.tsx           → "◆◆◆ Danger 3 of 5" — index with its denominator
    ModifierLedger.tsx       → 3 levels, lockable, 1.4.13-compliant, keyboard-reachable
    ModifierLedger.test.tsx
    worldEnums.ts            → exhaustive lookups with a loud default
docs/design/spec-magnitude-and-units.md   → the ledger row for the new class, and the `absolute` op
```

## Code style

Everything is a pure function of a `Magnitude` plus a sentence template. The family rides in the
type; no component ever asks what a number "looks like".

```ts
/** Whole loam units — a `long` on the wire. Never per-mille, whatever the field is named. */
type LoamMagnitude = Magnitude & { unit: "loamUnits" };

/** A stock has a denominator or it is not a reading. `capacity` is Pending until world-wire projects it. */
export function LoamStock({ held, capacity }: { held: LoamMagnitude; capacity: Pending<LoamMagnitude> }): JSX.Element;

/**
 * One ledger row. `operand` is a real term of `LoamUpkeep.For` — there are five and there
 * is no calendar term (LoamUpkeep.cs:40). Rows must sum to the total; a test asserts it.
 */
export type LedgerRow = {
  operand: "garrison" | "development" | "danger" | "intensity" | "handicap";
  label: string;          // player words — "4 bound × 2 each", never "garrisonMembers"
  contribution: Magnitude;
  children?: LedgerRow[]; // one level deep, and one only
};
```

## Testing strategy

Vitest, colocated. Five levels; the first is the Tier-1 gate and the fourth is the one that would
otherwise be found by a player.

1. **The refusal** — a value with no declared family cannot be passed. This is a *type* test, not a
   runtime one: `formatMagnitude` has no bare-`number` overload, and a test file that tries to pass
   one fails `tsc --noEmit`. That is how the shipped renderer already enforces it and the world
   families inherit it for free.
2. **A golden per family** — per-mille (including `1400 → ×1.40`, the trailing-zero trim, and
   round-away-from-zero), loam units (stock with and without a denominator, signed flow, period),
   counts (`dangerBand` with its denominator, `intelAge` phrased in the fiction's word for a turn),
   and the enum table (unmapped value throws loudly).
3. **The `CostMilli` case, by name** — `wellCost: 200` (named `wellCostMilli` until world-map W57's
   rename) renders *"A Well costs 200 loam"*, and a
   legion carrying 180 is refused. The three wrong renderings plate §M draws (`0.2 loam`, `20%`,
   `free`) are asserted **not** to occur. This test exists because the defect is in the model and no
   client-side type can catch it; only a fixture can.
4. **The ledger adds up** — the rows sum to the total for a generated set of operands, not just the
   worked example. Property-shaped: for any `(garrison, development, danger, intensity, handicap)`,
   the rendered rows reproduce `LoamUpkeep.For`'s result after one boundary rounding. A ledger that
   drifts from the engine is the failure that makes a ledger worse than nothing.
5. **1.4.13, asserted rather than claimed** — Esc closes the ledger and leaves the inspector open; the
   pointer can enter the ledger without it vanishing; it does not time out; Enter on a focused figure
   opens it locked and its rows are tabbable. Four assertions, one per obligation plus the keyboard
   route.

## Boundaries

- **Always:** declare the family at the boundary; add a class to
  [spec-magnitude-and-units.md](../../design/spec-magnitude-and-units.md)'s ledger in the same change
  that adds it to the union; round once, at the display boundary, away from zero; give every flow a
  period and every stock a denominator (or a stated `Pending` reason for the missing one); keep the
  ledger's rows equal to `LoamUpkeep.For`'s arguments.
- **Ask first:** **every new `UnitClass` and every new `Magnitude.op` arm.** The precedent is three
  classes, each owner-authorised on the day it was proposed, and both additions this module proposes
  (`loamUnits`, `op: "absolute"`) are on that path. Also any change to the lock gesture's default,
  which §M.1 makes a player setting rather than a constant.
- **Never:** render a number whose family was inferred rather than declared. Never trust a `…Milli`
  suffix. Never derive a loam figure in TypeScript — `spec-loam-fe.md` forbids it and the server
  already computes every one. Never fork a second magnitude renderer for the world. Never show a
  ledger row whose value the wire does not carry; show the `Pending` reason instead. Never let a
  bare `Strength` print when `Exact` is false — a band and a ceiling, or nothing.

## Success criteria

1. Every world magnitude renders through `formatMagnitude`, and no path accepts a bare `number` —
   proven by `tsc --noEmit`, not by review.
2. `loamUnits` and the `absolute` per-mille op exist in **one** place: the `UnitClass` union, the one
   renderer, and the unit ledger in `spec-magnitude-and-units.md`, edited together.
3. `FractureIntensityMilli 1400` renders `×1.40`; `StabilityMilli 240` renders `24%`; a small non-zero
   per-mille never renders `0%`.
4. A fixture proves `wellCost: 200` (`wellCostMilli` before world-map W57) reads as *200 loam* and that none of plate §M's three wrong
   renderings occur.
5. The ledger nests exactly three levels, its rows are the five `LoamUpkeep.For` arguments with **no
   calendar term**, and a property test proves the rows reproduce the total.
6. All four SC 1.4.13 obligations plus the keyboard route are asserted by tests, not asserted in prose.
7. Every operand the wire does not carry renders a player-readable `Pending` reason — never a blank,
   never a zero, never a client-side derivation.
8. `npm test`, `npm run build` and `npm run lint` are green.

## Open questions

**None.** §4.10 decided the four families and the refusal rule; §M.1 decided the ledger's depth, its
operands and its lock gesture. The two contract additions this module needs (`loamUnits`, the
`absolute` op) are not open questions — they are proposals with a recorded precedent, and they sit
under **Ask first** because that spec requires authorisation, not because the answer is unknown.
