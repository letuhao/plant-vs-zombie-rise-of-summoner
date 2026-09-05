# Spec: world-movement (wave 1)

**Status:** Draft — pending owner review. Module id `world-movement` in the [world map program](../world-map-program.md). Depends on `turn-engine`.

## Objective

The first verbs on the map: legions that march, meet, claim, and starve. Movement points and lane costs, zone of control, contact resolution, claiming a sector, and the supply connectivity that decides whether a claim pays anything.

Success looks like: a legion ordered across three lanes arrives when the forecast said it would, is stopped dead by a hostile force standing in its path, claims the sector it cleared, and loses that sector's yield the moment the chain back to the homeworld is cut.

## Design

### Legions

A legion is a `rpg_world_entities` row of kind `legion` with members drawn from the roster (`rpg_unique_actors`). This module adds the movement and stance semantics, not new storage.

| Property | v1 rule |
|---|---|
| **Movement points** | a per-turn budget: a base value plus the slowest member's modifier; refilled in the Upkeep phase |
| **Stance** | `march` · `scout` · `hold` (v1). Scout trades movement for what it reveals — priced in [world-intel](spec-world-intel.md) at **half a turn's march for twice the sight**; hold gives up movement entirely for a defensive bonus and, in supply, **recovery** — see *What `hold` is for* |
| **Supply** | in supply if a chain of friendly-controlled sectors and open lanes connects it to the homeworld; out of supply costs attrition each turn |
| **State** | `idle` · `marching` · `routed` — routed legions fall back and skip a turn's orders |

### What `routed` falls back to

Amended 2026-09-05. The rule reads as one line, but "fall back" has two shapes: a legion caught
mid-crossing a lane still carries `on_lane_id`/`on_lane_toward_sector_id`, so it simply turns around on
the lane it was already on. A legion that fully *arrived* at a sector this same turn — a march that
completed, `on_lane_id` cleared like any ordinary arrival — has nothing left in its stored state to
retreat down, even though it did use a lane to get there this turn.

The first cut of this feature only wired the mid-crossing shape and left the second silently falling
through to "nowhere to fall back from" — indistinguishable in code from a genuine long-standing
garrison losing a fight on ground it already held. That is a real completeness gap, not an intended
scope line: an attack that fails should retreat the way it came, not stand entrenched in the sector it
failed to take. Closed the same day by threading which lane an attacker used to arrive, entity id to
lane id, as movement-phase-local data (never a stored `WorldEntity` field, never hashed) from
`MovementPhase` down through `BattleReporting.Fight` to `BattleApplication`'s own fall-back logic — it
cannot outlive the `MovementPhase.Run` call that produced it, so nothing new is actually remembered
about the world.

### What `hold` is for

Amended 2026-08-22. `hold` was listed as a stance from wave 1, documented as "a static garrison in place", and behaved in code exactly like `march` — full movement, no effect. Pricing `scout` made the gap obvious, and closing it turned out to fix something worse.

**There is currently no way to heal.** Wounds accumulate from battles and from attrition and nothing ever removes them, so every legion in the game is on a one-way trip to death. That is not a balance problem, it is a missing verb.

