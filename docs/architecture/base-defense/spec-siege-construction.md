# Spec: `siege-construction`

**Module 12 of 29 · level 5 · depends on `siege-seam`, `structure-state` · [base-defense-map.md](../base-defense-map.md)**
**Status:** spec, 2026-09-04.

---

## Objective

**Four ways to get a structure onto the board, only one of which costs empire resources.**

This is owner decision 27, and it answers the audit's sharpest economic finding: at any plausible
material cost, a besieging legion could afford roughly **one** structure. A siege where the attacker
builds one palisade and stops is not a siege.

The owner's answer, verbatim:

> *"not every buiding cost resource, we can immediately deploy building by assembly them from
> consumables item... some can be summon by specific demon action... it cost actor resources like qi,
> other building cost no resource but actor stamina, hunger and that is kind of action that i mention
> like digging moat"*

Three of the four paths cost **no empire resource at all**. That is what makes the board dense enough
to fight over.

**Success looks like:** four acquisition paths, three of which reuse mechanisms that already ship, and
a besieging force that can meaningfully fortify.

---

## What already exists (verified at HEAD, 2026-09-04)

**Built.**

- `StructureCatalog` + `StructureDef` — `Cost`, `BuildTurns`, `RequiredSlotKind`, validated at
  load. Plus `MaxHp` / `BlocksMovement` from `structure-state`.
- `WorldSlot.ConstructionTurnsRemaining` — *"a positive count means a structure was just built and is
  not yet active"*. **Declared, and this module is among its first real users.**
- `LoamPolicy.WellCost` etc. — the costed-build path, already working for loam structures (renamed off `*CostMilli` world-map W57 — every one is a whole loam unit, never a per-mille).
- **Five actor resources**: `hp`, `stamina`, `hunger`, `spirit`, `qi` (resource hub). Paths 3 and 4
  spend these, and they are already modelled, already persisted, already regenerating.
- The action system — `ActionCatalog`, `CompiledAction`, `ActionValidator`, container binding. Paths
  2, 3 and 4 are **ordinary actions**.

**Real gaps.**

- No **building materials** as an empire resource. Owner decision 9: *"empire resouces is missing
  buiding resource, there are no stone, metal and some kind, cannot use soul to summon a wall, that
  confuse with wallnut demon family."* The owner named this resource **`ironwork`**.
- `shard-vein` (GuardHeavy ×4) and `material-seam` (GuardMedium ×3) **ship in maps and yield nothing**
  — heavily guarded slots that are currently pure decoration. They are `ironwork`'s natural faucet,
  already placed, already balanced by their guards.
- No mid-battle placement of any kind.

---

## The contract

### 1. Four paths, one enum

```csharp
/// <summary>
/// How a structure is acquired (base-defense-ideal.md decision 27). Three of the four cost no empire
/// resource — which is the point: at any plausible material cost a besieging legion could afford
/// about one structure, and a siege with one palisade in it is not a siege.
/// </summary>
public enum AcquisitionPath
{
    /// <summary>Materials + build turns. The existing loam-structure path, extended to ironwork.
    /// The only path that touches an empire stockpile. Accumulates: partial progress persists.</summary>
    Built,

    /// <summary>A consumable item is unpacked into a finished structure. Immediate — you carried it
    /// here already. The cost was paid when the item was crafted.</summary>
    Assembled,

    /// <summary>A demon action summons it, paid in `qi`. Actor resource, not empire.</summary>
    Summoned,

    /// <summary>Dug, piled, felled. Costs `stamina` and `hunger` and nothing else — the moat the
    /// owner named. No stockpile, no item, no qi: just work.</summary>
    Laboured
}
```

### 2. `Built` — the only path with an empire cost, and there are TWO stocks

⛔ **The first draft specced only `ironwork`, and decisions 16/17/18/28 require two.** Corrected by the
completeness audit.

| Stock | Decision | What it is | Source |
|---|---|---|---|
| **`rubble`** | 16, 17, **34** | The **bulk** material. Trench, rampart, wire | Mined from `material-seam` slots |
| **`ironwork`** | 16, 17, **28** | The **worked** material. Mine, emplacement, rampart facing | **Refined from `rubble`**, lossy and gated. Also mined from `shard-vein` |

