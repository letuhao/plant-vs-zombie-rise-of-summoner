# Spec: `combatant-kind`

**Module 8 of 29 · level 3 · depends on `battle-clock-profile` · [base-defense-map.md](../base-defense-map.md)**
**Status:** spec, 2026-09-04.

---

## Objective

**Let a structure enter a battle without behaving like a demon.**

Owner decision 4: buildings and obstacles are a **new kind of actor** — no level, no equipment, no
aura, but they have traits and actions. Two consequences the battle kernel does not currently allow
for:

1. **A wall must not take a turn.** It has no initiative and nothing to decide.
2. **A wall must not keep a battle alive.** `AnyActive("wave")` is what ends a battle. If an
   indestructible fence counts as an active enemy, the battle never ends — it hits `MaxRounds` and
   stalemates, every time, for every siege.

**Success looks like:** a structure is a real, targetable, damageable participant that never appears
in initiative and never prevents a victory — and every existing battle golden is byte-identical.

---

## What already exists (verified at HEAD, 2026-09-04)

**Built.**

- `BattleActorSetup` (`src/FusionRpg.Core/Battle/BattleModels.cs:7`) — `Key`, `Side`, `SpeciesId`,
  `TypeId`, `Level`, `ElementPrimary/Secondary`, `TraitIds`, `MaxHp`, `Atk`, `Defense`,
  `ChannelMods`.
- **Two `[JsonIgnore]` precedents on this exact record**, and both record the same incident:
  - `Index` (`:23-24`) — *"a first draft without it moved `ExpeditionResolverTests.Tier_goldens_are_locked`'s
    hash, because System.Text.Json serializes get-only computed properties by default."*
  - `SpecimenId` (`:40-41`) — *"`[JsonIgnore]` for the identical reason `Index` already carries one."*
- `BattleRunState.AnyActive(side)` — the battle's liveness test.
- The forced-basic-attack path — an actor with no loadout falls back to a single hand-built basic
  attack (`BasicAttack.cs`).
- `BattleModeProfile.OrdersBySpeed` — shipped (Gate 0 correction), so *whether* speed decides order is
  already data.

**Real gap.** Nothing distinguishes a combatant from a fixture. Every `BattleActorSetup` is assumed
animate.

---

## ⛔ Gate 0 corrected this module's central mechanism

The capability map specified `[JsonIgnore(WhenWritingDefault)]`. **The shipped precedent on this exact
record is plain `[JsonIgnore]`, twice**, and both comments record *why*: expedition tier resolution
serializes `BattleActorSetup` as part of a golden hash, and any newly-serialized member moves it.

`WhenWritingDefault` would serialize the field the moment it is non-default — which is precisely every
siege. That is strictly worse than the shipped answer:

- It moves no golden **today** (structures are always default in existing battles),
- and then moves one **later**, at the first siege, from a module that has long since shipped.

A hash that moves on a delay is worse than one that moves now, because the module that caused it is no
longer the module being debugged.

**Use plain `[JsonIgnore]`.** It matches two shipped precedents in the same file, and it is correct
because the kind is a *construction-time* property: a setup is built fresh from world state on every
resolve, so nothing ever needs to read the kind back out of JSON.

---

## The contract

### 1. The discriminator

```csharp
/// <summary>
/// What sort of thing this actor is (base-defense-ideal.md decision 4). Structures and obstacles are
/// a new actor kind: no level, no equipment, no aura — but traits, actions, and hit points.
///
/// <para><b>Plain <see cref="JsonIgnoreAttribute"/>, matching <see cref="Index"/> and
/// <see cref="SpecimenId"/> exactly.</b> Both of those carry one because expedition tier resolution
/// serializes this record into a golden hash and any newly-serialized member moves it — found the
/// hard way, twice, and recorded in their own comments. <c>WhenWritingDefault</c> was considered and
/// rejected: it would move that hash on the first siege instead of never, which is the same defect
/// with a delay long enough that the responsible module is no longer suspected.</para>
///
/// <para>Safe to ignore because the kind is construction-time: a setup is built fresh from world
/// state on every resolve, so it is never read back out of JSON.</para>
/// </summary>
[JsonIgnore]
public CombatantKind Kind { get; init; } = CombatantKind.Animate;

public enum CombatantKind
{
    /// <summary>A demon, a legion member, anything that takes turns. Index 0, so the default is
    /// today's behaviour for every existing caller.</summary>
    Animate,

    /// <summary>A wall, a tower, a barricade. Occupies a cell, can be attacked, never acts on its own
    /// initiative. May still act when garrisoned — see §4.</summary>
    Structure
}
```

**Two values, not five.** A richer taxonomy (obstacle vs building vs emplacement) is *content*
identity and belongs to `structure-seed`. What the kernel needs is exactly one bit: does this thing
take a turn.

### 2. `AnyActive` counts only what can act

```csharp
/// <summary>
/// Whether this side still has anyone who can fight. **Structures do not count** — otherwise an
/// indestructible fence on the defender's side keeps every siege alive to MaxRounds and turns every
/// victory into a stalemate. A wall is a fact of the ground, not an enemy that must be beaten.
/// </summary>
public bool AnyActive(string side) =>
    Actors.Any(a => a.Side == side && a.Alive && a.Kind == CombatantKind.Animate);
```

**Every existing actor is `Animate`**, so this predicate returns exactly what it returns today for
every existing battle. That is the byte-identity argument, and it is structural rather than empirical.

