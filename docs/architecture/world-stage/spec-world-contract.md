# Spec: world-contract

**Status: Draft — Phase 1 (Specify), awaiting owner review.** Module id `world-contract` in the
[world-stage capability map](../world-stage-map.md). **Level 1, no dependencies, and module 1 of the
program** — nothing else in `world-stage` can be adapted until this lands.

**Ideal:** [world-stage-ideal.md](../world-stage-ideal.md) §4.10, §8c.4, §8e.2, §9.
**Catalog:** [design/11-world-stage.html](../../design/11-world-stage.html) §M.

---

## Objective

Give the world domain a **sealed FE view contract**: the shapes every world component binds to, the
adapters that produce them from the wire, and a guard that actually stops a component binding to a
REST DTO instead.

Today that rule is prose with nothing behind it. `contractGuard.ts` scans `stages/`, `layers/` and
`ui/`, and matches only imports `from "@/lib/bus`. The world's DTOs live in
`web/fusion-rpg-web/src/features/world/worldTypes.ts`, so **a rebuilt `stages/world/` importing them
would pass the guard while violating the rule it exists to enforce.**

And the one world shape the contract does declare is wrong in a way that costs an ADR to fix.

### The narrowing, which is why this module is first

`contract/types.ts:272` declares `typeId: number`. The wire is `public string TypeId`
(`WorldDtos.cs:66`); `worldTypes.ts:39` agrees it is a string; the byte-pinned fixture holds strings
(`"typeId": ""` on an unknown sector).

`game-gui-map.md:142` puts *"rename or remove a field, narrow a type, change a unit family"* behind a
**contract version bump and an ADR**. Adding is free forever; this is not adding. So the very first
`adaptSector` cannot be written on the free path, and the repo has already written down why that
matters: *"getting a field wrong is cheap while nothing binds to it and expensive once eleven modules
do"* (`game-gui-map.md:147`).

**Success is that `stages/world/` cannot compile against a DTO, and every world number arrives with
its unit family attached.**

## Design

### 1. The six views

`SectorView` exists with 6 fields and no adapter — declared for *vocabulary completeness* while the
World stage was excluded (`types.ts:266-267`). It is corrected and joined by five siblings.

| View | Carries | Note |
|---|---|---|
| `SectorView` | identity, `typeId: string` **(corrected)**, ownership, intel state + age, phase, danger band, development, stability, the four loam readings, the four component readings, `willReleaseNextTurn`, habitability, layout position, warden presence, neglect | The one existing view; every addition is free, the `typeId` change is not |
| `LaneView` | id, endpoints, kind, state, length, width, hazard, ward level, `gateKeyId` | `GateKeyId` is a wiring gap today — declared `Pending` until `world-wire` fills it |
| `LegionView` | id, owner, position (in-sector **or** on-lane with progress), stance, movement remaining, routed, members with role, carried loam, capacity, burn, **runway** | Supply is the loam economy's core legion mechanic and none of it is on the wire yet |
| `SlotView` | index, kind, state, element, owner, guard, `structureId`, `constructionTurnsRemaining` | `structureId` is on the C# DTO and **missing from the TS mirror since L32** — the drift this module ends |
| `ForceView` | owner, kind, `exact`, strength, band name, band ceiling | Enemy strength is a **band** unless surveyed; that is a fog feature, not a limitation, and the view must make it impossible to render a band as an exact figure |
| `TurnEventView` | phase, kind, subject, detail, sectorId, and the **translated player sentence** | `world-playback` owns the translation; this owns the shape |

### 2. `Pending<T>` is the mechanism, and its two absent-cases are not the same

`pending.ts` already distinguishes `known` / `absent` / `pending`, and its own comment names the bug:
*"'you have none' must never look identical to 'this isn't wired up yet'."* The world domain has an
unusual number of the third case, and each must carry **player-facing copy**, not a developer note —
`contractGuard.ts:16-46` enforces a non-empty reason.

