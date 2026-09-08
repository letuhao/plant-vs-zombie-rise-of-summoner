# Spec: `siege-seam`

**Module 6 of 29 · level 2 · depends on `siege-board` · [base-defense-map.md](../base-defense-map.md)**
**Status:** spec, 2026-09-04.

---

## Objective

**Widen the world↔combat seam so a siege can cross it, and prove nothing else moves.**

`BattleRequest` / `BattleOutcome` / `IBattleResolver` already exist and already carry a battle across
the world/combat boundary. A siege needs three things they do not have: a **board**, **per-slot
results**, and a verb for **withdrawing**.

This module is where the golden risk concentrates, and it is also where the risk turns out to be
**zero** — verified, not assumed. `BattleRequest` and `BattleOutcome` are transient turn-time data:
they are constructed by `MovementPhase`/`SiegePhase`, consumed by `BattleApplication`, and dropped.
**Neither appears in `WorldCanonical`, and neither is persisted.** Only their *effects* on
`WorldState` are hashed.

**Success looks like:** the seam carries a siege, and every world golden is byte-identical, unblessed.

---

## ⛔ Name collision — found by Gate 0, and it changes this module

**`SiegePhase` already exists** (`src/FusionRpg.Core/World/Turn/SiegePhase.cs`) and means something
different: clearing a **slot guard**, driven by `WorldCommandKinds.Clear`, producing
`BattleKinds.Guard`. Its own doc comment is explicit — *"attacking what defends a slot rather than
what defends the ground"*.

The capability map did not catch this. The resolution:

| | Keeps | Gets |
|---|---|---|
| Existing `SiegePhase` | its name, its meaning, `BattleKinds.Guard` | nothing — **it is not modified** |
| This program | the `siege-*` **module ids** (doc-level, no collision) | a new `BattleKinds.District` and a new `DistrictAssaultPhase` |

**Do not extend `SiegePhase`.** A guard fight and a district assault differ in every dimension —
trigger, board, participants, win condition, duration. Merging them produces one method with two
modes, which is exactly the *"adding a mode should mean adding a row, never a branch"* failure this
program's level-0 module exists to avoid.

The player-facing word stays "siege". The code word for the new thing is **district assault**.

---

## What already exists (verified at HEAD, 2026-09-04)

**Built.**

- `BattleRequest` (`World/Turn/BattleSeam.cs`) — `BattleId`, `Kind`, `LocationId`, `TimeMilli`,
  `AttackerEntityId`, `DefenderEntityId`, `DefenderStationary`, `GuardWaveId`, `SlotIndex`.
- `BattleSideOutcome` — `EntityId`, `Survivors`, `Routed`, `Destroyed`.
- `BattleOutcome` — `BattleId`, `WinnerEntityId`, `GuardCleared`, `Sides`.
- `IBattleResolver.Resolve(request, combatants, seed)` — one method.
- `BattleKinds` — `Sector`, `Lane`, `Guard`, and `IdFor(turn, kind, location, attacker, defender)`,
  deliberately colocated *"so movement and sieges cannot drift into two different formats"*.
- `BattleReporting.Fight` — **the single funnel.** Both existing entry points go through it; it
  resolves, applies, conditionally clears a guard, and writes one report line.
- `BattleApplication.Apply` / `.ClearGuard` — *"whatever fights the battle, only this file decides
  what a rout or a wipeout means to the map, and there is exactly one of it."*
- `PlaceholderBattleResolver` — the only shipped implementation.

**Verified unhashed.** `WorldCanonical.Write` reads `WorldState` only. `BattleRequest`/`BattleOutcome`
never reach it. **Confirm this by reading `WorldCanonical.cs` at implementation time** — it is the
claim the whole module's zero-golden-risk rests on, and `DESIGN-GATE.md` rule 3 says test the
constraint rather than declare it.

**Real gaps.** No board on the request. No per-slot results. **No withdrawal verb** (audit F5) — the
outcome vocabulary is `Routed` / `Destroyed` / winner, and a raid that takes what it came for and
leaves is none of those.

---

## The contract

### 1. `BattleRequest` gains a board projection

```csharp
/// <summary>
/// The board this is fought on, or null for every battle kind that has none — which is all three
/// existing kinds. **Null is the default and the existing path**, so a sector fight, a lane meeting
/// and a guard clear construct exactly the record they construct today.
/// </summary>
public BoardProjection? Board { get; init; }

/// <summary>
/// What the world hands the combat module about the ground. Deliberately a PROJECTION rather than a
/// GridSpec: the world says which sector and which edge, and the combat side derives the grid from
/// district-layout. Passing a materialised grid across the seam would make the world module own a
/// board representation, which is the coupling BattleSeam.cs's own header refuses ("the world module
/// never learns anything about rounds, decks, or damage").
/// </summary>
public sealed record BoardProjection
{
    public string SectorId { get; init; } = "";
    public ulong WorldSeed { get; init; }
    public BoardEdge AttackerEdge { get; init; }
    /// <summary>Slot index → what stands there. Empty is legal — a district with no structures.</summary>
    public IReadOnlyList<SlotProjection> Slots { get; init; } = Array.Empty<SlotProjection>();
}
```

