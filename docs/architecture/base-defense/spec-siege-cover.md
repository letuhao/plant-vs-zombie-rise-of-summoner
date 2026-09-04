# Spec: `siege-cover`

**Module 11 of 21 · level 5 · depends on `siege-positions` · [base-defense-map.md](../base-defense-map.md)**
**Status:** spec, 2026-09-04.

---

## Objective

**Make standing behind something matter — which is the whole of trench warfare (owner decision, round 4).**

The owner's framing: obstacles and buildings exist *for this purpose*. A trench that does not change
your odds is scenery. So cover is the mechanic that makes every other structure decision meaningful,
and it is the one place this program changes a shipped vocabulary.

**Success looks like:** occupying a cover cell grants a flat dodge bonus, delivered through the effect
system that already exists, and the bonus is granted and revoked exactly on cell entry and exit.

---

## What already exists (verified at HEAD, 2026-09-04)

**Built — and this is why cover is not a new capability.**

- **`combat.dodge.*` is a registered derived-channel family.** `DerivedStatChannels.cs:88-91` —
  `CombatDodgeOmni`, `CombatDodgeFire`, `CombatDodgeIce`, `CombatDodgeAir`, and more. `CLAUDE.md`'s
  own RPG-layer rule uses this exact channel as its worked example of the right question:
  > *"Does the RPG layer express dodge? Yes — `combat.dodge.*` is a registered channel read by
  > `OverlayCombatCalculator`."*
- `FamilyExpansion.cs:55` — `combat.dodge.{variant}` maps to `pool.element-dodge`, so the channel is
  atom-reachable, not just declared.
- `ScopeMembershipEvent` / `ScopeMembershipTransition` (`Core/Match/ScopeMembershipEvents.cs:7`) —
  three values today: `Bound`, `Cleared`, `MindControlToggled`. Consumed by
  `BattlefieldOwnSideReactor.cs:75-86` and raised from `UniqueBindings.cs:146,224` and
  `MatchRuntime.cs:140`.
- `GridSpec.CellTerrain` (`siege-board`) — including `Gap`, which blocks movement but not sight.

**Real gap.** Nothing connects a cell to a stat.

---

## The one reviewed vocabulary change

The program allows exactly one, and this is it. It is allowed because there is a real mechanic behind
it rather than a speculative slot.

```csharp
public enum ScopeMembershipTransition
{
    Bound,
    Cleared,
    MindControlToggled,

    /// <summary>
    /// An actor entered a board cell (base-defense-ideal.md, trench warfare). Cover is delivered as
    /// scope membership rather than as a per-hit lookup: the effect system already owns "this actor
    /// has this modifier while a condition holds", and a second mechanism for the same idea would
    /// mean cover expiring by a different rule than every other temporary modifier in the game.
    /// </summary>
    CellEntered,

    /// <summary>Left a board cell. Paired with <see cref="CellEntered"/> — every entry has exactly
    /// one exit, including the exit caused by death.</summary>
    CellExited
}
```

**Why membership and not a per-hit lookup.** A per-hit "is the target in cover" check is simpler to
write and it is the wrong shape: cover would then be invisible to every UI that reads modifiers, would
not stack or cap by the same rules as other dodge sources, and would need its own expiry logic. Scope
membership makes cover *the same kind of thing* as every other temporary modifier.

**The pairing invariant is load-bearing.** An unpaired `CellEntered` leaks a permanent dodge bonus onto
an actor. Enforced by a test, and by emitting `CellExited` from exactly three places: move, death,
withdrawal.

---

## The contract

### 1. ⛔ Cover is flat CONTEST POINTS — not per-mille. The audit corrected this.

**The first draft of this spec wrote per-mille, and it was wrong.** §5.17 computed the actual scale
from shipped code, and a per-mille cover value is a fraction of something that does not exist:

> `BaseAccuracy(Θ) = 220 + 26·Θ` and `BaseDodge(Θ) = 26·Θ` (`BattleModels.cs:171-172`), with
> `accuracyScale: 100.0` — so **100 contest points is one sigmoid unit, and +50 dodge is half a
> unit.** Because both sides' `26·Θ` terms cancel in the `accuracy − dodge` difference, **a flat
> cover value stays exactly as decisive at Θ=200 as at Θ=1.**

That last sentence is the whole ladder argument, and **it only holds in the contest's own units.**
§2 rule 4: contests read `Θ` linearly, magnitudes read `P(Θ)`. Cover is a contest.