Decision 17 refused `stone` and `metal` — both *"collide with shipped content"*. Decision 34 settles
the bulk name as **`rubble`**.

Decision 18 binds both: **construction-only and world-scoped.** *"They never feed fusion, and they die
with the map — which is what keeps loam's scope discipline intact."* Enforce it by a guard, not a
convention: neither stock may appear in any fusion, crafting or account-scoped path.

#### The refine chain — decision 28

> *"The fifth stock stands on the refine chain: **`ironwork` is made from bulk material** at a lossy,
> gated rate. The exclusivity framing (§5.14) and the ratio framing (§5.15) are both retired — exactly
> one could stand, and this is it."*

```csharp
/// <summary>
/// Refines rubble into ironwork. LOSSY and GATED — economy principle P5's convertibility rule: a
/// conversion that is free and unlimited makes the two stocks one stock with two names.
///
/// <para><b>Gated by a structure, not by a cooldown.</b> Refining needs a working building on a slot,
/// so the rate is something the player BUILDS toward rather than waits out — and it gives the
/// besieger a reason to burn the enemy's foundry rather than only their granary.</para>
///
/// <para>long throughout, checked, divide by 1000 last and exactly once.</para>
/// </summary>
public static long Refine(long rubbleSpent, int yieldMilli) =>
    checked(rubbleSpent * yieldMilli / 1000);
```

**A new `StructureKind.Refinery`** joins `LoamSource` and `Storage` — the third thing a structure can
do, following the precedent `Storage`'s own comment set (*"a real third thing a structure can do"*).

#### The `ironwork` stock itself

It follows `LoamStock`'s shape exactly rather than inventing a second stockpile mechanism.

```csharp
/// <summary>
/// Worked stone and metal — the building material (owner decision 9). Distinct from loam, which is
/// life-force, and from souls, which are demon currency: "cannot use soul to summon a wall, that
/// confuse with wallnut demon family."
///
/// <para><b>long</b>, matching LoamStock exactly, and for the reason WorldSector.LoamStock's own
/// comment records: "the int version silently overflowed into negative upkeep at legal inputs."</para>
/// </summary>
public long IronworkStock { get; init; }

/// <summary>
/// The bulk material (decisions 16/17/34). Trenches, ramparts and wire are built from it directly;
/// ironwork is refined from it. Same shape, same type, same conditional canonical row — two stocks,
/// one mechanism.
/// </summary>
public long RubbleStock { get; init; }
```

**The faucets are already on the map.** `shard-vein` (GuardHeavy ×4) and `material-seam`
(GuardMedium ×3) are guarded slots that currently yield nothing. Making them yield `ironwork` and
`rubble` respectively gives the pair:

- a **faucet that names its own sink** (economy principle P1) — you clear a guarded vein *in order to*
  build,
- a **bottleneck pair** with loam (P4) — loam is life, ironwork is walls, and they are won differently,
- and **zero new map content**, because the slots ship already.

> **Verify the slot type ids before implementing.** `shard-vein` and `material-seam` were found during
> the ideal's survey. Re-read `SlotTypeCatalog` and confirm both exist and still yield nothing. If one
> has since acquired a yield, follow the code.

**This adds a hashed field to `WorldSector`**, so it uses the **same conditional-row discipline**
`structure-state` establishes:

```csharp
if (s.RubbleStock != 0)
    Row(sb, "sector-rubble", s.SectorId, s.RubbleStock);
if (s.IronworkStock != 0)
    Row(sb, "sector-ironwork", s.SectorId, s.IronworkStock);
```

Zero on every existing world → zero bytes → zero golden movement.

### 3. `Assembled` — a consumable becomes a building

An item with a `structure.assemble` atom. Consumed, structure appears **finished**
(`ConstructionTurnsRemaining = null`), on an adjacent legal cell.

