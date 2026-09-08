# Spec: `siege-pathing`

**Module 4 of 29 · level 2 · depends on `siege-board` · [base-defense-map.md](../base-defense-map.md)**
**Status:** spec, 2026-09-04.

---

## Objective

**Routes across the board, computed the same way on every machine, every replay, forever.**

A district board is bigger than the six-sector world graph, so the linear scan `ReachMap` uses is no
longer free — but the moment a heap is introduced, tie-breaking stops being incidental and becomes
load-bearing. `ReachMap`'s own comment is the warning, written by whoever chose *not* to use a heap:

> *"No priority queue: at six sectors the scan is free, and ties break by ordinal id so two equally
> cheap routes always settle in the same order. **A heap would need the same tie-break written
> explicitly or a replay could disagree with itself.**"*

This module is the "written explicitly" half. It is separated from `siege-board` for exactly that
reason: occupancy is a data structure with obvious correctness, and pathing is a determinism contract
where the failure mode is a replay that disagrees with itself on a different machine three weeks
later.

**Success looks like:** the same `(board, start, goal, costs)` yields a byte-identical route on every
run, and the tie-break is asserted by a test that fails if a heap is swapped in carelessly.

---

## What already exists (verified at HEAD, 2026-09-04)

**Built.**

- `ReachMap.Dijkstra` (`src/FusionRpg.Core/World/Ai/ReachMap.cs`) — the pattern to copy: integer
  costs, `.OrderBy(kv => kv.Key, StringComparer.Ordinal)` inside the frontier scan, unreachable is
  **absent** rather than `int.MaxValue`.
- `ReachMap.For`'s ceil-division to turns: `(cost + budget - 1) / budget`, with the reason inline —
  *"an arrival part-way through a turn is next turn"*.
- `GridDistance.Chebyshev` — an admissible, integer, zero-allocation heuristic. Already shipped.

**Real gap.** No board pathing of any kind.

---

## The contract

### 1. A* with an explicit, total tie-break

`src/FusionRpg.Core/Battle/Board/BoardPathfinder.cs`.

```csharp
/// <summary>
/// Cheapest route between two cells, or null when none exists. **Null is "no route", never a large
/// number** — the same distinction ReachMap already draws by making unreachable sectors absent: a
/// caller that conflates them walks a unit at a wall forever.
/// </summary>
public static BoardPath? Find(GridSpec spec, IBoardOccupancy occ, GridPos start, GridPos goal, MoveCosts costs);
```

**The tie-break is the specification, not an implementation detail.** A binary heap's pop order among
equal-priority entries is unspecified, so equality must be impossible. Every frontier entry is ordered
by a **total** key:

```
(fScore, hScore, cellIndex)
```

1. `fScore = gScore + h` — the A* priority.
2. `hScore` second: among equal `f`, prefer the node closer to the goal. This is the standard
   tie-break and it also *reduces* explored nodes, so it costs nothing.
3. **`cellIndex` third, and it is what makes the ordering total.** `spec.IndexOf(pos)` is a unique
   integer per cell, so no two frontier entries can ever compare equal. This is `ReachMap`'s
   `StringComparer.Ordinal` on ids, translated to a grid.

With a total comparator, the heap's internal ordering is irrelevant — there is exactly one valid pop
sequence, so any correct heap produces it.

**Neighbour enumeration is fixed and ordered**, not derived from a loop over `dr`/`dc` whose order a
refactor could change:

```csharp
// Row-major clockwise from north-west. FIXED ORDER — a change here changes which of two equal-cost
// routes is returned, which changes a replay. Not a style choice.
static readonly (int dr, int dc)[] Neighbours =
    { (-1,-1), (-1,0), (-1,1), (0,-1), (0,1), (1,-1), (1,0), (1,1) };
```

### 2. The heuristic must stay admissible, and that is a constraint on tuning

A* is only correct if `h` never overestimates. With Chebyshev movement and `Open = 10`:

```csharp
static long Heuristic(GridPos a, GridPos b, MoveCosts costs) =>
    (long)GridDistance.Chebyshev(a, b) * costs.MinStepCost;
```

`MinStepCost` is `min(open, rough, …)` over every **enterable** terrain — computed, not configured.
This is the line that keeps admissibility true no matter what a balance pass does to
`data/tuning/siege.v1.json`: if someone adds a cheap terrain, `MinStepCost` drops and the heuristic
stays valid automatically.

**Assert it.** A test that sets `rough` cheaper than `open` and still finds the optimal path is the
only thing standing between a balance pass and silently suboptimal routes.

> **Widen before multiplying.** `(long)Chebyshev(...) * MinStepCost`, not `(long)(Chebyshev * cost)`.
> `CLAUDE.md` rule 3, and here the cast placement is the difference between a correct heuristic and an
> overflowed negative one that makes A* return garbage rather than throw.

### 3. Occupancy is a *parameter*, not a fact of the board

A unit standing in a doorway blocks it now and will not next round. So the pathfinder takes an
occupancy view rather than reading `BoardState` directly:

```csharp
public interface IBoardOccupancy
{
    bool IsBlocked(GridPos p);
}
```

Two shipped implementations, and the second is the one the AI needs:

