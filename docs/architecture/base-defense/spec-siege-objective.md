# Spec: `siege-objective`

**Module 18 of 21 · level 3 · depends on `combatant-kind`, `district-layout` · [base-defense-map.md](../base-defense-map.md)**
**Status:** spec, 2026-09-04. **Added by the completeness audit** — its absence was the audit's
headline finding.

---

## Objective

**State what winning is, and what may stand on the board.**

Seventeen specs described how to build a board and none of them said what the game on it is. This
module is the rules module: the win condition, the force limits, and the arena rule.

Four owner decisions live here, and three of them are the numbers §5.9 calls *"the difficulty dial"*:

| Decision | The rule |
|---|---|
| **1** | *"A base has one central defense area. Lose it and you lose the base. Capture requires killing every troop standing in it."* |
| **4** | Legion slots are **even and paired** — N per side. Each legion has a **max member count, which does not exist today** and is free to choose |
| **5** | A **field cap** limits how many units stand on the board at once, **identical for both sides**. A flat authored integer per base tier, a tunable, **never derived from the empty-cell count** |
| **10 (half)** | *"Nothing is built inside the central area — it is a pure arena."* |

---

## What already exists (verified at HEAD, 2026-09-04)

**Built, and it is the pattern to copy — not the type.**

`CapPolicy.TryAdmit(side, LivingCounts, config)` (`src/FusionRpg.Core/Match/CapPolicy.cs:98`):

```csharp
public static GateResult TryAdmit(string? side, LivingCounts counts, CapPolicyConfig? config = null)
{
    config ??= CapPolicyConfig.Defaults();
    var s = (side ?? "").Trim();
    if (string.Equals(s, "plant", StringComparison.OrdinalIgnoreCase))
        return Check(counts.Plants, config.MaxLivingPlants, GateReasons.CapPlants);
    ...
    return GateResult.Reject(GateReasons.CapInvalidSide);
}
```

A per-side living-count gate with stable reject reason codes, `-1` as the unlimited sentinel, numbers
already in `data/tuning/match.v1.json`, driven by `MatchRuntime.TryAdmitSpawn`. **Built, tested,
tunable.** Its header says *"Never throws; never reads Data."*

Two things §5.9 says to carry across carefully, and both are load-bearing:

1. **It is asymmetric today** — 50 plants vs 80 zombies. *"The shape transfers; the values do not."*
   Decision 5 requires an identical cap for both sides.
2. **It is match-scoped and PvZ-sided** (`plant`/`zombie`/`bullet`, living in `MatchRuntime`).
   **Reuse the pattern, not the type**, or the world/battle boundary picks up a PvZ vocabulary it does
   not want (§2 rule 1 — the RPG layer is never built by changing what PvZ is).

**Real gaps.** No win condition. No field cap on a battle side. **No max member count on a legion
anywhere** (§3.6, asked and answered directly).

---

## The contract

### 1. The win condition

```csharp
/// <summary>
/// How a siege ends. Decision 1: the central defense area IS the objective — "lose it and you lose
/// the base. Capture requires killing every troop standing in it."
///
/// <para><b>Not a wipe.</b> A defender who still has soldiers in the outer ground has not lost, and
/// an attacker who has cleared the Core has won even with enemies behind them. That is what makes
/// the district's geometry matter rather than being scenery.</para>
/// </summary>
public enum SiegeOutcomeKind
{
    /// <summary>Every ANIMATE defender in the Core zone is dead. The base falls.</summary>
    CoreTaken,
    /// <summary>Every animate attacker is dead or withdrawn. The base holds.</summary>
    AssaultBroken,
    /// <summary>Neither, at the horizon. Decision 24: the engagement ends, the siege does not.</summary>
    Inconclusive
}
```

**Structures are excluded from both conditions**, following `combatant-kind`'s `Animate` filter for
exactly the reason recorded there: a wall standing in the Core would make the base uncapturable, and
"demolish everything" is not the objective decision 1 describes.

**Evaluated at each round boundary**, on the same event the round loop already fires — not per action.
Checking mid-round would let the order of two simultaneous deaths decide the winner.

### 2. The field cap — an authored integer, symmetric, and not derived

