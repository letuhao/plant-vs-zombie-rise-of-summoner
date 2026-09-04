# Spec: `siege-obstacles`

**Module 19 of 29 · level 4 · depends on `structure-state` · [base-defense-map.md](../base-defense-map.md)**
**Status:** spec, 2026-09-04. Added by the completeness audit (§5.18 had no module, and `Mine` had no
home). **Moved to level 4 by pass 3**, which found a dependency cycle.

> ### ⛔ Pass 3 — this module is the STRUCTURE VOCABULARY, and that broke a cycle
>
> It previously declared `siege-cover` as a dependency while `siege-cover` declared **this module** as
> one — a cycle, which the map's own rule forbids: *"If two modules each need the other, they are one
> module."*
>
> **The dependency was never real.** A Trench's cover value is a **data field**, not a call into the
> cover module. So this module owns the **vocabulary** — `ObstacleKind`, `AcquisitionPath`, the
> cover-radius fields, and the cell-entry transition — and `siege-cover` (11) and `siege-construction`
> (12) both **consume** it.
>
> Moving to level 4 costs nothing downstream: every consumer was already at level 5 or later.

---

## Objective

**Build the four obstacle kinds and one building that make trench warfare a set of decisions.**

§5.18 collapsed **seventeen named historical works** through **eight verbs** into **four kinds plus one
building**, and its authoring rule is the one that matters:

> *"Each row below exists only because cutting it removes a decision no other row can produce."*

That is the acceptance test for this module, restated: **five rows, five distinct decisions.** A sixth
row that produces a decision an existing row already produces is the second vocabulary `§2 rule 10`
forbids.

---

## The vocabulary — §5.18's table, and the decision each row creates

### The vocabulary this module owns

Three declarations, consumed by two other modules and defined once here:

| Type | Consumed by | Why here |
|---|---|---|
| `ObstacleKind` | `siege-cover`, `siege-construction` | the five rows below |
| `AcquisitionPath` | `siege-construction` (implements the four paths) | decision 27's vocabulary is structure identity, not construction machinery |
| `CoverRadius` / `CoverPowerMilli` on `StructureDef` | `siege-cover` (reads them) | **data, not a call** — this is the field that used to look like a dependency |
| `ScopeMembershipTransition.CellEntered/Exited` | this module's Mine | see above |

| # | Kind | Verbs | Material | Mechanically | **The decision it creates** |
|---|---|---|---|---|---|
| 1 | **Trench** | COVER | rubble | Occupiable **and passable**. Flat `combat.dodge.omni` delta on the occupant. Two tiers by *value* (sandbag / revetted) | *Where is it worth standing still?* |
| 2 | **Rampart** | BLOCK + BLOCK-LOF | rubble + a little ironwork | Not occupiable. Blocks movement **and fire**. **Destructible — razing it is a legitimate attacker action** | *Which routes exist at all?* |
| 3 | **Wire** | SLOW | rubble, cheapest | Neither blocks nor covers. **Multiplies the STAMINA cost of entering the cell** | *Is the short route worth the stamina?* |
| 4 | **Mine** | BITE + DENY | ironwork | **Damage on entry**, single-use. **Ignores cover** | *Open ground or covered ground?* — **the only obstacle that punishes the safe-looking cell** |
| — | **Emplacement** | COVER + a weapon | ironwork | **A building, not an obstacle.** Garrisoned (decision 15), acts through its occupant, who gets high cover plus a ranged action | *Is a body better spent shooting or standing?* |

**`CHANNEL` is deliberately not a kind.** §5.18: *"channelling is an emergent consequence of placing
BLOCK and SLOW next to a weapon… If BLOCK, SLOW and pathing exist, channelling is what the **player**
does with them."* Do not add a channel property; it would be a mechanic competing with the player's
own use of two that already exist.

---

## ⛔ The layer split the audit found wrong in `siege-board`

