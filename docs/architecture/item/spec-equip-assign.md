# Spec: `equip-assign`

**Module id:** `equip-assign` · **Program:** [item](../item-map.md) · **Build order:** 4 of 21
**Depends on:** `durable-ownership` (1), `armoury` (2), `slot-roles` (3)
**Rulings:** D5, D19 · decision [decision-d1-durable-ownership.md](decision-d1-durable-ownership.md)

## Objective

**Equipping is two acts: assign, then bind.** Own the durable half, rebuild the session half as a
projection, retire the three-item stub — and do it without deleting a shipped player feature.

## Design

### Assign is durable and ours; bind is session-scoped and E6's

`decision-d1-durable-ownership.md` corrected item-ideal §6.4's *"equipping = create a binding"*,
because **no owner scope durably named a specimen**. Five lanes hit it independently.

| Act | Owner | Lifetime |
|---|---|---|
| **Assign** — this player put this item in this role on this specimen | **this module**, `rpg_item_assignment` | survives restarts, deployments, recoveries |
| **Bind** — the runtime shadow | E6 | rebuilt as a **full projection** at deploy, never as a delta |

**This is the shipped architecture, not a workaround:** `UpsertUniqueEquipment` already rebuilds
rather than deltas, and `UniqueOwnerBinder.ToEntityKey` already discards the instance id at deploy.
It also makes unequip atomic — one row deleted, no second writer.

**And the durable owner scope now exists:** `OwnerKind.UniqueActor`, approved 2026-09-02 and in
`decisions.md`. §6.4 rejected `actor:{instanceId}` after tracing it end to end; that trace was
correct then and the scope was subsequently designed properly.

⚠ **Declared dependencies, reconciled.** The header said `1, 3` while the body reads three modules.
`rpg_item_assignment` carries `ref_kind ∈ rolled | stock` with `ref_id` an `instance_id` when rolled and
a **`container_id` when stock** (I13 §4.4) — so every stock cell points into `rpg_item_stock`, which is
**module 2's** table, and `StockDepleted` is a refusal this module raises against module 2's counter.
Module 3 supplies the `role` vocabulary *and* the unlock predicate this gate must consult (below).
⚠ `item-map.md:120` still lists module 4's dependencies as `1, 3` and needs the same correction.

### ⛔ Retiring the stub must not delete relics

The map says this module *"retires `rpg_unique_equipment` and the 3-item `UniqueEquipmentCatalog`
stub."* **An audit found a shipped player feature riding on exactly that pipeline.**

`RelicCatalog.cs:8` states it plainly: *"Equipping **reuses the existing per-actor
`rpg_unique_equipment` pipeline** via `UniqueEquipmentCatalog.IsKnownItem` / `TryGetGrant`."* Four
relics with `Rarity` and `Slot`, served at `/api/relics` (`RelicEndpoints.cs:15`), rendered by
`web/fusion-rpg-web/src/layers/relics/RelicsLayer.tsx`. No item module named them and no exclusion
covered them.

**So this module owns their disposition, and it is a decision, not a migration detail:**

| Option | Consequence |
|---|---|
| Relics become **base types** (module 6) | they join the item system properly; `/api/relics` folds into the armoury |
| Relics become **uniques** (module 17) | they keep their hand-authored character and break generator rules on purpose |
| Relics are **retired** | the FE layer and endpoint go with them |

**Recommended: uniques.** A relic is hand-authored, few, and characterful — which is what G1 is for.
⚠ Either way, **the row migration for existing `rpg_unique_equipment` data is this module's**, and
`RpgStore.UniqueActors.cs:606,645,654` read and write that table today.

### D19's surviving half — the equip gate — lands here

D19 split I11: per-species aptitude vectors went to the demon program, and *"the equip gate: **frame +
level**, and any faction clause"* stayed. **No module claimed it.** It is a bind-time refusal with a
reason payload, so it belongs beside the assignment that triggers it.

⚠ Distinct from module 3's unlock predicate: that asks *"does this actor have this slot?"*; this asks
*"may this actor wear this item?"*

