# Spec: `district-layout`

**Module 5 of 21 · level 2 · depends on `siege-board` · [base-defense-map.md](../base-defense-map.md)**
**Status:** spec, 2026-09-04.

---

## Objective

**Turn a sector into a board, the same way every time.**

Decision 26: the siege board is **the district around the Seat** — not the whole city, not one slot.
Outer ground carries obstacles and buildings; a central defense area holds the legions that are the
win condition.

This module is the pure function `(sectorId, worldSeed, slots) → GridSpec` plus its **stability
contract**, which is the harder half. A board that regenerates differently between the turn a player
looks at it and the turn they fight on it is worse than no board.

**Success looks like:** the same sector produces the same board on every replay, across turns, after
capture, and after slots are added — and the *only* thing that changes it is the sector's own slot
list growing.

---

## What already exists (verified at HEAD, 2026-09-04)

**Built.**

- `WorldSector` — `SectorId`, `TypeId`, `Climate`, `DangerBand`, `DevelopmentLevel`, `LayoutX/Y`,
  and `Slots` (*"Ordered by `WorldSlot.SlotIndex`, contiguous from zero"* — a stated invariant this
  module leans on).
- `WorldSlot` — `SlotIndex`, `SlotTypeId`, `State`, `OwnerFactionId`, `GuardWaveId`, `GuardState`,
  `StructureId`, `ConstructionTurnsRemaining`.
- `SlotTypeCatalog.SeatSlotTypeId` — how `SupplyGraph` already finds a Seat. The district's centre.
- `WorldTemplateCatalog.Build(templateId, seed, worldId)` — worlds are already regenerated
  deterministically from a seed; this module is the same discipline one level down.
- `WorldEntity.OnLaneId` / `OnLaneTowardSectorId` — where an attacker is coming *from*.

**Declared and unread** (all confirmed still unread at HEAD; each is an opportunity, not a blocker):

- `WorldLane.Width` and `WardLevel`
- `SlotState.Ruined` and `Depleted`
- `SectorTypeFlags.Fortress`

**Real gap.** Nothing converts a sector into a grid.

---

## The contract

### 1. The stability contract — four properties, all testable

This is the module's actual specification. The generator is easy; these are not.

| # | Property | Why it matters | How it is guaranteed |
|---|---|---|---|
| **S1** | **Byte-stable on replay** | `RpgStore.WorldTurns.cs:603` re-simulates a world from turn zero to re-derive a report. If the board differs, the re-derived siege differs from the one that happened. | Seeded only from `(worldSeed, sectorId)` — never from turn, never from a clock |
| **S2** | **Stable across turns** | A player scouts a district on turn 3 and assaults on turn 7. The walls must be where they looked. | The seed excludes turn |
| **S3** | **Unchanged by capture** | Taking a city does not rebuild its streets. | The seed excludes `OwnerFactionId` |
| **S4** | **Stable under slot growth** | Owner decision: *"Grow slots"*. Adding slot 7 must not move slots 0–6. | Each slot's cell is a **pure function of its own `SlotIndex`**, not of the list length |

**S4 is the one a naive implementation gets wrong.** Shuffling a slot list and dealing out positions
is the obvious approach and it re-deals everything the moment the list grows. Instead:

```csharp
/// <summary>
/// Where slot `i` sits. A function of the slot's OWN index and the district seed — never of how many
/// slots exist (S4). Adding slot 7 must leave slots 0..6 exactly where they were, because a player
/// who built a wall around their granary did not consent to it moving when they built a barracks.
/// </summary>
static GridPos CellForSlot(ulong districtSeed, int slotIndex, GridSpec spec);
```

Implemented as a **ring walk from the Seat**, deterministic and collision-free by construction:
slot `i` takes the `i`-th cell of a fixed spiral order out from the centre, with the spiral's
*rotation* (not its shape) picked from `districtSeed`. Growth appends; it never re-deals.

### 2. Board size — from base tier, and it is a genuine cap question

Board size scales with the sector. Two candidate inputs, and **`AGENTS.md`'s no-hard-ceilings rule
makes this a design decision rather than a formula choice**:

```
side = boardBaseSide + boardSidePerDevelopment × DevelopmentLevel + boardSidePerSlot × Slots.Count
```

All three terms are **flat integer tunables in `data/tuning/siege.v1.json`**.