`siege-board`'s four `CellTerrain` values (`Open`/`Rough`/`Blocking`/`Gap`) are **the board's fixed
ground**. §5.18's five rows are **placed objects**. The original specs blurred them in two places, and
both are corrected here:

| Was | Correct |
|---|---|
| `siege-construction`: a laboured moat is *"a terrain change, not a structure"* | §5.18 merges *"Moat / anti-tank ditch"* into **Rampart** — *"A cell you cannot enter and cannot stand on **is** a wall. Identical verbs."* A moat is a **Rampart built by the laboured path**, and it is **destructible**, which a terrain change is not |
| `siege-board`: `Rough` multiplies **movement cost** | **Wire multiplies STAMINA.** A different resource, a different decision. §5.18's own column says so, and `Actions/Cost/ActorResourcePools.cs:5-12` already makes movement cost stamina — the hook exists |

**The rule:** terrain is what `district-layout` generates and nobody built; obstacles are structures
with HP that either side placed. `Rough` stays as generated broken ground; `Wire` is a structure that
taxes stamina.

---

## What already exists (verified at HEAD, 2026-09-04)

§5.18's own verification table, re-confirmed:

| Needed | Status | Evidence |
|---|---|---|
| **Cover as a contest modifier on the occupant** | **Built** | `BattleStatComposer.cs:116-117` writes `CombatAccuracyOmni`/`CombatDodgeOmni`; `OverlayCombatCalculator.cs:162-164` resolves `accuracy − dodge` through a sigmoid |
| **Block line of fire** | ⭐ **Pure wiring gap** | `RequiresLineOfSight` is declared (`ActionRow.cs:49`), compiled (`ActionCompiler.cs:65`), carried (`CompiledAction.cs:37`), **persisted twice** (`RpgStore.Actions.cs:256`, `:373`) and hardcoded `false` in the battle fallback (`BattleRunState.cs:61`) — **and read by no evaluator anywhere in `src/`** |
| **Movement costs stamina** | **Built** | `Actions/Cost/ActorResourcePools.cs:5-12` |

**Real gap.** Damage-on-entry (`Mine`) exists nowhere — no trigger fires when an actor enters a cell.

---

## The contract

### 1. `ObstacleKind`, and it is a `StructureDef` facet

Obstacles are **structures** — they have HP (`structure-state`), occupy cells, and are destructible.
They are not a parallel system.

```csharp
/// <summary>
/// §5.18's closed vocabulary. FIVE rows, five distinct decisions — the authoring rule is that a row
/// exists only because cutting it removes a decision no other row can produce. A sixth kind must
/// name its own decision or it is the second vocabulary §2 rule 10 forbids.
/// </summary>
public enum ObstacleKind
{
    /// <summary>Not an obstacle — an ordinary building (well, granary, emplacement…).</summary>
    None,
    Trench,
    Rampart,
    Wire,
    Mine,
    Emplacement
}
```

`StructureDef` gains `ObstacleKind Obstacle { get; init; }`, defaulting to `None` — so the four shipped
loam rows are unaffected and no golden moves.

### 2. Trench — occupiable and passable

Cover value flows through `siege-cover`'s grant. **Two tiers by value, not by mechanism** (§5.17's
recommendation): sandbag and revetted are two `StructureDef` rows with different flat dodge deltas,
not two code paths.

`BlocksMovement = false`. A trench you can walk along **is** a trench — §5.18 folds *"communication
trench · sap"* into it for exactly that reason, at zero added mechanics.

### 3. Rampart — blocks fire, and that is the wiring gap closed

`BlocksMovement = true`, plus the new field decision 25 requires:

```csharp
/// <summary>
/// Blocks line of fire through this cell. Decision 25: an unoccupied building "occupies its cell,
/// blocks movement AND FIRE, and has HP. It simply does not act."
///
/// <para>This is what finally gives `RequiresLineOfSight` a reader — declared, compiled, carried and
/// persisted twice since the action program, and read by no evaluator anywhere. A pure wiring gap,
/// closed here because Rampart is the first thing in the game that has a reason to block a shot.</para>
/// </summary>
public bool BlocksLineOfFire { get; init; }
```