⚠ **`level_req` is enforced nowhere today** (`atom-layer-handoff.md` §2, A4) — **and the shape of the
gap is exact.** `BindGate.cs:53-54` implements the arm and `AtomRejectionReason.LevelTooLow` exists
(`AtomRejection.cs:116`), but `OwnerLevel` defaults to `null` (`BindGate.cs:17`) and the **only**
production construction is `RpgHub.cs:107` — `new BindContext(RuntimeId.Lawn)`, no `OwnerLevel`, no
`levelReq` argument. Every other call site that supplies one is a test (`BindGateTests.cs:191,199`).
So the arm is unreachable, not missing: a **wiring gap**, and the writer for `OwnerLevel` is this
module's projector.

⚠ **Which level does it compare against** — `rpg_unique_actors.level` (specimen) or the account? I11
§10.6 leaves it open. Recommended: **specimen**, because `level_req = itemLevel − 2`
(`ssot-generation.md` §4.1) exists so *"you should always be able to wear what the content you just
beat dropped"*, and that content was beaten by a specimen.

### ⛔ The unlock predicate must be consulted here, or module 3 built it for nobody

Module 3 ships `SlotUnlock.IsUnlocked(role, actor)` defaulting to open and tests that a configured rule
**can** close a slot (`spec-slot-roles.md:126` `a_configured_rule_can_close_a_slot_without_a_migration`).
**Nothing calls it.** This spec's gate tests covered frame, level and faction and never mentioned it —
a predicate built and never consulted is precisely the wiring gap `CLAUDE.md` names, and it would ship
green on both sides.

**So the assign path checks the predicate first**, before frame/level/faction: a role the predicate
closes is not a slot this specimen has, and asking *"may this actor wear this item?"* of a cell that
does not exist yet is the wrong question in the wrong order.

⚠ **A reason code is owed.** I13 §6's fourteen proposed codes have no arm for *the role exists on this
frame but the predicate closes it* — `RoleNotOnFrame`'s remedy is *"wrong specimen"*, which is a
different sentence to the player. Proposed: **`RoleLocked`**, remedy *"unlock this slot"*, matching the
gap board's own `locked` cell state (I13 §5.9). That is a fifteenth against a closed list, so it is an
**Ask first**, not a decision taken here.

### The three I11 mechanisms nobody owned

D19 kept *"frame + level and any faction clause"* here and moved the per-species attribute vectors to
the demon program. Three mechanisms sat in the gap between those halves. Each is settled below; one of
them is settled by **refusing** it.

#### 1. A requirement that lapses — the item stays on

I11 §2.6 rejects all three shipped answers (force-unequip cascades, do-nothing makes the requirement
decorative, blocking the cause makes gear immune to debuffs) and picks a fourth: **requirements are read
at the transition; a lapse never unbinds.** On the axes D19 left here, a lapse is narrow and real:

| Cause | Real on our axes? |
|---|---|
| **Level demotion** | **Yes.** `rpg_actor_progression` carries `highest_level` and `demotion_count` (`RpgStore.cs:361-362`) — levels in this repo go down |
| **A content revision raising `level_req`** | **Yes**, and it is the nastiest: it strands already-equipped copies |
| Frame changing under a worn item | **No.** A body does not change; `FrameMismatch` can only fire at the transition |
| A faction clause changing | **No.** And it is content-restricted to hand-authored uniques and set pieces anyway (I11 §2.3) |
| Unequipping the gear that granted the shortfall | **No** — impossible by the cycle rule below |

⛔ **This contradicts the projector as this spec first wrote it**, and the correction is in the code
block: a `Project` that filters standing assignments through the same admission test the *assign* path
uses **is** force-unequip, arriving through the back door on the next deploy. The two moments are
different. Assign admits or refuses; project carries the assignment and **reports the shortfall**.

⚠ **What we own and what we do not.** This module owns detecting the shortfall and naming it per role
with both numbers. The `overburdened` **status** I11 §2.2 specifies is not ours and does not exist:
`StatusCatalogBootstrap.cs:3` registers *"all 21 locked status ids — status-ssot.md §9"* and
`overburdened` is not among them, so a 22nd is a reviewed vocabulary change owned by the status program
— the same shape as D8's 13th atom kind (item-ideal §2g #2). Until it lands the shortfall is reported
and nothing is applied, which is the safe direction: the item keeps working, and the player is told.

#### 2. The cycle rule — stated here even though it cannot bite here yet

I11 §2.7, one line: **the gate reads attributes composed from every source EXCEPT containers of the four
equippable kinds — `item`, `gem`, `set`, `charm`.** Call it the *unassisted* value. Without it, two items
each granting what the other requires make legality order-dependent and partial failure undefined.