**No new economy.** The cost was paid at crafting. This is exactly the *"producing (craft series
equippement)"* building role from the owner's round-5 message closing its own loop: your workshops
make deployable fortifications, and you carry them to the siege.

### 4. `Summoned` — a demon action paid in `qi`

An ordinary action with a `qi` cost and a `structure.summon` atom. **Nothing new is needed**: the
action system already validates costs, the resource hub already holds `qi`, and `structure-seed` will
author which demons can do it.

This is the seedsmith Law-1 shape working as intended — the container-roll path already exists, so
this is a **wiring** question, not a build.

### 5. `Laboured` — stamina and hunger, no stockpile

The moat. An action costing `stamina` and `hunger` that converts a cell's terrain — `Open` → `Gap`
for a moat, `Open` → `Rough` for piled earth.

**Terrain change, not a structure.** A dug moat has no HP, cannot be repaired, and is not in
`StructureCatalog`. It is a cell whose terrain changed, which `siege-cover` already reads and
`siege-pathing` already routes around.

That is why it costs nothing but effort: there is nothing to build, only ground to move.

**It persists.** A moat dug during a siege is still there next turn, so the terrain override is stored
per slot alongside `structure-state`'s fields, under the same conditional row.

### 6. Placement rules — shared by all four paths

One validator, four callers:

1. The cell is on the board and is not `Blocking`.
1b. ⛔ **The cell is not in the `Core` zone.** Decision 10: *"Nothing is built inside the central area
    — it is a pure arena."* Both sides, both phases; an attacker who breaches cannot wall the Core shut
    behind them either. `siege-objective` owns the rule; this validator enforces it.
2. The cell is unoccupied.
3. The acting unit is **adjacent** to it (Chebyshev distance 1) — you build next to yourself.
4. The `RequiredSlotKind` matches, when the structure declares one.
5. **No ownership check.** Owner decision 4: buildings have no ownership, either side may build
   anywhere legal. Blocking the enemy's approach with your own wall, and their blocking yours, is the
   mechanic.

**Deployment costs a unit action** (decision 5) on every path, including `Assembled`. Unpacking is
still a turn you did not spend attacking.

### 7. ⛔ A new order kind passes FIVE plumbing sites — §7 cost 3

`WorldCommandKinds.Assault` is **not one line.** §7 cost 3, and the store's own comment records what
happens when a site is missed:

> *"Adding one to `WorldCommand` and forgetting it here loses it in the round trip … which is exactly
> how `stance` was found missing."*

| # | Site | Note |
|---|---|---|
| 1 | `WorldCommandKinds` | the constant |
| 2 | The `WorldCommand` field | the payload |
| 3 | `RpgStore.CommandPayload` | persistence |
| 4 | `WorldCommandRequest` | the API request type |
| 5 | The `WorldEndpoints` submit mapping | the wire |

**`bind-warden` currently fails sites 4 and 5** — a shipped precedent for the exact failure, so this is
a live gap rather than a hypothetical. Plus an admission arm and a resolver.

**A round-trip test is the acceptance**, not a checklist: submit the command through the API, commit
the turn, read it back, assert it survived. That is the one test the five-site list cannot be silently
half-done under.

### 8. A builder killed mid-build loses everything — no refund

§5.19 surveyed three shipped answers: the Days of Ruin Rig **restarts from scratch**; Total War
Warhammer III **destroys in-progress construction** when the funding capture point is lost; SC2/WC3
refund **75%** — but only on a *voluntary* cancel.

**Total loss needs no new mechanism:**

> *"`ActionRunner.Interrupt` already cancels every outstanding hit, and `InterruptRefundMilli` is
> per-envelope — **set it to zero.**"*

So a build envelope authors `InterruptRefundMilli = 0`. **Not a new rule — an authored value on a
shipped field**, which is why it is a line in this spec and not a module.

The distinction that matters: an **involuntary** interrupt (the builder dies, is displaced, is stunned)
refunds nothing. Whether a **voluntary** cancel refunds is a separate authored value, and the shipped
games only ever refund the voluntary case.

### 9. `BuildResolver.cs` is the reference implementation — read it first