**The world sends inputs, not a grid.** `district-layout` is a pure function of those inputs, so both
sides can derive the identical board without the world module depending on `Core/Battle`. This also
keeps the dependency arrow pointing the way the map drew it.

### 1b. `BattleRequest` also carries the DEPOT BUDGET

§5.13's own diagram, and it is the mechanism that keeps *"combat never writes world state"* true while
letting a side build during a battle:

```text
world turn  --BattleRequest{ budget }-->  siege board
                                          spends internally
            <--OutcomeRecord{ spent }--   world debits
```

```csharp
/// <summary>
/// What each side may spend during this battle. Null for every battle kind without a board.
///
/// <para><b>An in-battle build may NOT debit `WorldSector.LoamStock` or `WorldEntity.CarriedLoam`
/// directly</b> — §2 rule 7: "Combat never writes world state. It does not claim sectors, spend
/// shards, or move legions." The budget crosses in, the SPEND crosses back, and only the world
/// debits. siege-economy owns the reconciliation.</para>
/// </summary>
public IReadOnlyList<SideBudget>? Budgets { get; init; }
```

**The asymmetry is authored here, not invented downstream** (§5.13):

| Side | Source | Consequence |
|---|---|---|
| **Defender** | the sector's own `LoamStock` / `RubbleStock` / `IronworkStock` — at home, supplied | **Blockading production is how an attacker stops them rebuilding** |
| **Attacker** | `WorldEntity.CarriedLoam` — what the legion marched in with | Finite, and why decision 27's other three paths exist |

Still unhashed and unpersisted, so still zero goldens.

### 2. `BattleSideOutcome` gains a withdrawal, and it is not a rout

Audit **F5**. The three existing terminal states cannot express *"I came for the granary, I burned it,
I left intact."*

```csharp
/// <summary>
/// Left the field deliberately, whole. **Distinct from <see cref="Routed"/>**, which is "beaten but
/// alive and loses next turn's orders", and from <see cref="Destroyed"/>. A raid that achieves its
/// objective and withdraws has not been beaten and must not be penalised as though it had — that
/// penalty is precisely what would make raiding a dominated strategy nobody ever picks.
/// </summary>
public bool Withdrawn { get; init; }
```

**`BattleApplication.Apply` must handle it**, and the handling is the *absence* of a penalty:

```csharp
if (side.Destroyed) continue;

entities.Add(entity with
{
    Members = side.Survivors,
    // Withdrawn is NOT routed. This line is the whole feature: a withdrawing force keeps its orders.
    Routed = entity.Routed || (side.Routed && !side.Withdrawn)
});
```

**Mutual exclusivity is validated, loudly.** `Withdrawn && Destroyed` is incoherent, and a resolver
that produces it has a bug that would otherwise show up as a ghost army. Throw at `Apply`.

### 3. `BattleOutcome` gains per-slot results

```csharp
/// <summary>
/// What happened to each slot on the board. Empty for every battle that has no board — the same
/// default-is-today's-behaviour discipline as <see cref="BattleRequest.Board"/>.
/// </summary>
public IReadOnlyList<SlotOutcome> SlotResults { get; init; } = Array.Empty<SlotOutcome>();

public sealed record SlotOutcome
{
    public int SlotIndex { get; init; }
    /// <summary>Remaining structure HP. **long** — a structure's HP is a magnitude contentScale
    /// touches, and CLAUDE.md's rule 1 is unconditional for those.</summary>
    public long StructureHp { get; init; }
    public bool StructureDestroyed { get; init; }
    /// <summary>Who ended the battle occupying it — possession is by occupation (decision 4:
    /// buildings have no ownership). Null means nobody.</summary>
    public string? HeldByFactionId { get; init; }
}
```

### 4. `BattleReporting.Fight` grows a third application step

It already has two conditional post-steps (`Apply`, then `ClearGuard` when `GuardCleared`). Slots are
the third, in the same shape:

```csharp
var next = BattleApplication.Apply(world, outcome);

if (outcome.GuardCleared && request.SlotIndex is { } slotIndex)
    next = BattleApplication.ClearGuard(next, request.LocationId, slotIndex);

// New, and empty for every existing kind — so every existing battle takes the identical path.
if (outcome.SlotResults.Count > 0)
    next = BattleApplication.ApplySlotResults(next, request.LocationId, outcome.SlotResults);
```