⛔ **Build-readiness note (2026-09-04): the gate's frame arm is INERT until X1 lands.** No species
carries a `frame` yet, so `RoleNotOnFrame` can never fire. The **predicate, level and faction arms are
live**, and the module's payoff test — an item changing a number — does not touch frame at all. **Ship
the arm, assert it is inert, and let X1 close it** — do not stub a default frame, which would silently
admit assignments the gate exists to refuse.

⚠ **Honest scope:** on frame, level and faction — the three axes D19 left here — **nothing equippable
can move the input**, so the rule refuses nothing today. It is stated and enforced structurally anyway,
because this module is where the gate is built and the first attribute clause to arrive would otherwise
inherit a composer that never excluded anything. The gate takes its actor snapshot from a composer that
excludes the four kinds, and a test proves an equippable grant cannot flip an admission.

**The one indirection is not ours to close.** I11 §2.7: *"a container of an equippable kind may not grant
a binding of a non-equippable kind"* — otherwise a charm grants a trait and the cycle walks back in. That
is a **load-time validation owned by the effect-atom program** (I11 §9.2), and it is named here rather
than absorbed. ⚠ It also needs D27's `gem`/`set`/`charm` kinds to exist at all (**X7**).

#### 3. Element affinity is refused as a gate — the lane already ruled it out

Not a gap in this module; **a mechanism the lane declines**, recorded here so the next reader does not
re-open it. I11 §2.4 gives three reasons and all three still hold against code:

1. A soft derate needs a runtime magnitude multiplier that does not exist — `values_json` is frozen at
   instantiate and *"bind rolls nothing"*.
2. The element matrix is **already** a matter of degree through `combat.power.*` and the Element Hub.
   A requirement-shaped element penalty prices the same thing twice.
3. `element.type.primary` / `element.type.secondary` are actor **metadata, not derived channels**
   (`actor-hub-ssot.md:198`). Gating on them would make a metadata field load-bearing in the bind path
   for no mechanical gain.

**So: an advisory, never a clause.** The UI marks off-affinity gear and the player learns it from the
damage numbers — **module 20's** surface, filed there. This module asserts the negative: no element
clause can reach the gate. ⚠ Do not confuse this with **D22**, which reverted socket affinity to a
*bonus* — a different axis on a different module (16), and it argues in the same direction.

## Commands

```powershell
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~Assignment"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~BindGate"
```

## Project structure

```text
src/FusionRpg.Data/Sqlite/RpgStore.Items.cs            EDIT — rpg_item_assignment
src/FusionRpg.Core/Items/EquipProjector.cs             new — assignments -> bindings, full rebuild
src/FusionRpg.Core/Items/EquipGate.cs                  new — unlock predicate + frame + level +
                                                         faction; Admits / Projectable / Explain
src/FusionRpg.Core/Items/UnassistedAttributes.cs       new — the composer that excludes the four
                                                         equippable container kinds (I11 §2.7)
src/FusionRpg.Core/Match/UniqueEquipmentCatalog.cs     RETIRE — after the relic disposition lands
src/FusionRpg.Server/RelicEndpoints.cs                 EDIT — per the disposition chosen
```

## Code style

```csharp
// A full projection, never a delta. UpsertUniqueEquipment already works this way and
// UniqueOwnerBinder.ToEntityKey already discards the instance id at deploy - so rebuilding is the
// shipped shape, not a simplification. It is also what makes unequip atomic: one assignment row
// deleted, and the next projection simply does not produce that binding.
//
// Two moments, two tests. Admits() is the ASSIGN gate and is hard (I11 2.2). Projectable() is the
// DEPLOY test and is deliberately weaker: a standing assignment whose level_req lapsed still
// projects, because filtering it here is force-unequip wearing a projection's clothes - the answer
// I11 2.6 rejects for cascading. A lapse produces a reported shortfall, not a missing binding.
// The only thing Projectable() drops is a binding the runtime cannot execute at all (a disabled
// atom beneath it), and that skip is NAMED in the deploy result - I13 5.6's best-effort rule.
public ProjectionResult Project(long specimenId)
{
    var actor = _actorOf(specimenId);                       // unassisted: excludes item/gem/set/charm
    var rows = _store.ListAssignments(specimenId);
    return new ProjectionResult(
        Bindings:  rows.Where(a => _gate.Projectable(a, actor)).Select(ToBinding).ToList(),
        Shortfalls: rows.Where(a => !_gate.Admits(a, actor)).Select(a => _gate.Explain(a, actor)).ToList(),
        Skipped:   rows.Where(a => !_gate.Projectable(a, actor)).Select(a => _gate.Explain(a, actor)).ToList());
}
```

