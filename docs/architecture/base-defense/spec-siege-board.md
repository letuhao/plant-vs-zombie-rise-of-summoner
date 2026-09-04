# Spec: `siege-board`

**Module 3 of 21 · level 1 · depends on nothing built here (see below) · [base-defense-map.md](../base-defense-map.md)**
**Status:** spec, 2026-09-04.

---

## Objective

**Give the grid vocabulary a board to live on.**

`GridPos` and `GridDistance` already exist and are used by real targeting code. What does not exist is
anything that says *how big the grid is*, *what is standing on which cell*, or *which cells are
passable*. Every consumer today handles the absence with the same sentinel — `PositionOf` returns
`null`, and `GridDistance.InRange` documents that with no board *"every range check passes"*.

This module is `A10 battle-board`, the module those sentinels were written waiting for.

**Success looks like:** a `GridSpec` + occupancy structure that `ActionTargetResolver`,
`UsabilityEvaluator` and `StubIntentSource` can read through the seams they already have, with no
change to their call signatures — and with the no-board path still byte-identical.

## Scope note on dependencies

The map lists this at level 1 after `battle-clock-profile`. That ordering is about **landing** order,
not a code dependency: nothing in this module references the profile. It is sequenced second so the
horizon is settled before anything resolves a battle on a board. It may be *written* in parallel.

---

## What already exists (verified at HEAD, 2026-09-04)

**Built.**

- `GridPos` — `public readonly record struct GridPos(int Row, int Col)`
  (`src/FusionRpg.Core/Actions/GridDistance.cs`). Deliberately *"independent of any one board
  representation — callers adapt their own entity-snapshot fields into this."*
- `GridDistance.Chebyshev` / `InRange` / `Square` — one metric, and its comment records *why*
  Chebyshev: *"the shipped `Square` area shape of size n already IS a Chebyshev ball of radius
  (n-1)/2 — this is the metric the existing code implies, not an arbitrary choice."*
- `IBattleView.PositionOf` — the read seam, `null` = no board.
- `ActionValidator.ValidateAction(row, containerAtomIds, boardAvailable)` — `Area` targeting is
  **already rejected** when `boardAvailable` is false (`ActionValidator.cs:41`).
- `ActionSeeder` — `boardAvailable` already filters which shapes are eligible to seed.
- `Combat/BoardSnapshot.cs` — **a different thing with a confusable name.** It is the injector's live
  lawn capture (`BoardEntitySnap`, `FindPtr`), consumed by `CombatDamageDispatcher` and
  `TargetResolver`. It is **not** a tactical grid and must not be extended into one.

**Wiring gap.**

- `BattleRunState.PositionOf` returns `null` unconditionally (`BattleRunState.cs:407`), with the
  reason stated inline: *"no board exists"*. One line.
- Every production caller passes `boardAvailable: false`.

**Real gap.**

- No `GridSpec`. No occupancy. No passability. No terrain.

---

## The contract

### 1. `GridSpec` — the board's shape

`src/FusionRpg.Core/Battle/Board/GridSpec.cs`, new folder.

```csharp
/// <summary>
/// A board's dimensions and its per-cell terrain. Immutable and value-equal: a board is an input to
/// a battle, never mutable state the battle edits.
/// </summary>
public sealed record GridSpec
{
    public int Rows { get; init; }
    public int Cols { get; init; }

    /// <summary>
    /// Row-major, length Rows*Cols. One byte-sized ordinal per cell — NOT a per-cell object: a
    /// district board is small, but this is read on every pathing step and every area enumeration,
    /// and an array of records would allocate on each.
    /// </summary>
    public IReadOnlyList<CellTerrain> Cells { get; init; } = Array.Empty<CellTerrain>();

    public bool Contains(GridPos p) => p.Row >= 0 && p.Row < Rows && p.Col >= 0 && p.Col < Cols;
    public int IndexOf(GridPos p) => p.Row * Cols + p.Col;
    public CellTerrain TerrainAt(GridPos p) => Cells[IndexOf(p)];
}

/// <summary>
/// What a cell is made of. An ordinal, per the seedsmith rule that a model picks enums and
/// deterministic code picks magnitudes — every movement cost and cover value keyed off this lives in
/// tuning, never here.
/// </summary>
public enum CellTerrain
{
    /// <summary>Ordinary ground. The default, and index 0, so a zero-filled array is a plain board.</summary>
    Open,
    /// <summary>Costs more to cross. Not impassable — a slow route is a decision, a wall is not.</summary>
    Rough,
    /// <summary>Blocks movement and line of sight. The district's own walls and terrain.</summary>
    Blocking,
    /// <summary>Blocks movement, does NOT block line of sight — a chasm, a moat, a rampart edge.</summary>
    Gap
}
```