```csharp
/// <summary>
/// How many units one side may have standing on the board at once. Decision 5.
///
/// <para><b>Identical for both sides, and authored per base tier.</b> NOT derived from the empty-cell
/// count — §5.9's degenerate strategy: if the cap is f(empty cells) and shared, the defender shrinks
/// the attacker's cap by building. Wall off thirty of forty cells and the attacker deploys two units
/// at a time into a board full of towers. "That is not a hard defense to beat — it is a defense that
/// cannot be attacked, which is the same thing and worse."</para>
///
/// <para><b>A structural per-runtime cap, not a progression ceiling</b> (AGENTS.md exempts per-frame
/// and runtime caps). It bounds how much can exist at one moment, never how strong anything becomes —
/// ssot-power-scale.md §11.3's board-cap exemption, and this comment is the exemption being stated
/// out loud as that rule requires.</para>
///
/// <para>Arknights is the one shipped game measured for this (§4.1): 39 buildable tiles is the SPACE;
/// characterLimit = 8 is the CONCURRENCY, and the concurrency is the difficulty dial. The board's
/// size still matters — it decides where things stand and how far they walk — it just is not also the
/// deployment budget.</para>
/// </summary>
public sealed record FieldCapConfig
{
    /// <summary>Per side. -1 = unlimited, matching CapPolicy's own sentinel exactly.</summary>
    public int MaxLivingPerSide { get; init; } = -1;
}

public static GateResult TryAdmit(string side, int livingOnSide, FieldCapConfig config);
```

**Stable reject reason codes**, following `CapPolicy`'s own `GateReasons.CapPlants` shape:
`siege.cap.side` and `siege.cap.invalid-side`. A rejection a player can be told the reason for is the
difference between "the game refused" and "the game is broken".

**Structures do not count against the field cap.** They are not deployed units, and counting them
would recreate the derived-from-cells degeneracy through the back door — building a wall would shrink
your own army.

### 3. Legion slots — even capacity, and filling it is optional

```csharp
/// <summary>
/// How many legions the central defense area holds PER SIDE. Even by decision 4 — 2 v 2, 4 v 4.
///
/// <para><b>"Even" means the CAPACITY is even, not that both sides must fill it</b> (§5.8). An
/// attacker with three legions may assault a 4-slot area and simply be outnumbered. Requiring a full
/// roster would gate a verb behind an inventory count, "which is the shape of rule that produces
/// 'I cannot attack and I do not know why'."</para>
/// </summary>
public int LegionSlotsPerSide { get; }
```

**Validated even at load**, loudly, matching `StructureCatalog.Validate`'s stance that a bad row is a
startup error rather than a runtime surprise. An odd slot count silently breaks the pairing rule the
whole fight's legibility rests on.

### 4. Max members per legion — the number that does not exist yet

§3.6 established there is **no limit today**, so this is free to choose. Two shipped precedents say
which way to author it, and they disagree — §6 names the winner:

> *"Author it like `expeditions.v1.json`'s `squadSlots`, **never like `WebMatchService`'s
> `const int maxSquad = 6`**."*

A tunable, from the first line of code. Growth per `DevelopmentLevel` is a second tunable, defaulting
to zero.

### 5. The central area is a pure arena

Decision 10: *"Nothing is built inside the central area."* One rule, enforced in the shared placement
validator `siege-construction` owns:

```csharp
// Decision 10: the Core is a pure arena. This is the rule that keeps towers and troops from
// competing for space — defenses occupy the outer ground, legions occupy the Core, and the field cap
// is authored independently of both. §0's own stated consequence: the degenerate "wall off the board
// to starve the attacker" strategy cannot arise, and Dungeon Defenders' two-budget separation is
// satisfied structurally rather than by tuning.
if (zone == DistrictZone.Core) return Reject(SiegeRejectReasons.CoreIsAnArena);
```

**Both sides, both phases.** An attacker who breaches cannot wall the Core shut behind them either.

### 6. `DefenderBonusMilli` must shrink as fortifications land

§5.8, verified live at `PlaceholderBattleResolver.cs:79-83`:

```csharp
var entrenched = request.DefenderStationary
                 || string.Equals(defender.Stance, MovementPolicy.Hold, StringComparison.Ordinal);
if (entrenched) defenderWeight = defenderWeight * DefenderBonusMilli / 1000;
```

*"one flat per-mille multiplier for standing still … If the board carries the asymmetry, that
multiplier should shrink toward nothing as real fortifications land, **or the defender gets paid twice
for the same thing**."*

