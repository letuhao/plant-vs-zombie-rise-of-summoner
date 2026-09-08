# Spec: `siege-engagement`

**Module 20 of 29 · level 7b · depends on `siege-resolver` (7), `siege-objective` (3b) · [base-defense-map.md](../base-defense-map.md)**
**Status:** spec, 2026-09-04. **Added by the completeness audit** — decision 24's multi-turn loop had
no module, and `siege-resolver` treated a district assault as one battle that always resolves to a
winner.

---

## Objective

**A siege is many engagements, one per map turn.**

Decision 24, and its reasoning is a rule the repo already holds:

> *"Batch waves cycle **within one engagement**, not across map turns. A map turn resolves one
> engagement; inside it the field cap cycles batches until one side is spent or the objective falls.
> **A siege spans turns because engagements repeat, not because waves do.** The owner's reason is the
> rule the repo already holds: 'HOMM3 and other games have different turn for world map and each
> battle — explicit boundary, easier to define and code.'"*

So there are **three nested clocks**, and conflating any two is the defect this module exists to
prevent:

```
map TURN        one per world step        one engagement
  └─ engagement   many rounds             batches cycle until spent / objective falls
       └─ ROUND     the battle kernel's   one MaxRounds horizon
```

**Success looks like:** an assault that neither takes the Core nor breaks resolves as *inconclusive*,
the world advances one turn, both sides act on the map, and next turn the assault resumes against a
district that changed in between.

---

## Why this is a module and not a paragraph

`siege-resolver` returns a `BattleOutcome` with a winner. Decision 24 says an engagement may end with
neither — and that is not an edge case, it is **the normal outcome of a real siege**. Without it:

- The playability audit's finding stands unfixed: *"Turn 4 is turn 1 with less HP. No positional
  progress survives; re-engagement is automatic (`MovementPhase.cs` calls `ContactResolver`
  unconditionally); and `BattleSideOutcome.Routed` 'keeps the field it is on' — the loser cannot
  withdraw."* Decision 26 closed the *multi-turn* half of that; **this module is where "closed" gets
  built.**
- Every siege runs to `MaxRounds` and yields `Stalemate`, which is F2's failure wearing a different
  hat.

---

## What already exists (verified at HEAD, 2026-09-04)

**Built.**

- `BattleOutcome.WinnerEntityId` is **already nullable** — *"Null when nobody won — mutual destruction,
  or a guard that held."* The vocabulary for "no winner" exists; nothing acts on it as a *resumable*
  state.
- `BattleSideOutcome.Routed` — *"Beaten but alive: it keeps the field it is on and loses next turn's
  orders."* **Keeping the field is exactly right for a siege** and wrong for the withdraw case, which
  is why `siege-seam` adds `Withdrawn`.
- `SectorPhase.Besieged` — **declared and unused.** §11.6 recorded it as *"left unused as derivable
  state"*, and this module is where that judgement gets re-examined.
- `MovementPhase` → `ContactResolver` — re-engagement is automatic and unconditional, which is what
  makes a siege repeat without a new trigger.
- `TurnEngine.Step`'s phase order — `Sieges` (phase 3) runs before `Pressure` (phase 6), which is why
  `siege-supply`'s exemption is a prerequisite of this loop and not a follow-up.

**Real gap.** Nothing represents "this siege is ongoing".

---

## The contract

### 1. Three exits, and only one of them ends the siege

```csharp
/// <summary>
/// How one ENGAGEMENT ended. Distinct from how the SIEGE ends — a siege ends only on CoreTaken or
/// AssaultBroken; every other exit means "again next turn".
/// </summary>
public enum EngagementExit
{
    /// <summary>The Core fell. The siege ends; the base changes hands.</summary>
    CoreTaken,
    /// <summary>Every animate attacker is dead or withdrawn. The siege ends; the base holds.</summary>
    AssaultBroken,
    /// <summary>Batches exhausted or the round horizon reached with both sides alive.
    /// **The siege continues.** The normal outcome of a real siege.</summary>
    Spent,
    /// <summary>The attacker chose to leave, whole. siege-seam's `Withdrawn` (F5). The siege ends
    /// with no capture and NO ROUT PENALTY — a raid that got what it came for.</summary>
    Withdrawn
}
```

`Spent` is the addition. `siege-objective`'s `Inconclusive` maps onto it.

### 2. What persists between engagements — and what must not

This is the module's real specification, and it is constrained by a rule §2 rule 7 already fixes:
**combat is stateless between turns.**

| Persists | Where | Why |
|---|---|---|
| Structure HP and destruction | `WorldSlot.StructureHp` (`structure-state`) | Already hashed world state. A breached wall stays breached |
| Slot depletion | `WorldSlot.SlotDepletionMilli` | Same |
| Legion members and wounds | `BattleSideOutcome.Survivors` → `BattleApplication.Apply` | The existing path, unchanged |
| Terrain changes | `structure-state`'s conditional row | A dug moat is a Rampart and it is world state |
| **That a siege is ongoing** | `SectorPhase.Besieged` — **see §3** | The one new fact |