**The consequence is a design decision, and it is the right one:** killing every defender wins the
siege even if walls still stand. The alternative — walls must be levelled to win — makes the objective
"demolition" rather than "assault", which is not the mechanic decision 26 describes (the win condition
is *the legions in the central defense area*).

### 3. Structures never enter initiative

`BattleEngine`'s round loop and `ActorTurnMachine` filter to `Animate` when selecting who acts. A
structure has no `TurnReadiness`, is never scheduled, and never passes.

**Under `OrdersBySpeed = true`** (the `siege` profile) this matters more than under `classic-round`: a
speed-ordered queue that includes zero-speed fixtures would put every wall at the back of every round
and burn scheduling work on entities that do nothing. Filtering at selection avoids that entirely
rather than making it cheap.

### 4. Garrisoning — the one way a structure acts

Owner decision (round 6, option 1): *"garrisoned mean take control like make product, control weapon
and attack enemy, not every buiding have control, like a wall."*

The clean expression, and it needs **no new mechanism**:

> **A garrisoned structure does not act. The unit inside it does, with the structure's actions
> available to it.**

```csharp
/// <summary>
/// The animate actor currently occupying this structure, or null. A garrisoned structure lends its
/// actions to its occupant; it never acts on its own initiative, so it never enters the turn queue
/// and CombatantKind.Structure stays a complete statement of "does not take turns".
/// </summary>
public string? GarrisonedBy { get; init; }
```

The occupant's `HeldActionsOf` is the union of its own actions and the structure's. That is a read
through `IBattleView` — **an interface that already exists** and whose doc comment already anticipates
its implementations changing (*"this interface is what confines that change to one implementation
later"*). No signature moves.

A wall has no actions, so garrisoning one grants nothing — which is exactly the owner's *"not every
buiding have control, like a wall"*, expressed as an empty list rather than as a second flag.

### 5. The forced-basic-attack path is gated

`BasicAttack`'s fallback exists so an actor with no loadout can still fight. A wall with no loadout
must **not** acquire a punch.

```csharp
// A structure with no actions has nothing to do — it does not fall back to a basic attack. The
// fallback exists so an animate actor is never inert; a wall being inert is the point.
if (setup.Kind == CombatantKind.Structure && actions.Count == 0) return NoActions;
```

### 6. What a structure does not get

Owner decision 4, restated so it is not lost: structures **have no level, cannot be equipped, and do
not receive aura, buff or debuff** — those scopes serve demon-kind actors.

This module enforces the first two by construction (`Level` is unread for structures; `SpecimenId` is
null so `EquipAtomSource.ModsFor` resolves to nothing). **The buff/debuff scope question is not solved
here** — it is `siege-cover`'s, which introduces the one reviewed vocabulary change the program allows
(a cell-entry/exit `ScopeMembershipTransition`) with a real mechanic behind it — **`siege-obstacles`'
Mine**, not cover. Cover introduced it, then released it when decision 35 replaced terrain cover with
per-shot shooting math; pass 3 caught the gap and reassigned it.

---

## Tunables

**None.** A discriminator and two predicates. A number here would be in the wrong module.

## Numeric types

None introduced. `MaxHp` is already `long` on `BattleActorSetup`, which is what a structure's HP flows
into from `structure-state` — no widening needed, and worth asserting rather than assuming.

## Boundaries

**Always:** plain `[JsonIgnore]` on the kind · `Animate` at index 0 · filter at selection, not by
special-casing inside the turn machine.

**Ask first:** a third `CombatantKind` value (it is almost certainly content identity, and
`structure-seed` owns that).

**Never:** `[JsonIgnore(WhenWritingDefault)]` on this field — see the boxed correction · let a
structure into `AnyActive` · give a structure a level or an equipment lookup · put a structure in the
turn queue "with zero speed" instead of excluding it.

---

## Testing

`tests/FusionRpg.Core.Tests/Battle/`.

| Test | Asserts |
|---|---|
| `All_twelve_goldens_are_byte_identical` | **the gate** — eight battle + four expedition |
| `Expedition_tier_goldens_are_locked` | the existing test the two `[JsonIgnore]` comments name by name. It is the specific canary; run it explicitly |
| `Kind_is_not_serialized` | serialize a `Structure` setup, assert `"Kind"` is absent from the JSON |
| `Structures_do_not_count_toward_any_active` | a side of nothing but walls is not active |
| `Battle_ends_when_all_animate_defenders_die` | with walls still standing — the stalemate, prevented |
| `Structures_never_appear_in_initiative` | over a 50-round battle, assert zero turns taken |
| `Structure_with_no_actions_gets_no_basic_attack` | §5, directly |
| `Garrisoned_structure_lends_actions_to_its_occupant` | union of both lists |
| `Garrisoning_a_wall_grants_nothing` | the owner's *"not every buiding have control"* |
| `Garrisoned_structure_still_takes_no_turn` | the structure itself remains inert |
| `Structures_are_targetable_and_damageable` | inert ≠ invulnerable |
| `Structure_hp_is_long_end_to_end` | no narrowing between `WorldSlot.StructureHp` and `BattleActorSetup.MaxHp` |

## Success criteria

1. All twelve goldens byte-identical, unblessed — `Tier_goldens_are_locked` named explicitly.
2. `Kind` is absent from serialized output.
3. A siege with surviving walls and no surviving defenders **ends**.
4. Structures are targetable and damageable but take zero turns.
5. Garrison lends actions without the structure ever entering the queue.
6. `Core/Battle` passes the Gate-0-extended determinism guard.

## Open questions

None. The `WhenWritingDefault` question was open in the map and Gate 0 answered it from two shipped
precedents in the same file.
