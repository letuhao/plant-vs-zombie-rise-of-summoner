# Spec: `siege-cover`

**Module 11 of 29 · level 5 · depends on `siege-positions`, `siege-obstacles` (level 4) · [base-defense-map.md](../base-defense-map.md)**
**Status:** spec, **rewritten 2026-09-04 for owner decision 35.** The previous version specced cover as
a terrain-keyed dodge bonus. **Decision 35 replaces that mechanism entirely** with the Heroes of Might
and Magic III shooting model plus targetable obstacles.

---

## ⛔ What changed, and why the old spec was not salvageable by editing

| | Old spec (superseded) | Decision 35 |
|---|---|---|
| **What grants cover** | the terrain of the cell you stand on | an **obstacle projecting a cover area** around itself |
| **What it modifies** | the defender's `combat.dodge.omni` (a contest) | the **shot's power** (a multiplier) |
| **Line of fire** | `Blocking` terrain blocks a shot outright | **a unit or obstacle in the way REDUCES power** — it does not stop the shot |
| **Distance** | not modelled | **a range penalty** — HoMM3's most characteristic rule |
| **Who ignores it** | nothing | **a projectile kind** that pays no penalty |
| **Obstacles** | terrain, not targetable | **targetable and destructible** — the addition beyond HoMM3 |

The old version keyed everything off `CellTerrain`. The new one keys off *what stands between two
actors*, which is a different question asked at a different time. Editing would have left the terrain
assumption buried in the tunables and the tests.

---

## Objective

**Shooting is a decision about the line, not just the target.**

Owner decision 35, verbatim:

> *"obstacle need cover area, target in the area consider coverage · the obstacle can be target and
> destroy · there will be two types of projectile: 1 will be penalty when fight through obstacle,
> 2 will get no penalty · range attack have range penalty, if shooter is block by unit or obstacle,
> the power will reduce · this mechanism is inspire of heroes of might and magic 3 shoot mechanism ·
> we make it more by add targetable obstacle so shooter or some unit can destroy obstacle/building ·
> **this mechanism need to build both battle engine and action system**"*

**Four mechanics**, and each creates a decision the others cannot:

| # | Mechanic | The decision it creates |
|---|---|---|
| 1 | **Cover area** — an obstacle covers cells within an authored radius | *Where do I stand?* |
| 2 | **Range penalty** — power falls off with distance | *Do I close, or shoot from here?* |
| 3 | **Obstruction penalty** — a unit or obstacle in the line reduces power | *Do I move for a clean line, or shoot through?* |
| 4 | **Projectile kind** — some shots pay none of it | *Which shooter do I bring?* |

Plus the addition beyond HoMM3: **the obstacle is a target.** Shoot the rampart down, then shoot what
was behind it. That is what makes mechanic 3 a *problem to solve* rather than a tax to accept.

---

## The prior art, with its shipped numbers

HoMM3's shooters take a **50% damage penalty** in each of three situations, and they stack:

| Situation | Penalty | Our analogue |
|---|---|---|
| Target beyond half the battlefield | ×0.5 | **mechanic 2** — the range penalty |
| Shooting over a castle wall during a siege | ×0.5 | **mechanic 3** — obstruction |
| Shooter adjacent to an enemy (in melee) | ×0.5 | **mechanic 3b** — the melee lock |
| Sharpshooter / Grand Elf variants | **ignore them** | **mechanic 4** — the projectile kind |

**Two things to take, and one not to.**

⭐ **Take: the penalties are multiplicative, not subtractive.** This resolves §5.17's own objection to
damage-side cover — *"a damage-side cover value must justify itself against `P(Θ)`, and a flat one
would decay to irrelevance as Θ grows."* **A per-mille multiplier is scale-free.** `×500‰` is `×500‰`
at Θ=1 and at Θ=200, so it never touches `P(Θ)` and never decays. A flat *subtraction* would have; a
ratio does not. **This is the whole reason decision 35 is architecturally legal.**

⭐ **Take: a unit that ignores the penalty is the counter-play.** HoMM3's Sharpshooter is a whole unit
identity built out of one exemption — decision 35's *"two types of projectile"*, and a content axis
both `structure-seed` and the demon corpus can use.

⛔ **Do not take: 50% everywhere.** HoMM3 uses one number for three different situations because it
was cheap, not because it was right. Ours are three tunables.

---

## What already exists (verified at HEAD, 2026-09-04)

**Built.**

- `GridDistance.Chebyshev` — the metric mechanic 2 measures in.
- `DamagePacket` → `CombatDamageDispatcher` → `ShieldGate`/`OverlayCombatCalculator` → Funnel — where
  a power multiplier applies.