**This module does not delete it** — the placeholder still resolves every non-district battle. It
makes it **a tunable that a district assault reads as zero**, so the two models never stack. The
coincidence is worth noting: the shipped `1250` is identical to Civ IV's +25% fortify bonus.

---

## Tunables

`data/tuning/siege.v1.json`. §6's `field`, `slots.legion` and `legion` blocks, which had no owner
before this module.

| Key | Unit | Default | Why |
|---|---|---|---|
| `field.maxLivingPerSide` | units | **unset (−1)** | **The difficulty dial** (§5.9, Arknights). Decision 29 keeps it unset until a real board exists to measure on |
| `field.betweenWavesPauseTicks` | sim ticks | **unset** | Decision 6's batch pause |
| `slots.legion.perSide` | legions | `2` | Even by decision 4 |
| `slots.legion.perDevelopmentLevel` | legions | `0` | Growth, off by default |
| `legion.maxMembers` | members | **unset** | §3.6 — free to choose, and decision 29 defers it |
| `defense.districtDefenderBonusMilli` | per-mille | `1000` (no bonus) | §5.8 — the placeholder's `1250` must not stack with real works |

## Numeric types

All `int`. Every quantity here is a **count of things that exist at one moment** — bounded by the cap
itself, structural, and none is a magnitude `contentScale` touches. `CLAUDE.md`'s `long` rule does not
reach a roster size.

`defense.districtDefenderBonusMilli` is `int` per-mille, and the **divide by 1000 happens once, last**.

## Boundaries

**Always:** field cap symmetric and authored · legion slots validated even · evaluate the win
condition at round boundaries · stable reject reason codes · state the cap exemption in a comment.

**Ask first:** setting any of the unset tunables (decision 29) · a third `SiegeOutcomeKind`.

**Never:** derive the cap from empty cells (§5.9's degenerate strategy) · count structures against the
cap or the win condition · reuse `CapPolicy`'s PvZ side vocabulary · require a full roster to attack ·
let `DefenderBonusMilli` stack with fortifications · a `const` roster limit — `WebMatchService`'s
`const int maxSquad = 6` is named in §6 as the anti-pattern.

---

## Testing

| Test | Asserts |
|---|---|
| `Core_cleared_of_animate_defenders_ends_the_siege` | **decision 1**, the objective |
| `Surviving_defenders_in_the_outer_ground_do_not_prevent_a_capture` | not a wipe |
| `Structures_in_the_core_do_not_prevent_a_capture` | the uncapturable-base bug |
| `Attacker_wiped_breaks_the_assault` | the other terminal |
| `Neither_at_the_horizon_is_inconclusive` | feeds `siege-engagement` |
| `Win_condition_is_evaluated_at_round_boundaries_only` | two simultaneous deaths cannot race |
| `Field_cap_is_identical_for_both_sides` | decision 5 |
| `Field_cap_is_not_derived_from_empty_cells` | **the degenerate strategy.** Wall off 30 of 40 cells; assert the attacker's cap is unchanged |
| `Structures_do_not_count_against_the_field_cap` | building does not shrink your own army |
| `Cap_rejections_carry_a_stable_reason_code` | `CapPolicy`'s precedent |
| `Unlimited_sentinel_is_minus_one` | matches `CapPolicy` exactly |
| `Odd_legion_slot_count_throws_at_load` | decision 4, loudly |
| `A_three_legion_attacker_may_assault_a_four_slot_area` | §5.8's *"I cannot attack and I do not know why"* |
| `Nothing_can_be_built_in_the_core` | decision 10, **both sides, both phases** |
| `District_assault_reads_defender_bonus_as_zero` | §5.8's double-pay, prevented |
| `Placeholder_battles_still_read_1250` | the non-district path is untouched |
| `No_const_roster_limit_exists` | source scan for a hardcoded member cap |

## Success criteria

1. The win condition is stated, evaluated at round boundaries, and excludes structures.
2. The field cap is symmetric, authored, and provably not derived from board space.
3. Legion slots validate even; a partial roster may still attack.
4. `legion.maxMembers` exists as a tunable and as no `const` anywhere.
5. Nothing can be placed in the Core, by either side, in either phase.
6. `DefenderBonusMilli` cannot stack with real fortifications.
7. `CapPolicy`'s PvZ vocabulary appears nowhere in this module.

## Open questions

None. Decision 29 leaves the force-size numbers deliberately unset — an answered question whose answer
is "unset until the first balance pass".