```csharp
/// <summary>
/// Cover value by terrain, in FLAT CONTEST POINTS added to `combat.dodge.omni`.
///
/// <para><b>Not per-mille.</b> 100 points is one sigmoid unit (`accuracyScale: 100.0`); §5.17's own
/// figures are <b>trench +40, emplacement +80</b>. Both sides' 26·Θ terms cancel in the
/// accuracy−dodge difference, so a flat value is exactly as decisive at Θ=200 as at Θ=1 — which is
/// precisely what §2 rule 4 demands of a contest, and what a per-mille value would quietly break by
/// introducing a second scale beside the one the contest already uses.</para>
///
/// <para><b>Never `P(Θ)`, never a new `f(level)`.</b></para>
/// </summary>
public static int DodgePointsFor(CellTerrain terrain);
```

### 2. Delivered as an effect grant on the channel that already exists

Entering a cover cell grants a `combat.dodge.omni` modifier scoped to the actor; exiting revokes it.
**No new channel, no new atom, no new resolver.** §5.18 verified the path end to end:
`BattleStatComposer.cs:116-117` writes `CombatAccuracyOmni`/`CombatDodgeOmni`, and
`OverlayCombatCalculator.cs:162-164` resolves `accuracy − dodge` through the sigmoid.

`combat.dodge.omni` rather than an element variant: cover works against arrows and fire alike —
except where rule 2 below says otherwise, which is a *matrix row*, not a different channel.

### 3. The `(damage source × cover type)` table

Cover is not uniform. A trench is excellent against direct fire and useless against something dropped
on you.

| Cover terrain | vs `Direct` | vs `Area` | vs `Melee` |
|---|---|---|---|
| `Open` | 0 | 0 | 0 |
| `Rough` | partial | partial | 0 |
| `Blocking` (adjacent) | high | partial | 0 |
| `Gap` (behind) | high | 0 | 0 |

| `Trench` / `Emplacement` (`siege-obstacles`) | **+40 / +80** | partial | 0 |

**A fourth damage source: `Entry`** — a mine (`siege-obstacles` kind 4) *"ignores cover"*, so every
row against `Entry` is **0**. This is exactly why the matrix is a data shape rather than a scalar on
the defender: §5.17's *"one CoH3 idea to adopt regardless"*.

**Every cell is a tunable**, in `data/tuning/siege.v1.json` as a nested map of **contest points** — not
a `switch` in code. This is a balance surface by [tunables-ssot.md](../tunables-ssot.md)'s own test: a balance
pass would absolutely change these, and a rebuild-per-tweak loop here is the expensive kind.

**Melee is zero everywhere**, and that is structural rather than tuned: cover is about interposing
something between you and a distant attacker, and someone in your trench is not distant. It is still
a table row so a designer can see the zero and know it is deliberate.

### 4. Damage source classification

`DamageSourceKind` — `Direct`, `Area`, `Melee` — derived from the action's existing targeting mode
(`ActionTargetMode.Area` → `Area`; range 1 → `Melee`; otherwise `Direct`). **Derived, not a new
authored field**, so no content needs re-authoring and `structure-seed` inherits no new obligation.

### 5. `Gap` blocks line of sight — and it does not

`siege-board` defines `Blocking` as blocking movement *and* sight, `Gap` as blocking movement only.
Cover reads both:

- Standing **behind** a `Blocking` cell relative to the shooter: high cover.
- Standing **behind** a `Gap`: cover against direct fire (they must shoot across it), none against
  area (it lands anyway).

This is what makes the moat from decision 27's "laboured" path a real defensive work rather than a
decoration — and it is why `siege-board` split the two terrain values in the first place.

### 6. `RequiresLineOfSight` gets its first reader

The ideal found `RequiresLineOfSight` declared and unread. Cover's LOS trace is its natural consumer:
a `Blocking` cell between shooter and target either blocks the shot entirely (if the action requires
LOS) or grants cover (if it does not).

**Verify it is still unread before claiming this.** If something else has since started reading it,
follow that code rather than this spec.

### 7. §5.17 rule 2 — beat cover with a damage TYPE, not a bigger number

**The most consistent finding across three independent games**, and §5.17 states the consequence of
omitting it bluntly: *"without it the trench-warfare fantasy becomes the stalemate it is named
after."*

| Game | Mechanism |
|---|---|
| CoH3 | flame is **×1.25** damage into green cover, **×1.5** into a garrison |
| Foxhole | trenches carry **97% HE mitigation** but are *"resistant to all damage types except Demolition"* |
| Panzer Corps 2 | engineers **ignore 50–100%** of entrenchment |

**We already have an element hub, so this is a table row, not a mechanism.**

