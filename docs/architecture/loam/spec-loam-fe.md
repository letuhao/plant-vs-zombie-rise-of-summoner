# Spec: loam-fe (wave 2)

**Status:** **Sealed 2026-08-23** — owner-approved.
 Module id `loam-fe` in the
[loam capability map](../loam-map.md). Depends on `loam-turn`.
**Why it is pre-gate:** map §7 findings **S1** and **S2**. The ⭐ gate is an owner playtest, and the
owner cannot judge what they cannot see.

## Objective

Make the empire's loam situation legible at a glance, and make *what do I let go?* a decision the
player can actually take rather than one the engine takes for them silently.

Success looks like: opening `#/world`, you can tell within a second which ground is safe, which is
slipping, and which is not yours to keep — and clicking a sector tells you what it earns, what it
costs, and what it is connected to.

## Design

### This module is not optional and it is not decoration

The capability map originally listed FE as *"wave 2+, parallel"*. That was wrong. The gate asks
whether holding ground is an interesting decision, and the standing lesson from the VFX stream is
**trust the owner's eyes over event telemetry** — a mechanic judged from narrative scripts an
assistant writes is a mechanic judged by the assistant.

### The wire — what `loam-turn` must project (S2)

`spec-loam-model` covers raw state: `FractureIntensityMilli` (terrain, visible once scouted) and
`LoamStock` (live, owner-only). **The decision needs derived numbers too**, and no spec put them on
the wire:

| Field | Scope | Rule |
|---|---|---|
| `production` per sector | owner-only | What it earns this turn |
| `upkeep` per sector | owner-only | What it costs, after intensity and handicap |
| `net` per sector | owner-only | The number the abandonment decision is actually about |
| `componentId` + component totals | owner-only | Which block of territory pays for this sector |
| `stabilityMilli` | owner-only | The fade countdown |
| `habitable` | **anyone who has scouted it** | Whether the ground can be kept at all — it is a property of the terrain, like intensity |

Derived numbers are computed from `loam-calc` at projection time, never stored — they are a function
of state and would only be a second thing to keep true.

**The fog rule is the same one `loam-model` sets and it must be property-tested here too**: nothing
owner-only reaches a faction that does not hold the sector, across `/state`, the turn report, and any
new endpoint. W22 established that shape as a property test rather than a spot check.

### Territory is light in the dark

Ideal §9.3 — and the reason it is cheap is that the field already exists. `StabilityMilli` is a
0–1000 per sector that is already hashed and already replayed:

```
anchored, healthy   → bright
fading              → dimming, in proportion, and visibly
barren / unkeepable → dark, with a distinct treatment from "fading"
not yours           → the existing fog treatment, unchanged
```

**Fading and barren must not look the same.** One is a problem you can solve and the other is ground
that was never yours to keep; a player who confuses them will make the wrong decision every time.

### The gauge

One reading, always visible, the way a city-builder shows power: **income, upkeep, net, and stock**
for the empire — and per component when territory is split, because after the S3 resolution *"my
empire is fine"* can be false while half of it starves.

A component that cannot pay for itself is the single most important thing the screen can say, so it
says it plainly rather than making the player derive it from four numbers.

### The abandonment surface

`loam-turn` releases the weakest contributor automatically when a component cannot pay (ideal §7.7 —
the player never distributes loam, they only choose what to give up). The FE's job is to make that
**visible before it happens**, not after:

- a sector the engine will release next turn is marked, with the reason,
- and the player can pin a sector as *keep* or mark one as *release first*.

Pinning is a **player-set priority override**, which `spec-loam-turn` deferred until there was a
surface to set it from. This is that surface. If it proves fiddly at the gate, it is the first thing
to cut — the automatic rule alone is playable.

### What this module does not do

No new top-level route. `#/world` exists; this adds layers and a panel to it.
[game-gui-principles.md](../game-gui-principles.md) GG-1 — *menus open over where the player already
is* — and a fifth navigation entry for "the loam screen" would be exactly the mistake it names.

## Commands