- `CompiledAction.MinRange` / `MaxRange` — Chebyshev cells, compiled and carried.
- `BattleStatComposer.cs:116-117` + `OverlayCombatCalculator.cs:162-164` — the **contest** path
  (`accuracy − dodge` through a sigmoid). **This module no longer uses it**, said plainly so a reader
  does not go looking for a dodge grant that is not there.

**Wiring gap.**

- **`RequiresLineOfSight`** — declared (`ActionRow.cs:49`), compiled (`ActionCompiler.cs:65`), carried
  (`CompiledAction.cs:37`), **persisted twice** (`RpgStore.Actions.cs:256`, `:373`), hardcoded `false`
  in the battle fallback (`BattleRunState.cs:61`) — **and read by no evaluator anywhere in `src/`.**
  This module is its first reader, and decision 35 changes its meaning: not *"the shot is blocked"* but
  *"the shot pays the obstruction penalty."*

**Real gaps.** No line-of-fire trace. No range falloff. No projectile kind. No cover area.

---

## The contract

### 1. Mechanic 1 — the cover area, an authored radius per kind

Decision 39.

```csharp
/// <summary>
/// How far this obstacle's cover reaches, in Chebyshev cells. 0 = no cover area at all.
///
/// <para><b>Authored per obstacle kind</b> (decision 39): the seed writes the KIND, a tunable writes
/// the CELLS. Seedsmith Law 2 — a model has no calibrated sense of how many cells a rampart shelters,
/// and a wrong number there is invisible in review while a wrong kind is not.</para>
/// </summary>
public int CoverRadius { get; init; }
```

A target within `CoverRadius` of a **live** obstacle is **covered**: incoming ranged power is
multiplied by that obstacle's `CoverPowerMilli`.

**Live is load-bearing.** A destroyed obstacle (`SlotState.Ruined`) projects nothing — the whole reason
mechanic 5 matters.

**Cover does not stack across obstacles. The best single cover applies.** Stacking makes a cluster of
cheap works strictly better than one good one, which is the distribution-skew failure
`05-failure-modes.md` records: every individual number defensible, the offering degenerate.

### 2. Mechanic 2 — the range penalty

```csharp
/// <summary>
/// Power multiplier from distance. Measured in Chebyshev cells against a threshold authored as a
/// FRACTION of the board's own side (district-layout §2) rather than as an absolute — an 18-cell
/// board and a 30-cell board must not share a falloff point, or a stronghold's longer sightlines are
/// a free buff to every archer standing on it.
/// </summary>
public static int RangePowerMilli(int chebyshevDistance, int boardSide, SiegeShootingPolicy policy);
```

**The threshold scales with the board; the multiplier does not.**

### 3. Mechanic 3 — the obstruction penalty, and it REDUCES rather than blocks

The line from shooter to target is traced. **Every cell it crosses holding a live obstacle or a live
actor contributes one obstruction.**

```csharp
/// <summary>
/// Power multiplier from things standing in the line of fire. Decision 35: "if shooter is block by
/// unit or obstacle, the power will reduce" — REDUCE, not block. A blocked shot is a refused action
/// the player must be taught to understand; a weakened shot is a decision they already understand.
///
/// <para><b>Units obstruct too, not only obstacles.</b> That is what makes body-blocking a real tactic
/// and a defender's own crowded courtyard a liability.</para>
///
/// <para><b>Multiplicative per obstruction, bounded by a soft floor.</b> Two obstructions are worse
/// than one; twenty are not twenty times worse, or a crowded board makes shooting pointless.</para>
/// </summary>
public static int ObstructionPowerMilli(IReadOnlyList<Obstruction> inLine, SiegeShootingPolicy policy);
```

**The trace is a Bresenham line over the grid, and it is determinism-sensitive**: it must produce the
same cell sequence for `(a → b)` on every machine, and — because a shot and a return shot trace the
same corridor — **it must be symmetric**, or A obstructs B without B obstructing A.

> ### ⚠️ §2 rule 10 — the trace is NOT a fifth area shape
>
> *"Closed vocabularies — do not start a third. The action layer already owns a grid vocabulary
> (`GridPos`, Chebyshev distance, **four area shapes**, `ChosenCell` anchoring). Inventing a second grid
> model beside it is the exact defect the atom program exists to stop."*
>
> The four shipped shapes are `Row · Column · Square · Rectangle`
> (`ActionTargetSpec.cs:42-48`). **A line-of-fire trace is not among them and must not become a fifth**
> — it is a *traversal used to compute a penalty*, never a way to select targets. It returns cells to
> inspect, not cells to hit.
>
> **The test:** if anything ever passes the trace's output to a targeting resolver, a fifth shape has
> been introduced by the back door.

Where a line passes exactly between two cells, the tie-break is **the lower cell index**, stated
explicitly. Same discipline `siege-pathing` applies to its heap, for the identical reason:
`ReachMap`'s own warning that an implicit tie-break lets *"a replay disagree with itself."*