**Why `Gap` is separate from `Blocking`:** the moat that decision 27's "laboured" acquisition path
digs must stop a unit without stopping an archer. Folding the two into one value would make a dug moat
also a smoke screen, which is a mechanic nobody asked for and a bug nobody would find quickly.

### 2. `BoardState` — who is standing where

Occupancy is the one mutable part, and it is mutable **within a battle only**.

```csharp
/// <summary>
/// Cell occupancy for one battle. Mutable inside a resolve, never persisted, never hashed — the
/// world stores the district LAYOUT (district-layout) and the structure STATE (structure-state);
/// where a given soldier stood on round 7 is not world state and must not become it.
/// </summary>
public sealed class BoardState
{
    public GridSpec Spec { get; }

    /// <summary>Actor key → cell. One cell per actor.</summary>
    public IReadOnlyDictionary<string, GridPos> Positions { get; }

    /// <summary>
    /// Cell → occupant key, or null. **One occupant per cell** — the invariant every mechanic in
    /// this program assumes (cover is delivered to "the occupant", income to "whoever garrisons").
    /// Enforced on every move, loudly.
    /// </summary>
    public string? OccupantAt(GridPos p);

    /// <summary>
    /// Passable for MOVEMENT: inside the board, terrain is not Blocking or Gap, and no one is
    /// standing there. Structures make their own cell impassable via structure-state; this method
    /// does not know about structures and must not learn.
    /// </summary>
    public bool CanEnter(GridPos p);

    public void Place(string actorKey, GridPos p);   // throws if occupied or impassable
    public void Move(string actorKey, GridPos to);   // throws if !CanEnter(to)
    public void Remove(string actorKey);             // on death or withdrawal
}
```

**`Place`/`Move` throw rather than returning false.** A caller that tries to move into an occupied
cell has a bug in its intent generation, and a silent no-op turns that into a unit that mysteriously
does not advance — the hardest class of bug to see in a turn log. `siege-ai` and `siege-pathing` both
ask `CanEnter` first; the throw is for the case where they did not.

### 3. Determinism: iteration order is part of the contract

`Positions` is a `Dictionary` internally and **must never be enumerated for anything that affects the
outcome.** Where an ordered walk over actors is needed, callers order by actor key, ordinal —
the same discipline `LegionSupply` already applies (`.OrderBy(x => x.Entity.EntityId,
StringComparer.Ordinal)`).

State this as a test, not a convention:

```csharp
[Fact] public void Board_exposes_no_order_dependent_enumeration() // reflection scan, or an
                                                                  // explicit ordered accessor only
```

### 4. Wiring the existing sentinels — and keeping the null path exact

Three consumers already branch on absence. **This module changes none of their signatures.**

| Consumer | Today | With a board |
|---|---|---|
| `BattleRunState.PositionOf` (`:407`) | `=> null` | `=> _board?.Positions.TryGetValue(actorKey, …)` — still `null` when `_board` is null |
| `GridDistance.InRange` | either side null → `true` | unchanged; now gets real values |
| `ActionValidator` `boardAvailable` | `false` at every production call | `_board is not null` |

**The no-board path must stay byte-identical.** `BattleRunState` gains a `BoardState? _board` that is
`null` for every caller that does not supply one, which is every caller until `siege-resolver`. This
is what makes the module golden-free: a nullable field that nothing sets changes no serialized bytes
and no code path.

> **`BattleRunState` is inside `Core/Battle`, which Gate 0 just brought under the determinism guard.**
> No `DateTime`, no `System.Random` in any file added here. The board rolls nothing.

### 5. What this module does **not** do

- **No pathing.** `CanEnter` is a predicate; routes are `siege-pathing`.
- **No structures.** A structure occupying a cell is `structure-state`, delivered through the same
  occupancy API.
- **No layout generation.** Where the walls are is `district-layout`.
- **No rendering.** `board-render` imports `GridSpec`; `GridSpec` never imports anything visual.

---

## Tunables

`data/tuning/siege.v1.json` — **a new domain file.** Per
[tunables-ssot.md](../tunables-ssot.md).