| Does **not** persist | Why |
|---|---|
| Unit positions on the board | **Battle state.** Persisting it would make combat stateful between turns — the seam violation `siege-stage` also refuses for its disconnect case |
| Round number, initiative, cooldowns, statuses | Same |
| Board income earned (`siege-economy`'s depot) | Battle-scoped by design — *"a quarry seized for six rounds should pay for six rounds of walls, not enrich your empire forever"* |

**So each engagement re-deploys from `district-layout`'s deterministic placement.** That is not a
limitation dressed up as a feature: it is what makes each turn a fresh tactical problem against a
district that changed, rather than the *"turn 4 is turn 1 with less HP"* the audit named.

### 3. `SectorPhase.Besieged` — derivable, and this module keeps it that way

§11.6 recorded it as *"left unused as derivable state"*. **That judgement holds**, and the reason is
`SupplyGraph`'s own, which `siege-supply` quotes: a stored flag *"is exactly the kind of derived state
that goes stale the first time a lane is cut, and it would then be wrong in the one situation the
player cares about."*

```csharp
/// <summary>
/// Whether this sector is under siege — DERIVED every turn, never stored. An enemy force standing in
/// a sector with a Seat the faction still owns IS the siege; there is no second fact to record.
/// Deriving it also means an attacker who marches away ends the siege for free, with no cleanup step
/// that could be missed.
/// </summary>
public static bool IsUnderSiege(WorldState world, string sectorId);
```

**`SectorPhase.Besieged` is set from it for display**, not read as truth. Zero new hashed state, so
this module moves **no golden at all** — which is what lets it sit at level 7 beside the resolver
rather than back at the golden-locked landing.

### 4. Re-engagement is automatic, and that is already true

`MovementPhase` calls `ContactResolver` unconditionally. An attacker still standing in the sector next
turn fights again with **no new trigger and no new command**. The `DistrictAssaultPhase`
(`siege-seam`) fires on the `Assault` command for a *fresh* assault; a *continuing* one needs nothing.

**Leaving is the only action that ends it.** March out — an ordinary move order — and the siege is
over because `IsUnderSiege` derives false. That is the withdraw verb F5 asked for, at the map scale,
using machinery that already ships.

### 5. The turn/round boundary — stated, not linked

> ### ⛔ Map step = **turn**. Battle step = **round**. Engagement = one turn's worth of rounds.
>
> Restated here in full because a downstream session reads this doc rather than its links, and this
> module is the one place all three clocks are in scope at once.
>
> **Never convert between them.** No UI element, report line, log entry or API field may express a
> round count as a turn count or vice versa. `siege-stage` shows both, separately labelled.
>
> A siege lasting six turns is **six engagements**, each of many rounds. It is not "a battle with 300
> rounds", and it is not "six rounds".

### 6. Reporting — one line per engagement

```csharp
report.Add(phase, TurnReportKinds.Battle, request.BattleId,
    $"district:{sectorId}:{exit}", sectorId: sectorId);
```

Through `BattleReporting.Fight`, the single funnel, so *"a battle always costs the same and always
shows up in the report the same way"*. A six-turn siege is six report lines the player can read as a
narrative — which is worth more than one summary line at the end.

---

## Tunables

| Key | Unit | Default | Why |
|---|---|---|---|
| `engagement.maxPerSiege` | engagements | **unset (unlimited)** | **Deliberately unset, and it must stay that way.** A cap here would be a hard progression ceiling on how long a player may besiege — precisely what `AGENTS.md` forbids. A siege ends when someone wins or leaves, never on a timer |

That single row is the whole tunable surface, and its default is "no limit".

## Numeric types

No new magnitudes. Engagement counts are `int` and unbounded in practice — if a count is ever
displayed, it is presentation, never a gate.

## Boundaries

**Always:** derive `IsUnderSiege`, never store it · re-deploy each engagement from
`district-layout` · route every engagement through `BattleReporting.Fight` · keep rounds and turns
separate everywhere.

**Ask first:** persisting anything battle-scoped between engagements · capping engagements per siege.

**Never:** store board positions in `WorldState` · convert rounds to turns · treat `Spent` as a loss
for either side · give `Withdrawn` a rout penalty · a stored `IsBesieged` field.

---

## Testing

| Test | Asserts |
|---|---|
| `Spent_engagement_leaves_the_siege_ongoing` | **decision 24**, the core case |
| `Structure_damage_persists_between_engagements` | a breached wall stays breached |
| `Board_positions_do_not_persist` | **the seam.** Assert `WorldState` holds no cell data after an engagement |
| `Each_engagement_redeploys_deterministically` | same placement, same district |
| `A_district_that_changed_produces_a_different_fight` | *"turn 4 is turn 1 with less HP"*, prevented — build a wall between engagements and assert the second differs |
| `Marching_away_ends_the_siege_with_no_cleanup` | derived state, so nothing to reset |
| `Is_under_siege_is_never_stored` | source scan — no `IsBesieged` field on `WorldSector` |
| `Core_taken_ends_the_siege_and_transfers_the_base` | terminal 1 |
| `Assault_broken_ends_the_siege_and_the_base_holds` | terminal 2 |
| `Withdrawn_ends_the_siege_with_no_rout_penalty` | **F5**, at map scale |
| `Six_turn_siege_produces_six_report_lines` | the narrative |
| `A_siege_has_no_turn_limit` | no hard ceiling — run 200 engagements |
| `Rounds_are_never_reported_as_turns` | the boundary, asserted on the wire |
| `World_goldens_byte_identical` | **the gate** — this module adds no hashed state |
| `Besieged_garrison_survives_across_engagements` | integration with `siege-supply`'s F1 fix — the reason that module is a prerequisite |

## Success criteria

1. An inconclusive engagement leaves the siege ongoing and the world advances one turn.
2. World state persists across engagements; battle state provably does not.
3. `IsUnderSiege` is derived, never stored — proven by scan.
4. Marching away ends a siege with no cleanup step.
5. No engagement cap exists.
6. Rounds and turns are never conflated on any wire or report.
7. Zero world goldens moved.

## Open questions

None. Decision 24 is explicit and the persistence split falls out of §2 rule 7.
