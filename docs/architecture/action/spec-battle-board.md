# Spec: battle-board (A10)

Module **A10** in the [action map](../action-map.md). Depends on **A1**.

> **Deferred by the owner** — built with the board map / battle area, not in wave 1. Specced ahead because documents reconcile cheaply and code does not, and because `A2`, `A7`, and `A9` all carry parameters that are inert precisely until this exists.

## Objective

Give the battle a space: cells, positions, occupancy, distance, and movement legality — in the shape of Galaxy Online.

Today the battle has none. `SelectTarget` walks a list, `FindAdjacentWithTrait` means adjacency by **list index**, and everyone can hit everyone. Nothing is ever out of reach.

## Design (locked on approval)

### 1. A 2-D grid, sized per encounter, from the seed

Dimensions are **encounter data, not `CombatPolicy`**. Policy is process-wide and carries lawn bounds; battle bounds must travel with the encounter or two concurrent battles share one board size.

**Random size is part of the determinism surface.** Dimensions come from the encounter's **seeded** generator and must be reproducible from `(setup, seed)` — the same rule every other roll in the battle already follows. An ambient draw here makes every replay a lie.

**The random range must be bounded**, and the bound is a balance decision rather than a technical one: range 3 is long on a 5×5 board and short on 20×20. Either the interval is stated or ranges become a fraction of the board — and bounded absolute ranges are simpler. *"Random"* without a stated interval is the bug.

### 2. One actor per cell

No overlap. Three rules follow, and each should be written rather than discovered:

| Rule | Consequence |
|---|---|
| **Destination is free** | A move to an occupied cell is refused, not resolved |
| **A blocked line paths around, or the move is refused** | Straight-line teleport through an occupant is no longer acceptable |
| **Spawns need a free cell** | Including the case where a summon has **nowhere to land** — the game's core verb hits this, so it is not an edge case |

Body-blocking arrives free: one actor in a corridor stops a column. That is a feature, and it is the first real use of position.

### 3. Chebyshev distance

```
distance(a, b) = max(|Δcol|, |Δrow|)     — diagonals cost 1
```

Not arbitrary. The shipped `Square` area shape of size *n* **is** a Chebyshev ball of radius `(n−1)/2`, so this is the metric the existing shape code already implies. Manhattan would contradict a shape that ships, and the disagreement would surface as an area that does not match its own range.

### 4. It builds a `BoardSnapshot`, and that is the whole integration

`BoardSnapshot` is described as a *"frozen lawn census"*, but structurally it is `{ Ptr, Side, TypeId, Col, Row, MindControlled, Living }` — nothing about it is lawn-specific except the comment. Grid bounds already come from `CombatPolicy.LastCol` / `LastRow`.

> So a battle grid builds a `BoardSnapshot` and **the entire targeting stack works unchanged, `Area` included.** No resolver change, no second targeting path.

One detail that must be asserted rather than assumed: **the snapshot is built in engine list order**, because `A2`'s `SourceOrder` depends on it and `A5`'s byte-identity depends on `SourceOrder`.

### 5. Where the board lives

The board is **battle state**, not actor state — it is created with the encounter, mutated by movement and spawns, and destroyed with it. It is part of what a save persists and what a replay reconstructs, so it joins the state the trace covers.

**Battle grid only.** PvZ mode is a stateless observer with no queue and no per-actor machine; the lawn has its own grid and this module never touches it. The two games share no state.

### 6. This is a golden-mover

Positions plus range-gated targeting change **who gets hit**. Under the freeze-first ordering this belongs in the single combined re-bless with T9, E12, and fog — **never** in `A5`.

It also flips `A7` from golden-neutral to golden-moving, because "nearest" stops being undefined. That transition should be a deliberate step with its own fixture, not a side effect noticed later.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~BattleBoard"
```

## Structure

```
src/FusionRpg.Core/Actions/Board/BattleBoard.cs      (cells, occupancy, mutation)
src/FusionRpg.Core/Actions/Board/BoardGenerator.cs   (seeded dimensions, bounded)
src/FusionRpg.Core/Actions/Board/GridDistance.cs     (Chebyshev — shared with A2)
src/FusionRpg.Core/Actions/Board/Pathing.cs          (destination-free, blocked-line)
tests/FusionRpg.Core.Tests/Actions/Board/
```

## Testing strategy

- **Same seed, same board** — dimensions and starting placements reproduce exactly. Asserted across two independent generator instances, since a shared instance hides state leakage.
- **Chebyshev matches the shipped `Square` shape** — a `Square` of size *n* contains exactly the cells within radius `(n−1)/2`. If this fails, the metric is wrong rather than the test.
- **Two actors never share a cell** — through movement, through spawning, and through a spawn that has nowhere to go. The third case is the one that gets forgotten and is the one the summon verb hits.
- **A blocked line refuses or paths — never passes through.** Written with a single blocker on the only straight route, because a wider board lets a naive implementation appear correct.
- **The snapshot is in engine list order**, asserted directly, because `A5`'s byte-identity rests on it through `SourceOrder`.
- **Bounded dimensions** — the generator never produces a board outside its stated interval, over a large seed sweep rather than a handful.

## Boundaries

- **Always:** seed the dimensions; keep bounds per-encounter; build the snapshot in engine list order; use Chebyshev.
- **Ask first:** changing the metric; unbounded dimensions; letting the board touch lawn geometry.
- **Never:** `CombatPolicy` as the home for battle bounds; an ambient RNG; two actors in one cell; a second targeting path.

## Success criteria

1. `Area` targeting works in battle with **no change to `TargetResolver`**.
2. Boards reproduce from `(setup, seed)`.
3. Occupancy holds through movement, spawning, and the no-free-cell case.
4. `A2`'s range parameters and `A7`'s "nearest" stop being inert, in one deliberate golden-moving step.
