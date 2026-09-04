# Spec: `siege-economy`

**Module 13 of 21 · level 6 · depends on `siege-construction` · [base-defense-map.md](../base-defense-map.md)**
**Status:** spec, 2026-09-04.

---

## Objective

**Make the board itself worth fighting over, and make holding it pay.**

Owner decision, round 5: *"not good if resource come from world map, the siege block resource cannot
work."* This was a correction to my own earlier claim that there is no harvest loop on the board.
There is — the units doing it are your soldiers rather than dedicated workers, and that is the
difference from a conventional RTS.

Three jobs:

1. **Board income** — nodes on the board yield per turn to whoever garrisons them.
2. **The depot** — a battle-scoped budget seeded from world stock, reconciled spend-only.
3. **Capture transfers the stockpile** (audit **F11**) — taking a granary must take what is in it.

**Success looks like:** taking and holding ground on the board changes what you can afford *during the
siege*, and a besieger who seizes the enemy's stores can fund their assault with them.

---

## What already exists (verified at HEAD, 2026-09-04)

**Built.**

- `WorldSector.LoamStock` (`long`) and `IronworkStock` (`long`, from `siege-construction`).
- `StructureKind.LoamSource` / `Storage`, `YieldMultiplierMilli`, `CapacityBonus`.
- `LoamPhases.DrawProportionally` — proportional draw with *"remainder to the first in ordinal id
  order"*. The exact allocation shape this module needs one level down.
- `LegionSupply.DistributeToLegions` — the same pattern applied to legions, and its own comment says
  *"Same shape as `LoamPhases.DrawProportionally`, one level down."* This module is the third
  application, and it says so rather than inventing a fourth.
- `WorldSlot.OwnerFactionId`, `SlotDepletionMilli` (from `structure-state`).

**Real gaps.** No per-turn board income. No battle-scoped budget. **Capture does not transfer stored
resources** (F11).

---

## The contract

### 1. Board income — garrison, don't own

Decision 4: buildings have no ownership. Decision (round 6): *"garrisoned mean take control like make
product."* So income follows **occupation**, not a title:

```csharp
/// <summary>
/// What a node yields this round, to whoever is standing on it. NOT to whoever owns the sector —
/// buildings have no ownership (decision 4) and possession is by occupation. A besieger who takes
/// your quarry is mining it while you watch, which is the whole reason to contest the outer ground.
/// </summary>
public static IReadOnlyList<BoardYield> YieldsFor(BoardState board, GridSpec spec, int round);
```

Rules:

- A node with **no** occupant yields nothing. Not to the sector owner, not to anyone.
- A node whose occupant is a **structure** yields nothing — a structure does not garrison another
  structure (`combatant-kind`).
- An **exhausted** node yields nothing (`structure-state.IsExhausted`), and reports it once.
- Yields accrue to the occupant's **side**, not to the individual actor.

**Iteration is ordinal-ordered by cell index**, so two nodes yielding into the same depot on the same
round always do so in the same order. Determinism is not incidental here — it is a sum.

### 2. The depot — a battle-scoped budget, reconciled spend-only

A siege lasts many rounds; the world turn is one step. Something must hold resources *during* the
battle.

```csharp
/// <summary>
/// One side's spendable budget inside one siege. Seeded from world stock at battle start, credited by
/// board income, debited by construction.
///
/// <para><b>Reconciled spend-only.</b> At battle end, the world stock is reduced by what was SPENT —
/// it is never overwritten with the depot's final balance. The difference matters: overwriting makes
/// board income silently mint world resources, so a defender could farm their own siege indefinitely
/// by never resolving it. Spending is the only thing that crosses back.</para>
///
/// <para>Income earned on the board is therefore <b>battle-scoped</b>: it funds this siege and
/// evaporates. That is the correct shape — a quarry seized for six rounds should pay for six rounds
/// of walls, not enrich your empire forever.</para>
/// </summary>
public sealed class SiegeDepot
{
    public long Loam { get; }
    public long Ironwork { get; }
    public long LoamSpentFromWorld { get; }      // the only figures that cross back
    public long IronworkSpentFromWorld { get; }
}
```

**Spending draws from board income first, world stock second.** So `…SpentFromWorld` only grows once
locally-earned resources are exhausted, and a well-run siege that lives off the land costs the empire
nothing. That is a real strategic payoff for contesting the outer ground, and it falls out of the
ordering rather than needing a rule.

> **This is why the depot is not simply the sector's stock.** Two sides spend during one battle;
> exactly one of them owns the sector. A shared mutable stock would let the attacker spend the
> defender's loam by accident.

### 3. Capture transfers the stockpile — F11

```csharp
/// <summary>
/// Audit F11: taking a Storage structure takes what is in it. Without this, capturing a granary gets
/// you an empty building and the defender keeps grain they no longer have a granary for — which is
/// both nonsensical and removes the entire reason to target storage.
/// </summary>
public static SiegeDepot TransferOnCapture(SiegeDepot captor, SlotOutcome slot, StructureDef def);
```