**Destructible, and razing is a first-class attacker action.** A Rampart with a repair cost
(`structure-state.RepairCost`) and no special-case destruction path — it dies like any structure and
leaves `SlotState.Ruined`.

### 4. Wire — stamina, not movement

```csharp
/// <summary>
/// Per-mille multiplier on the STAMINA cost of entering this cell. 1000 = unchanged.
///
/// <para><b>Stamina, not movement cost</b> (§5.18). Doubling a movement cost makes the cell a longer
/// walk, which the pathfinder simply routes around and the player never thinks about. Taxing stamina
/// makes the SHORT route expensive — so the decision is "is the short route worth the stamina", which
/// is a decision no other row produces. Movement already costs stamina
/// (`Actions/Cost/ActorResourcePools.cs:5-12`), so this is a multiplier on a live path.</para>
///
/// <para>Bounded ratio conceptually but NOT capped above — a 5000‰ wire is legal. AGENTS.md's
/// no-hard-ceilings rule applies: this is a magnitude a balance pass raises, not a bounded fraction.</para>
/// </summary>
public int EntryStaminaMultiplierMilli { get; init; } = 1000;
```

`siege-pathing` reads it **only through `MoveCosts`' stamina channel**, never as a movement cost — or
Wire silently becomes a second Rough.

### 5. Mine — the one genuinely new mechanic

Damage on entry. Nothing in the engine fires on cell entry today, so this needs a trigger — and it
reuses the transition `siege-cover` already introduces rather than adding a second:

> ### ⛔ Pass 3 — this module OWNS the trigger. It used to borrow one that no longer exists.
>
> This section read *"fired on `ScopeMembershipTransition.CellEntered`, **which siege-cover already
> emits**."* The decision-35 rewrite made that false: `siege-cover` §8 now states it introduces **no**
> membership change, because cover is evaluated per shot. **Cover released the program's one allowed
> vocabulary change and this module never claimed it — so the Mine fired on nothing.**

```csharp
/// <summary>
/// The program's ONE reviewed vocabulary change, spent here — on a real mechanic, which is the bar.
///
/// <para>siege-cover originally introduced this and released it when decision 35 replaced terrain
/// cover with per-shot shooting math. A mine genuinely needs a cell-entry event: nothing else in the
/// engine fires when an actor enters a cell.</para>
/// </summary>
public enum ScopeMembershipTransition
{
    Bound,
    Cleared,
    MindControlToggled,
    /// <summary>An actor entered a board cell. A mine on that cell triggers.</summary>
    CellEntered,
    /// <summary>Left a board cell. Paired with CellEntered — emitted on move, death and withdrawal,
    /// so an entry can never be left dangling.</summary>
    CellExited
}
```

**`BattlefieldOwnSideReactor.cs:75-86` switches on the existing three values.** Adding two is exactly
where an unhandled-case throw or a silent default appears — assert the new values fall through
harmlessly.

Four properties, each from §5.18's row:

1. **Damage on entry** — through the ordinary `DamagePacket` → `CombatDamageDispatcher` path, so
   shields, elements and the Funnel all apply. **Never a direct HP write.**
2. **Single-use** — consumed on trigger; the slot becomes `SlotState.Ruined`.
3. **Ignores cover** — an explicit row in `siege-cover`'s `(damage source × cover type)` matrix, which
   is *why* that matrix is a data shape rather than a scalar. **`DamageSourceKind.Entry`** is the
   fourth source kind, and every cover row against it is `0`.
4. **⛔ REVEALED, not hidden** — audit **F9**. §5.18 says *"unrevealed to the other side"*; F9 found
   that contradicts §5.16 R6 (no hidden modifiers) and §5.20's Into the Breach foundation, which has
   **zero** hidden information. **F9's recommendation is taken: mines are visible to both sides.**