The prior art is unanimous about where the verb lives. Total War's [encamp](https://totalwarwarhammer.fandom.com/wiki/Encamp) stance disables movement, raises defence, and adds replenishment; Civilization's fortify grants +50% defence for standing still; [Rise of Nations](https://riseofnations.fandom.com/wiki/Healing) makes garrisoning the primary way anything heals at all. Immobility buys defence and recovery, everywhere.

So `hold` is:

| | Effect |
|---|---|
| Movement | **0** — it refills to nothing, and a `move` order for a held legion is dropped at reveal |
| In battle | counts as stationary for the defender bonus even if the attacker also stood still |
| In the Pressure phase | **recovers `RecoveryMilli = 150` of each member's health per turn — but only in supply** |

Recovery lives in Pressure beside attrition because they are the same thing seen from both sides: supply gives, and its absence takes. Out of supply, a held legion still starves — standing still does not feed anyone.

`RecoveryMilli = 150` against attrition's `50` means roughly seven turns from near-death to whole, and three turns of holding undoes one turn of a bad fight. First guess, per *double it or cut it by half*; the lever is one constant.

### Lane cost and event-ordered movement

Cost per lane is integer per-mille: `laneLength × typeMultiplier × (1000 + hazard) / 1000`, with a **corridor discount** and a **ley discount when the legion's banner element matches the lane's element**. Banner element is the plurality element of the legion's members, ties broken by the ring's declared order — a pure function, computed, never stored.

Movement resolves through the turn engine's **discrete-event queue** rather than fixed sub-steps. Each marching legion seeds an arrival event at an integer turn-fraction (0–1000 per-mille); a legion on a lane carries `lane_progress` so a march that does not finish this turn resumes exactly where it stopped. When two legions occupy the same lane in opposite directions, their **crossing time is solved arithmetically** — `crossTime = (1000 − progressA − progressB) × 1000 / (speedA + speedB)` in integer per-mille — and that exact moment is enqueued, so the meeting point never depends on a sampling rate or on which legion was processed first.

### Zone of control

Any hostile entity standing in a sector projects control onto the lanes touching it:

1. A legion entering that sector **stops immediately**, spending no further movement this turn.
2. Supply chains **do not pass through** it.

That is the whole rule, and it is deliberately the whole rule: it makes a force's *position* matter before any combat happens, which is the cheapest strategic depth available.

### Contact

Contact is evaluated when an event fires, in stable entity order:

| Case | Resolution |
|---|---|
| Two hostile forces in one sector | a battle request; neither side is the defender unless one was stationary at turn start |
| Two hostile forces crossing on a lane | a battle request on the lane, at the exact crossing moment |
| Forces of the same faction | stack freely; no merge in v1 |

**Guards are not contact.** A slot's guard defends the vein or the lair, not the ground, so marching through a guarded sector is free — only hostile *entities* project zone of control. Attacking a guard is a deliberate order:

| Command | Effect |
|---|---|
| `clear` (entityId, slotIndex) | a battle request against that slot's `guard_wave_id`; on victory `guard_state` becomes `cleared` |

Battle requests go out through `IBattleResolver` — the wave-1 placeholder now, the real combat seam at `combat-handoff`. Outcomes come back as records the world applies: survivors, wounds, deaths, rout.

### Claiming

A sector becomes **claimable** when no hostile entity stands in it and **every slot's `guard_state` is `cleared`**. A legion present at the Snapshot phase with a `claim` command committed flips `owner_faction_id`, moves the sector's phase to `held`, and stamps the turn. Claiming is idempotent: claiming what you already own is a no-op recorded in the report.

Because guards are per slot, a rich sector costs several turns and several fights before it can be held — which is the intended shape, not a side effect.

Slots inside a claimed sector do not become developed automatically — that is `sector-development`'s job. This module only establishes who holds the ground.

### Supply connectivity

Recomputed once per turn in the Pressure phase, as a traversal from every faction's homeworld/Seat through sectors that faction controls, over lanes that are `open` and not under hostile zone of control.

- A held sector that fails the traversal is **disconnected**: it stays yours, its yields stop (once yields exist), and its state is visible in the report.
- A legion outside supply takes attrition each turn.

Traversal is a plain breadth-first pass over the graph in stable order — cheap, deterministic, and recomputed rather than cached, because a cached connectivity flag is exactly the kind of derived state that rots.

### Commands added

| Command | Payload | Legality at Reveal |
|---|---|---|
| `move` | entityId, ordered lane path | entity is yours, is idle or marching, path is contiguous from its position |
| `stance` | entityId, stance | entity is yours; takes effect at the **next** Snapshot refill, so a stance change costs the turn you make it |
| `clear` | entityId, slotIndex | entity is yours, stands in that sector, and the slot's guard is still intact |
| `claim` | entityId, sectorId | entity is yours and stands in that sector |

Illegal-at-reveal commands are dropped with a reason into the turn report, never thrown.

**`stance` was never implemented.** Wave 1 shipped `stand-fast`, `move`, `clear` and `claim`; `stance` is in this table
and is not a command kind, so a legion's stance is whatever the template authored and can never change. Both `scout` and
`hold` are dead letters until it exists — which makes it wave 2 work, not a nicety.

Taking effect at the *next* refill rather than immediately is what stops a legion marching its full distance and then
declaring itself dug in for the defensive bonus. Committing to a posture has to cost the turn you commit.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests    # movement math, ZOC, contact, supply traversal
dotnet test tests\FusionRpg.E2E.Tests     # SIM: the wave-1 20-turn checkpoint with real movement
.\scripts\guard-dal.ps1
```

## Structure

```
src/FusionRpg.Core/World/Movement/  → MovementMath.cs, LaneCost.cs, ZoneOfControl.cs,
                                      ContactResolver.cs, SupplyGraph.cs, MovementCommands.cs
src/FusionRpg.Core/World/Turn/      → phase wiring only (Move, Contact, Pressure)
tests/FusionRpg.Core.Tests/World/   → cost goldens, crossing cases, ZOC cases, supply cases
tests/FusionRpg.E2E.Tests/          → the checkpoint scenario
```

## Code style

Everything here is a pure function over the world model with an injected RNG stream where a roll is needed; the phase wiring in `TurnEngine` calls them and applies results. Integer per-mille throughout. No storage of anything recomputable — `banner element`, `in supply`, and `claimable` are all computed.

## Testing strategy

- **Movement:** lane-cost goldens per lane type including corridor and ley discounts; a march spanning three turns resumes exactly; movement never goes negative or exceeds the budget.
- **Crossing:** two legions on one lane in opposite directions meet at the arithmetically exact time, identical regardless of processing order; a property test over random speed and progress pairs asserts the crossing point always lies between both start positions and that no event is ever enqueued earlier than the one being processed.
- **Zone of control:** entering a hostile sector halts; supply refuses to route through it; a legion already inside is not re-halted every sub-step.
- **Contact:** each row of the contact table, including the stationary-defender case and same-faction stacking.
- **Supply:** cutting one junction disconnects exactly the expected set; reconnecting restores it; out-of-supply attrition applies once per turn.
- **Determinism:** the whole checkpoint scenario replays byte-identically, and reversing the input order of entities changes nothing.

## Boundaries

- **Always:** pure functions; stable ordering; recompute derived state; every dropped command reported with a reason.
- **Ask first:** new stances, legion merging/splitting, movement-point formula changes, making attrition lethal.
- **Never:** caching supply or banner element; letting movement read a wall clock; resolving combat inside this module (it requests, it does not fight); writing SQL outside Data.

## Success criteria

1. The 20-turn checkpoint runs with real movement, claims, and supply, hashes to a golden, and replays byte-identically. 2. Every ZOC, contact, and supply case has a passing test. 3. Crossing legions meet deterministically. 4. Claims are idempotent and reported. 5. All suites and guards green.

## Open questions

Whether `hold` should convert a legion into a first-class garrison entity or stay a stance (the base module will have an opinion); whether scouting reveals through zone of control or is blocked by it — a fog question that `fog-and-intel` inherits.