| Key | Unit | Default | Why tunable |
|---|---|---|---|
| `board.moveCost.open` | cost units | `10` | Balance |
| `board.moveCost.rough` | cost units | `20` | Balance — the whole point of `Rough` is that this is dialled |
| `board.maxCells` | cells | `4096` | **Structural**, and must say so in a comment: an allocation/perf bound on one board, not a progression ceiling. `AGENTS.md` exempts *"structural limits (recursion, buffers)"*. A board larger than this is a bug in `district-layout`, not an ambitious player. |

**Costs are integers, base 10 rather than base 1.** `Open = 10` leaves room for a "0.5× road" as `5`
without ever introducing a fraction — the same reason per-mille exists elsewhere in this repo.

## Numeric types

- `Rows`, `Cols`, cell indices: **`int`**. Structural dimensions bounded by `board.maxCells`; they are
  not magnitudes `contentScale` touches.
- Movement costs: **`int`** per step, **`long`** for any accumulated path total. A path total is a sum
  over an unbounded number of steps in principle, and `CLAUDE.md`'s rule 1 applies to any accumulator
  that could grow — the widen is free and the alternative is an audit finding.
- **No `float` anywhere.** Chebyshev is integer by construction; the moment a diagonal cost becomes
  `1.414` this module has broken determinism. If a diagonal must cost more than an orthogonal step,
  it costs `14` against `Open = 10` — integer, exact, and hashable.

## Boundaries

**Always:** integer arithmetic only · order by actor key, ordinal, wherever order is observable ·
keep the `null`-board path exact.

**Ask first:** raising `board.maxCells` · adding a fifth `CellTerrain` value (the enum is small on
purpose, and `structure-seed` may want to own terrain identity instead).

**Never:** extend `Combat/BoardSnapshot` into a tactical grid — it is the injector's lawn capture and
`BattlefieldScopeExecutor.cs:11` records that its shape *"matches the injector's live capture fields
exactly"* · store `BoardState` in `WorldState` · put a movement cost in code.

---

## Testing

`tests/FusionRpg.Core.Tests/Battle/Board/`.

| Test | Asserts |
|---|---|
| `Chebyshev_matches_the_shipped_square_shape` | the existing invariant still holds against a real board — `GridDistance.Square(c, n)` is exactly the cells within radius `(n-1)/2` |
| `Index_round_trips_for_every_cell` | `IndexOf`/`TerrainAt` on a non-square board, both dimensions ≠ each other (a square board hides a row/col transposition) |
| `Gap_blocks_movement_but_not_sight` | the reason `Gap` exists, asserted rather than commented |
| `One_occupant_per_cell_is_enforced` | `Place` into an occupied cell throws |
| `Move_into_blocking_throws` | not a silent no-op |
| `Null_board_path_is_byte_identical` | **the gate.** Full battle golden suite with `_board` null |
| `Position_of_returns_null_with_no_board` | `BattleRunState.cs:407`'s contract survives the change |
| `Board_has_no_order_dependent_enumeration` | determinism, structurally |
| `Max_cells_is_enforced_loudly` | a 5000-cell spec throws at construction, not at render |

## Success criteria

1. `GridSpec` + `BoardState` exist, are integer-only, and hold no clock and no RNG.
2. Every existing battle golden is byte-identical with no board supplied.
3. `BattleRunState.PositionOf` returns real positions when a board is present and `null` when not,
   proven both ways.
4. `ActionValidator`'s `Area` rejection flips correctly on `boardAvailable`, proven both ways.
5. No signature in `Core/Actions` changed.
6. `Core/Battle` passes the Gate-0-extended `WorldDeterminismGuardTests`.

## Open questions

**One.** Are diagonal moves legal, and if so do they cost more?

Chebyshev distance already *implies* diagonals are one step — that is what Chebyshev means, and
`GridDistance.Square` already enumerates diagonal neighbours as equidistant. So the metric has
answered "are they legal": **yes**, and changing that would desynchronise movement from every range
check already shipped.

The open half is cost. **Recommendation: same cost as orthogonal** (`board.moveCost.open` for both),
because a differing cost re-introduces the Chebyshev/Euclidean mismatch through the back door — a unit
would move in a Chebyshev circle but pay a Euclidean price. If playtest says diagonal movement feels
too strong, the dial is `board.moveCost.diagonalSurcharge`, integer, defaulting to `0`. **Owner
decision; the recommendation is safe to build against and reversible in one tuning row.**