> **The telegraph model is not a weaker mine.** Into the Breach's whole thesis is Justin Ma's *"we
> wanted to make something where **every death felt like your own fault**"* — a visible mine is a
> denied cell the attacker must pay to cross or route around, which is `DENY` working exactly as the
> verb intends. A hidden mine is a coin flip.

### 6. Emplacement — a building, not an obstacle

`ObstacleKind.Emplacement` with `BlocksMovement = false`, a high cover value, and **a ranged action in
its action list**. Garrisoning it lends that action to the occupant — `combatant-kind` §4 already
specs the mechanism and this is its first real content.

**Its decision is the interesting one:** *"is a body better spent shooting or standing?"* — and it is
real only because the field cap (`siege-objective`) makes bodies scarce. Without decision 5, an
emplacement is free value.

### 7. Each kind declares which paths can produce it

§5.24: *"**§5.18's four obstacle kinds** — Unchanged as a vocabulary — but **each kind now declares
which paths can produce it.** A moat is path 4; a pillbox is path 1 or 2."*

```csharp
/// <summary>
/// Which of decision 27's four acquisition paths can produce this structure. A subset of
/// {built, assembled, summoned, laboured}; **`none` is illegal** — a structure no path can produce is
/// a catalog row that can never appear on a board.
/// </summary>
public IReadOnlyList<AcquisitionPath> AcquisitionPaths { get; init; } = Array.Empty<AcquisitionPath>();
```

| Kind | Typical paths |
|---|---|
| Trench | laboured, built |
| Rampart (incl. the moat) | **laboured** for a dug moat; built for a raised wall |
| Wire | built, assembled |
| Mine | built, assembled |
| Emplacement | built, assembled |

> **This is also a `structure-seed` change**, and §5.24 names it: *"**New in the structure seed
> contract:** an `acquisitionPaths` field — `VALIDATED`, a subset of
> `{built, assembled, summoned, laboured}`, `none` illegal. It joins the eleven catalogs in
> `structure-seed-ideal.md` §5 as a **twelfth**."* Recorded here because this module consumes it; the
> catalog belongs to that program.

**Validated at load**, like every other catalog rule: an empty `AcquisitionPaths` throws.

### 8. What §5.18 cut — do not re-add

Recorded so a later session does not helpfully restore one:

| Cut | Why |
|---|---|
| Parapet · parados · revetment · fire step | Construction details of one object. Distinct only with **directional cover**, which needs facing — *"nothing in `BattleActorSetup` or `EntityFacts` carries one"* |
| **Traverse / fire bay** | Deleted outright. Its whole job is defeating **enfilade**, and *"on a turn-based square grid with per-cell damage resolution, **enfilade does not exist**"* |
| Abatis · dragon's teeth · Czech hedgehog · tank trap | Four names for one verb. They differ by *which vehicle class* they stop, and **we have no unit size or type classes** |
| Moat / anti-tank ditch | **Rampart** — identical verbs |
| Sandbag emplacement | Trench, tier 1 — a tier on an existing kind |
| Pillbox | Emplacement in concrete |
| **Dugout** | *Deferred* — its distinctiveness is CONCEAL, and fog is map-scope only (`world-intel`) today. Revisit after fog |
| **Smoke** | Not an obstacle at all — a temporary conceal effect, and the effect-atom layer already owns those |

**Four is a floor, not a compromise:** CoH3 ships five live cover types plus four declared-and-inert;
Wesnoth ships ~12 defense terrains.

---

## Tunables

`data/tuning/siege.v1.json`, `obstacles.*`. Every row is a `StructureDef` in the catalog; these are the
magnitudes behind them.