**`BattleReporting.Fight` stays the single funnel.** Its own comment — *"Both places that start a
fight ... go through here, so a battle always costs the same and always shows up in the report the
same way"* — is the reason the new phase must not bypass it.

### 5. `BattleKinds.District` and a fourth entry point

```csharp
/// <summary>
/// An assault on the district around a Seat (base-defense-ideal.md decision 26). Distinct from
/// <see cref="Guard"/>: a guard defends one slot and is cleared by a `clear` order; a district
/// assault is fought on a board for the legions standing in its core.
/// </summary>
public const string District = "district";
```

`DistrictAssaultPhase` — a new file, modelled on `SiegePhase`'s structure (command loop, the same
`Drop` reason strings, `BattleReporting.Fight` at the end), driven by a new
`WorldCommandKinds.Assault`.

**`BattleReporting.Fight`'s `sectorId` line needs one look.** It currently reads:

```csharp
sectorId: request.Kind == BattleKinds.Lane ? null : request.LocationId
```

A district assault's `LocationId` is a sector id, so it falls on the correct side of that ternary
already. **Verify, do not assume** — the comment above that line records that putting a lane id in the
sector slot is *"exactly the class of bug world-stage W13 exists to fix"*.

### 6. What this module does not do

It **widens** the seam. It does not implement `IBattleResolver` (that is `siege-resolver`), does not
generate a board (`district-layout`), does not persist structure HP (`structure-state`). The new
fields are all defaulted such that every existing caller constructs the record it constructs today.

---

## Tunables

**None.** A data-shape module. If a number appears here, it is in the wrong module.

## Numeric types

| Field | Type | Why |
|---|---|---|
| `SlotOutcome.StructureHp` | **`long`** | a magnitude `contentScale` touches — `CLAUDE.md` rule 1, unconditional |
| `BoardProjection.WorldSeed` | `ulong` | matches `WorldTemplateCatalog.Build` exactly |
| `SlotIndex` | `int` | matches `WorldSlot.SlotIndex` |

## Boundaries

**Always:** default every new field to today's behaviour · keep `BattleReporting.Fight` the single
funnel · keep `BattleApplication` the only file that decides what an outcome means to the map.

**Ask first:** adding a second `IBattleResolver` method (the interface has exactly one, on purpose).

**Never:** modify `SiegePhase` · reuse `BattleKinds.Guard` for a district assault · put a `GridSpec`
on `BattleRequest` · let `Withdrawn` imply `Routed` · construct a `BattleRequest` outside a phase.

---

## Testing

`tests/FusionRpg.Core.Tests/World/Turn/`.

| Test | Asserts |
|---|---|
| `World_goldens_are_byte_identical` | **the gate**, and the whole module's risk in one line |
| `Battle_request_and_outcome_are_absent_from_world_canonical` | the structural proof, not just the golden — a reflection or source scan over `WorldCanonical.Write` |
| `Existing_three_kinds_construct_an_identical_record` | `Board` null, `SlotResults` empty |
| `Withdrawn_is_not_routed` | **F5.** A withdrawing entity keeps its orders |
| `Withdrawn_and_destroyed_together_throws` | incoherent outcome, loud |
| `Withdrawn_round_trips_through_apply` | survivors preserved, `Routed` unset |
| `Slot_results_apply_only_when_present` | empty list takes the existing path exactly |
| `Guard_clearing_still_works_unchanged` | `SiegePhase` untouched, proven |
| `District_kind_puts_a_sector_id_in_the_sector_slot` | the W13 bug class, not reintroduced |
| `Battle_id_format_is_shared` | `BattleKinds.IdFor` used for district too — the drift its colocation exists to prevent |
| `Board_projection_round_trips` | including the empty-slots case |
| `Budget_crosses_in_and_spend_crosses_back` | §5.13's diagram, both directions |
| `Defender_and_attacker_budgets_come_from_different_sources` | the blockade asymmetry |
| `No_battle_path_writes_world_stock_directly` | **§2 rule 7**, by source scan over the resolver namespace |

## Success criteria

1. Every world golden byte-identical, unblessed.
2. `WorldCanonical.Write` has been **read** and its independence from the seam types confirmed by a
   test, not by this document.
3. `Withdrawn` is expressible end-to-end and carries no rout penalty.
4. `SiegePhase.cs` is unmodified — `git diff` on it is empty.
5. `BattleReporting.Fight` remains the only place a battle is started.

## Open questions

None. The `SiegePhase` collision was the one real unknown and Gate 0 resolved it.
