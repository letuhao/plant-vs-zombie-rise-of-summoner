# Spec: `siege-supply`

**Module 2 of 29 · level 0 · no dependencies · [base-defense-map.md](../base-defense-map.md)**
**Status:** spec, 2026-09-04.

---

## Objective

**A base with stores is not a legion in the field.** Today, being besieged makes a sector drop out of
its *own owner's* supply — which means the defender starves inside their own walls while the besieger,
standing on open ground, does not.

This is audit finding **F1**. It is not a siege feature; it is a defect in the supply rule that the
siege feature makes unavoidable. It is specced first, at level 0, with no dependencies, because
**every economic claim the rest of the program makes is false until it is fixed** — a defender who
cannot draw on their own stockpile has no economy to besiege.

**Success looks like:** a besieged sector keeps feeding the garrison standing in it, and the
starvation clock the siege actually wants (a blockade cutting the sector off from the *rest* of the
empire) is the one that runs.

---

## What already exists (verified at HEAD, 2026-09-04)

**Built.**

- `SupplyGraph.ConnectedSectors` (`src/FusionRpg.Core/World/Movement/SupplyGraph.cs:19`) — BFS from
  every held Seat, recomputed every turn and deliberately never cached.
- `SupplyGraph.InSupply` — a force on a lane counts if either end does.
- `LegionSupply.Resolve` (`src/FusionRpg.Core/World/Loam/LegionSupply.cs`) — top-up then burn;
  outside supply, `remaining < 0` destroys the entity outright with a `legion.starved:` report.
- `LoamPolicy.CarryPerBearer` / `BurnPerMember` — capacity and burn scale with *different* things, on
  purpose.

**The defect, quoted.** `ConnectedSectors`'s local predicate:

```csharp
bool Usable(string sectorId) =>
    byId.TryGetValue(sectorId, out var sector)
    && string.Equals(sector.OwnerFactionId, factionId, StringComparison.Ordinal)
    && !ZoneOfControl.IsHeldAgainst(world, sector.SectorId, factionId);
```

`ZoneOfControl.IsHeldAgainst` is *"an enemy force is standing here contesting it"* — which is the
definition of being besieged. So a besieged sector is `!Usable`, and:

1. It is **not reachable** — it drops out of `SupplyReach.From`'s traversal, so the garrison inside is
   `!InSupply`, burns, and is **destroyed outright** the turn its carried loam goes negative.
2. **It is not a source either** — and this is the sharper half. The `seats` query filters by
   `Usable(s.SectorId)` before selecting Seats. So **besieging a faction's only Seat deletes that
   faction's entire supply network**, everywhere on the map, in one move. Every legion they own falls
   out of supply simultaneously and starves together.

That second consequence is audit finding **F1b**, the "capital immunity" defect. The ideal named it
without fixing it. This module fixes both, because they are one line apart and fixing only the first
leaves a one-move empire-delete on the board.

**Real gap.** There is no concept of *"cut off from the rest of the empire, but standing on its own
stores."* Supply is binary: reachable, or starving.

---

## The contract

### 1. Split `Usable` into reachability and sourcehood

The two uses of `Usable` want different answers and currently share one.

```csharp
/// <summary>
/// Can supply TRAVEL through this sector? No — a contested sector is a roadblock, and that was
/// always the right answer for the traversal.
/// </summary>
bool Traversable(string sectorId) =>
    Owned(sectorId) && !ZoneOfControl.IsHeldAgainst(world, sectorId, factionId);

/// <summary>
/// Can supply ORIGINATE here? Yes, even under siege (F1b). A capital under attack is still a
/// capital: its granaries did not stop existing because someone is standing outside them. Making
/// contested-ness disqualify a SOURCE meant one legion parked on one sector deleted a faction's
/// whole network in a single move.
/// </summary>
bool Source(string sectorId) => Owned(sectorId);
```

`seats` selects on `Source`; `SupplyReach.From` traverses on `Traversable`. **A besieged Seat is still
a source, and still supplies itself.**

> **Corrected during implementation (base-defense-todo.md task 2.1, 2026-09-05) — do not trust the
> claim above at face value.** `SupplyReach.From` does **not** include its seed nodes regardless of
> the usable predicate; its seeds are gated by the SAME predicate as traversal, so a besieged (hence
> non-`Traversable`) Seat drops out of `connected` exactly like before this fix, not "automatically" as
> this section originally implied. The actual fix explicitly unions every besieged **owned** sector
> into the result *after* the BFS, in `SupplyGraph.ConnectedSectors`, rather than relying on the seed
> behaviour described above. The net effect is the same (a besieged sector is `connected`, feeds from
> its own stock, does not starve) — the mechanism is not what this paragraph says.