```csharp
/// <summary>
/// Elements that ignore a given cover kind, wholly or partly. §5.17 rule 2: "fire ignores trench
/// cover is a one-row rule that makes composition matter and cannot be brute-forced."
///
/// <para>Authored per (element x cover kind) in tuning — a THIRD axis on the matrix rather than a
/// special case in code, so adding "acid ignores rampart cover" is a row a balance pass writes.</para>
/// </summary>
// cover.ignoreMilli.fire.trench = 1000   — fire ignores a trench entirely
```

**Per-mille here is correct and is not a contradiction of §1.** This value *is* a fraction — of the
cover points, which are themselves flat contest points. The flat value is the magnitude; the ignore
factor is a ratio of it, bounded 0..1000, and `AGENTS.md` exempts bounded ratios explicitly.

### 8. §5.17 rule 4 — cover decays with the occupant's CONDITION, not with turns

Advance Wars scales terrain defense by **current HP** — *"a 5 HP unit in 2-star woods gets 10%, not
20%"* — so a fortress stops being one exactly when it is most needed. §5.17: *"We have a better hook
and it needs no new mechanism: stamina/hunger exhaustion already debuffs derived stats and is
re-evaluated on read."*

**So this is not a new mechanism at all.** Cover grants a `combat.dodge.omni` modifier; exhaustion
already debuffs derived stats through the resource hub's exhaustion-as-status path. An exhausted
occupant's cover is worth less **because their dodge is worth less**, with no cover-specific decay
code.

**Verify this before claiming it.** Read the exhaustion path and confirm it reaches
`combat.dodge.omni`. If it debuffs only a narrower set of channels, say so — that is a **wiring gap**
with a `file:line`, not a delivered feature.

> It also *"closes the loop with decision 13's block their resource and exhaust them"* — a besieger who
> cuts the supply line makes the defender's trenches worse, which is the trench-warfare fantasy and the
> economy pointing at the same mechanic.

### 9. §5.17 rule 5 — show the number on the wire

**Cover illegibility is the most repeated bug class in Relic's entire patch history** — two separate
patches for indicators *"rendered floating in the air"*, plus a long tail of *"balustrades now provide
cover as expected"*, *"removed cover from Greek staircases"*, *"hangar assets no longer provide cover
— suspended sections were providing cover to units beneath them"*. **The recurring failure is that the
cover a player sees and the cover the sim computes drift apart.**

XCOM's two most-cited problems are the same class: the 95%-miss perception gap, and per-difficulty aim
assist that is never surfaced.

**The pattern to copy is built and inert:** `BlockedTarget.tsx` / `blockedPlacement.ts`. The wire
carries the contribution, not just the total:

```
"this shot is at -40 because the target is in a trench"
```

**We are structurally immune to the drift itself** — decision 3 makes the structure *be* the cover
source, so there is one object, one class, one sprite. The remaining obligation is stating the number,
and GG-55 points the same way.

### 10. Structures do not receive cover

`combatant-kind`: structures get no aura, buff or debuff. A wall does not hide behind a wall.
Enforced by a guard on the grant, not by convention — a structure entering a cell emits no
`CellEntered`.

---

## Tunables

`data/tuning/siege.v1.json`, `cover.*`. **Contest points, not per-mille**, except the ignore factors.

| Key | Unit | Default | Why |
|---|---|---|---|
| `cover.points.trench` | contest points | `40` | §5.17's own figure |
| `cover.points.trenchRevetted` | contest points | `60` | Balance |
| `cover.points.emplacement` | contest points | `80` | §5.17's own figure |
| `cover.points.rough` | contest points | `15` | Balance |
| `cover.points.blockingAdjacent` | contest points | `30` | Balance |
| `cover.points.gapBehind` | contest points | `25` | Balance |
| `cover.sourceMultiplierMilli.<source>.<kind>` | per-mille | see §3 | Balance — the `(damage source x cover type)` matrix. `melee` and `entry` rows are **0** and structural; the comment says so |
| `cover.ignoreMilli.<element>.<kind>` | per-mille | `fire.trench = 1000`, rest `0` | Balance — §5.17 rule 2 |
| `cover.maxStackPoints` | contest points | `100` | Balance — a **soft** cap (one full sigmoid unit), configurable per `AGENTS.md`. Not a hard ceiling |

**100 points is one sigmoid unit**, so `maxStackPoints = 100` is a legible default: stacked cover can
be worth at most one full unit of contest advantage.

## Numeric types

- Cover values: **`int` flat contest points.** Not per-mille, not bounded above by 1000 — they are
  points on the same scale as `BaseDodge(Θ) = 26·Θ`. **Never `P(Θ)`**: a contest reads `Θ` linearly
  (§2 rule 4), and the flat value is what makes cover equally decisive at every `Θ`.