> ### ⛔ This is deliberately NOT on the power ladder, and that is a correction
>
> An earlier draft of the ideal put board dimensions on `P(Θ)`. **That was a category error and it is
> recorded here so it is not re-made.** `P(Θ)` is quadratic with `B = 0.4`, so `P(1) = 106` — a board
> "106 cells on a side" at the very first index, growing quadratically from there. The board would
> saturate any renderer, any pathfinder, and any player's attention on turn one.
>
> [ssot-power-scale.md](../power/ssot-power-scale.md) §10's inventory is a closed list of
> **power-shaped** scales; a board dimension is not one. **Contests read `Θ`, magnitudes read
> `P(Θ)`, and a board side is neither** — it is a flat, board-bounded structural dimension, the same
> class as a screen size.
>
> `board.maxCells` (from `siege-board`) bounds it, and that bound is **structural** — an allocation
> and rendering limit on one board, explicitly exempt under `AGENTS.md`'s *"structural limits
> (recursion, buffers)"*. It is not a progression ceiling: a player's power grows without limit; the
> district they fight in does not need to.

### 3. The three zones

```csharp
public enum DistrictZone
{
    /// <summary>Outside the wall. Where a besieger deploys and where obstacles are dug.</summary>
    Approach,
    /// <summary>The wall line itself — Blocking terrain except at gates.</summary>
    Rampart,
    /// <summary>Inside. Slots live here; the win condition stands here (decision 26).</summary>
    Core
}
```

`Core` is a centred rectangle whose side is `coreSideMilli` per-mille of the board side — per-mille
because it is a **ratio**, which `AGENTS.md` exempts from the ceilings rule explicitly and which keeps
it integer.

**Gates.** `gateCount` openings in the `Rampart`, placed at the cardinal midpoints and rotated by the
district seed. At least one gate is **always** on the entry edge — otherwise a besieger must breach
before they can act, and *"the attacker cannot reach the defender"* is not a difficulty setting.

### 4. The entry edge comes from the map, not from the board

Which edge the attacker deploys on is `entity.OnLaneId`'s endpoint relative to the sector — the
approach is the direction they actually marched from. Deterministic from world state, and it means a
flanking march changes the assault, which is free strategic depth from data that already exists.

```csharp
/// <summary>
/// Which board edge the attacker enters on: derived from the lane they arrived by, so marching the
/// long way round genuinely changes the assault. Falls back to `North` when the attacker is already
/// standing in the sector (no lane) — deterministic, and the case exists because a garrison that
/// turns on its host has no approach march.
/// </summary>
public static BoardEdge EntryEdgeFor(WorldState world, WorldEntity attacker, string sectorId);
```

Ordering the lanes by `LaneId` ordinal before picking makes this replay-stable when two lanes share an
endpoint.

### 5. Reading what is already declared-and-unread

Three shipped-but-unread fields become inputs here. Each is a real feature for near-zero cost, and
each must be **read, not assumed** at implementation time:

| Field | Becomes | Note |
|---|---|---|
| `SectorTypeFlags.Fortress` | `+fortressRampartBonus` to wall thickness, `−1` gate | The flag exists and nothing reads it. Verify the flag is actually set on any shipped sector type before claiming this works — if no template sets it, this is a **wiring gap** and must be reported as one, not as a delivered feature. |
| `WorldLane.WardLevel` | Approach-zone depth on that lane's edge | A warded lane means a longer approach under fire |
| `SlotState.Ruined` / `Depleted` | Slot's cell is `Rough`, and carries no structure | Ruins are rubble. Already-declared enum values, zero new vocabulary |

### 6. `DevelopmentLevel` is read and defaulted

Per the map's *"What it is not"*: sector-scale economy belongs to `sector-development`. This module
**reads** `DevelopmentLevel` and treats absence as `0`. It never writes it.

### 7. Output is not persisted

`GridSpec` is **derived, not stored.** It is recomputed from `(worldSeed, sectorId, slots)` whenever
needed — the same stance `SupplyGraph` takes for connectivity and for the same reason: a cached board
goes stale the first time a slot changes, and it would then be wrong exactly when it matters.

**Nothing in this module touches `WorldCanonical`.** Zero goldens move. That is what makes it
level 2.

---

## Tunables

`data/tuning/siege.v1.json`.

| Key | Unit | Default | Why tunable |
|---|---|---|---|
| `district.boardBaseSide` | cells | `24` | Balance — the base district |
| `district.boardSidePerDevelopment` | cells | `2` | Balance |
| `district.boardSidePerSlot` | cells | `1` | Balance |
| `district.coreSideMilli` | per-mille of side | `400` | **Ratio** — bounded 0..1000, exempt, and it must say so |
| `district.gateCount` | gates | `2` | Balance |
| `district.rampartThickness` | cells | `1` | Balance |
| `district.fortressRampartBonus` | cells | `1` | Balance |
| `district.approachDepth` | cells | `4` | Balance |
| `district.approachDepthPerWardLevel` | cells | `1` | Balance |

