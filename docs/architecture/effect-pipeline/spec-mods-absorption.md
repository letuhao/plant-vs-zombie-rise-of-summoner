# Spec: `mods-absorption`

**Module id:** `mods-absorption` · **Program:** [effect-pipeline](../effect-pipeline-map.md) · **Build order:** 5 of 10
**Depends on:** `instance-producer` (module 4) · **Sequenced immediately after the proof, never inside it**

## Objective

Move equipped-slot effects off `rpg_unique_stat_mods.mods_json` and onto `effect_binding`, exactly as
E6 always planned: *"`effect_binding` replaces the logical `foundation_effect_grant` and **absorbs
today's `mods_json` grant blobs**."* Verified 2026-09-02 the deferral never closed:
`UniqueEquipmentCatalog.BuildModsJson` (`UniqueEquipmentCatalog.cs:75`) still builds grant blobs from
equipped slots directly into `rpg_unique_stat_mods.mods_json` (table at `RpgStore.cs:407`), consumed by
`UniqueLoadoutSpec` — a live, save-affecting path with zero relationship to the atom layer.

Owner, Q11: *"absorb it. equipment spec is generate before atom effect ship, so it is not complete, i
still defer it until now, so this time to fix it."*

## Design

### Why this is its own module, not a line inside `instance-producer`

`effect-pipeline-map.md`'s own reasoning, restated because it is the reason the build order sequences
this AFTER the proof: *"the producer's job is to work where there is no shipped data to break."*
`mods_json` is **live, save-affecting unique-actor data** — real players' real equipped items, today.
Migrating it is a different risk class than proving a fixture container resolves, and **two risks in
one change is how a proof becomes a post-mortem**. The proof (module 4) runs first, against a fixture
that touches nothing live; this module runs second, against everything live.

### The migration shape

For each equipped item on a unique actor:

1. Resolve the item's own `item.*` container through `InstanceProducer.Produce` (module 4), owned by
   the actor's `OwnerScope`, `slot` set to the equip slot, `source` recording the item instance.
2. The resulting `effect_binding` row replaces the equivalent entry `BuildModsJson` used to synthesize.
3. `mods_json`'s **grant half stops being written at cutover.** The column stays on disk, unread for
   grants, and its removal is the separate later cleanup Boundaries already defers.

#### ⛔ DECIDED 2026-09-03 (owner removed themselves as a gate) — no read-through window. The cutover is per-actor and atomic

**Step 3 used to read *"derived, then removed — read-through during the migration window"*, and that
contradicted two other sections of this same spec:**

- *"There is no steady state where both are live for the same slot"* (§"The double-grant invariant")
- **Never:** *"leave a live actor with a grant through both paths simultaneously, **even during
  migration** — the cutover is per-actor and atomic, not gradual per-slot"* (§Boundaries)

**The atomic cutover wins**, and the read-through clause is deleted rather than reconciled:

1. **It is stated twice and testable once.** `an_actor_never_carries_the_same_grant_through_both_paths`
   is already in the testing table. A read-through window makes that test unwritable — during the
   window, both paths *are* live by design, so the invariant has no moment at which it holds.
2. **It is the reversible half.** A per-actor atomic cutover is re-runnable per actor and needs no
   code that must later be deleted. A read-through window needs a dual-read path in
   `UniqueLoadoutSpec`, which then becomes its own removal task — a second migration created by the
   first.
3. **It carries no risk the window was buying.** The window's implied benefit is *"a half-migrated
   actor still works"*, and per-actor atomicity delivers that directly: an actor is either fully on
   `effect_binding` or fully on `mods_json`, and both are complete states.

**What would overturn it:** an actor whose equipped set is too large to migrate inside one
transaction. `rpg_unique_stat_mods` is per-actor and slot-bounded, so that is not a live concern —
if it ever becomes one, the answer is a per-actor **queue**, still atomic per actor, never a
read-through.

### The double-grant invariant this module exists to close

Until this module runs, path 1 (`Instantiator` → `effect_binding`, module 4) and path 2 (`mods_json`,
today's shipped path) both exist. **An actor must never receive the same equipped item's effect through
both.** The migration is the boundary that makes this true — before it, only path 2 is live for
equipment; after it, only path 1 is. There is no steady state where both are live for the same slot.

### What does NOT change

The equip/unequip UI flow, slot validation (`UniqueEquipmentCatalog.NormalizeSlot`), and the
`absolutes`/flat-key legacy fields `BuildModsJson` also carries (non-grant stat absolutes, out of this
module's scope — only the **grant** half moves).

## Commands

```powershell
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~(UniqueEquipment|ModsAbsorption)"
.\scripts\guard-single-writer.ps1
```

## Project structure

```text
src/FusionRpg.Core/Match/UniqueEquipmentCatalog.cs   edit — BuildModsJson's grant half calls
                                                       InstanceProducer instead of writing a blob
src/FusionRpg.Data/Sqlite/RpgStore.UniqueActors.cs   edit — the equip/unequip write path
tests/FusionRpg.Core.Tests/Match/ModsAbsorptionTests.cs   new
```

## Code style

```csharp
// E6's original plan, executed: "absorbs today's mods_json grant blobs." Equip now calls
// InstanceProducer.Produce for the item's own container; mods_json stops being the grant's source
// of truth the moment this ships.
```

## Testing strategy

| Test | Asserts |
|---|---|
| `equipping_an_item_produces_a_real_binding` | path 1 now live for equipment |
| `an_actor_never_carries_the_same_grant_through_both_paths` | the double-grant invariant, mechanically |
| `unequipping_removes_the_binding_not_just_the_mods_json_entry` | withdraw is symmetric |
| `existing_save_data_migrates_without_a_stat_change` | a real save fixture, before/after equality on the actor's effective stats |
| `absolutes_and_flat_keys_are_unaffected` | scope discipline — only the grant half moves |

## Boundaries

**Always:** produce through `InstanceProducer`, never write `mods_json` grants directly after this
ships; keep the non-grant `absolutes`/flat-key fields untouched.

**Ask first:** deleting `rpg_unique_stat_mods` entirely (a schema removal, not just a stop-writing) —
that is a separate, later cleanup once the migration window closes.

**Never:** leave a live actor with a grant through both paths simultaneously, even during migration —
the cutover is per-actor and atomic, not gradual per-slot.

## Success criteria

- [ ] Every equipped item on every unique actor has a real `effect_binding`.
- [ ] The cutover is **per-actor and atomic**; no read-through window ships (§"The migration shape",
      decided 2026-09-03).
- [ ] No actor carries the same item's grant through `mods_json` and `effect_binding` at once, proven by test.
- [ ] A real save fixture's effective stats are unchanged before/after migration.