| Implementation | `IsBlocked` | Used by |
|---|---|---|
| `SolidOccupancy` | terrain + every occupant | actual movement — you cannot walk through anyone |
| `TerrainOnlyOccupancy` | terrain only | `siege-ai`'s planning — *"can I ever get there"* vs *"can I get there this instant"* |

Without the split, an AI surrounded by its own allies concludes the goal is unreachable and stands
still — the single most visible AI failure in any tactical game, and it is caused by asking the wrong
one of these two questions.

### 4. Bounded work, and it throws

```csharp
/// <summary>
/// Structural bound on one search, not a progression ceiling (AGENTS.md exempts per-runtime caps).
/// A search that exceeds it is a bug — an unbounded board, or a cost that went negative — and must
/// throw rather than return a wrong-but-plausible partial route.
/// </summary>
const int MaxExpansionsMultiple = 4;   // × spec.Rows * spec.Cols
```

A correct A* expands each cell at most once, so `4 × cellCount` is pure headroom. Exceeding it means
the invariant broke; a `null` return there would be indistinguishable from "no route" and would hide
the defect permanently.

**Negative costs throw at `MoveCosts` construction**, not during search. A negative edge makes
Dijkstra and A* both silently wrong rather than loudly broken, and the cost table comes from tuning —
which means a typo in a JSON file could otherwise corrupt every route in the game.

### 5. `BoardPath`

```csharp
public sealed record BoardPath
{
    /// <summary>Start-inclusive, goal-inclusive. A path from a cell to itself is one step, not zero
    /// — callers slice by movement budget and an empty list makes that arithmetic a special case.</summary>
    public IReadOnlyList<GridPos> Steps { get; init; } = Array.Empty<GridPos>();

    /// <summary><b>long.</b> A sum over an unbounded step count — CLAUDE.md rule 1. The per-step
    /// cost is int; the accumulator is not.</summary>
    public long TotalCost { get; init; }
}
```

---

## Tunables

`data/tuning/siege.v1.json`. Costs are shared with `siege-board`; this module adds no new cost row.

| Key | Unit | Default | Why |
|---|---|---|---|
| `board.moveCost.diagonalSurcharge` | cost units | `0` | Balance — the dial `siege-board`'s open question names. `0` keeps Chebyshev honest. |

`MaxExpansionsMultiple` is **structural** and stays a `const` with the comment above — changing it
does not change how the game feels, only whether a bug is caught.

## Numeric types

| Value | Type | Why |
|---|---|---|
| per-step cost | `int` | bounded by the tuning table |
| `gScore`, `fScore`, `TotalCost` | **`long`** | accumulators over an unbounded step count |
| `hScore` | **`long`** | it is compared against `f`, and a mixed-width comparison is where a widen gets forgotten |
| cell index | `int` | bounded by `board.maxCells` |

**No `float`.** A Euclidean heuristic would be both non-integer and *inadmissible* against Chebyshev
movement — wrong twice.

## Boundaries

**Always:** total comparator, `(f, h, cellIndex)` · fixed neighbour order · throw on negative cost ·
integer arithmetic.

**Ask first:** switching to jump-point search or any hierarchical scheme (both change which equal-cost
route is returned, which is a replay change) · caching paths across rounds.

**Never:** a `float` heuristic · `int.MaxValue` for unreachable · reading `BoardState` directly
instead of through `IBoardOccupancy` · `HashSet`/`Dictionary` enumeration anywhere the result depends
on order.

---

## Testing

`tests/FusionRpg.Core.Tests/Battle/Board/`.

| Test | Asserts |
|---|---|
| `Equal_cost_routes_resolve_identically_across_10000_runs` | **the module's reason for existing.** A symmetric board with two mirror-image optimal routes; assert the same one every time |
| `Tie_break_survives_a_heap_swap` | run the same search through the linear-scan reference and the heap; assert identical `Steps`. This is the test `ReachMap`'s comment asks for |
| `Heuristic_stays_admissible_when_rough_is_cheaper_than_open` | a balance pass cannot silently break optimality |
| `Optimal_cost_matches_a_brute_force_dijkstra` | on 50 seeded random boards |
| `No_route_returns_null_not_a_large_number` | walled-off goal |
| `Terrain_only_occupancy_routes_through_allies` | the AI-standing-still failure, prevented |
| `Solid_occupancy_does_not` | the other half, or the split is pointless |
| `Negative_cost_throws_at_construction` | not during search |
| `Expansion_cap_throws_rather_than_returning_partial` | a wrong route must never look like no route |
| `Path_from_a_cell_to_itself_is_one_step` | the budget-slicing arithmetic |
| `Gap_is_impassable_but_transparent` | consistent with `siege-board`'s reason for the value |

**The 10,000-run test is not paranoia.** Non-determinism from unordered container iteration is exactly
the failure that reproduces on one machine and not another; a single run proves nothing.

## Success criteria

1. Two equal-cost routes always resolve to the same one, proven over 10,000 runs.
2. The heap implementation and a linear-scan reference return byte-identical paths on 50 random
   boards.
3. Admissibility holds under an adversarial cost table.
4. No `float`, no unordered enumeration, no `int.MaxValue` sentinel.
5. `Core/Battle` still passes the Gate-0-extended determinism guard.

## Open questions

None. `siege-board`'s diagonal-cost question is the only live one and it is recorded there; this
module reads whatever that dial says and its admissibility proof holds either way.