Fields declared `pending` at this module's completion, each retired by `world-wire`:
`carriedLoam`, member `role`, `constructionTurnsRemaining`, `wardenBindingId`, `neglectedTurns`,
`pressureMilli`, effective capacity, `gateKeyId`, the calendar, and the prospected set.

### 3. Unit families map onto the **existing sealed union** — they do not start a new one

**Corrected 2026-09-03 while speccing `world-numbers`.** A magnitude renderer already exists:
`i18n/magnitude.ts:15`'s `formatMagnitude` switches on a **closed 12-class `UnitClass` union**
(`contract/types.ts:28-44`) whose SSOT is [design/spec-magnitude-and-units.md](../../design/spec-magnitude-and-units.md).
An earlier draft of this section sketched a branded `LoamUnits` type — **that would have been a third
classification**, which `DESIGN-GATE.md`'s Stats row names as the exact failure it was widened to
prevent (*"Two classifications of a channel already exist and are verified against consumers…
inventing a third is the failure this row exists to prevent"*).

So: every world magnitude is typed as a `Magnitude` carrying one of the **existing** classes —
`perMilleRatio`, `gameUnits`, `count`, and so on. A class the world genuinely needs and the union
lacks is an **owner-authorised contract change** to that SSOT, exactly as `ladderIndex`,
`aptitudePoints` and `reciprocalPoints` were in 2026-08-26. It is not this module's to invent.

The four families in play, and the `UnitClass` each maps to:

| Family | Wire type | Members |
|---|---|---|
| per-mille | `int` | `stabilityMilli`, `pressureMilli`, `fractureIntensityMilli`, `hazardMilli`, `laneProgressMilli`, and `movementRemaining` — whose name says nothing about its unit at all |
| whole loam units | `long` | every `loam*` and `component*` reading |
| counts / indices | `int` | `dangerBand`, `developmentLevel`, `intelAge`, `wardLevel` |
| enum-as-string | `string` | .NET casing — `"Watched"`, `"Held"`, `"Warband"` — never kebab |

**The trap this module must make unrepresentable:** `StructureDef.Cost` and
`LoamPolicy.WellCost` / `WaystationCost` / `GranaryCost` (named `…CostMilli` until world-map W57
renamed them off the misleading suffix) hold **whole loam units** — compared directly against
`CarriedLoam` at `BuildResolver.cs:101` and subtracted at `:115`. A renderer trusting a `Milli` name
is wrong by 1000×, which the rename removes at the source but the view types must still not depend
on the field's own name to get right. The view types therefore carry
the family in the type, so a magnitude cannot reach a renderer unlabelled. `world-numbers` **extends**
the existing renderer to refuse an unlabelled value; this module is what makes "unlabelled" impossible
upstream.

### 4. The move, and the widened guard — both, per §8e.2

- **Move** the world DTO types to `web/fusion-rpg-web/src/lib/bus/world.ts`, where every other
  domain's already live. This makes the *existing* guard bite with no guard change, and it stops the
  world being the exception — the same root cause as its hex-guard exemption (`hexGuard.ts:23-27`)
  and its GG-7 reachability exemption.
- **Widen** `contractGuard` so a feature-local DTO import is caught too. That closes the *class* of
  defect rather than this instance.

### 5. Adapters are proven against the byte-pinned fixture

`first-light.json` is generated and asserted byte-for-byte by `WorldFixtureTests.cs:28-50`, and is
consumed by seven files. `adaptWorld*` is tested against that fixture, so an adapter and the server
cannot drift silently — which is exactly how `worldTypes.ts` lost `structureId` for two waves.

## What stays out

- **Rendering.** No component in this module. `world-render` and `world-inspector` consume it.
- **The magnitude renderer.** It already exists (`i18n/magnitude.ts`); `world-numbers` extends it. This
  module owns only the types it refuses without.
- **Filling the `pending` fields.** `world-wire` does that, and this module's job is to make each one
  say *why* it is pending in words a player could read.
- **The turn-report translation.** `world-playback` owns the table; this owns `TurnEventView`.

## Commands

```powershell
cd web\fusion-rpg-web
npm test                 # vitest run
npm run build            # tsc --noEmit && vite build
npm run lint
```

The contract guard runs inside the test suite (`contractGuard.test.ts`), so a violation fails
`npm test` rather than needing a separate script.

## Project structure

```
web/fusion-rpg-web/src/
  contract/
    types.ts            → the six world views added; `SectorView.typeId` corrected
    adapt.ts            → adaptSector / adaptLane / adaptLegion / adaptSlot / adaptForce / adaptTurnEvent
    adapt.test.ts       → against first-light.json
    contractGuard.ts    → widened to catch feature-local DTO imports
    contractGuard.test.ts
  lib/bus/
    world.ts            → the world DTO types, moved here from features/world/worldTypes.ts
docs/architecture/decisions.md   → the typeId ADR row
```

## Code style

A view is a plain type; the adapter is a pure function; the unit family rides on the field name and
is documented once at the type, not at each use.

```ts
// Corrected 2026-09-04 (W4) — this block used to show the branded `LoamUnits` type §3 above
// rejects. There is no brand. A loam magnitude is a `Magnitude` carrying an existing `UnitClass`,
// `gameUnits` as the deferred fallback until `loamUnits` is owner-authorised (§3's own open item).

export type SectorView = {
  sectorId: string;
  /** .NET enum string, not kebab. `typeId` was `number` until the 2026-09-03 ADR. */
  typeId: string;
  intel: "Unknown" | "Rumored" | "Scouted" | "Watched";
  /** Turns since last seen. A count, not a per-mille. */
  intelAge: number;
  loam: {
    net: Magnitude; // unit: "gameUnits" today — becomes "loamUnits" the day that class is authorised
    // …production / upkeep / stock, same family
  };
  /** Pending until `world-wire` projects it — the reason is player-facing copy. */
  neglectedTurns: Pending<Magnitude>; // unit: "count"
};
```

## Testing strategy

Vitest, colocated. Four levels, and the last is the one that matters most:

1. **Shape** — every view compiles against the fixture; no `any`, no unchecked cast.
2. **Adapter** — `adaptWorld*` against `first-light.json`, including the **unknown-sector case**: an
   unseen sector serialises every field at its record default (`WorldEndpoints.cs:271-277`), so it is
   indistinguishable from a zeroed known one **except by `intel`**. The adapter must branch on
   `intel`, never on emptiness, and a test asserts that.
3. **Pending discipline** — every `pending` field has a non-empty, player-facing reason
   (`contractGuard` already enforces this; the test proves the world fields are covered).
4. **Guard** — a fixture file importing a DTO from `features/` **fails the guard**. This test is the
   module's whole point: without it, §8e.2 is prose again.

## Boundaries

- **Always:** declare a field rather than defer it; give every `pending` a player-readable reason;
  brand a magnitude with its unit family; test an adapter against the generated fixture, never a
  hand-written double.
- **Ask first:** any *further* narrowing or rename beyond `typeId` — each is its own version bump and
  ADR. Also any change to `Pending<T>` itself, which ten other modules already bind to.
- **Never:** import a REST DTO into `stages/`, `layers/` or `ui/`. Never derive a loam number in
  TypeScript — `spec-loam-fe.md` is explicit and the server already computes every one of them.
  Never let a magnitude reach a component without its family.

## Success criteria

1. `SectorView.typeId` is `string`, and the change is recorded as an ADR row in `decisions.md` with a
   contract version bump.
2. All six world views exist, and every field either has a value or a player-readable `pending`
   reason.
3. The world DTOs live in `lib/bus/world.ts`; nothing outside `src/contract/` imports them.
4. **A test proves the guard catches a feature-local DTO import** — the rule has a gate, not a
   sentence.
5. `adaptWorld*` round-trips the byte-pinned fixture, and branches on `intel` rather than emptiness.
6. `structureId` is on the TS side, ending a drift that has been invisible to CI since L32.
7. `npm test`, `npm run build` and `npm run lint` are green.

## Open questions

**None.** §8e.2 decided move-and-widen; the `typeId` correction follows from the wire and needs an ADR
rather than a decision.
