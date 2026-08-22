# Capability map: world map program

**Status:** Map + wave-1 module specs drafted 2026-08-21 — **pending owner review**. No build until the specs are approved.
**Ideal it implements:** [world-graph-ideal.md](world-graph-ideal.md) (vision; owner picks recorded there). **Grounding audit:** [rpg-mechanism-audit-2026-08-21.md](rpg-mechanism-audit-2026-08-21.md).

## What this program is

A turn-based strategy map for the RPG: **sectors** as graph nodes, each a small board of **slots** you develop, joined by **lanes** you march legions down. Build economy, build army, expand territory, defend the homeworld, and eventually take Zomboss's fortress.

This wave builds **the foundation only** — the places and the clock. Objects that live on the map (buildings, generators, lairs, warcamps, bases) are later modules, and they land *into* this foundation rather than beside it. The owner's framing: without storage and a clock there is nowhere to put anything.

## Locked shape (from the ideal)

1. **Turn-based at the SSOT, simultaneous resolution.** A turn resolves every entity at once; the barrier waits for every commander to commit, with no deadline. `state(N+1) = step(state(N), commands(N))`.
2. **The barrier is the time model.** Adding a deadline would make it real-time; adding a wall-clock period would make it idle/persistent. `step` must never know why the barrier released.
3. **Strategy clock only.** A battle's internal clock (rounds, initiative, cooldowns) belongs to the combat stream. The seam is a request out, an outcome record in — narrow and versioned.
4. **Determinism is the product.** Integer math, stable ordering, seeded per-system streams, no wall clock inside `step`; a save is `(seed, template, command log)` and replay must be byte-identical.
5. **Content is data, not switch statements.** Sector types, slot types, lane types, and templates are validated catalogs. The audit's finding about parallel `switch (id)` blocks is what this rule exists to prevent.
6. **Gameless-first and one economy** stay untouched: no injector work anywhere in this program, all SQL inside `FusionRpg.Data`, existing ledgers reused when the map starts paying out.

## Modules

| Module id | Responsibility | Depends on | Wave |
|---|---|---|---|
| `world-model` | The nouns and their storage: worlds, sectors, slots, lanes, factions, entities; code-authored catalogs with validation; one hand-authored starter map | — | **1** |
| `turn-engine` | The SSOT clock: commands as data, per-turn command log, `WaitForAll` barrier, phase order, sim sub-steps, pure `step()`, turn report, state hash, replay | `world-model` | **1** |
| `world-movement` | The first verbs: legions, movement points, lane costs, zone of control, contact, claim, supply connectivity | `turn-engine` | **1** |
| `world-intel` | Fog of war for **every** faction: per-faction belief state, visibility rules, remembered snapshots with staleness, per-faction projection of the state endpoint, fog on the map view | `world-movement` | **2** |
| `world-topology` | Graph analysis of the lane network: all-pairs travel cost, articulation points, **reconnection cost** — what it costs the empire to lose one sector | `world-model` | **2** |
| `ai-commander` | Zomboss and neutral factions committing through the same command interface; policies over **beliefs**, the value matrix, difficulty as policy | `world-intel`, `world-topology` | 2 |
| `world-fe` | `#/world`: graph render, sector inspector, order queue, End Turn, turn report playback | `world-movement` | 2 |
| `sector-development` | Slot buildings, sector projects, production and upkeep, weekly recruit pulses, the calendar's economic half | `world-movement` | 3 |
| `combat-handoff` | `BattleRequest` / `OutcomeRecord` seam to the combat stream, replacing wave 1's placeholder resolver | `world-movement` + combat stream | 3 |
| `world-generator` | Templates: typed zones, value budgets, guard bands, connection rules — replaces hand-authored maps | `sector-development` | 4 |
| `fog-and-intel` | ~~Intel states, watch radius, stale-stamped views~~ — **superseded by `world-intel`, pulled forward to wave 2** (owner, 2026-08-22: fog is a prerequisite for a tunable AI, not a later polish). What remains here is the arrival forecast | `world-generator` | 4 |
| `bases-and-defense` | Base layouts, siege boards, offline defense resolution | `combat-handoff`, combat stream's board model | 5 |

**Build order:** `world-model` → `turn-engine` → `world-movement` → `world-fe` → (`world-intel` ∥ `world-topology`) → `ai-commander` → `sector-development` ∥ `combat-handoff` → `world-generator` → `fog-and-intel` → `bases-and-defense`.

Module specs: [world/spec-world-model.md](world/spec-world-model.md) · [world/spec-turn-engine.md](world/spec-turn-engine.md) · [world/spec-world-movement.md](world/spec-world-movement.md) · [world/spec-world-intel.md](world/spec-world-intel.md) · [world/spec-world-topology.md](world/spec-world-topology.md) · [world/spec-ai-commander.md](world/spec-ai-commander.md).

**Status (2026-08-22):** `world-model`, `turn-engine`, `world-movement`, `world-fe`, `world-intel` and `world-topology` are built and green — waves 1 and 2's first half, checkpoints 1–7, with only an owner look left on the map itself. Next in the build order: **`ai-commander`**, whose spec was rewritten against fog and then verified line by line against the shipped code; it is the last module of wave 2 and it is unbuilt.

## Why the generator is deliberately last of the four

Every knob a generator owns — guard bands, value budgets, slot mixes, lane widths — is tuned against a loop that does not exist yet. Building it early means balancing in the dark, and the audit already found this codebase authoring content faster than the systems that read it. Wave 1 ships **one hand-authored six-sector map**; the generator arrives when the loop can tell us whether a map is any good.

## Wave-1 checkpoint (the gate that proves the SSOT)

A CI test that loads the starter map, runs 20 turns from a scripted command log with stub commanders, and asserts:

1. the final state hash matches a golden,
2. replaying the same `(seed, template, command log)` reproduces it **byte-identically**,
3. every turn wrote exactly one transaction and one turn-log row,
4. no wall-clock read occurs inside `step` (guard test).

Nothing else in the program is safe to build before that passes.

## What this program does not touch

Shipped expeditions keep running on their real-time timers; their refactor onto turns happens **after** the map is complete (owner). No injector changes, no changes to `BattleEngine` semantics, no new event kinds in the existing ingest vocabulary, and no modification of the effect Funnel or Writer paths.

## Open questions carried from the ideal

Homeworld loss penalty (menu in the ideal §10.5) · campaign length in turns · sim steps per turn · whether the RTS/idle barrier policies stay genuinely open or remain a documented property · one world per save vs seasonal reroll.