**The stored amount is proportional to the structure's surviving HP.** Burning a granary to the ground
destroys the grain; taking it intact takes the lot. That makes "storm it or shell it" a real decision
rather than a formality, and it needs no new state — `SlotOutcome.StructureHp` already carries the
number.

```csharp
// long × long, one divide, last. checked. Same discipline as structure-state.RepairCost.
var recovered = checked(stored * Math.Max(0, hp) / def.MaxHp);
```

Guard `def.MaxHp <= 0` before dividing — an indestructible structure would otherwise divide by zero,
and `structure-state` makes `MaxHp = 0` a legal, shipped value on all four existing rows.

### 4. Depletion advances on harvest, not on time

`SlotDepletionMilli` grows by `structure.depletionPerHarvestMilli` **each time a node actually yields**
— not per turn. A node nobody garrisons does not deplete.

This is what makes the owner's decision (*"stop mining and product, because the resource can
exhausted"*) a genuine tension rather than a countdown: **the more you harvest, the sooner it dies.**
Contested nodes are consumed faster because both sides work them in turn.

### 5. Economy principles this module is checked against

From the empire-economy SSOT, and each stated because a downstream session reads this doc, not its
links:

| Principle | How this module satisfies it |
|---|---|
| **P1 — a faucet names its sink** | Board income funds board construction. It cannot leave the battle. |
| **P4 — bottleneck pairs** | Loam (life, from the map) and ironwork (walls, from guarded veins) are won differently and neither substitutes. |
| **P6 — two competing sinks** | Depot spend goes to *fortify* or to *repair*. Both are always affordable, never both at once. |
| **P2 — growth-rate match** | Board income is flat per node per round; it does not scale with `Θ`, so it cannot outrun the sinks it feeds. **Deliberately not on `P(Θ)`** — a node's yield is a property of the ground. |

---

## Tunables

`data/tuning/siege.v1.json`, `economy.*`.

| Key | Unit | Default | Why |
|---|---|---|---|
| `economy.nodeYieldPerRound.loam` | units | `5` | Balance |
| `economy.nodeYieldPerRound.ironwork` | units | `3` | Balance |
| `economy.depotSeedMilli` | per-mille of sector stock | `1000` | Balance — how much of the stockpile is actually reachable during a siege. Bounded ratio, exempt |
| `economy.captureRecoveryMilli` | per-mille | `1000` | Balance — a scaling factor on top of the HP proportion |

## Numeric types

**Every stock, yield, spend and transfer is `long`.** These are magnitudes `contentScale` reaches, and
they accumulate over an unbounded number of rounds — `CLAUDE.md` rule 1 applies twice over.

Per-mille factors are `int`; **the divide by 1000 happens once, last**, after every multiply, `checked`
throughout.

## Boundaries

**Always:** `long` for every resource quantity · reconcile spend-only · ordinal cell order for income ·
`checked` arithmetic · guard `MaxHp <= 0` before dividing.

**Ask first:** letting board income persist past the battle (it changes the whole economy's shape) ·
scaling node yield with `Θ`.

**Never:** overwrite world stock with a depot balance · yield to a non-garrisoned node · put a yield on
`P(Θ)` · divide before multiplying · a bare literal in the economy policy file.

---

## Testing

| Test | Asserts |
|---|---|
| `Ungarrisoned_nodes_yield_nothing` | |
| `A_structure_occupying_a_node_yields_nothing` | `combatant-kind`'s rule holds here too |
| `Income_accrues_to_the_occupant_not_the_owner` | decision 4, economically |
| `Board_income_never_reaches_world_stock` | **the mint bug, prevented.** Earn heavily, spend nothing, assert world stock unchanged |
| `Only_spend_crosses_back` | spend more than earned; assert world stock down by exactly the difference |
| `Board_income_is_spent_before_world_stock` | the live-off-the-land payoff |
| `Capture_transfers_proportionally_to_surviving_hp` | **F11** |
| `Destroying_storage_destroys_the_stores` | HP 0 → nothing recovered |
| `Capture_from_an_indestructible_structure_does_not_divide_by_zero` | `MaxHp == 0`, which all four shipped rows have |
| `Depletion_advances_on_harvest_not_on_time` | an ungarrisoned node never depletes |
| `Contested_nodes_deplete_faster` | both sides working it |
| `Exhausted_nodes_stop_yielding_and_report_once` | |
| `Income_order_is_deterministic_over_10000_runs` | it is a sum |
| `Transfer_overflows_loudly` | `OverflowException` |
| `World_goldens_byte_identical_with_no_siege` | **the gate** |

## Success criteria

1. Board income exists, follows occupation, and **cannot** mint world resources.
2. Only spend reconciles back to the world.
3. Capture transfers proportionally, with the zero-HP and zero-MaxHp cases both handled.
4. Depletion is harvest-driven.
5. Every quantity is `long`, every product `checked`, every divide last.
6. `audit-overflow.py` and `audit-magic-numbers.py` clean.

## Open questions

None. `siege-construction` carries the one live economic question (ironwork tradeability) and
recommends the answer this module builds against — a besieged sector is its own supply component and
trades with nobody, so no rule is needed here.
