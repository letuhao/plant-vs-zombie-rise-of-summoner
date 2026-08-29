# Spec: movement-actions (A9)

> **Reconciled 2026-08-27.** Checked against the sealed [action-ideal.md](../action-ideal.md) and
> found **substantively intact**; only the notes below change.
> — Unchanged, and its two load-bearing claims are now cited by two other modules: `slot_consuming = false`
> is the precedent `A8` §2.1 reuses for the guard stance, and *"if this module grows a runtime of its own,
> something is wrong"* is the rule `A8` §1 adopts verbatim.
> — An **earned** movement action must be a move **plus a rider** (ideal §5 of the roster), or it is strictly
> worse than the free basic move and would never take a loadout place.


Module **A9** in the [action map](../action-map.md). Depends on **A5**, **A10**.

## Objective

Make moving an action like any other — because by the membership rule it is one:

> Anything an actor does that interacts with the environment or itself, costs resource or time, and needs a cooldown, **is an action. No exception.**

Moving costs time and changes the actor's relationship to the environment. It is not a special case, and the value of this module is largely in *not* making it one.

## Design (locked on approval)

### 1. An ordinary action row

No new machinery. A move is `rpg_action` with a movement container, priced by `time_cost_ticks`, gated by `A4`, resolved by `A2`.

| Field | Value |
|---|---|
| `tags_json` | `movement` — what `A7` looks for when it cannot reach |
| `time_cost_ticks` | Non-zero. This is the whole cost model — see §2 |
| `slot_consuming` | **false** |
| `range_channel` | `move.range` |
| `target_spec_json` | `Mode = Area`, `AnchorSource = ChosenCell` — a destination cell, not an actor |
| costs | Optional. A dash may cost `stamina`; a step need not |

**`slot_consuming = false` is load-bearing.** The concurrency width exists so a limited number of actors may be *mid-action* at once. If movement took a slot, at `W = 1` only one actor on the board could ever move — and a slot-free periodic pulse would be inexpressible for the same reason. The flag exists for exactly this case.

### 2. Move-then-attack falls out of the clock — no compound action, no Action Points

Readiness is `TimeCostTicks / rate`. A cheap step (say 200) and an expensive strike (800) cost differently, so a fast actor fits **both** into the window a slow one needs for one swing, and a slow actor must choose.

> **The time cost *is* the economy.** Movement is a peer of attack, not a phase of it.

This is why the timeline's `ActionPoints` economy is not needed here. It still ships for modes that want a fixed per-turn budget; this mode simply is not one.

### 3. Destination legality is `A10`'s, not a second rule

One actor per cell. A move resolves only if the destination is free, and a blocked line paths around or is refused. `A9` **calls** those rules; it must not restate them, or the two copies drift the first time pathing changes.

The interesting failure is the one to write down: **a move whose destination is taken between commit and resolve.** The action's `commitment` field already answers it — `EarlyBound` fizzles (paying its cost and cooldown, per the standing rule that committing is what costs), `EarlyBoundWithFallback` retargets to the nearest free cell.

### 4. `move.range` — a derived channel, and not a speed

| Concept | Unit | Channel |
|---|---|---|
| How **far** per move | cells | `move.range` |
| How **often** an actor acts | ticks | `turn.speed`, `turn.haste` |

Conflating them is the classic mistake: a "fast" actor in a grid game means *acts more often*, and a "mobile" one means *covers more ground*. They scale different things and a single stat cannot express both. `move.range` registers in [actor-hub-ssot.md](../actor-hub-ssot.md) §3 alongside `resource.*`.

### 5. Battle grid only

PvZ mode is a stateless observer — Unity owns when a zombie walks, and the overlay never schedules that. An earlier draft of the map tied this module to lawn geometry; that was wrong and would have broken the observer boundary on the first movement action.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~MovementAction"
```

## Structure

```
src/FusionRpg.Core/Actions/Movement/MoveAction.cs   (the authored row + resolution)
tests/FusionRpg.Core.Tests/Actions/Movement/
```

Deliberately one file. If this module grows a runtime of its own, movement has stopped being an ordinary action and something is wrong.

## Testing strategy

- **A move is resolved by the ordinary action path** — an architecture test failing if movement acquires its own commit, resolve, or cooldown code. The whole claim of this module is that it needs none.
- **At `W = 1`, an actor moves while another is mid-swing** — the direct proof that `slot_consuming = false` is doing its job. With the flag flipped this test fails, which is what makes it worth having.
- **A fast actor moves and attacks in the window a slow one uses for one attack** — the clock-is-the-economy claim, asserted as an event sequence rather than a wall-clock measurement.
- **A destination taken between commit and resolve fizzles or retargets by `commitment`**, and **pays either way**.
- **`move.range` scales distance and `turn.speed` scales frequency** — two actors differing in only one stat each, proving the two are not the same knob wearing different names.
- **Movement never touches lawn geometry**, as a guard-style source assertion.

## Boundaries

- **Always:** call `A10` for legality; keep `slot_consuming = false`; price movement in `time_cost_ticks`.
- **Ask first:** giving movement its own runtime; a compound move-and-attack action; movement outside battle mode.
- **Never:** a second occupancy or pathing rule; `turn.speed` as a distance stat; lawn geometry.

## Success criteria

1. A move is one authored row and no new runtime.
2. At `W = 1` the board is not frozen into single-file movement.
3. Move-then-attack works with no compound action and no Action Points.
4. Distance and frequency remain visibly separate stats.