```powershell
cd web/fusion-rpg-web; npm test; npm run lint; npm run build
dotnet test tests\FusionRpg.E2E.Tests --filter FullyQualifiedName~World
$env:FUSIONRPG_BLESS_WORLD_FIXTURE=1; dotnet test tests\FusionRpg.E2E.Tests --filter FullyQualifiedName~World
```

## Project structure

```
src/FusionRpg.Contracts/WorldDtos.cs                → the derived fields above
src/FusionRpg.Server/WorldEndpoints.cs              → projection, owner-only gating
web/fusion-rpg-web/src/features/world/WorldPage.tsx → overlay layers
web/fusion-rpg-web/src/features/world/LoamGauge.tsx
web/fusion-rpg-web/src/features/world/SectorPanel.tsx → per-sector economy readout
web/.../world.fixture.json                           → regenerated with the new fields
tests/FusionRpg.E2E.Tests/World*.cs                  → fog property tests over the new fields
```

## Code style

Follow the existing `WorldPage` conventions. Controls are real controls — bordered, hoverable,
`aria-pressed`, `data-testid` — the lesson from the force picker that shipped as unclickable text.
Player-facing copy uses player words: **"fading"**, **"cannot be kept"**, **"your supply is split"** —
never `componentId`, `StabilityMilli`, or `intensityMilli`. GG-1's warning about engine vocabulary on
a player surface applies directly.

## Testing strategy

**Fog, as a property** — over every new field, every projection, every faction. Not a spot check.
This is the W22 shape and it is the one that would leak if it were done as examples.

**Component split** — a severed territory shows two components with independent totals, and the
starving one is visibly identified.

**The four states are distinguishable** — anchored, fading, barren, not-yours each render differently,
asserted rather than eyeballed, because "fading looks like barren" is a bug the owner would report as
"the map is confusing" and we would look for in the wrong place.

**Fixture-driven** — the FE builds against `world.fixture.json` as it does today, so the web suite does
not need a running server.

## Boundaries

- **Always:** owner-only gating asserted as a property; player words on player surfaces; layers on
  `#/world`, not a new route; real controls.
- **Ask first:** any new top-level navigation; showing another faction's economy in any form;
  persisting FE-only state that the engine would need for replay.
- **Never:** engine vocabulary in player copy; deriving loam numbers in TypeScript — the server
  projects what `loam-calc` computes, or the two will disagree and the disagreement will be silent.

## Success criteria

1. The four ground states are visually distinct and asserted.
2. No owner-only number reaches a non-owner, proven over every projection.
3. A split territory reads as split, with the starving component named.
4. The owner can play ten turns on `two-hearths` and answer the gate question **without reading a
   turn report** — that is the bar this module exists to clear.

## Decided (2026-08-23)

- **Pinning ships after the gate.** The automatic rule is playable on its own, and the gate should test
  the mechanic rather than a UI for overriding it.
- **~~The gauge belongs to the world panel, not the stage HUD.~~ Amended 2026-09-03 — it lives in
  both: summary up, detail down.** A compact income · upkeep · **net** · stock strip in the stage HUD,
  and the full per-component breakdown in the world panel.

  The original reasoning stands and is what makes the amendment safe: `resource-hub-ssot.md` §4
  requires a surface carrying two scopes to separate them by *scope*, and this is empire scope —
  neither the lawn's `pvz.*` sun bank nor an actor's pools. A HUD strip carrying **only** empire scope
  does not mix scopes, so the rule is satisfied rather than bent. What the original decision got wrong
  was the consequence: this spec's own §"The gauge" calls for *"one reading, always visible, the way a
  city-builder shows power"*, and a panel the player must open cannot be always visible. The shipped
  `LoamGauge.tsx:6` carries that same sentence in its doc comment while sitting in a scrolling column
  — the claim was never deliverable from a panel.

  Decided by the owner while reviewing [plate 11](../../design/11-world-stage.html) §G, which draws
  both halves. Full reasoning: [world-stage-ideal.md](../world-stage-ideal.md) §8b.5.