| Key | Unit | Default | Why |
|---|---|---|---|
| `obstacles.trench.sandbagDodge` | contest points | `40` | §5.17's own figure |
| `obstacles.trench.revettedDodge` | contest points | `60` | Balance |
| `obstacles.emplacement.dodge` | contest points | `80` | §5.17's own figure |
| `obstacles.wire.entryStaminaMultiplierMilli` | per-mille | `2000` | Balance |
| `obstacles.mine.damage` | damage | **unset** | Decision 29 — and it is a magnitude, so it reads `P(Θ)` per §6 |
| `obstacles.rampart.rubbleCost` / `.ironworkCost` | units | **unset** | Decision 29 |

## Numeric types

| Value | Type | Why |
|---|---|---|
| **Mine damage** | **`long`**, from `P(Θ)` | §6: *"① Magnitudes — HP, damage — `long`, derived from `P(Θ)`"* |
| Cover values | **`int`** flat contest points | §5.17 — a contest, linear, never `P(Θ)` |
| `EntryStaminaMultiplierMilli` | `int` per-mille | divide by 1000 last, exactly once |
| Build costs, HP | **`long`** | magnitudes, `checked` |

## Boundaries

**Always:** five rows, five distinct decisions · obstacles are structures with HP · mine damage
through `DamagePacket`, never a direct HP write · one cell-entry mechanism · mines revealed.

**Ask first:** a sixth `ObstacleKind` — it must name a decision no existing row produces · restoring
anything from the cut table.

**Never:** a `CHANNEL` property · Wire as a movement-cost multiplier · a hidden mine · a second
cell-entry hook · directional cover (there is no facing) · a moat modelled as terrain.

---

## Testing

| Test | Asserts |
|---|---|
| `Five_kinds_produce_five_distinct_decisions` | a design test: each kind changes an AI/player choice no other kind changes |
| `Trench_is_occupiable_and_passable` | you can walk through and stand in it |
| `Trench_tiers_differ_by_value_not_mechanism` | one code path, two rows |
| `Rampart_blocks_movement_and_fire` | **decision 25** |
| `Requires_line_of_sight_finally_has_a_reader` | the wiring gap, closed — **plus a companion test asserting it had none before** |
| `Rampart_is_destructible_and_leaves_ruins` | razing is a legitimate action |
| `A_laboured_moat_is_a_rampart_not_terrain` | the layer-split correction |
| `Wire_taxes_stamina_not_movement` | **the correction.** Assert movement cost is unchanged and stamina is not |
| `Wire_does_not_change_pathfinder_route_length` | it is not a second Rough |
| `Mine_damages_on_entry_through_the_damage_packet` | shields and elements apply |
| `Mine_is_single_use` | second entry is safe |
| `Mine_ignores_cover` | every cover row against `Entry` is 0 |
| `Mine_is_visible_to_both_sides` | **F9** |
| `Mine_fires_on_the_same_transition_cover_uses` | one mechanism |
| `Emplacement_lends_its_action_when_garrisoned` | `combatant-kind` §4's first content |
| `No_directional_cover_exists` | source scan for a facing field |
| `Every_obstacle_declares_at_least_one_acquisition_path` | §5.24 — `none` illegal, throws at load |
| `A_dug_moat_is_a_laboured_rampart` | the layer split and the path, together |
| `Obstacle_kind_defaults_to_none` | four shipped rows unaffected, goldens unmoved |
| `This_module_does_not_depend_on_siege_cover` | **P3-1**, the cycle — an import scan |
| `Every_cell_entered_is_paired_with_a_cell_exited` | move, death and withdrawal all emit — **the leak test**, inherited from the old cover spec |
| `Existing_membership_consumers_ignore_the_new_transitions` | `BattlefieldOwnSideReactor.cs:75-86` falls through, never throws |

## Success criteria

1. Five rows, each with a test proving its distinct decision.
2. `RequiresLineOfSight` has a reader for the first time.
3. Wire taxes stamina and provably not movement.
4. Mines fire through the shared cell-entry transition, damage through `DamagePacket`, and are visible.
5. A moat is a Rampart.
6. `ObstacleKind.None` default leaves every existing structure and golden untouched.

## Open questions

None. F9 was the one open fork and its recommendation is taken.