## Numeric types

- Board dimensions, zone sizes, gate counts: **`int`**, bounded by `board.maxCells`.
- `districtSeed`: **`ulong`**, matching `WorldTemplateCatalog.Build`'s existing seed type exactly.
- `coreSideMilli`: **`int`** per-mille, and **the divide by 1000 happens exactly once, last**
  (`CLAUDE.md` rule 4).

Derivation, and it is fixed:

```csharp
// Order matters and is part of the contract: hashing (worldSeed, sectorId) in any other order, or
// with any other mixer, produces a different board. Changing this line invalidates every scouted
// district in every save.
static ulong DistrictSeed(ulong worldSeed, string sectorId) =>
    SeededRng.Mix(worldSeed, SeededRng.HashOrdinal(sectorId));
```

Reuse `Battle/SeededRng`'s existing mixer. **Do not write a new hash** — that is a private `f(seed)`
and it is the same defect class as a private `f(level)`.

## Boundaries

**Always:** seed from `(worldSeed, sectorId)` only · slot cells from `SlotIndex` alone · at least one
gate on the entry edge · order lanes by `LaneId` ordinal before any pick.

**Ask first:** changing `DistrictSeed`'s mixing (it invalidates every existing scouted board) ·
reading a field the map's §3 lists as declared-and-unread without first verifying anything sets it.

**Never:** include turn, owner, or a clock in the seed · persist `GridSpec` · write
`DevelopmentLevel` · put a board dimension on `P(Θ)` — see the boxed correction in §2 · invent a hash.

---

## Testing

`tests/FusionRpg.Core.Tests/World/District/`.

| Test | Asserts |
|---|---|
| `Same_sector_same_seed_same_board_10000_times` | **S1** |
| `Board_is_identical_on_turn_3_and_turn_70` | **S2** |
| `Capture_does_not_change_the_board` | **S3** — flip `OwnerFactionId`, assert byte-identical |
| `Adding_a_slot_moves_no_existing_slot` | **S4.** Grow 6 → 7 slots, assert cells 0–5 unmoved. *The test most likely to fail on a first implementation.* |
| `Every_slot_gets_a_distinct_cell` | no collisions at any slot count from 1 to the cap |
| `At_least_one_gate_is_on_the_entry_edge` | for all four edges |
| `Entry_edge_follows_the_arrival_lane` | attacking from two lanes gives two different edges |
| `Entry_edge_is_stable_when_two_lanes_share_an_endpoint` | ordinal ordering, asserted |
| `Fortress_flag_changes_the_wall` | **and a companion test asserting some shipped sector type actually sets the flag** — otherwise report it as a wiring gap |
| `Ruined_slots_are_rough_and_carry_no_structure` | |
| `Board_never_exceeds_maxCells` | at the largest legal `DevelopmentLevel` and slot count |
| `Core_zone_is_never_empty` | the degenerate small-board case: `coreSideMilli` of a 24-cell side must not floor to 0 |
| `World_goldens_unmoved` | nothing here is hashed |

## Success criteria

1. All four stability properties (S1–S4) hold, each with its own test.
2. A board is never persisted and never enters `WorldCanonical`.
3. No board dimension is derived from `P(Θ)`.
4. `SeededRng`'s existing mixer is reused; no new hash function exists in this module.
5. The three declared-and-unread fields are either genuinely read **or** reported as wiring gaps with
   `file:line` — not silently claimed.

## Open questions

**One, and it needs the owner.** Where do the *defender's* legions start?

The win condition is the legions in the central defense area (decision 26). Two readings:

- **(a) Fixed formation in the `Core`** — the defender always starts inside the wall, positioned by
  the same deterministic spiral. Simple, stable, and it makes the wall meaningful from round one.
- **(b) Defender-placed during a pre-battle deployment step** — decision 5 already establishes
  pre-battle deployment costing unit actions and resources, so the machinery is coming anyway.

**Recommendation: (a) for this module, with (b) layered on in `siege-positions`.** A deterministic
default placement is needed regardless — for auto-resolve at step 7, which is the gate that proves the
whole program works with no FE. (b) then becomes an override rather than a prerequisite, and step 7
does not wait on a deployment UI.