- Source multipliers and ignore factors: **`int` per-mille**, bounded 0..1000 — exempt ratios, stated
  in their comments. **The divide by 1000 happens once, last**, after every multiply (`CLAUDE.md`
  rule 4).
- Stacked cover: **`int`**, summed as points before any multiplier is applied.
- **No `float` anywhere.** The contest resolves an integer point difference through the sigmoid; a
  float cover value would reorder outcomes differently on different runtimes.

## Boundaries

**Always:** pair every `CellEntered` with a `CellExited` · **contest points, never per-mille, never
`P(Θ)`** · every cover value in tuning · integer arithmetic throughout · show the contribution on the
wire, not just the total.

**Ask first:** a sixth `ScopeMembershipTransition` value — this module spends the program's one
allowed vocabulary change · making `cover.maxStackMilli` a hard cap.

**Never:** a per-hit cover lookup that bypasses the effect system · cover on a structure · a `float`
dodge value · a `Math.Min` that silently clamps rather than a documented soft cap · **cover expressed
as a per-mille of anything** · a turn-counting dig-in bonus (§5.17 rule 1: *"do not add passive dig-in
that grows with turns stationary"* — decision 14 already prices building as an action, and free
entrenchment would be a second unpriced path to the same bonus).

---

## Testing

| Test | Asserts |
|---|---|
| `Cover_grants_and_revokes_on_cell_entry_and_exit` | the core loop |
| `Every_cell_entered_is_paired_with_a_cell_exited` | over a 50-round battle including deaths and withdrawals — **the leak test** |
| `Death_emits_cell_exited` | one of the three emit sites |
| `Withdrawal_emits_cell_exited` | the third, and the one most likely to be forgotten |
| `Melee_ignores_cover_entirely` | every terrain |
| `Gap_covers_against_direct_but_not_area` | the reason `Gap` exists, end-to-end |
| `Blocking_between_shooter_and_target_grants_cover` | the LOS trace |
| `Requires_line_of_sight_blocks_the_shot_outright` | its first reader — **and a companion test asserting the field is genuinely unread today**, or report a wiring gap |
| `Structures_receive_no_cover` | `combatant-kind`'s rule, enforced |
| `Cover_stacks_up_to_the_soft_cap_and_the_cap_is_configurable` | not a hard stop |
| `Dodge_channel_is_read_by_the_existing_calculator` | through `OverlayCombatCalculator`, not a parallel path |
| `Existing_membership_consumers_ignore_the_new_transitions` | `BattlefieldOwnSideReactor.cs:75-86` switches on three values — assert the two new ones fall through harmlessly rather than throwing |
| `Cover_is_expressed_in_contest_points_not_per_mille` | **the correction**, asserted — +40 moves the sigmoid by 0.4 units |
| `Flat_cover_is_equally_decisive_at_theta_1_and_theta_200` | the ladder argument, as a test |
| `Fire_ignores_trench_cover` | §5.17 rule 2 |
| `An_exhausted_occupant_gets_less_from_cover` | §5.17 rule 4 — **and a companion test proving exhaustion actually reaches `combat.dodge.omni`**, or report a wiring gap |
| `The_wire_carries_the_cover_contribution_not_just_the_total` | §5.17 rule 5 |
| `Mines_ignore_cover` | the `Entry` source row |
| `No_turn_counting_dig_in_bonus_exists` | §5.17 rule 1, by source scan |
| `All_goldens_byte_identical_with_no_board` | no cover without a board |

**The `BattlefieldOwnSideReactor` test is the one a first implementation misses.** Adding enum values
to a type another module `switch`es on is exactly where a silent default or an unhandled-case throw
appears.

## Success criteria

1. Cover grants and revokes correctly, with zero leaks over a full battle.
2. Every cover value lives in tuning; the code contains no bare cover literal.
3. `Gap` and `Blocking` behave differently, proven.
4. `RequiresLineOfSight` has a reader, or is reported as a wiring gap with `file:line`.
5. Existing `ScopeMembershipTransition` consumers are unaffected.
6. All goldens byte-identical with no board.

## Open questions

**One, and the owner should settle it.** Does cover apply against the **attacker's** accuracy or the
**defender's** dodge?

They are numerically equivalent in a single-roll system and they diverge the moment anything else
reads either channel — a demon with high innate dodge stacks differently from one whose attacker is
being penalised.

**Recommendation: the defender's dodge**, i.e. `combat.dodge.omni` as specced. It is the channel that
already exists and is already read, it makes cover visible in the defender's own stat panel (which is
where a player looks to understand why they survived), and it stacks with innate dodge by the rules
the game already has rather than by a new interaction. **Safe to build against; reversible by moving
the grant to the accuracy channel if playtest disagrees.**