§5.19: *"`BuildResolver.cs` is a complete, tested vertical slice — **ten refusal gates**, debit at
`:115`, site written at `:124`, resolving in `Snapshot` so it can see a claim that landed the same
turn. And **`build` passes all five plumbing sites**, unlike `bind-warden` — a new order kind inherits
a working reference implementation rather than a hunt."*

**Read it before writing `WorldCommandKinds.Assault`.** §7 cost 3's five sites are a checklist; this
file is a worked example of all five done correctly.

Two of its properties change under decision 14 and must be changed deliberately, not inherited:

1. *"Build costs no action and no movement today"* — `BuildResolver` never touches
   `MovementRemaining`. Decision 14 makes build cost an action.
2. *"There is no per-entity order cap anywhere … **One legion may file 200 builds in a turn**"*
   (`MaxCommandsPerSubmit = 200`). Pricing build in a unit's action closes this for the board; the
   **world-scope** hole stays open and is named here so it is not mistaken for closed.

### 10. Pre-battle and in-battle both

Decision 5: *"pre battle and in battle, deployment cost unit action and requirement resources."*
**One code path, two entry points.** Pre-battle deployment is round 0 with a larger action budget —
not a separate system with its own rules, which would immediately drift.

---

## Tunables

`data/tuning/siege.v1.json` (`construction.*`) and `data/tuning/world.v{n}.json` (`ironwork.*`).

| Key | Unit | Default | Why |
|---|---|---|---|
| `ironwork.shardVeinYield` | units/turn | `4` | Balance — the ironwork faucet |
| `rubble.materialSeamYield` | units/turn | `3` | Balance — the rubble faucet |
| `refine.rubblePerIronwork` | rubble | `4` | Balance — the refine chain's input side (decision 28) |
| `refine.yieldMilli` | per-mille | `600` | Balance — **lossy** by decision 28. Bounded ratio, exempt |
| `refine.perTurnCap` | ironwork/turn | **unset** | Balance — the **gate**. Decision 29 |
| `construction.labour.moatStaminaCost` | stamina | `30` | Balance |
| `construction.labour.moatHungerCost` | hunger | `15` | Balance |
| `construction.labour.moatTurns` | turns | `2` | Balance |
| `construction.summonQiCostMilli` | per-mille of max qi | `250` | Balance |
| `construction.actionCostPerDeploy` | actions | `1` | Balance |
| `construction.preBattleActionBudget` | actions | **unset** | Decision 29 |

**Yields are `4` and `3` because that is what the guards already say** — `shard-vein` carries
GuardHeavy ×4 and `material-seam` GuardMedium ×3. Starting the balance at the numbers the map already
encodes means the first playtest tests the map's own intent rather than an invented one.

## Numeric types

| Value | Type | Why |
|---|---|---|
| `IronworkStock` | **`long`** | matches `LoamStock`, whose comment records the `int` overflow that happened |
| build costs | **`long`** | magnitudes `contentScale` reaches |
| stamina/hunger costs | `int` | bounded actor resources |
| `summonQiCostMilli` | `int` per-mille | bounded ratio — exempt, comment says so |
| accumulated progress | **`long`** | an accumulator |

Cost arithmetic: **widen before multiplying, divide by 1000 last, `checked`.** Same as
`structure-state.RepairCost`, and audited by the same tool.

## Boundaries

**Always:** one placement validator for all four paths · `long` for stockpiles and costs · conditional
canonical rows · deployment costs an action on every path.

**Ask first:** a fifth acquisition path · adding a second empire resource beyond `ironwork` ·
`preBattleActionBudget`.

**Never:** let a soul buy a wall (decision 9, explicitly) · an ownership check on placement
(decision 4) · a separate pre-battle system · a bare cost literal in a Policy file.

---

## Testing

