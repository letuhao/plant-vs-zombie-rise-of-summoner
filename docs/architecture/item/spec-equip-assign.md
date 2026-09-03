# Spec: `equip-assign`

**Module id:** `equip-assign` · **Program:** [item](../item-map.md) · **Build order:** 4 of 21
**Depends on:** `durable-ownership` (1), `slot-roles` (3)
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

⚠ **`level_req` is enforced nowhere today** (`atom-layer-handoff.md` §2, A4). The gate exists in
`BindGate` for runtime support, scope legality and stale content; the level arm needs a writer for
`OwnerLevel`, which is this module's projector.

⚠ **Which level does it compare against** — `rpg_unique_actors.level` (specimen) or the account? I11
§10.6 leaves it open. Recommended: **specimen**, because `level_req = itemLevel − 2`
(`ssot-generation.md` §4.1) exists so *"you should always be able to wear what the content you just
beat dropped"*, and that content was beaten by a specimen.

## Commands

```powershell
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~Assignment"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~BindGate"
```

## Project structure

```text
src/FusionRpg.Data/Sqlite/RpgStore.Items.cs            EDIT — rpg_item_assignment
src/FusionRpg.Core/Items/EquipProjector.cs             new — assignments -> bindings, full rebuild
src/FusionRpg.Core/Items/EquipGate.cs                  new — frame + level + faction refusal
src/FusionRpg.Core/Match/UniqueEquipmentCatalog.cs     RETIRE — after the relic disposition lands
src/FusionRpg.Server/RelicEndpoints.cs                 EDIT — per the disposition chosen
```

## Code style

```csharp
// A full projection, never a delta. UpsertUniqueEquipment already works this way and
// UniqueOwnerBinder.ToEntityKey already discards the instance id at deploy - so rebuilding is the
// shipped shape, not a simplification. It is also what makes unequip atomic: one assignment row
// deleted, and the next projection simply does not produce that binding.
public IReadOnlyList<BindingRow> Project(long specimenId) =>
    _store.ListAssignments(specimenId)
          .Where(a => _gate.Admits(a, _actorOf(specimenId)))
          .Select(ToBinding)
          .ToList();
```

## Testing strategy

| Test | Asserts |
|---|---|
| `an_assignment_survives_a_restart` | the durable half |
| `bindings_are_rebuilt_as_a_full_projection` | never a delta |
| `unequip_is_one_row_delete_with_no_second_writer` | atomicity §6.4 claims |
| `unequip_does_not_destroy_the_item` | module 1's R1, asserted from this side too |
| `the_gate_refuses_a_wrong_frame_with_a_reason` | D19's surviving half |
| `level_req_is_actually_enforced` | ⭐ A4 — it is enforced nowhere today |
| `level_req_compares_against_the_specimen_not_the_account` | the recommendation, pinned |
| `existing_rpg_unique_equipment_rows_migrate_without_loss` | the shipped data |
| `the_four_relics_survive_the_stub_retirement` | ⭐ the shipped player feature |
| `no_caller_of_UniqueEquipmentCatalog_remains` | the stub is actually gone |

## Boundaries

**Always:** rebuild bindings as a full projection; delete an assignment to unequip; give every gate
refusal a reason payload.

**Ask first:** the relic disposition (it changes a shipped endpoint and an FE layer); which level
`level_req` reads.

**Never:** write a binding as a delta. Never retire `rpg_unique_equipment` before relics have a home.
Never let the gate refuse silently — an effect that does nothing with no explanation is the failure
the whole atom layer exists to remove.

## Success criteria

- [ ] Assignment is durable; bindings are a rebuilt projection; unequip is one delete.
- [ ] The equip gate enforces frame and level, with reasons — and `level_req` is enforced at all.
- [ ] Relics have a named home and still work end to end, endpoint and FE layer included.
- [ ] `rpg_unique_equipment` rows are migrated and the stub catalog has no callers.