**`RequiresLineOfSight` gets its first reader here**, with its meaning fixed by decision 35: an action
that sets it pays the obstruction penalty; one that does not ignores obstructions entirely. It is
**never** a hard block.

#### 3b. The melee lock

HoMM3's third penalty: a shooter with an enemy adjacent shoots at half power. One adjacency check; it
makes closing on archers the correct answer; without it a ranged unit standing in a melee is strictly
better than one that retreated. `meleeLockPowerMilli`, tunable, exemptible by the projectile kind.

### 4. Mechanic 4 — the projectile kind

```csharp
/// <summary>
/// Which shooting penalties this action pays. Decision 35's "two types of projectile", widened to
/// flags because HoMM3's own exemptions are not uniform — a Grand Elf ignores the range penalty; a
/// Sharpshooter ignores all three.
///
/// <para><b>Flags, not two values.</b> Two values force every future exemption into an all-or-nothing
/// choice, and the shipped prior art already has both shapes.</para>
/// </summary>
[Flags]
public enum ProjectilePenalties
{
    None        = 0,
    Range       = 1 << 0,
    Obstruction = 1 << 1,
    MeleeLock   = 1 << 2,
    All         = Range | Obstruction | MeleeLock
}
```

**Default is `All`** — an ordinary shot pays everything, and an exemption is authored content. A
conservative default makes every exemption a deliberate identity choice rather than an oversight.

**This is the action-system half decision 35 names.** `ActionRow` gains the field, `ActionCompiler`
compiles it, `CompiledAction` carries it, and it persists in the two places `RequiresLineOfSight`
already does (`RpgStore.Actions.cs:256`, `:373`) — **that flag is the reference for all five sites.**

### 5. Mechanic 5 — obstacles are targets

Already delivered by `structure-state` (HP) and `combatant-kind` (a `Structure` never takes a turn but
is targetable). **This module's addition is that shooting one is worth doing**: destroying a rampart
removes its cover area *and* its obstruction — a two-for-one that makes "shoot the wall first" a plan
rather than a wasted turn.

**Assert both effects disappear together.** A destroyed obstacle that still obstructs is exactly the
bug this mechanic's appeal rests on not having.

### 6. Where the multipliers apply — one place, once

All four compose into a **single per-mille power factor** applied to the outgoing `DamagePacket`
**before** the dispatcher, so shields, elements, the Funnel and FA10 all see one already-adjusted
number.

```csharp
// long × int × int × int × int, one divide chain, all at the end, checked.
// CLAUDE.md rule 3 (widen before multiplying) and rule 4 (divide by 1000 last).
var power = checked(basePower
    * cover * range * obstruction * meleeLock
    / 1000 / 1000 / 1000 / 1000);
```

**Four divides, each by 1000, all after every multiply.** Combining them into `/ 1_000_000_000_000` is
arithmetically equal and **forbidden** — that divisor is itself large, and the product above it
overflows first. This is the rule-4 case where the naive simplification *is* the bug.

### 7. Legibility — §5.17 rule 5 survives the rewrite

Relic's most repeated bug class is cover illegibility; XCOM's two most-cited complaints are both
perception gaps. **The wire carries each factor separately**, never only the product:

```
range ×500 · obstruction ×700 · cover ×600  →  ×210 total
```

`BlockedTarget.tsx` / `blockedPlacement.ts` are built and inert — the pattern to copy. **Four
multipliers make this more important than it was for one dodge number, not less.**

### 8. What this module no longer does

- **No `combat.dodge.omni` grant.** The contest path is untouched by cover.
- **No `ScopeMembershipTransition` change.** The program's one allowed vocabulary change is **not spent
  here** — cover is evaluated per shot, so no membership is entered or left.
  ⛔ **`siege-obstacles` now OWNS that transition for its Mine** — pass 3 found the budget released here
  and claimed by nobody, which left the Mine firing on nothing. It is claimed there; do not re-add it.
- **No `(damage source × cover type)` matrix.** Superseded by four multipliers plus the projectile
  flags, which express the same counter-play with one fewer table.

---

## Tunables

`data/tuning/siege.v1.json`, `shooting.*`. **All per-mille multipliers, 1000 = no penalty.**

| Key | Unit | Default | Why |
|---|---|---|---|
| `shooting.rangeThresholdMilli` | per-mille of board side | `500` | HoMM3's "half the battlefield", as a ratio so it scales |
| `shooting.rangePowerMilli` | per-mille | `500` | HoMM3's 50% |
| `shooting.obstructionPowerMilli` | per-mille, **per obstruction** | `700` | Ours — HoMM3 has a single wall check |
| `shooting.obstructionFloorMilli` | per-mille | `250` | **Soft floor**, configurable per `AGENTS.md`, so a crowded board stays shootable |
| `shooting.meleeLockPowerMilli` | per-mille | `500` | HoMM3's 50% |
| `obstacles.<kind>.coverRadius` | cells | `1` | **Decision 39** — authored per kind |
| `obstacles.<kind>.coverPowerMilli` | per-mille | `600` | Balance |