## Testing strategy

| Test | Asserts |
|---|---|
| `an_assignment_survives_a_restart` | the durable half |
| `bindings_are_rebuilt_as_a_full_projection` | never a delta |
| `a_binding_with_no_backing_assignment_is_absent_after_the_next_projection` | ⭐ the row above is passed by an **append-only** implementation on the happy path — it only ever compares the produced list. Delete the assignment out of band, re-project, assert the binding is **gone**. That is the assertion "full projection, never a delta" was making and could not prove |
| `assigning_into_a_slot_the_unlock_predicate_closes_is_refused_with_a_reason` | ⭐ module 3's `SlotUnlock` is consulted on the equip path, not merely built |
| `a_lapsed_level_req_reports_a_shortfall_and_keeps_the_binding` | I11 §2.6 — the projector is not a force-unequip |
| `a_content_revision_raising_level_req_strands_nothing` | the same rule at the nastiest cause |
| `the_gate_input_excludes_the_four_equippable_container_kinds` | I11 §2.7's unassisted value, structural before the first attribute clause arrives |
| `no_element_clause_can_reach_the_gate` | I11 §2.4's refusal, asserted as a negative |
| `unequip_is_one_row_delete_with_no_second_writer` | atomicity §6.4 claims |
| `unequip_does_not_destroy_the_item` | module 1's R1, asserted from this side too |
| `the_gate_refuses_a_wrong_frame_with_a_reason` | D19's surviving half |
| `the_frame_arm_is_inert_while_no_species_carries_a_frame` | ⭐ the X1 gap, **asserted rather than assumed** — and it fails once X1 lands, which is the reminder to populate |
| `level_req_is_actually_enforced` | ⭐ A4 — the arm exists at `BindGate.cs:53` and no production caller supplies `OwnerLevel` (`RpgHub.cs:107`), so assert the refusal end to end, not the branch |
| `level_req_compares_against_the_specimen_not_the_account` | the recommendation, pinned |
| `existing_rpg_unique_equipment_rows_migrate_without_loss` | the shipped data |
| `the_four_relics_survive_the_stub_retirement` | ⭐ the shipped player feature |
| `no_caller_of_UniqueEquipmentCatalog_remains` | the stub is actually gone |

## Boundaries

**Always:** rebuild bindings as a full projection; delete an assignment to unequip; give every gate
refusal a reason payload; consult module 3's unlock predicate before the frame/level/faction arms;
compose the gate's actor input from the **unassisted** sources only.

**Ask first:** the relic disposition (it changes a shipped endpoint and an FE layer); which level
`level_req` reads; the fifteenth reason code `RoleLocked`.

**Never:** write a binding as a delta. Never retire `rpg_unique_equipment` before relics have a home.
Never let the gate refuse silently — an effect that does nothing with no explanation is the failure
the whole atom layer exists to remove. **Never force-unequip a lapsed requirement**, and never let the
projector do it by omission. Never make element affinity a clause (I11 §2.4).

## Success criteria

- [ ] Assignment is durable; bindings are a rebuilt projection; unequip is one delete — and a binding
      with no backing assignment is **absent** after the next projection, proven out of band.
- [ ] The equip gate enforces the unlock predicate, frame and level, with reasons — and `level_req` is
      enforced at all, end to end, not as a branch nothing reaches.
- [ ] A lapsed requirement keeps its binding and reports a named shortfall; the `overburdened` status
      is filed against the status program as a 22nd id, not invented here.
- [ ] The gate's actor input excludes `item` / `gem` / `set` / `charm`, and a test proves an equippable
      grant cannot flip an admission.
- [ ] No element clause exists on any axis; the off-affinity advisory is filed against module 20.
- [ ] Relics have a named home and still work end to end, endpoint and FE layer included.
- [ ] `rpg_unique_equipment` rows are migrated and the stub catalog has no callers.
