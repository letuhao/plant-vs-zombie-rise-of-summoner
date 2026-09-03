# Spec: `durable-ownership`

**Module id:** `durable-ownership` · **Program:** [item](../item-map.md) · **Build order:** 1 of 21
**Depends on:** nothing
**Rulings:** D5, D9 · corrections [item-ideal.md](../item-ideal.md) §2e (C3, S2), §2f.2 (D9's premise)

## Objective

Make an item a **durable, owned thing** — and in doing so close both live defects on code that is
already running in production.

**Users:** every other item module; the equip path; support.

**Success is measurable and it is not a feature demo:** unequip an item and it still exists; import
content and every owned item still binds. Neither is true today.

> ⭐ **This module has standalone value before anything else in the program lands.** `ProduceAndBind`
> is called in production (`RpgStore.UniqueActors.cs:756`), so both defects are live now. Whether the
> rest of the item program proceeds is irrelevant to whether this should ship.

## Design

### The four defects, each verified against code

| id | Defect | Evidence | Severity |
|---|---|---|---|
| **R1** | **Unequipping destroys the item.** The orphan sweep deletes any `effect_instance` no binding points at, and runs *"after a withdraw"* | `RpgStore.AtomInstances.cs:607-620`, called from `Withdraw` at `:565` | data loss |
| **R2** | **One content import silently disables every rolled item.** `ResolveBindings` refuses on strict `catalog_revision` equality | `RpgStore.AtomInstances.cs:437` | total, silent |
| **S2** | `definitions.md:316` promises `FK ON DELETE CASCADE` on `effect_binding`; the DDL declares **no foreign key at all** — three indices and nothing else | `RpgStore.AtomInstances.cs:83-97` | R1's mirror: deleting an instance orphans bindings |
| **C3** | `effect_atom.name` is never validated; empty names load clean | `AtomRow.cs:31` (defaults `""`); `AtomRowValidator` reads only `def.Name`, a *parameter* name | lint |

### R1 — ownership is the second reachability root

**The sweep is not wrong; its reachability graph is incomplete.** An instance is reachable through a
binding *or* through ownership. Today only the first exists, so unequip = unreachable = deleted.

`rpg_item` is that second root, and it is [ssot-inventory.md](ssot-inventory.md) §4.2's design
unchanged — PK `instance_id`, one-to-one with `effect_instance`, carrying what an effect instance
should not: `player_id`, `acquired_utc`, `origin_kind`/`origin_ref`, `locked`, `seen`, `stale`,
`disposition`, `note`, `revision`.

```sql
-- the sweep becomes: unreachable = no binding AND no owner
DELETE FROM effect_instance i
 WHERE NOT EXISTS (SELECT 1 FROM effect_binding b WHERE b.instance_id = i.instance_id)
   AND NOT EXISTS (SELECT 1 FROM rpg_item       o WHERE o.instance_id = i.instance_id);
```

**Why `rpg_item` and not a column on `effect_instance`:** ownership is item *policy*;
`effect_instance` belongs to the atom program, whose contract is content-derived reproducibility.
Ownership is not content, and adding `player_id` there would break the byte-identity comparison's
meaning (§4.2's own argument).

### R2 — per-atom compatibility, and D9's premise was false

**D9 argued the frozen values make the revision check redundant. They are never read.**

`ResolveBindings` uses `instance.Atoms` **only as an id list** and populates `rows` from the **live
catalog** (`:446-449`); `InstanceAtomRow.ValuesJson` is read at exactly one place —
`Instantiator.cs:65`, the content fingerprint. **No frozen magnitude reaches the runtime.** So the
`:435` comment D9 dismissed is accurate.

**Therefore the order is: make frozen values authoritative first, then drop the blunt check.**

| Step | Change |
|---|---|
| **1** | `ResolveBindings` composes from `InstanceAtomRow.ValuesJson`, falling back to the catalog row only for fields an instance does not freeze |
| **2** | Replace `instance.CatalogRevision != current` with a **per-atom** test: the atom still exists, is enabled, and its identity-defining fields are unchanged (content-hash compare) |
| **3** | The existing per-atom existence loop at `:445-452` stays — it is the precise version of what the blunt check approximates |

⚠ **Deliberate consequence, recorded rather than discovered** ([item-ideal.md](../item-ideal.md)
§2f.2): until step 1 lands, **a content patch retunes items players already own.** That is the
current shipped behaviour and this module does not change it silently. Whether it *should* change is
§2g's one open product call.

### S2 and C3

**S2** — declare the FK the contract already promises. With R1's second root in place, `ON DELETE
CASCADE` from `effect_binding.instance_id` is safe: deleting an instance is now a deliberate act
(disposition), not a side effect of unbinding.

**C3** — `AtomRowValidator` gains a name check. Smallest fix in the program; it is Stage-0 work in
`atom-layer-handoff.md` §7 and has no dependency.

### §2b.1's namespaced reason code lands here

`ContentRuleViolated` was chosen over 101 discrete codes and closed eight lanes' open questions —
and **has zero code hits**. This module is the first schema-validation consumer, so it defines the
code, the per-lane `rule` namespace registry, and the load-time wiring. Later modules register
namespaces; they do not each add a code.

## Commands

```powershell
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~AtomInstances"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~BindResolution"
.\scripts\guard-dal.ps1
```

## Project structure

```text
src/FusionRpg.Data/Sqlite/RpgStore.Items.cs          new — rpg_item DDL + CRUD
src/FusionRpg.Data/Sqlite/RpgStore.AtomInstances.cs  EDIT — sweep predicate (:607-620),
                                                       ResolveBindings (:437), effect_binding FK (:83)
src/FusionRpg.Core/Effects/Atoms/AtomRowValidator.cs EDIT — C3, the name check
src/FusionRpg.Core/Effects/Atoms/AtomRejection.cs    EDIT — ContentRuleViolated + rule namespace
tests/FusionRpg.Data.Tests/Items/OwnershipTests.cs   new
```

## Code style

```csharp
// Two reachability roots, not one. A binding says "equipped"; rpg_item says "owned". Before rpg_item
// existed the sweep was correct AND destroyed owned gear on unequip, because unequipped and
// unreachable were the same state (item-ideal.md D5).
static bool IsUnreachable(string instanceId) =>
    !HasBinding(instanceId) && !HasOwner(instanceId);
```

## Testing strategy

| Test | Asserts |
|---|---|
| `unequipping_does_not_delete_an_owned_instance` | ⭐ R1, the data-loss defect, directly |
| `withdrawing_the_last_binding_of_an_UNOWNED_instance_still_collects_it` | the sweep still does its job — no leak |
| `a_content_import_leaves_untouched_items_bindable` | ⭐ R2 |
| `an_import_that_disables_one_atom_invalidates_only_items_carrying_it` | precision, not the sledgehammer |
| `an_atom_whose_identity_fields_changed_is_refused` | what the per-atom hash adds over the existing existence loop |
| `deleting_an_instance_cascades_its_bindings` | S2 — the FK `definitions.md:316` promised |
| `an_empty_atom_name_is_rejected_at_load` | C3 |
| `rpg_item_is_one_to_one_with_effect_instance` | PK contract, no second identity |
| `no_rolled_value_is_duplicated_into_rpg_item` | §4.2's rule — rolls live in the instance |
| `ContentRuleViolated_carries_a_registered_rule_namespace` | §2b.1's code, wired not just named |

## Boundaries

**Always:** treat ownership and binding as independent reachability roots; keep rolled values out of
`rpg_item`; reject at load rather than discovering at roll time.

**Ask first:** anything touching E6's binding contract beyond the two amendments named here —
`item/README.md` marks that surface ask-first, and this module deliberately stays inside it.

**Never:** delete an owned instance as a side effect of any operation. Never let `rpg_item` carry a
magnitude. Never widen `ContentRuleViolated` into a second code family — the point was one code with
a namespaced payload.

## Success criteria

- [ ] Unequipping an owned item leaves the instance intact, proven by test.
- [ ] A content import invalidates only items whose atoms actually changed.
- [ ] `effect_binding` declares the FK `definitions.md:316` promises, and cascade is tested.
- [ ] An empty `effect_atom.name` fails at load.
- [ ] `ContentRuleViolated` exists in code with a namespace registry and one real consumer.
- [ ] `guard-dal.ps1` green — all SQL inside `FusionRpg.Data`.