**No `P(Θ)` in this file**, for the reason the prior-art section gives: multipliers are scale-free, so
they need no ladder and cannot decay.

## Numeric types

| Value | Type | Why |
|---|---|---|
| Base power / damage | **`long`** | a magnitude `contentScale` reaches |
| Every multiplier | `int` per-mille, bounded 0..1000 | exempt ratio, stated in each comment |
| The composed product | **`long`, `checked`** | four multiplies before any divide — the widen is the safety argument |
| Distances, radii, cell indices | `int` | board-bounded, structural |

**No `float`.** A power chain in floating point produces a different last digit on a different runtime,
and this chain feeds a hashed battle report.

## Boundaries

**Always:** multiplicative per-mille, never a flat subtraction · divide by 1000 **four times, each
after every multiply** · trace the line deterministically, symmetrically, with a stated tie-break ·
surface each factor separately on the wire · a destroyed obstacle projects nothing.

**Ask first:** a fifth multiplier · making an obstruction a hard block · stacking cover.

**Never:** `P(Θ)` on a multiplier · a `float` in the chain · combining the four divides into one ·
blocking a shot outright (decision 35 says *reduce*) · a cover grant on `combat.dodge.omni` · spending
the vocabulary-change budget here.

---

## Testing

`tests/FusionRpg.Core.Tests/Battle/Board/` and `.../Actions/`.

| Test | Asserts |
|---|---|
| `Target_in_a_cover_area_takes_reduced_power` | mechanic 1 |
| `Cover_radius_is_authored_per_kind` | decision 39 |
| `Best_single_cover_applies_and_covers_do_not_stack` | the degenerate-cluster failure, prevented |
| `A_destroyed_obstacle_projects_no_cover_and_no_obstruction` | **both together** — mechanic 5's whole appeal |
| `Power_falls_off_beyond_the_range_threshold` | mechanic 2 |
| `Range_threshold_scales_with_board_side` | an 18-cell and a 30-cell board differ |
| `A_unit_in_the_line_reduces_power` | **units obstruct, not only obstacles** |
| `An_obstruction_reduces_but_never_blocks` | decision 35's own word |
| `Obstructions_compound_but_stop_at_the_floor` | a crowded board is still shootable |
| `Line_trace_is_identical_across_10000_runs` | determinism |
| `Line_trace_is_symmetric` | A obstructs B iff B obstructs A |
| `The_trace_is_never_passed_to_a_targeting_resolver` | **P4-6**, §2 rule 10 — no fifth area shape |
| `Line_trace_tie_breaks_to_the_lower_cell_index` | the stated rule |
| `Requires_line_of_sight_finally_has_a_reader` | **plus a companion test proving it had none before** |
| `Requires_line_of_sight_means_pays_obstruction_not_blocked` | the meaning decision 35 fixed |
| `A_shooter_with_an_adjacent_enemy_shoots_weaker` | 3b |
| `Projectile_flags_exempt_exactly_what_they_name` | one per flag, plus `All` and `None` |
| `Default_projectile_pays_every_penalty` | conservative default |
| `Projectile_kind_survives_all_five_plumbing_sites` | the action-system half, round-tripped |
| `Penalties_compose_multiplicatively_in_one_place` | and the packet reaches the dispatcher pre-adjusted |
| `Power_chain_overflows_loudly` | `OverflowException`, not a wrapped negative |
| `Four_divides_beat_one_combined_divide` | against a `BigInteger` reference at a magnitude where the combined divisor overflows |
| `Multipliers_are_equally_decisive_at_theta_1_and_theta_200` | the scale-free claim |
| `The_wire_carries_each_factor_separately` | §5.17 rule 5 |
| `All_goldens_byte_identical_with_no_board` | **the gate** |

## Success criteria

1. Four mechanics, each with its own tests and tunables.
2. Every multiplier per-mille and scale-free; **no `P(Θ)`, no `float`** in this module.
3. The line trace is deterministic over 10,000 runs, symmetric, with a stated tie-break.
4. `RequiresLineOfSight` has a reader, meaning *pays obstruction*, never *blocked*.
5. Destroying an obstacle removes cover **and** obstruction, proven together.
6. `ProjectilePenalties` passes all five sites `RequiresLineOfSight` already occupies.
7. Each factor separately visible on the wire.
8. All goldens byte-identical with no board.

## Open questions

None. Decision 35 specifies the mechanism, decision 39 the cover area; the numbers are tunables and
belong to the first balance pass.