That single change is the whole of F1's fix for the sector. The garrison standing in a besieged Seat
is `InSupply` again, tops up from its own `LoamStock`, and does not starve.

### 2. A besieged sector draws only on itself

Correct so far, but incomplete: a besieged sector that is *also* still traversable-adjacent to
friendly territory would draw from the whole component, which makes a blockade meaningless.

`TerritoryComponents.For` already partitions by connectivity. Because a besieged sector is no longer
`Traversable`, it forms **its own single-sector component** — so it draws on its own `LoamStock` and
nothing else, automatically. **No new code.** The component partition does the work, provided
`TerritoryComponents.For` uses the same `Traversable` predicate.

> **Verify this before implementing.** `TerritoryComponents.For` was not re-read during Gate 0. If it
> partitions on a predicate of its own that still folds in `IsHeldAgainst`, the behaviour above is
> already correct and this section is a no-op; if it partitions on plain ownership, a besieged sector
> would pool with its neighbours and the blockade would leak. **Read it, then either cite it as
> already-correct or fix it.** Do not assume from this spec.

### 3. The starvation clock the siege actually wants

With §1 and §2 in place the mechanic inverts into the right shape, with no new state:

| Situation | Before | After |
|---|---|---|
| Garrison inside a besieged Seat | Starves, destroyed | Feeds from the sector's own `LoamStock` |
| That stockpile runs dry | — | *Then* it starves — the blockade clock, on the real number |
| Besieger on open ground outside supply | Starves normally | Unchanged |
| Faction's only Seat besieged | **Whole empire's supply deleted** | Only that sector is isolated |

**The siege's economic pressure is now the sector's own `LoamStock` draining under garrison top-up
with no inbound resupply.** That is a real, legible, already-hashed number — not a new clock, not a
new flag, not a new field.

### 4. §7 cost 6 — slot ownership must follow sector capture

> *"Slot ownership does not follow sector capture. `ClaimResolver` captures the sector and never
> touches `WorldSlot.OwnerFactionId`, so a captured sector's slots keep the previous owner. **If the
> board is the sector zoomed in, this becomes visible and has to be fixed.**"*

Decision 3 makes the board the sector zoomed in, so it is now visible: capture a city and every
building on the board still reads as the enemy's.

**Included here rather than in its own module** because it is one line in `ClaimResolver` and it is the
same class of defect as F1 — a world-layer rule that base defense makes load-bearing.

```csharp
// §7 cost 6: slots follow the sector. Buildings have no ownership ON THE BOARD (decision 12 —
// possession is by occupation), but WorldSlot.OwnerFactionId is the world-layer fact the outcome
// record settles, and a captured sector whose slots still name the loser is simply stale.
Slots = sector.Slots.Select(sl => sl with { OwnerFactionId = captorId }).ToList()
```

**This one DOES move a golden** if any shipped world has a slot whose owner differs from its sector's.
Check before landing; if it does, it batches with `structure-state`'s landing rather than opening a
second `RulesetVersion` conversation.

### 5. A report line the player can act on

`supply.cut:` already fires for a holding off the chain. A besieged sector is a *different* situation
and must read differently, or the player cannot tell "my road was cut" from "I am under siege".

```csharp
report.Add(phase, TurnReportKinds.Event, owner, "supply.besieged:" + sector.SectorId, sector.SectorId,
    audience: owner);
```

Fires when a sector is `Source` but not `Traversable` — precisely the new state this module creates.
Paired with the existing `supply.restored`, that is enough for the FE to show a siege without
inventing anything.

**This is a report line, not hashed state.** `TurnReport` entries do not enter `WorldCanonical`, so
this moves no golden. Confirm that before landing.

---

## Tunables

**None.** This module introduces no number. Every magnitude it touches (`CarryPerBearer`,
`BurnPerMember`, `RecoveryMilli`) already lives in `LoamPolicy` and is unchanged.

That is worth stating explicitly: a defect fix that also introduces a dial is two changes, and the
second one hides the first.

## Numeric types

`LoamStock` is already `long`, and `WorldSector`'s own comment records why — *"the `int` version
silently overflowed into negative upkeep at legal inputs."* Nothing here widens or narrows anything.

## Boundaries

**Always:** keep `ConnectedSectors` recomputed per turn and uncached — its own doc comment explains
that a stored flag *"would then be wrong in the one situation the player cares about"*, and a siege is
that situation · re-run the world goldens.