| Test | Asserts |
|---|---|
| `World_goldens_byte_identical_at_zero_ironwork` | **the gate** |
| `Each_of_the_four_paths_places_a_structure` | four tests, one per path |
| `Assembled_is_immediate` | `ConstructionTurnsRemaining` null |
| `Built_accumulates_across_turns` | partial progress persists |
| `Summoned_spends_qi_and_nothing_else` | no stockpile touched |
| `Laboured_spends_stamina_and_hunger_and_nothing_else` | the owner's moat, exactly |
| `Moat_changes_terrain_and_is_not_a_structure` | absent from `StructureCatalog`, has no HP |
| `Moat_persists_across_turns` | |
| `Either_side_may_build_anywhere_legal` | decision 4, both directions |
| `Placement_requires_adjacency` | distance 2 is rejected |
| `Occupied_and_blocking_cells_are_rejected` | |
| `Every_path_costs_a_unit_action` | including `Assembled` |
| `Shard_vein_yields_ironwork_and_material_seam_yields_rubble` | **plus a companion test asserting they yielded nothing before** — otherwise this is not the gap it claims to be |
| `Refining_is_lossy` | decision 28 — 4 rubble does not become 4 ironwork |
| `Refining_is_gated_by_a_refinery_structure` | not a cooldown |
| `Neither_stock_reaches_fusion_or_crafting` | **decision 18**, by guard rather than convention |
| `Both_stocks_die_with_the_map` | world-scoped, never account-scoped |
| `Nothing_can_be_built_in_the_core` | decision 10, both sides, both phases |
| `Assault_command_survives_the_api_round_trip` | **§7 cost 3's five sites**, as one test rather than a checklist |
| `Ironwork_round_trips_as_long_through_sqlite` | |
| `Build_cost_overflows_loudly` | `OverflowException`, not a wrapped negative |
| `Interrupted_build_refunds_nothing` | §5.19 — `InterruptRefundMilli = 0` on an involuntary interrupt |
| `Killing_a_builder_destroys_the_progress` | the same rule from the player's side |
| `A_besieging_legion_can_afford_more_than_one_structure` | **the audit finding, as a test.** Simulate a plausible besieging force and assert it can place at least four structures across the paths |

That last test is unusual and deliberate: the finding that motivated decision 27 was economic, so the
acceptance is economic.

## Success criteria

1. Four paths work; three touch no empire resource.
2. `ironwork` exists with a faucet on already-shipped map content, and world goldens are unmoved at
   zero.
3. The moat is a terrain change, persists, and is not a structure.
4. No ownership check anywhere in placement.
5. A besieging force can fortify meaningfully — asserted, not assumed.
6. `audit-overflow.py` and `audit-magic-numbers.py` both clean for this module's files.

## Open questions

**None.** ✅ **Decision 38 (owner, 2026-09-04): `rubble` and `ironwork` trade FREELY between sectors.**

Decision 19's logistics framing wins: *"a city, you can consider it as a planet, so it full economy and
can run along, trading between sectors like city trading or stellar trading."*

> ### ⚠️ What this deliberately gives up, stated once so it is not rediscovered
>
> The recommendation was *supply-lines-only*, because free trade means **material denial stops being a
> siege lever** — a besieged city ships walls in. Round 5 had protected that (*"the siege block
> resource cannot work"*). **The owner chose free trade with that cost stated, and it is accepted.**
>
> **The blockade still has teeth, in two other places**, and this is why the choice is coherent rather
> than a loss:
>
> | Lever | Still bites? | Why |
> |---|---|---|
> | **Loam** — life-force, upkeep, garrison top-up | **Yes** | `siege-supply` makes a besieged sector its own single-sector supply component. Loam does **not** trade freely; it is drawn per connected component |
> | **Board income** — the nodes on the siege board | **Yes** | `siege-economy`: a besieger who garrisons a node takes its yield during the engagement |
> | **Materials** — `rubble` / `ironwork` | **No** | Decision 38 |
>
> So a siege starves a base of **life and ground**, not of stone. That is a defensible shape — the
> defender can keep rebuilding walls while their garrison goes hungry, which makes the loam clock the
> real one and stops the siege from being decided by a stockpile check.
>
> **If a playtest shows sieges never resolve because walls are infinitely replaceable**, the narrow fix
> is `refine.perTurnCap` and the labour cost of placement — both already tunables here — before
> reopening decision 38.
