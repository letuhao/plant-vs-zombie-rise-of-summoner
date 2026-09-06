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
- **Six actor resources**: `hp`, `stamina`, `hunger`, `spirit`, `qi`, `poise` (resource hub). Paths 3 and 4
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

> ⛔ **Correction, 2026-09-06 — a THIRD real spec defect, same class as §7's two.** This section
> originally named `structure.assemble` as if `AtomKindRegistry` could carry "place a structure" the
> way it already carries damage/status/shield. It could not, as shipped: `AtomKindRegistry` is a
> closed, PvZ-opcode vocabulary where every `Board`-attached kind is Battle=`None` (Lawn/Unity-only —
> none has ever reached the tactical siege board), `BattleEffectSink.Execute` hardcoded acceptance to
> exactly `ApplyResourceDelta`/`ApplyStatus`/`ModifyStat`, and no atom/effect DTO anywhere carried a
> board-cell target — confirmed by two independent research passes plus direct reading of all three.
> **Brought to the owner via `AskUserQuestion`** (extend the atom system, vs. a dedicated atom-free
> resolver matching `BuildResolver.cs`'s own precedent) — **decided: extend the atom system.** Built:
> a new 8th attach point (`AttachPoint.Siege`) and a single new 17th kind, **`structure.place`**
> (params `structureId`, `instant`), shared across all four acquisition paths rather than one
> `structure.assemble`/`structure.summon` pair — the board-effect is identical for all four ("place
> this structure, instant or not"); only the cost source differs, which is the calling ACTION's own
> concern, never the atom's. See `tasks/base-defense-todo.md` 15.3b for the full build record.

An item consumed via an action carrying a `structure.place` atom (`instant: true`). Structure appears
**finished** (`ConstructionTurnsRemaining = null`), on an adjacent legal cell.

**No new economy.** The cost was paid at crafting. This is exactly the *"producing (craft series
equippement)"* building role from the owner's round-5 message closing its own loop: your workshops
make deployable fortifications, and you carry them to the siege.

### 4. `Summoned` — a demon action paid in `qi`

An ordinary action with a `qi` cost and the SAME `structure.place` atom (§3's correction above,
`instant: true` — a summoned structure is also immediate). **Nothing new needed beyond the atom
itself**: the action system already validates costs, the resource hub already holds `qi`, and
`structure-seed` will author which demons can do it.

This is the seedsmith Law-1 shape working as intended — the container-roll path already exists, so
authoring the action/container is a **content** question now, not a mechanism build.

### 5. `Laboured` — stamina and hunger, no stockpile

> ⛔ **Correction, 2026-09-06 — this section is contradicted by `spec-siege-obstacles.md` (module 19,
> level 4, so it is the authoritative, EARLIER-owning spec for this vocabulary) and was never
> propagated back here.** §5.18/`spec-siege-obstacles.md` §"The layer split the audit found wrong in
> `siege-board`" states plainly: *"A cell you cannot enter and cannot stand on IS a wall. Identical
> verbs. A moat is a Rampart built by the laboured path, and it is destructible, which a terrain
> change is not."* Its own Boundaries table lists **"a moat modelled as terrain"** under **Never**.
> The paragraph below (kept, struck through in spirit rather than deleted, so a future reader sees
> what was wrong and why) is the exact mistake that rule exists to refuse.

The moat. An action costing `stamina` and `hunger` that ~~converts a cell's terrain — `Open` → `Gap`
for a moat, `Open` → `Rough` for piled earth.~~

~~**Terrain change, not a structure.** A dug moat has no HP, cannot be repaired, and is not in
`StructureCatalog`. It is a cell whose terrain changed, which `siege-cover` already reads and
`siege-pathing` already routes around.~~

~~That is why it costs nothing but effort: there is nothing to build, only ground to move.~~

~~**It persists.** A moat dug during a siege is still there next turn, so the terrain override is
stored per slot alongside `structure-state`'s fields, under the same conditional row.~~

**What replaces it**: a moat is an ordinary `StructureDef` row (`Obstacle: ObstacleKind.Rampart`,
`AcquisitionPaths: [Laboured]`, `BlocksMovement: true`, `BlocksLineOfFire: true`) placed through the
SAME `structure.place` atom §3/§4 already use (`instant: false` — digging takes `construction.labour.moatTurns`,
authored as the row's own `StructureDef.BuildTurns`). It has HP (from `MaterialTier`, decision 32,
the same power-ladder every other structure uses), is destructible, and leaves `SlotState.Ruined` like
any other structure — no terrain-override field is needed anywhere, and `WorldSlot` gains nothing new
for this path. Costing `stamina`/`hunger` rather than `Rubble`/`Ironwork` is the ACTION's own cost
declaration (§4's own "the cost source differs, never the atom's concern"), not a property of the
structure or the atom.

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

### 7. ⛔ No new `WorldCommandKinds` member — all four paths are unit actions

> ⛔ **Correction, 2026-09-06, superseding an earlier same-day correction.** This section originally
> claimed a new order kind was needed (first drafted as `WorldCommandKinds.Assault`, then corrected to
> `WorldCommandKinds.Construct` once the naming collision with `siege-seam`'s own `Assault` was found).
> **Both were wrong about the mechanism, not just the name.** §9 and §10 of this SAME spec state,
> twice, in plain prose, that **all four paths — Built included — are unit actions**: decision 5,
> *"pre battle and in battle, deployment cost unit action"*, and §Testing's own
> `Every_path_costs_a_unit_action` line names `Built` explicitly, not just the other three. A
> `WorldCommand` resolves once per world turn, outside any battle — no live board exists then, and a
> `WorldEntity` has no board-cell position outside an active `BattleEngine.Resolve` call (confirmed
> directly), so `ConstructionPlacement.CanPlace`'s own adjacency rule cannot be evaluated from a
> world-command resolver at all. Brought to the owner rather than guessed at a second time — confirmed
> 2026-09-06: **no new `WorldCommandKinds` member exists for construction. All four paths, Built
> included, are ordinary unit actions sharing `ConstructionPlacement.CanPlace` (§6), exactly as §9's own
> "the action system... paths 2, 3 and 4 are ordinary actions" already said** — Built simply completes
> that sentence rather than being the one exception to it.
>
> **Category and tag, decided the same pass**: none of the four paths had an `ActionCategory`/`ActionTag`
> assignment anywhere in this program's docs before now (confirmed absent). Investigated the existing,
> explicitly-closed two-vocabulary system first (`spec-action-seeding.md` §3: `ActionCategory` ×
> `ActionTag`, *"inventing a third vocabulary is the exact defect the atom program exists to stop"*)
> rather than proposing a new `SupportSubCategory` layer, even though `StatusKind`×`StatusL2bCategory`
> is a real precedent for exactly that shape elsewhere. **Decided: `ActionCategory.Support` for all
> four; `Summoned` reuses the existing `ActionTag.Summon`; `Built`/`Assembled`/`Laboured` share one new
> tag, `ActionTag.Construct`** (a reviewed addition to the existing 8-value enum — not a new enum, not
> a nested sub-category; `ActionEnums.cs`).
>
> **What this removes from this spec's own remaining task list**: the five-plumbing-sites checklist
> below, `bind-warden`'s citation as a precedent, and the round-trip-test acceptance were all specific
> to a world-command mechanism that turned out not to exist for this module. They are struck rather
> than silently deleted, so a future reader sees what was ruled out and why:
>
> ~~A new order kind passes FIVE plumbing sites (`WorldCommandKinds` / the `WorldCommand` field /
> `RpgStore.CommandPayload` / `WorldCommandRequest` / the `WorldEndpoints` submit mapping), citing
> `bind-warden`'s own documented failure at sites 4-5, with a round-trip API test as the acceptance.~~
> **What replaces it**: an atom (`structure.assemble`, `structure.summon`) or a terrain-override action
> (Laboured's moat) for the three already-scoped as "ordinary actions" in §3-§5, plus `Built`'s own
> action needing a battle→world seam delta this spec does not yet name (no existing `BattleOutcome`/
> `BattleSideOutcome` field carries "a structure was placed here" back to `WorldSlot` today) — real,
> deferred work, not a five-site checklist to re-run.

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

**Read for the peacetime `Build` shape's own discipline** (ten refusal gates, debit-then-write,
resolving where a same-turn claim is already visible) even though §7's correction means siege-time
`Built` does NOT reuse this file's own `WorldCommandKinds`-resolved mechanism — it is a unit action
(§7), so its own resolution lives in the action/atom system instead, not in a new `Snapshot`-phase
resolver modeled on this one. What DOES carry over unchanged from reading this file: the "no ownership
check on siege-time placement" contrast (peacetime `Build` HAS one, `build.not-yours`; decision 4
forbids one for `Built`), and the ten-gates discipline as a model for how many distinct ways a
placement attempt can legitimately fail.

One property of `BuildResolver` changes under decision 14 and must be changed deliberately, not
inherited: *"there is no per-entity order cap anywhere … **One legion may file 200 builds in a
turn**"* (`MaxCommandsPerSubmit = 200`) — pricing every path in a unit's own action economy (§9/§10)
closes this for the siege board structurally (an actor has a bounded number of actions per engagement);
the **world-scope** peacetime hole stays open regardless and is named here so it is not mistaken for
closed by this correction.

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
| `Built_Assembled_Summoned_Laboured_all_resolve_as_unit_actions` | §7's correction — none reaches `TurnEngine.Step` as a `WorldCommand` |
| `Support_category_and_the_right_tag_are_authored_on_every_construction_action` | `Summon` for Summoned, `Construct` for the other three — §7's category/tag decision |
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

## §11. Real content + the activation mechanism (15.3b, 2026-09-06) — a FOURTH correction

> ⛔ **This section's own finding is bigger than this module.** §9 named `BuildResolver.cs` as the
> reference implementation for the peacetime `Build` shape, but never checked whether ANY real,
> non-`BasicAttack` action had ever actually FIRED its own atoms in this codebase's history. It had
> not. `BattleRunState.BindContainers` grants an actor's held-action containers PERMANENTLY at battle
> setup, but nothing anywhere calls `Bag.OnEvent` for a committed action's own `OnActivate` atoms except
> the hardcoded `BasicAttack.cs` special case — confirmed by exhaustive grep (`Bag.OnEvent(` has exactly
> two production call sites, both in `BasicAttack.cs`) and by the fact that every production caller of
> `BattleEngine.Resolve` omits `containerResolver` entirely, which means a real container-bearing action
> would THROW at `BattleRunState`'s own constructor the instant anyone actually tried it. **This is the
> whole action program's own foundational scope, not `siege-construction`'s** — named here because it
> was found here, not claimed as this module's fix.

**What this module built instead, to prove its own four paths work without solving that larger gap**:
a second, narrow, hardcoded activation path — `ConstructionActivation.Fire` — the exact same shape
`BasicAttack.cs` itself already is (a special case, not a generalization). `ConstructionActions.cs`
compiles the four `structure.place` atoms through the REAL `AtomCompiler`/`AtomPushCodec` pipeline (not
hand-built `EffectDef`s), authors the four `ActionRow`s (`ActionCategory.Support`, `Summon`/`Construct`
tags per §7's own decision), and wires `Summoned`/`Laboured`'s costs through the existing
`ActionCostRow`/`CostLedger` mechanism directly.

**Two more real, previously-undiscovered defects found and fixed while proving this end to end** — the
SAME class of bug as each other, and the fourth and fifth instances of a pattern this codebase's own
`Compilability.cs` comments already named three times before (`stat.derived`, `bullet.modify`,
`wave.control`): `AtomCompiler.OpcodeOf`'s switch and `Compilability`'s separate `OpcodeKinds` set (and,
one seam further downstream, `EffectOverlayMerge`'s `AllowedByAction` dictionary) must BOTH list a kind
for it to actually reach the Compiled path and actually be grantable — `structure.place`/
`PlaceStructure` was added to the first list this session (session 2) but not the other two, so every
`structure.place` atom silently routed to the Runner path, and any real `Grant()` of it would have
thrown `"unknown action PlaceStructure"` at the very first line. Found by a real, failing end-to-end
test (`ConstructionActionsTests`), not by inspection. Both fixed, and a NEW general guard test added for
each (`Every_kind_OpcodeOf_maps_is_also_in_Compilabilitys_own_OpcodeKinds_set`,
`Every_EffectActions_constant_has_an_AllowedByAction_entry`) — closing the CLASS of bug, not just these
two instances, since three prior instances with no general guard is how it reached a fourth and fifth.

**Content scope, decided rather than assumed**: one real structure — `moat` (`StructureCatalog.cs`) —
is buildable via **all four** acquisition paths (`AcquisitionPaths` is a list; nothing stops a
structure using every path it authors a cost for). A fuller siege roster (Trench/Wire/Mine/Emplacement,
separate structures per path) is `structure-corpus`'s (module 24) own job — this module proves the
`structure.place` mechanism end to end with one real, shipped structure, not a content roster nobody
has designed yet. `Built`'s own cost (`ConstructRubbleCost`/`ConstructIronworkCost`) is real and
non-zero on this row for the first time.

**`Built`'s own cost gate, built as a small, dedicated pure function pair** (`ConstructionCost` in
`ConstructionActions.cs`) rather than a fourth `ActionCostRow` resource kind: confirmed no existing
mechanism can express "spend a WORLD-scoped resource through a unit action" —
`ActionCostRow.ResourceId` only resolves against the closed six actor resources, and `IStockLedger` is
actor-inventory-scoped. `ConstructionCost.CanAffordBuilt`/`SpendBuilt` mirror
`ConstructionPlacement.CanPlace`'s own "pure validator, caller applies it" shape exactly.

**What is still genuinely missing, named rather than assumed done**: `Assembled`'s own item-consumption
precondition (`AssembledConsumableItemId` is a placeholder id — the real `StockDemand`/`IStockLedger`
wiring, and the real craftable item content, are `structure-corpus`'s own job); no intent source ever
calls `ConstructionActivation.Fire` in a live battle yet (`siege-ai`'s own job, unchanged from this
module's earlier framing); `SiegeDepot`'s own Rubble tracking (fixed, `BoardEconomy.cs`) is not yet
wired into `DistrictAssaultResolver`'s own `request.Budgets` flow — `ConstructionCost` reads a
`WorldSector`'s stocks directly today, not a live `SiegeDepot`; and the module's own headline acceptance
test (`A_besieging_legion_can_afford_more_than_one_structure`) still needs a live intent source to
drive, which is why it is not yet written.

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