**Ask first:** any change to `LoamPolicy` magnitudes · giving a besieged sector a *different* burn
rate (tempting, and it is a balance change dressed as a bug fix).

**Never:** add an `IsBesieged` field to `WorldSector` — it is derivable from `ZoneOfControl` every
turn, and a stored copy is the exact staleness the file already refuses · make `Recover` work under
siege (a besieged garrison feeding itself is the fix; a besieged garrison *healing* is a buff nobody
asked for).

---

## Testing

`tests/FusionRpg.Core.Tests/World/` alongside the existing loam and movement suites.

| Test | Asserts |
|---|---|
| `Besieged_seat_still_supplies_its_own_garrison` | the F1 fix, directly — garrison `InSupply`, no `legion.starved:` |
| `Besieging_the_only_seat_does_not_delete_the_network` | **F1b.** Two friendly sectors elsewhere stay connected while the Seat is contested |
| `Besieged_sector_draws_only_on_its_own_stock` | a neighbour's full granary does not feed a besieged sector |
| `Besieged_garrison_starves_when_its_own_stock_runs_dry` | the blockade clock still bites — the fix does not make defenders immortal |
| `Supply_cannot_route_THROUGH_a_besieged_sector` | `Traversable` still excludes it; a sector behind it falls off the chain |
| `Besieger_outside_supply_still_burns` | the attacker gets no free ride from this change |
| `Supply_besieged_report_fires_once_and_distinctly` | not confused with `supply.cut:` |
| `Captured_sector_slots_change_owner` | §7 cost 6 |
| `Slot_owner_change_is_checked_against_every_shipped_world` | whether it moves a golden — **measured, not assumed** |
| `World_goldens_unmoved` | **the gate** for the supply half; the whole fix there is in a predicate |

**Determinism:** no RNG, no clock. `SupplyGraph`'s BFS is already stable-id-ordered; splitting the
predicate does not touch ordering. Assert that explicitly — a set-iteration change here is exactly the
kind of thing that reproduces on one machine and not another.

## Success criteria

1. A besieged Seat's garrison survives indefinitely while the sector has stock, and starves when it
   does not.
2. Besieging a faction's only Seat isolates **one** sector, not the faction.
3. `TerritoryComponents.For` has been **read** and its predicate either cited as already-correct or
   fixed — this box may not be ticked by assumption.
4. Every existing world golden is byte-identical, unblessed.
5. `ConnectedSectors` is still uncached and still recomputed per turn.

## Open questions

**None.** ✅ **Decision 42 (owner, 2026-09-04): a besieged garrison's top-up IS rationed.**

```csharp
/// <summary>
/// Draw rate for a garrison topping up inside a besieged sector, per-mille of normal. Below 1000, a
/// stockpile lasts proportionally longer under siege than in peacetime — which is what a siege
/// historically was.
///
/// <para><b>Bounded ratio</b> (0..1000), exempt from AGENTS.md's no-hard-ceilings rule, stated here as
/// that rule requires.</para>
/// </summary>
public static int BesiegedRationMilli { get; }   // data/tuning/loam.v{n}.json
```

Applied to the `Demand` computed in `LegionSupply.Resolve`'s top-up loop, **before**
`DrawProportionally` — so a rationed garrison **asks for less** rather than being served less, and the
proportional-draw arithmetic is untouched.

> ### ⚠️ The recommendation was *no*, and why it was overruled matters for the test plan
>
> The objection was attribution: *"a balance dial on a defect fix makes it impossible to tell which one
> changed a playtest."* That objection is **answered by a test, not by deferral**:
>
> **`Ration_at_1000_reproduces_the_unrationed_fix_exactly`.** At the neutral default the ration is a
> no-op, so the defect fix can be validated on its own by setting one tuning row. The dial and the fix
> stay separable at any time, which is what the deferral was really buying.
>
> **Default it to `1000` (no rationing) on first landing**, flip it in the first balance pass. The fix
> lands clean; the dial is already there when it is wanted.

### Tests this adds

| Test | Asserts |
|---|---|
| `Ration_at_1000_reproduces_the_unrationed_fix_exactly` | **decision 42's separability guarantee** |
| `Ration_below_1000_makes_a_stockpile_last_longer` | the dial does what it says |
| `Rationing_does_not_change_proportional_draw_arithmetic` | applied to `Demand`, before `DrawProportionally` |
| `Rationing_applies_only_under_siege` | a peacetime garrison is unaffected |
